using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Enums;

namespace SNextSeafood.Application.Strategies;

public class PaymentUrlGenerationResult
{
    public bool RequiresRedirect { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;
    public string ExternalSandboxUrl { get; set; } = string.Empty;
    public string TransactionRef { get; set; } = string.Empty;
    public string SecureHash { get; set; } = string.Empty;
}

public class PaymentSignatureVerificationResult
{
    public bool IsValidSignature { get; set; }
    public bool IsPaymentSuccess { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public string GatewayTransactionNo { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// STRATEGY PATTERN: IPaymentGatewayStrategy
/// Đa hình hóa các phương thức thanh toán: VnPayStrategy (HMAC-SHA512), MoMoStrategy (QR), CodStrategy.
/// </summary>
public interface IPaymentGatewayStrategy
{
    PaymentMethodType SupportedMethod { get; }
    string MethodName { get; }
    PaymentUrlGenerationResult GeneratePaymentUrl(Order order, string transactionRef, string hostBaseUrl, string clientIp);
    PaymentSignatureVerificationResult VerifyCallback(IReadOnlyDictionary<string, string> parameters);
    string BuildSignedCallbackUrl(string hostBaseUrl, string returnPath, string transactionRef, decimal amountVnd, string responseCode, string bankCode = "NCB");
}

public class VnPayStrategy : IPaymentGatewayStrategy
{
    private readonly IConfiguration _configuration;

    public VnPayStrategy(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public PaymentMethodType SupportedMethod => PaymentMethodType.VNPay;
    public string MethodName => "VNPay";

    public PaymentUrlGenerationResult GeneratePaymentUrl(Order order, string transactionRef, string hostBaseUrl, string clientIp)
    {
        var tmnCode = _configuration["VnPay:TmnCode"] ?? "SNEXTVNP";
        var hashSecret = _configuration["VnPay:HashSecret"] ?? "SNEXTSEAFOOD2026SECRETKEYHMACSHA512VNPAYSANDBOX";
        var vnpBaseUrl = _configuration["VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnPath = _configuration["VnPay:ReturnUrl"] ?? "/Payment/PaymentCallback";
        var useLocalSimulator = bool.TryParse(_configuration["VnPay:UseLocalSimulator"], out var sim) ? sim : true;

        var returnUrl = $"{hostBaseUrl.TrimEnd('/')}{returnPath}";
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var expireDate = DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss");
        var amountInVnpUnit = ((long)(order.FinalAmount * 100)).ToString(CultureInfo.InvariantCulture);

        var vnpParams = new SortedList<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = amountInVnpUnit,
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Thanh toan don hang S-NEXT Seafood {order.OrderCode}",
            ["vnp_OrderType"] = "food",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_ExpireDate"] = expireDate,
            ["vnp_TxnRef"] = transactionRef
        };

        var queryBuilder = new StringBuilder();
        foreach (var kv in vnpParams.Where(kv => !string.IsNullOrEmpty(kv.Value)))
        {
            queryBuilder.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
        }

        var signData = queryBuilder.ToString().TrimEnd('&');
        var vnpSecureHash = ComputeHmacSha512(hashSecret, signData);
        var fullQuery = $"{signData}&vnp_SecureHash={vnpSecureHash}";

        var externalSandboxUrl = $"{vnpBaseUrl}?{fullQuery}";
        var simulatorUrl = $"{hostBaseUrl.TrimEnd('/')}/Payment/VnPayGateway?{fullQuery}&orderCode={WebUtility.UrlEncode(order.OrderCode)}";

        return new PaymentUrlGenerationResult
        {
            RequiresRedirect = true,
            RedirectUrl = useLocalSimulator ? simulatorUrl : externalSandboxUrl,
            ExternalSandboxUrl = externalSandboxUrl,
            TransactionRef = transactionRef,
            SecureHash = vnpSecureHash
        };
    }

    public string BuildSignedCallbackUrl(
        string hostBaseUrl,
        string returnPath,
        string transactionRef,
        decimal amountVnd,
        string responseCode,
        string bankCode = "NCB")
    {
        var tmnCode = _configuration["VnPay:TmnCode"] ?? "SNEXTVNP";
        var hashSecret = _configuration["VnPay:HashSecret"] ?? "SNEXTSEAFOOD2026SECRETKEYHMACSHA512VNPAYSANDBOX";
        var payDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var transactionNo = responseCode == "00" ? $"VNP{Random.Shared.Next(10000000, 99999999)}" : "0";

        var callbackParams = new SortedList<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = ((long)(amountVnd * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_BankCode"] = bankCode,
            ["vnp_BankTranNo"] = $"VNP{DateTime.UtcNow:MMddHHmmss}",
            ["vnp_CardType"] = "ATM",
            ["vnp_OrderInfo"] = $"Thanh toan don hang S-NEXT {transactionRef}",
            ["vnp_PayDate"] = payDate,
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_TransactionNo"] = transactionNo,
            ["vnp_TransactionStatus"] = responseCode,
            ["vnp_TxnRef"] = transactionRef
        };

        var sb = new StringBuilder();
        foreach (var kv in callbackParams.Where(x => !string.IsNullOrEmpty(x.Value)))
        {
            sb.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
        }

        var signData = sb.ToString().TrimEnd('&');
        var secureHash = ComputeHmacSha512(hashSecret, signData);
        return $"{hostBaseUrl.TrimEnd('/')}{returnPath}?{signData}&vnp_SecureHash={secureHash}";
    }

    public PaymentSignatureVerificationResult VerifyCallback(IReadOnlyDictionary<string, string> parameters)
    {
        var hashSecret = _configuration["VnPay:HashSecret"] ?? "SNEXTSEAFOOD2026SECRETKEYHMACSHA512VNPAYSANDBOX";

        var vnpParams = new SortedList<string, string>(StringComparer.Ordinal);
        foreach (var kv in parameters)
        {
            if (kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                && !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                && !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(kv.Value))
            {
                vnpParams[kv.Key] = kv.Value;
            }
        }

        var sb = new StringBuilder();
        foreach (var kv in vnpParams)
        {
            sb.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
        }

        var signData = sb.ToString().TrimEnd('&');
        var receivedHash = parameters.TryGetValue("vnp_SecureHash", out var h) ? h : string.Empty;
        var expectedHash = ComputeHmacSha512(hashSecret, signData);
        var isValidHash = !string.IsNullOrWhiteSpace(receivedHash) &&
                          string.Equals(receivedHash, expectedHash, StringComparison.OrdinalIgnoreCase);

        var responseCode = parameters.TryGetValue("vnp_ResponseCode", out var rc) ? rc : "99";
        var txnRef = parameters.TryGetValue("vnp_TxnRef", out var tr) ? tr : string.Empty;
        var transactionNo = parameters.TryGetValue("vnp_TransactionNo", out var tn) ? tn : string.Empty;
        var rawAmount = parameters.TryGetValue("vnp_Amount", out var amtStr) && long.TryParse(amtStr, out var amtVal)
            ? amtVal / 100m
            : 0m;

        var isSuccess = isValidHash && responseCode == "00";

        return new PaymentSignatureVerificationResult
        {
            IsValidSignature = isValidHash,
            IsPaymentSuccess = isSuccess,
            TransactionRef = txnRef,
            GatewayTransactionNo = transactionNo,
            ResponseCode = responseCode,
            Amount = rawAmount,
            RawPayload = signData,
            Message = !isValidHash
                ? "Cảnh báo bảo mật: Chữ ký điện tử vnp_SecureHash (HMAC-SHA512) không hợp lệ!"
                : isSuccess
                    ? "Giao dịch thanh toán VNPay thành công (vnp_ResponseCode = 00). Đã xác thực HMAC-SHA512."
                    : $"Giao dịch VNPay không thành công hoặc khách hàng đã hủy (Mã phản hồi: {responseCode})."
        };
    }

    public static string ComputeHmacSha512(string key, string inputData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

public class MoMoStrategy : IPaymentGatewayStrategy
{
    private readonly IConfiguration _configuration;

    public MoMoStrategy(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public PaymentMethodType SupportedMethod => PaymentMethodType.MoMo;
    public string MethodName => "MoMo";

    public PaymentUrlGenerationResult GeneratePaymentUrl(Order order, string transactionRef, string hostBaseUrl, string clientIp)
    {
        var secretKey = _configuration["MoMo:SecretKey"] ?? "K951B6PE1waDMi640xX08PD3vg6EkVlz";
        var rawData = $"orderCode={order.OrderCode}&txnRef={transactionRef}&amount={(long)order.FinalAmount}";
        var signature = VnPayStrategy.ComputeHmacSha512(secretKey, rawData);

        var simulatorUrl = $"{hostBaseUrl.TrimEnd('/')}/Payment/MoMoGateway?orderCode={WebUtility.UrlEncode(order.OrderCode)}&txnRef={WebUtility.UrlEncode(transactionRef)}&amount={(long)order.FinalAmount}&signature={signature}";

        return new PaymentUrlGenerationResult
        {
            RequiresRedirect = true,
            RedirectUrl = simulatorUrl,
            ExternalSandboxUrl = simulatorUrl,
            TransactionRef = transactionRef,
            SecureHash = signature
        };
    }

    public string BuildSignedCallbackUrl(
        string hostBaseUrl,
        string returnPath,
        string transactionRef,
        decimal amountVnd,
        string responseCode,
        string bankCode = "MOMOQR")
    {
        var secretKey = _configuration["MoMo:SecretKey"] ?? "K951B6PE1waDMi640xX08PD3vg6EkVlz";
        var transId = responseCode == "00" ? $"MOMO{Random.Shared.Next(10000000, 99999999)}" : "0";
        var rawData = $"paymentMethod=MoMo&txnRef={transactionRef}&amount={(long)amountVnd}&responseCode={responseCode}&transId={transId}";
        var signature = VnPayStrategy.ComputeHmacSha512(secretKey, rawData);

        return $"{hostBaseUrl.TrimEnd('/')}{returnPath}?paymentMethod=MoMo&vnp_TxnRef={WebUtility.UrlEncode(transactionRef)}&vnp_Amount={(long)(amountVnd * 100)}&vnp_ResponseCode={responseCode}&vnp_TransactionNo={transId}&momo_Signature={signature}";
    }

    public PaymentSignatureVerificationResult VerifyCallback(IReadOnlyDictionary<string, string> parameters)
    {
        var secretKey = _configuration["MoMo:SecretKey"] ?? "K951B6PE1waDMi640xX08PD3vg6EkVlz";
        var txnRef = parameters.TryGetValue("vnp_TxnRef", out var tr) ? tr : string.Empty;
        var responseCode = parameters.TryGetValue("vnp_ResponseCode", out var rc) ? rc : "99";
        var transId = parameters.TryGetValue("vnp_TransactionNo", out var tn) ? tn : string.Empty;
        var amount = parameters.TryGetValue("vnp_Amount", out var amt) && long.TryParse(amt, out var parsed)
            ? parsed / 100m
            : 0m;
        var receivedSig = parameters.TryGetValue("momo_Signature", out var sig) ? sig : string.Empty;

        var rawData = $"paymentMethod=MoMo&txnRef={txnRef}&amount={(long)amount}&responseCode={responseCode}&transId={transId}";
        var expectedSig = VnPayStrategy.ComputeHmacSha512(secretKey, rawData);
        var isValid = !string.IsNullOrWhiteSpace(receivedSig) &&
                      string.Equals(receivedSig, expectedSig, StringComparison.OrdinalIgnoreCase);

        var isSuccess = isValid && responseCode == "00";

        return new PaymentSignatureVerificationResult
        {
            IsValidSignature = isValid,
            IsPaymentSuccess = isSuccess,
            TransactionRef = txnRef,
            GatewayTransactionNo = transId,
            ResponseCode = responseCode,
            Amount = amount,
            RawPayload = rawData,
            Message = isSuccess
                ? "Thanh toán Ví MoMo / VietQR thành công."
                : "Giao dịch Ví MoMo bị hủy hoặc thất bại."
        };
    }
}

public class CodStrategy : IPaymentGatewayStrategy
{
    public PaymentMethodType SupportedMethod => PaymentMethodType.COD;
    public string MethodName => "COD";

    public PaymentUrlGenerationResult GeneratePaymentUrl(Order order, string transactionRef, string hostBaseUrl, string clientIp)
    {
        return new PaymentUrlGenerationResult
        {
            RequiresRedirect = false,
            RedirectUrl = $"{hostBaseUrl.TrimEnd('/')}/Order/Invoice/{order.OrderCode}",
            TransactionRef = transactionRef,
            SecureHash = "COD_INNER_CITY_VERIFIED"
        };
    }

    public string BuildSignedCallbackUrl(
        string hostBaseUrl,
        string returnPath,
        string transactionRef,
        decimal amountVnd,
        string responseCode,
        string bankCode = "COD")
    {
        return $"{hostBaseUrl.TrimEnd('/')}{returnPath}";
    }

    public PaymentSignatureVerificationResult VerifyCallback(IReadOnlyDictionary<string, string> parameters)
    {
        return new PaymentSignatureVerificationResult
        {
            IsValidSignature = true,
            IsPaymentSuccess = true,
            ResponseCode = "00",
            Message = "Đơn hàng COD Nội thành TP.HCM đã được xác nhận."
        };
    }
}

public interface IPaymentStrategyFactory
{
    IPaymentGatewayStrategy GetStrategy(PaymentMethodType method);
    IPaymentGatewayStrategy GetStrategyByName(string methodName);
}

public class PaymentStrategyFactory : IPaymentStrategyFactory
{
    private readonly IEnumerable<IPaymentGatewayStrategy> _strategies;

    public PaymentStrategyFactory(IEnumerable<IPaymentGatewayStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IPaymentGatewayStrategy GetStrategy(PaymentMethodType method)
    {
        return _strategies.FirstOrDefault(s => s.SupportedMethod == method)
            ?? throw new NotSupportedException($"Phương thức thanh toán {method} chưa được hỗ trợ.");
    }

    public IPaymentGatewayStrategy GetStrategyByName(string methodName)
    {
        if (string.Equals(methodName, "MoMo", StringComparison.OrdinalIgnoreCase))
            return GetStrategy(PaymentMethodType.MoMo);
        if (string.Equals(methodName, "COD", StringComparison.OrdinalIgnoreCase))
            return GetStrategy(PaymentMethodType.COD);
        return GetStrategy(PaymentMethodType.VNPay);
    }
}
