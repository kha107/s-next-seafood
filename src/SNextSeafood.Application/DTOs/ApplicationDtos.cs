using SNextSeafood.Domain.Enums;

namespace SNextSeafood.Application.DTOs;

public class FefoBatchAllocationPreviewDto
{
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public int AllocatedQuantity { get; set; }
    public DateTime ManufactureDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public decimal ImportUnitPrice { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string StorageRack { get; set; } = string.Empty;
    public bool IsNearExpiry => DaysUntilExpiry > 0 && DaysUntilExpiry <= 15;
}

public class FefoSimulationResultDto
{
    public int ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string PackagingSpec { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int TotalAvailableQuantity { get; set; }
    public bool CanFulfill { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<FefoBatchAllocationPreviewDto> Allocations { get; set; } = new();
}

public class ImportBatchRequestDto
{
    public int ProductVariantId { get; set; }
    public int SupplierId { get; set; }
    public string? CustomBatchCode { get; set; }
    public DateTime ManufactureDate { get; set; } = DateTime.UtcNow.Date.AddDays(-2);
    public DateTime ExpiryDate { get; set; } = DateTime.UtcNow.Date.AddMonths(6);
    public decimal ImportUnitPrice { get; set; }
    public int InitialQuantity { get; set; }
    public string StorageRack { get; set; } = "KHO-LANH-A01";
    public string PerformedBy { get; set; } = "Trần Minh Tâm (Quản lý Kho WMS)";
    public string? Note { get; set; }
}

public class DiscardBatchRequestDto
{
    public int BatchId { get; set; }
    public int QuantityToDiscard { get; set; }
    public string Reason { get; set; } = "Xuất hủy lô hàng quá hạn sử dụng theo quy trình kiểm soát chất lượng chuỗi lạnh S-NEXT";
    public string PerformedBy { get; set; } = "Trần Minh Tâm (Quản lý Kho WMS)";
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public int VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string PackagingSpec { get; set; } = string.Empty;
    public string StorageCondition { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int AvailableStock { get; set; }
    public string? EarliestBatchCode { get; set; }
    public DateTime? EarliestBatchExpiry { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CheckoutRequestDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string District { get; set; } = "Quận 10, TP. Hồ Chí Minh";
    public string? DeliveryNote { get; set; }
    public string? VoucherCode { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.VNPay;
    public List<CheckoutItemInputDto> Items { get; set; } = new();
}

public class CheckoutItemInputDto
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}

public class CheckoutResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal FinalAmount { get; set; }
    public PaymentMethodType PaymentMethod { get; set; }
    public bool RequiresOnlineRedirect { get; set; }
    public string? PaymentRedirectUrl { get; set; }
    public string? TransactionRef { get; set; }
    public List<FefoBatchAllocationPreviewDto> AllocatedBatches { get; set; } = new();
}

public class PaymentCallbackResultDto
{
    public bool Success { get; set; }
    public bool SecureHashVerified { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string TransactionRef { get; set; } = string.Empty;
    public string GatewayTransactionNo { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class WmsDashboardSummaryDto
{
    public int TotalActiveBatches { get; set; }
    public int NearExpiryBatchCount { get; set; }
    public int ExpiredBatchCount { get; set; }
    public int LowStockBatchCount { get; set; }
    public int TotalRemainingUnits { get; set; }
    public int TotalReservedUnits { get; set; }
    public int TotalAvailableUnits { get; set; }
    public decimal TotalInventoryCostValue { get; set; }
    public decimal TotalRevenuePaid { get; set; }
    public decimal VnPayRevenue { get; set; }
    public decimal MoMoRevenue { get; set; }
    public decimal CodRevenue { get; set; }
    public int TotalOrdersCount { get; set; }
    public int PendingPaymentOrdersCount { get; set; }
    public decimal SpoilageRatePercent { get; set; }
    public decimal SpoilageCostValue { get; set; }
}
