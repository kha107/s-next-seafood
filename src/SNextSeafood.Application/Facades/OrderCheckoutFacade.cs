using SNextSeafood.Application.DTOs;
using SNextSeafood.Application.Strategies;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Enums;
using SNextSeafood.Domain.Interfaces;

namespace SNextSeafood.Application.Facades;

public interface IOrderCheckoutFacade
{
    Task<(decimal DiscountAmount, Voucher? Voucher, string Message)> CalculateVoucherDiscountAsync(
        string? voucherCode,
        decimal orderSubtotal,
        CancellationToken cancellationToken = default);

    Task<CheckoutResultDto> PlaceOrderAndInitiatePaymentAsync(
        CheckoutRequestDto request,
        string hostBaseUrl,
        string clientIp,
        CancellationToken cancellationToken = default);

    Task<PaymentCallbackResultDto> ProcessPaymentCallbackAsync(
        IReadOnlyDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// FACADE PATTERN: OrderCheckoutFacade
/// Phối hợp UnitOfWork (Database Transaction), WarehouseFefoFacade (Phân bổ lô FEFO & Giữ hàng tạm thời)
/// và IPaymentGatewayStrategy (VNPay HMAC-SHA512 / MoMo QR / COD) trong một luồng giao dịch nguyên tử (ACID).
/// </summary>
public class OrderCheckoutFacade : IOrderCheckoutFacade
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseFefoFacade _warehouseFefoFacade;
    private readonly IPaymentStrategyFactory _paymentStrategyFactory;

    public OrderCheckoutFacade(
        IUnitOfWork unitOfWork,
        IWarehouseFefoFacade warehouseFefoFacade,
        IPaymentStrategyFactory paymentStrategyFactory)
    {
        _unitOfWork = unitOfWork;
        _warehouseFefoFacade = warehouseFefoFacade;
        _paymentStrategyFactory = paymentStrategyFactory;
    }

    public async Task<(decimal DiscountAmount, Voucher? Voucher, string Message)> CalculateVoucherDiscountAsync(
        string? voucherCode,
        decimal orderSubtotal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            return (0m, null, string.Empty);
        }

        var voucher = await _unitOfWork.Orders.GetActiveVoucherByCodeAsync(voucherCode, cancellationToken);
        if (voucher is null)
        {
            return (0m, null, $"Mã giảm giá '{voucherCode}' không tồn tại hoặc đã hết hạn.");
        }

        if (orderSubtotal < voucher.MinOrderAmount)
        {
            return (0m, voucher, $"Đơn hàng tối thiểu {voucher.MinOrderAmount:N0}đ mới áp dụng được mã {voucher.Code}.");
        }

        var rawDiscount = Math.Round(orderSubtotal * voucher.DiscountPercent / 100m, 0);
        var finalDiscount = voucher.MaxDiscountAmount > 0
            ? Math.Min(rawDiscount, voucher.MaxDiscountAmount)
            : rawDiscount;

        return (finalDiscount, voucher, $"Áp dụng mã {voucher.Code} giảm {finalDiscount:N0}đ thành công!");
    }

