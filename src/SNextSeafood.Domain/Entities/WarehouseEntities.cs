using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SNextSeafood.Domain.Enums;

namespace SNextSeafood.Domain.Entities;

public class Supplier
{
    [Key]
    public int SupplierId { get; set; }

    [Required, MaxLength(50)]
    public string SupplierCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ContactPerson { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(150)]
    public string ColdStorageStandard { get; set; } = "ISO 22000 / HACCP (-18°C)";

    public ICollection<WarehouseBatch> WarehouseBatches { get; set; } = new List<WarehouseBatch>();
    public ICollection<WarehouseReceipt> WarehouseReceipts { get; set; } = new List<WarehouseReceipt>();
}

public class WarehouseBatch
{
    [Key]
    public int BatchId { get; set; }

    /// <summary>
    /// Mã lô hàng (VD: LOT-202611-001)
    /// </summary>
    [Required, MaxLength(80)]
    public string BatchCode { get; set; } = string.Empty;

    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>
    /// Ngày sản xuất / cấp đông (NSX)
    /// </summary>
    public DateTime ManufactureDate { get; set; }

    /// <summary>
    /// Hạn sử dụng (HSD)
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Ngày nhập kho S-NEXT
    /// </summary>
    public DateTime ImportDate { get; set; }

    /// <summary>
    /// Giá vốn nhập (VNĐ/đơn vị quy cách)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ImportUnitPrice { get; set; }

    /// <summary>
    /// Số lượng nhập ban đầu
    /// </summary>
    public int InitialQuantity { get; set; }

    /// <summary>
    /// Tồn thực tế trong kho
    /// </summary>
    public int RemainingQuantity { get; set; }

    /// <summary>
    /// Số lượng đang giữ cho đơn chờ thanh toán online (Stock Reservation)
    /// </summary>
    public int ReservedQuantity { get; set; }

    [MaxLength(80)]
    public string StorageRack { get; set; } = "KHO-LANH-A01";

    public BatchStatus BatchStatus { get; set; } = BatchStatus.Active;

    /// <summary>
    /// Tồn kho khả dụng (AvailableQuantity = RemainingQuantity - ReservedQuantity nếu còn hạn sử dụng)
    /// </summary>
    [NotMapped]
    public int AvailableQuantity =>
        (ExpiryDate > DateTime.UtcNow && BatchStatus != BatchStatus.Expired && BatchStatus != BatchStatus.Depleted)
            ? Math.Max(0, RemainingQuantity - ReservedQuantity)
            : 0;

    [NotMapped]
    public int DaysUntilExpiry => (int)Math.Ceiling((ExpiryDate.Date - DateTime.UtcNow.Date).TotalDays);

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<OrderItemBatchAllocation> OrderAllocations { get; set; } = new List<OrderItemBatchAllocation>();
}

public class WarehouseReceipt
{
    [Key]
    public int ReceiptId { get; set; }

    /// <summary>
    /// Mã phiếu nhập/xuất (VD: NK-202611-01, XK-202611-05)
    /// </summary>
    [Required, MaxLength(80)]
    public string ReceiptCode { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string ReceiptType { get; set; } = "Import"; // Import, SaleExport, SpoilageDiscard

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(120)]
    public string PerformedBy { get; set; } = "Admin Kho S-NEXT";

    [MaxLength(500)]
    public string? Note { get; set; }

    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}

public class InventoryTransaction
{
    [Key]
    public int TransactionId { get; set; }

    /// <summary>
    /// Mã phiếu tham chiếu (VD: NK-202611-01, XK-202611-05, HOLD-202611-01)
    /// </summary>
    [Required, MaxLength(80)]
    public string ReceiptCode { get; set; } = string.Empty;

    public int? WarehouseReceiptId { get; set; }
    public WarehouseReceipt? WarehouseReceipt { get; set; }

    public int BatchId { get; set; }
    public WarehouseBatch? Batch { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public InventoryTransactionType TransactionType { get; set; }

    /// <summary>
    /// Số lượng biến động (+ khi nhập/hoàn giữ, - khi xuất bán/xuất hủy/giữ chỗ)
    /// </summary>
    public int QuantityChanged { get; set; }

    public int RemainingQuantityAfter { get; set; }
    public int ReservedQuantityAfter { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(120)]
    public string PerformedBy { get; set; } = "Hệ thống FEFO";

    [MaxLength(500)]
    public string? Note { get; set; }
}
