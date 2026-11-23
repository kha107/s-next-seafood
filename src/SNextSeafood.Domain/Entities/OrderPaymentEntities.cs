using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SNextSeafood.Domain.Enums;

namespace SNextSeafood.Domain.Entities;

public class Voucher
{
    [Key]
    public int VoucherId { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Description { get; set; } = string.Empty;

    public int DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxDiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinOrderAmount { get; set; }

    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsClearanceVoucher { get; set; }
}

public class Order
{
    [Key]
    public int OrderId { get; set; }

    /// <summary>
    /// Mã đơn hàng (VD: ORD-20261116-8801)
    /// </summary>
    [Required, MaxLength(80)]
    public string OrderCode { get; set; } = string.Empty;

    [MaxLength(80)]
    public string CustomerId { get; set; } = "GUEST";

    [Required, MaxLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(150)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, MaxLength(350)]
    public string ShippingAddress { get; set; } = string.Empty;

    [MaxLength(120)]
    public string District { get; set; } = "Quận 10, TP. Hồ Chí Minh";

    [MaxLength(400)]
    public string? DeliveryNote { get; set; }

    [MaxLength(50)]
    public string? AppliedVoucherCode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalAmount { get; set; }

    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.VNPay;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.PendingPayment;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}

public class OrderItem
{
    [Key]
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    public ICollection<OrderItemBatchAllocation> BatchAllocations { get; set; } = new List<OrderItemBatchAllocation>();
}

/// <summary>
/// Lưu chính xác đơn hàng đã phân bổ bao nhiêu gói từ BatchCode nào theo thuật toán FEFO để truy xuất nguồn gốc thực phẩm.
/// </summary>
public class OrderItemBatchAllocation
{
    [Key]
    public int AllocationId { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int BatchId { get; set; }
    public WarehouseBatch? Batch { get; set; }

    [Required, MaxLength(80)]
    public string BatchCode { get; set; } = string.Empty;

    public int AllocatedQuantity { get; set; }

    public DateTime ManufactureDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportUnitPrice { get; set; }

    public bool IsReservationReleased { get; set; }
    public bool IsExportConfirmed { get; set; }
}

public class PaymentTransaction
{
    [Key]
    public int PaymentTransactionId { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, MaxLength(40)]
    public string PaymentMethod { get; set; } = "VNPay"; // VNPay, MoMo, COD

    /// <summary>
    /// Mã tham chiếu giao dịch gửi sang cổng thanh toán (vnp_TxnRef)
    /// </summary>
    [Required, MaxLength(100)]
    public string TransactionRef { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch phản hồi từ cổng thanh toán (vnp_TransactionNo / MoMo TransId)
    /// </summary>
    [MaxLength(100)]
    public string? GatewayTransactionNo { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Mã phản hồi từ cổng thanh toán (VD: "00" = Thành công, "24" = Khách hủy)
    /// </summary>
    [MaxLength(40)]
    public string ResponseCode { get; set; } = "PENDING";

    /// <summary>
    /// Trạng thái kiểm tra chữ ký điện tử HMAC-SHA512 (vnp_SecureHash)
    /// </summary>
    public bool SecureHashVerified { get; set; }

    [MaxLength(3000)]
    public string? CallbackPayload { get; set; }

    [MaxLength(1500)]
    public string? PaymentUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}