    public async Task<CheckoutResultDto> PlaceOrderAndInitiatePaymentAsync(
        CheckoutRequestDto request,
        string hostBaseUrl,
        string clientIp,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return new CheckoutResultDto
            {
                Success = false,
                Message = "Giỏ hàng đang trống. Vui lòng chọn sản phẩm thủy hải sản trước khi đặt hàng."
            };
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var orderCode = $"ORD-{DateTime.UtcNow.AddHours(7):yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            var isOnlinePayment = request.PaymentMethod != PaymentMethodType.COD;

            var order = new Order
            {
                OrderCode = orderCode,
                CustomerId = "CUST-WEB",
                CustomerName = request.CustomerName.Trim(),
                CustomerPhone = request.CustomerPhone.Trim(),
                CustomerEmail = request.CustomerEmail?.Trim() ?? string.Empty,
                ShippingAddress = request.ShippingAddress.Trim(),
                District = request.District.Trim(),
                DeliveryNote = request.DeliveryNote?.Trim(),
                PaymentMethod = request.PaymentMethod,
                OrderStatus = isOnlinePayment ? OrderStatus.PendingPayment : OrderStatus.Processing,
                PaymentStatus = PaymentStatus.Unpaid,
                CreatedAt = DateTime.UtcNow
            };

            decimal subtotal = 0m;
            var previewAllocations = new List<FefoBatchAllocationPreviewDto>();

            foreach (var itemInput in request.Items.Where(i => i.Quantity > 0))
            {
                var variant = await _unitOfWork.Products.GetVariantWithProductAndBatchesAsync(itemInput.VariantId, cancellationToken)
                    ?? throw new InvalidOperationException($"Không tìm thấy biến thể sản phẩm #{itemInput.VariantId}.");

                var lineTotal = variant.Price * itemInput.Quantity;
                subtotal += lineTotal;

                var orderItem = new OrderItem
                {
                    Order = order,
                    ProductVariantId = variant.VariantId,
                    Quantity = itemInput.Quantity,
                    UnitPrice = variant.Price,
                    LineTotal = lineTotal
                };

                // Gọi WarehouseFefoFacade để phân bổ lô hàng theo FEFO (ExpiryDate ASC, ImportDate ASC)
                var itemAllocations = await _warehouseFefoFacade.AllocateFefoForOrderItemAsync(
                    order,
                    orderItem,
                    isOnlineReservation: isOnlinePayment,
                    cancellationToken);

                order.OrderItems.Add(orderItem);

                foreach (var alloc in itemAllocations)
                {
                    previewAllocations.Add(new FefoBatchAllocationPreviewDto
                    {
                        BatchId = alloc.BatchId,
                        BatchCode = alloc.BatchCode,
                        AllocatedQuantity = alloc.AllocatedQuantity,
                        ManufactureDate = alloc.ManufactureDate,
                        ExpiryDate = alloc.ExpiryDate,
                        DaysUntilExpiry = (int)Math.Ceiling((alloc.ExpiryDate.Date - DateTime.UtcNow.Date).TotalDays),
                        ImportUnitPrice = alloc.ImportUnitPrice
                    });
                }
            }

            var (discountAmount, voucher, _) = await CalculateVoucherDiscountAsync(request.VoucherCode, subtotal, cancellationToken);
            if (voucher is not null && discountAmount > 0)
            {
                order.AppliedVoucherCode = voucher.Code;
            }

            // Miễn phí vận chuyển xe lạnh cho đơn từ 500.000đ, ngược lại 30.000đ
            var shippingFee = subtotal >= 500000m ? 0m : 30000m;
            var finalAmount = Math.Max(0m, subtotal - discountAmount + shippingFee);

            order.TotalAmount = subtotal;
            order.DiscountAmount = discountAmount;
            order.ShippingFee = shippingFee;
            order.FinalAmount = finalAmount;

            await _unitOfWork.Orders.AddAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Sinh giao dịch thanh toán qua Strategy Pattern
            var strategy = _paymentStrategyFactory.GetStrategy(request.PaymentMethod);
            var suffix = request.PaymentMethod == PaymentMethodType.VNPay ? "VNP"
                       : request.PaymentMethod == PaymentMethodType.MoMo ? "MOMO" : "COD";
            var transactionRef = $"{order.OrderCode}-{suffix}";

            var urlResult = strategy.GeneratePaymentUrl(order, transactionRef, hostBaseUrl, clientIp);

            var paymentTx = new PaymentTransaction
            {
                OrderId = order.OrderId,
                PaymentMethod = strategy.MethodName,
                TransactionRef = transactionRef,
                GatewayTransactionNo = request.PaymentMethod == PaymentMethodType.COD ? $"COD-{Random.Shared.Next(10000, 99999)}" : null,
                Amount = order.FinalAmount,
                ResponseCode = request.PaymentMethod == PaymentMethodType.COD ? "00" : "PENDING",
                SecureHashVerified = request.PaymentMethod == PaymentMethodType.COD,
                PaymentUrl = urlResult.RedirectUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.PaymentTransactions.AddAsync(paymentTx, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CheckoutResultDto
            {
                Success = true,
                Message = isOnlinePayment
                    ? $"Đã tiếp nhận đơn hàng {order.OrderCode}. Vui lòng quét mã QR để hoàn tất thanh toán."
                    : $"Đặt hàng {order.OrderCode} thành công! Nhân viên S-NEXT sẽ liên hệ xác nhận giao hàng lạnh đến quý khách.",
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                ShippingFee = order.ShippingFee,
                FinalAmount = order.FinalAmount,
                PaymentMethod = request.PaymentMethod,
                RequiresOnlineRedirect = urlResult.RequiresRedirect,
                PaymentRedirectUrl = urlResult.RedirectUrl,
                TransactionRef = transactionRef,
                AllocatedBatches = previewAllocations
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new CheckoutResultDto
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<PaymentCallbackResultDto> ProcessPaymentCallbackAsync(
        IReadOnlyDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default)
    {
        var methodHint = queryParams.TryGetValue("paymentMethod", out var pm) && !string.IsNullOrWhiteSpace(pm)
            ? pm
            : "VNPay";

        var strategy = _paymentStrategyFactory.GetStrategyByName(methodHint);
        var verification = strategy.VerifyCallback(queryParams);

        if (string.IsNullOrWhiteSpace(verification.TransactionRef))
        {
            return new PaymentCallbackResultDto
            {
                Success = false,
                SecureHashVerified = false,
                Message = "Không tìm thấy mã tham chiếu giao dịch (vnp_TxnRef)."
            };
        }

        // Tìm OrderCode từ TransactionRef (VD: ORD-20261116-1004-VNP -> ORD-20261116-1004)
        var orderCode = verification.TransactionRef;
        var lastDash = orderCode.LastIndexOf('-');
        if (lastDash > 4 && (orderCode.EndsWith("-VNP", StringComparison.OrdinalIgnoreCase)
                             || orderCode.EndsWith("-MOMO", StringComparison.OrdinalIgnoreCase)
                             || orderCode.EndsWith("-COD", StringComparison.OrdinalIgnoreCase)))
        {
            orderCode = orderCode[..lastDash];
        }

        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(orderCode, cancellationToken);
        if (order is null)
        {
            return new PaymentCallbackResultDto
            {
                Success = false,
                SecureHashVerified = verification.IsValidSignature,
                TransactionRef = verification.TransactionRef,
                Message = $"Không tìm thấy đơn hàng tương ứng với mã {orderCode}."
            };
        }

        // Kiểm tra Idempotency: Nếu đơn hàng đã xử lý Paid hoặc Cancelled rồi thì trả về kết quả luôn
        var paymentTx = order.PaymentTransactions
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault(p => p.TransactionRef == verification.TransactionRef)
            ?? order.PaymentTransactions.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return new PaymentCallbackResultDto
            {
                Success = true,
                SecureHashVerified = true,
                OrderCode = order.OrderCode,
                TransactionRef = verification.TransactionRef,
                GatewayTransactionNo = paymentTx?.GatewayTransactionNo ?? verification.GatewayTransactionNo,
                ResponseCode = "00",
                PaymentMethod = strategy.MethodName,
                Amount = order.FinalAmount,
                Message = $"Đơn hàng {order.OrderCode} đã được xác nhận thanh toán trước đó."
            };
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (paymentTx is not null)
            {
                paymentTx.GatewayTransactionNo = verification.GatewayTransactionNo;
                paymentTx.ResponseCode = verification.ResponseCode;
                paymentTx.SecureHashVerified = verification.IsValidSignature;
                paymentTx.CallbackPayload = verification.RawPayload;
                if (verification.IsPaymentSuccess)
                {
                    paymentTx.PaidAt = DateTime.UtcNow;
                }
                _unitOfWork.PaymentTransactions.Update(paymentTx);
            }

            if (verification.IsValidSignature && verification.IsPaymentSuccess)
            {
                // Luồng B - Bước 2A: Thanh toán thành công (vnp_ResponseCode == "00")
                await _warehouseFefoFacade.ConfirmSaleExportFromReservationAsync(
                    order,
                    $"Hệ thống thanh toán {strategy.MethodName}",
                    cancellationToken);

                order.OrderStatus = OrderStatus.Processing;
                order.PaymentStatus = PaymentStatus.Paid;
                order.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Orders.Update(order);
            }
            else
            {
                // Luồng B - Bước 2B: Thanh toán thất bại hoặc khách hủy giao dịch
                await _warehouseFefoFacade.ReleaseReservationForCancelledOrderAsync(
                    order,
                    $"Hệ thống thanh toán {strategy.MethodName}",
                    "Khách hàng hủy giao dịch hoặc thanh toán chưa hoàn tất",
                    cancellationToken);

                order.OrderStatus = OrderStatus.Cancelled;
                order.PaymentStatus = PaymentStatus.Failed;
                order.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Orders.Update(order);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new PaymentCallbackResultDto
            {
                Success = verification.IsPaymentSuccess,
                SecureHashVerified = verification.IsValidSignature,
                OrderCode = order.OrderCode,
                TransactionRef = verification.TransactionRef,
                GatewayTransactionNo = verification.GatewayTransactionNo,
                ResponseCode = verification.ResponseCode,
                PaymentMethod = strategy.MethodName,
                Amount = order.FinalAmount,
                Message = verification.IsPaymentSuccess
                    ? $"Cảm ơn quý khách! Đơn hàng {order.OrderCode} đã được thanh toán thành công và đang được kho lạnh S-NEXT chuẩn bị giao hàng."
                    : $"Giao dịch thanh toán cho đơn hàng {order.OrderCode} đã bị hủy. Số lượng sản phẩm đã được hoàn trả về kho hàng."
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
