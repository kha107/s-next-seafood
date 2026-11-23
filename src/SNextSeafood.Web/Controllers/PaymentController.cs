using Microsoft.AspNetCore.Mvc;
using SNextSeafood.Application.Facades;
using SNextSeafood.Application.Strategies;
using SNextSeafood.Domain.Enums;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Web.Helpers;
using SNextSeafood.Web.Models;

namespace SNextSeafood.Web.Controllers;

public class PaymentController : Controller
{
    private readonly IOrderCheckoutFacade _checkoutFacade;
    private readonly IPaymentStrategyFactory _strategyFactory;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentController(
        IOrderCheckoutFacade checkoutFacade,
        IPaymentStrategyFactory strategyFactory,
        IUnitOfWork unitOfWork)
    {
        _checkoutFacade = checkoutFacade;
        _strategyFactory = strategyFactory;
        _unitOfWork = unitOfWork;
    }

    private bool CanAccessOrder(string orderCode)
    {
        return User.IsInRole("Admin") || SessionCartHelper.IsOrderAuthorizedInSession(HttpContext.Session, orderCode);
    }

    [HttpGet("Order/Track")]
    public IActionResult Track()
    {
        return View();
    }

    [HttpPost("Order/Track")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(string orderCode, string customerPhone)
    {
        var code = (orderCode ?? string.Empty).Trim().ToUpperInvariant();
        var phone = (customerPhone ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(phone))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ Mã đơn hàng và Số điện thoại đặt hàng để xác thực.";
            return View();
        }

        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(code);
        if (order is null || !string.Equals(order.CustomerPhone.Trim(), phone, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng khớp với Mã đơn hàng và Số điện thoại bạn đã nhập.";
            ViewData["OrderCode"] = code;
            ViewData["CustomerPhone"] = phone;
            return View();
        }

        SessionCartHelper.AuthorizeOrderInSession(HttpContext.Session, order.OrderCode);
        return RedirectToAction(nameof(Invoice), new { orderCode = order.OrderCode });
    }

    [HttpGet("Payment/VnPayGateway")]
    public async Task<IActionResult> VnPayGateway(
        string? orderCode,
        string? vnp_TxnRef,
        string? vnp_SecureHash)
    {
        var resolvedOrderCode = !string.IsNullOrWhiteSpace(orderCode)
            ? orderCode.Trim()
            : (!string.IsNullOrWhiteSpace(vnp_TxnRef) && vnp_TxnRef.EndsWith("-VNP", StringComparison.OrdinalIgnoreCase)
                ? vnp_TxnRef[..^4]
                : string.Empty);

        if (string.IsNullOrWhiteSpace(resolvedOrderCode) || !CanAccessOrder(resolvedOrderCode))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập giao dịch thanh toán này. Vui lòng xác thực đơn hàng của bạn.";
            return RedirectToAction(nameof(Track));
        }

        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(resolvedOrderCode);
        if (order is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var txnRef = !string.IsNullOrWhiteSpace(vnp_TxnRef) ? vnp_TxnRef : $"{order.OrderCode}-VNP";
        var strategy = _strategyFactory.GetStrategy(PaymentMethodType.VNPay);
        var hostBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var urlGen = strategy.GeneratePaymentUrl(order, txnRef, hostBaseUrl, clientIp);
        var successUrl = strategy.BuildSignedCallbackUrl(hostBaseUrl, "/Payment/PaymentCallback", txnRef, order.FinalAmount, "00", "NCB");
        var cancelUrl = strategy.BuildSignedCallbackUrl(hostBaseUrl, "/Payment/PaymentCallback", txnRef, order.FinalAmount, "24", "NCB");
        var tamperedUrl = successUrl + "TAMPERED999";

        var cleanOrderTag = order.OrderCode.Replace("-", "").ToUpperInvariant();
        var vm = new PaymentGatewaySimulatorViewModel
        {
            Order = order,
            IsMoMo = false,
            GatewayName = "Thanh Toán Chuyển Khoản Ngân Hàng / VNPay QR",
            QrImageUrl = "/images/payments/vnpay-bank-qr.jpg",
            BankOrWalletName = "Ngân hàng TMCP Quân Đội (MB Bank) — VietQR / Napas 247",
            AccountHolder = "NGUYEN VAN TRUNG",
            AccountNumber = "0344460993",
            TransferContent = $"SNEXT {cleanOrderTag}",
            TransactionRef = txnRef,
            AmountVnd = order.FinalAmount,
            SecureHash = !string.IsNullOrWhiteSpace(vnp_SecureHash) ? vnp_SecureHash : urlGen.SecureHash,
            ExternalSandboxUrl = urlGen.ExternalSandboxUrl,
            SuccessCallbackUrl = successUrl,
            CancelCallbackUrl = cancelUrl,
            TamperedHashCallbackUrl = tamperedUrl
        };

        return View(vm);
    }

    [HttpGet("Payment/MoMoGateway")]
    public async Task<IActionResult> MoMoGateway(string orderCode, string txnRef, string? signature)
    {
        if (string.IsNullOrWhiteSpace(orderCode) || !CanAccessOrder(orderCode))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập giao dịch thanh toán này.";
            return RedirectToAction(nameof(Track));
        }

        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(orderCode);
        if (order is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var strategy = _strategyFactory.GetStrategy(PaymentMethodType.MoMo);
        var hostBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = strategy.BuildSignedCallbackUrl(hostBaseUrl, "/Payment/PaymentCallback", txnRef, order.FinalAmount, "00", "MOMOQR");
        var cancelUrl = strategy.BuildSignedCallbackUrl(hostBaseUrl, "/Payment/PaymentCallback", txnRef, order.FinalAmount, "24", "MOMOQR");
        var cleanOrderTag = order.OrderCode.Replace("-", "").ToUpperInvariant();

        var vm = new PaymentGatewaySimulatorViewModel
        {
            Order = order,
            IsMoMo = true,
            GatewayName = "Thanh Toán Ví Điện Tử MoMo / VietQR",
            QrImageUrl = "/images/payments/momo-qr.jpg",
            BankOrWalletName = "Ví Điện Tử MoMo — VietQR / Napas 247",
            AccountHolder = "NGUYEN VAN TRUNG",
            AccountNumber = "0344460993 (Ví MoMo *******597)",
            TransferContent = $"SNEXT MOMO {cleanOrderTag}",
            TransactionRef = txnRef,
            AmountVnd = order.FinalAmount,
            SecureHash = signature ?? string.Empty,
            ExternalSandboxUrl = successUrl,
            SuccessCallbackUrl = successUrl,
            CancelCallbackUrl = cancelUrl,
            TamperedHashCallbackUrl = successUrl + "INVALID"
        };

        return View("VnPayGateway", vm);
    }

    [HttpGet("Payment/PaymentCallback")]
    public async Task<IActionResult> PaymentCallback()
    {
        var queryDict = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var callbackResult = await _checkoutFacade.ProcessPaymentCallbackAsync(queryDict);

        if (string.IsNullOrWhiteSpace(callbackResult.OrderCode))
        {
            TempData["ErrorMessage"] = callbackResult.Message;
            return RedirectToAction("Index", "Home");
        }

        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(callbackResult.OrderCode);
        if (order is null)
        {
            TempData["ErrorMessage"] = callbackResult.Message;
            return RedirectToAction("Index", "Home");
        }

        SessionCartHelper.AuthorizeOrderInSession(HttpContext.Session, order.OrderCode);

        var vm = new OrderInvoiceViewModel
        {
            Order = order,
            CallbackResult = callbackResult
        };

        return View("Invoice", vm);
    }

    [HttpGet("Order/Invoice/{orderCode}")]
    public async Task<IActionResult> Invoice(string orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode) || !CanAccessOrder(orderCode))
        {
            TempData["ErrorMessage"] = "Vì lý do bảo mật thông tin khách hàng, vui lòng nhập Mã đơn hàng và Số điện thoại đặt hàng để xem hóa đơn.";
            return RedirectToAction(nameof(Track));
        }

        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(orderCode);
        if (order is null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy đơn hàng {orderCode}.";
            return RedirectToAction(nameof(Track));
        }

        var vm = new OrderInvoiceViewModel
        {
            Order = order,
            CallbackResult = null
        };

        return View(vm);
    }
}
