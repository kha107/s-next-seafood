using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNextSeafood.Application.DTOs;
using SNextSeafood.Application.Facades;
using SNextSeafood.Application.Strategies;
using SNextSeafood.Domain.Interfaces;

namespace SNextSeafood.Web.Controllers.Api;

[ApiController]
[Route("api/catalog")]
[Produces("application/json")]
[Tags("1. Catalog & Real-Time FEFO Stock API")]
public class CatalogApiController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseFefoFacade _fefoFacade;

    public CatalogApiController(IUnitOfWork unitOfWork, IWarehouseFefoFacade fefoFacade)
    {
        _unitOfWork = unitOfWork;
        _fefoFacade = fefoFacade;
    }

    /// <summary>
    /// Lấy danh sách sản phẩm Thủy hải sản S-NEXT kèm tồn kho khả dụng (AvailableQuantity) và hạn sử dụng lô ưu tiên FEFO.
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetCatalogProducts([FromQuery] int? categoryId = null, [FromQuery] string? search = null)
    {
        var products = await _unitOfWork.Products.GetCatalogProductsWithBatchesAsync(categoryId, search);
        var now = DateTime.UtcNow;

        var response = products.Select(p => new
        {
            p.ProductId,
            p.Name,
            p.Slug,
            CategoryName = p.Category?.Name,
            p.Origin,
            p.StorageCondition,
            p.PackagingMethod,
            p.ImageUrl,
            Variants = p.Variants.Select(v =>
            {
                var eligibleBatches = v.WarehouseBatches
                    .Where(b => b.ExpiryDate > now && (b.RemainingQuantity - b.ReservedQuantity) > 0)
                    .OrderBy(b => b.ExpiryDate)
                    .ThenBy(b => b.ImportDate)
                    .ToList();

                var firstFefoBatch = eligibleBatches.FirstOrDefault();

                return new
                {
                    v.VariantId,
                    v.Sku,
                    v.PackagingSpec,
                    v.Price,
                    v.SafetyStockThreshold,
                    AvailableQuantity = eligibleBatches.Sum(b => Math.Max(0, b.RemainingQuantity - b.ReservedQuantity)),
                    TotalReservedQuantity = v.WarehouseBatches.Sum(b => b.ReservedQuantity),
                    CurrentFefoBatch = firstFefoBatch is null ? null : new
                    {
                        firstFefoBatch.BatchCode,
                        firstFefoBatch.ManufactureDate,
                        firstFefoBatch.ExpiryDate,
                        firstFefoBatch.DaysUntilExpiry,
                        BatchAvailable = firstFefoBatch.AvailableQuantity,
                        Status = firstFefoBatch.BatchStatus.ToString()
                    }
                };
            })
        });

        return Ok(response);
    }
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/warehouse")]
[Produces("application/json")]
[Tags("2. Warehouse FEFO Management API (WMS - Admin Only)")]
public class WarehouseFefoApiController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseFefoFacade _fefoFacade;

    public WarehouseFefoApiController(IUnitOfWork unitOfWork, IWarehouseFefoFacade fefoFacade)
    {
        _unitOfWork = unitOfWork;
        _fefoFacade = fefoFacade;
    }

    /// <summary>
    /// Lấy danh sách tất cả lô hàng trong kho lạnh S-NEXT (Hỗ trợ lọc: all | nearexpiry | expired | lowstock | reserved).
    /// </summary>
    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches([FromQuery] string? filter = null)
    {
        var batches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync(filter);
        var data = batches.Select(b => new
        {
            b.BatchId,
            b.BatchCode,
            ProductName = b.ProductVariant?.Product?.Name,
            Sku = b.ProductVariant?.Sku,
            PackagingSpec = b.ProductVariant?.PackagingSpec,
            SupplierName = b.Supplier?.Name,
            b.ManufactureDate,
            b.ExpiryDate,
            b.DaysUntilExpiry,
            b.ImportDate,
            b.ImportUnitPrice,
            b.InitialQuantity,
            b.RemainingQuantity,
            b.ReservedQuantity,
            b.AvailableQuantity,
            b.StorageRack,
            BatchStatus = b.BatchStatus.ToString()
        });
        return Ok(data);
    }

    /// <summary>
    /// Mô phỏng thuật toán phân bổ lô hàng FEFO (First Expired, First Out) khi khách đặt số lượng Q.
    /// Ví dụ: variantId=1, quantity=10 sẽ tự động lấy hết 6 gói ở lô cận date LOT-202611-001 rồi tách lấy tiếp 4 gói ở lô LOT-202611-002.
    /// </summary>
    [HttpGet("simulate-fefo")]
    public async Task<IActionResult> SimulateFefo([FromQuery] int variantId = 1, [FromQuery] int quantity = 10)
    {
        var result = await _fefoFacade.SimulateFefoAllocationAsync(variantId, quantity);
        return Ok(result);
    }

    /// <summary>
    /// Lập phiếu nhập kho lô hàng mới từ Nhà cung cấp (Ghi nhận WarehouseReceipts, WarehouseBatches và InventoryTransactions loại Import).
    /// </summary>
    [HttpPost("import-batch")]
    public async Task<IActionResult> ImportBatch([FromBody] ImportBatchRequestDto request)
    {
        try
        {
            var batch = await _fefoFacade.ImportNewBatchAsync(request);
            return Ok(new
            {
                Success = true,
                Message = $"Đã nhập kho thành công lô hàng {batch.BatchCode} (Số lượng: {batch.InitialQuantity}).",
                batch.BatchId,
                batch.BatchCode,
                batch.RemainingQuantity,
                batch.ExpiryDate,
                Status = batch.BatchStatus.ToString()
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Lập phiếu xuất hủy lô hàng quá hạn hoặc hư hỏng (Ghi nhận InventoryTransactions loại SpoilageDiscard).
    /// </summary>
    [HttpPost("discard-batch")]
    public async Task<IActionResult> DiscardBatch([FromBody] DiscardBatchRequestDto request)
    {
        try
        {
            var tx = await _fefoFacade.DiscardSpoiledOrExpiredBatchAsync(request);
            return Ok(new
            {
                Success = true,
                Message = tx.Note,
                tx.TransactionId,
                tx.ReceiptCode,
                tx.QuantityChanged,
                tx.RemainingQuantityAfter,
                tx.CreatedAt
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Tra cứu lịch sử biến động thẻ kho (Import, Reservation, SaleExport, ReleaseReservation, SpoilageDiscard).
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetInventoryTransactions([FromQuery] int count = 50)
    {
        var list = await _unitOfWork.WarehouseBatches.GetRecentInventoryTransactionsAsync(count);
        var data = list.Select(t => new
        {
            t.TransactionId,
            t.ReceiptCode,
            TransactionType = t.TransactionType.ToString(),
            BatchCode = t.Batch?.BatchCode,
            ProductName = t.Batch?.ProductVariant?.Product?.Name,
            Sku = t.Batch?.ProductVariant?.Sku,
            OrderCode = t.Order?.OrderCode,
            t.QuantityChanged,
            t.RemainingQuantityAfter,
            t.ReservedQuantityAfter,
            t.PerformedBy,
            t.CreatedAt,
            t.Note
        });
        return Ok(data);
    }
}

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
[Tags("3. Orders, Stock Reservation & Traceability API")]
public class OrdersApiController : ControllerBase
{
    private readonly IOrderCheckoutFacade _checkoutFacade;
    private readonly IUnitOfWork _unitOfWork;

    public OrdersApiController(IOrderCheckoutFacade checkoutFacade, IUnitOfWork unitOfWork)
    {
        _checkoutFacade = checkoutFacade;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Tạo đơn hàng mới, phân bổ và giữ hàng tạm thời theo FEFO (Stock Reservation), áp dụng Voucher và sinh URL thanh toán VNPay (HMAC-SHA512).
    /// </summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto request)
    {
        var hostBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _checkoutFacade.PlaceOrderAndInitiatePaymentAsync(request, hostBaseUrl, clientIp);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Truy xuất nguồn gốc thực phẩm theo Mã đơn hàng (Xem chi tiết từng gói hàng xuất từ BatchCode nào, NSX, HSD, Nhà cung cấp).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("{orderCode}/traceability")]
    public async Task<IActionResult> GetOrderTraceability(string orderCode)
    {
        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(orderCode);
        if (order is null)
        {
            return NotFound(new { Message = $"Không tìm thấy đơn hàng {orderCode}" });
        }

        return Ok(new
        {
            order.OrderId,
            order.OrderCode,
            order.CustomerName,
            order.CustomerPhone,
            order.ShippingAddress,
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            PaymentMethod = order.PaymentMethod.ToString(),
            order.TotalAmount,
            order.DiscountAmount,
            order.ShippingFee,
            order.FinalAmount,
            order.AppliedVoucherCode,
            order.CreatedAt,
            Items = order.OrderItems.Select(i => new
            {
                ProductName = i.ProductVariant?.Product?.Name,
                Sku = i.ProductVariant?.Sku,
                PackagingSpec = i.ProductVariant?.PackagingSpec,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                FefoBatchAllocations = i.BatchAllocations.Select(a => new
                {
                    a.BatchCode,
                    a.AllocatedQuantity,
                    a.ManufactureDate,
                    a.ExpiryDate,
                    Supplier = a.Batch?.Supplier?.Name,
                    StorageRack = a.Batch?.StorageRack,
                    a.IsExportConfirmed,
                    a.IsReservationReleased
                })
            }),
            PaymentTransactions = order.PaymentTransactions.Select(p => new
            {
                p.PaymentMethod,
                p.TransactionRef,
                p.GatewayTransactionNo,
                p.Amount,
                p.ResponseCode,
                p.SecureHashVerified,
                p.CreatedAt,
                p.PaidAt
            })
        });
    }
}

[ApiController]
[Route("api/payments")]
[Produces("application/json")]
[Tags("4. Payment Gateway Webhook / IPN (VNPay HMAC-SHA512 & MoMo)")]
public class PaymentWebhookApiController : ControllerBase
{
    private readonly IOrderCheckoutFacade _checkoutFacade;
    private readonly IPaymentStrategyFactory _strategyFactory;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentWebhookApiController(
        IOrderCheckoutFacade checkoutFacade,
        IPaymentStrategyFactory strategyFactory,
        IUnitOfWork unitOfWork)
    {
        _checkoutFacade = checkoutFacade;
        _strategyFactory = strategyFactory;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Endpoint Webhook IPN chính thức (/api/payments/ipn): Kiểm tra chữ ký vnp_SecureHash (HMAC-SHA512),
    /// nếu ResponseCode == "00" thì trừ chính thức RemainingQuantity và giảm ReservedQuantity; nếu thất bại thì hoàn giữ hàng.
    /// </summary>
    [HttpGet("ipn")]
    public async Task<IActionResult> ProcessIpnGet()
    {
        var queryDict = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var result = await _checkoutFacade.ProcessPaymentCallbackAsync(queryDict);

        return Ok(new
        {
            RspCode = result.SecureHashVerified ? "00" : "97",
            Message = result.Message,
            Details = result
        });
    }

    /// <summary>
    /// Công cụ giả lập nhanh Callback thanh toán trên Swagger/Postman cho đơn hàng đang chờ thanh toán (PendingPayment).
    /// Cho phép chọn responseCode = "00" (Thành công -> Trừ kho FEFO) hoặc "24" (Khách hủy -> Hoàn trả ReservedQuantity).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("simulate-callback")]
    public async Task<IActionResult> SimulateSignedCallback(
        [FromQuery] string orderCode = "ORD-20261116-1004",
        [FromQuery] string responseCode = "00")
    {
        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(orderCode);
        if (order is null)
        {
            return NotFound(new { Message = $"Không tìm thấy đơn hàng {orderCode}" });
        }

        var latestTx = order.PaymentTransactions.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var txnRef = latestTx?.TransactionRef ?? $"{order.OrderCode}-VNP";
        var strategy = _strategyFactory.GetStrategy(order.PaymentMethod);
        var hostBaseUrl = $"{Request.Scheme}://{Request.Host}";

        var signedUrl = strategy.BuildSignedCallbackUrl(hostBaseUrl, "/api/payments/ipn", txnRef, order.FinalAmount, responseCode);
        var uri = new Uri(signedUrl);
        var parsedQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)
            .ToDictionary(k => k.Key, v => v.Value.ToString());

        var callbackResult = await _checkoutFacade.ProcessPaymentCallbackAsync(parsedQuery);
        return Ok(callbackResult);
    }
}
