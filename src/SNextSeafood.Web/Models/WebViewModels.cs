using SNextSeafood.Application.DTOs;
using SNextSeafood.Domain.Entities;

namespace SNextSeafood.Web.Models;

public class CatalogIndexViewModel
{
    public IReadOnlyList<Category> Categories { get; set; } = new List<Category>();
    public IReadOnlyList<Product> Products { get; set; } = new List<Product>();
    public IReadOnlyList<Voucher> ActiveVouchers { get; set; } = new List<Voucher>();
    public int? SelectedCategoryId { get; set; }
    public string? SearchQuery { get; set; }
    public int NearExpiryBatchCount { get; set; }
}

public class ProductDetailViewModel
{
    public Product Product { get; set; } = null!;
    public ProductVariant SelectedVariant { get; set; } = null!;
    public FefoSimulationResultDto FefoPreview { get; set; } = new();
    public IReadOnlyList<Product> RelatedProducts { get; set; } = new List<Product>();
    public IReadOnlyList<Voucher> ActiveVouchers { get; set; } = new List<Voucher>();
}

public class CartCheckoutViewModel
{
    public List<CartItemDto> CartItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal FinalAmount { get; set; }
    public string? AppliedVoucherCode { get; set; }
    public string? VoucherMessage { get; set; }
    public IReadOnlyList<Voucher> AvailableVouchers { get; set; } = new List<Voucher>();
    public Dictionary<int, FefoSimulationResultDto> FefoAllocationsByVariant { get; set; } = new();
    public CheckoutRequestDto CheckoutInput { get; set; } = new()
    {
        CustomerName = "Nguyễn Hoàng Nam",
        CustomerPhone = "0908668899",
        CustomerEmail = "hoangnam.nguyen@huflit.edu.vn",
        ShippingAddress = "828 Sư Vạn Hạnh, Phường 13, Quận 10, TP. Hồ Chí Minh",
        District = "Quận 10, TP. Hồ Chí Minh",
        DeliveryNote = "Đóng thùng giữ nhiệt đá Gel khô -18°C, giao giờ hành chính."
    };
}

public class PaymentGatewaySimulatorViewModel
{
    public Order Order { get; set; } = null!;
    public bool IsMoMo { get; set; }
    public string GatewayName { get; set; } = "Chuyển Khoản Ngân Hàng / VNPay QR";
    public string QrImageUrl { get; set; } = "/images/payments/vnpay-bank-qr.jpg";
    public string BankOrWalletName { get; set; } = "Ngân hàng TMCP Quân Đội (MB Bank) - VietQR / Napas 247";
    public string AccountHolder { get; set; } = "NGUYEN VAN TRUNG";
    public string AccountNumber { get; set; } = "0344460993";
    public string TransferContent { get; set; } = string.Empty;
    public string TransactionRef { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string SecureHash { get; set; } = string.Empty;
    public string ExternalSandboxUrl { get; set; } = string.Empty;
    public string SuccessCallbackUrl { get; set; } = string.Empty;
    public string CancelCallbackUrl { get; set; } = string.Empty;
    public string TamperedHashCallbackUrl { get; set; } = string.Empty;
}

public class OrderInvoiceViewModel
{
    public Order Order { get; set; } = null!;
    public PaymentCallbackResultDto? CallbackResult { get; set; }
}

public class AdminWarehouseViewModel
{
    public string ActiveFilter { get; set; } = "all";
    public IReadOnlyList<WarehouseBatch> Batches { get; set; } = new List<WarehouseBatch>();
    public WmsDashboardSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = new List<Supplier>();
    public IReadOnlyList<Product> Products { get; set; } = new List<Product>();
}

public class AdminImportAndTransactionsViewModel
{
    public int? SelectedVariantId { get; set; }
    public ImportBatchRequestDto ImportForm { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; set; } = new List<Supplier>();
    public IReadOnlyList<Product> Products { get; set; } = new List<Product>();
    public IReadOnlyList<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    public IReadOnlyList<WarehouseReceipt> Receipts { get; set; } = new List<WarehouseReceipt>();
}

public class AdminOrdersViewModel
{
    public string StatusFilter { get; set; } = "all";
    public IReadOnlyList<Order> Orders { get; set; } = new List<Order>();
    public WmsDashboardSummaryDto Summary { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public WmsDashboardSummaryDto Summary { get; set; } = new();
    public IReadOnlyList<Order> RecentOrders { get; set; } = new List<Order>();
    public IReadOnlyList<WarehouseBatch> AlertBatches { get; set; } = new List<WarehouseBatch>();
    public IReadOnlyList<InventoryTransaction> RecentTransactions { get; set; } = new List<InventoryTransaction>();
}
