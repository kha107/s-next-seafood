using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SNextSeafood.Domain.Entities;

public class Category
{
    [Key]
    public int CategoryId { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    [MaxLength(80)]
    public string IconClass { get; set; } = "bi-water";

    public int DisplayOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    [Key]
    public int ProductId { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(350)]
    public string ShortDescription { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Origin { get; set; } = "Việt Nam";

    /// <summary>
    /// Điều kiện bảo quản: Đông lạnh -18°C / Mát 0-4°C / Khô
    /// </summary>
    [Required, MaxLength(100)]
    public string StorageCondition { get; set; } = "Đông lạnh -18°C";

    [MaxLength(150)]
    public string PackagingMethod { get; set; } = "Đóng gói hút chân không IQF";

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsFeatured { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}

public class ProductVariant
{
    [Key]
    public int VariantId { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Mã SKU chuẩn hóa (VD: SNEXT-SALMON-500G)
    /// </summary>
    [Required, MaxLength(80)]
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// Quy cách đóng gói (VD: Khay 500g, Túi 1kg, Thùng 5kg)
    /// </summary>
    [Required, MaxLength(100)]
    public string PackagingSpec { get; set; } = "Khay 500g";

    public int WeightGrams { get; set; } = 500;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    /// <summary>
    /// Ngưỡng cảnh báo tồn tối thiểu (SafetyStockThreshold)
    /// </summary>
    public int SafetyStockThreshold { get; set; } = 15;

    public ICollection<WarehouseBatch> WarehouseBatches { get; set; } = new List<WarehouseBatch>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
