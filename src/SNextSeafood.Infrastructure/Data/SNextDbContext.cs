using Microsoft.EntityFrameworkCore;
using SNextSeafood.Domain.Entities;

namespace SNextSeafood.Infrastructure.Data;

public class SNextDbContext : DbContext
{
    public SNextDbContext(DbContextOptions<SNextDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<WarehouseBatch> WarehouseBatches => Set<WarehouseBatch>();
    public DbSet<WarehouseReceipt> WarehouseReceipts => Set<WarehouseReceipt>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemBatchAllocation> OrderItemBatchAllocations => Set<OrderItemBatchAllocation>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Categories
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // Products
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductVariants
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("ProductVariants");
            entity.HasIndex(e => e.Sku).IsUnique();
            entity.HasOne(v => v.Product)
                  .WithMany(p => p.Variants)
                  .HasForeignKey(v => v.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Suppliers
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers");
            entity.HasIndex(e => e.SupplierCode).IsUnique();
        });

        // WarehouseBatches
        modelBuilder.Entity<WarehouseBatch>(entity =>
        {
            entity.ToTable("WarehouseBatches");
            entity.HasIndex(e => e.BatchCode).IsUnique();
            entity.HasIndex(e => new { e.ProductVariantId, e.ExpiryDate, e.ImportDate });

            entity.HasOne(b => b.ProductVariant)
                  .WithMany(v => v.WarehouseBatches)
                  .HasForeignKey(b => b.ProductVariantId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Supplier)
                  .WithMany(s => s.WarehouseBatches)
                  .HasForeignKey(b => b.SupplierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // WarehouseReceipts
        modelBuilder.Entity<WarehouseReceipt>(entity =>
        {
            entity.ToTable("WarehouseReceipts");
            entity.HasIndex(e => e.ReceiptCode).IsUnique();
            entity.HasOne(r => r.Supplier)
                  .WithMany(s => s.WarehouseReceipts)
                  .HasForeignKey(r => r.SupplierId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // InventoryTransactions
        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.ToTable("InventoryTransactions");
            entity.HasIndex(e => e.ReceiptCode);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(t => t.Batch)
                  .WithMany(b => b.InventoryTransactions)
                  .HasForeignKey(t => t.BatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.WarehouseReceipt)
                  .WithMany(r => r.InventoryTransactions)
                  .HasForeignKey(t => t.WarehouseReceiptId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.Order)
                  .WithMany(o => o.InventoryTransactions)
                  .HasForeignKey(t => t.OrderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Vouchers
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.ToTable("Vouchers");
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Orders
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasIndex(e => e.OrderCode).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // OrderItems
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasOne(i => i.Order)
                  .WithMany(o => o.OrderItems)
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.ProductVariant)
                  .WithMany(v => v.OrderItems)
                  .HasForeignKey(i => i.ProductVariantId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItemBatchAllocations
        modelBuilder.Entity<OrderItemBatchAllocation>(entity =>
        {
            entity.ToTable("OrderItemBatchAllocations");
            entity.HasOne(a => a.OrderItem)
                  .WithMany(i => i.BatchAllocations)
                  .HasForeignKey(a => a.OrderItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Batch)
                  .WithMany(b => b.OrderAllocations)
                  .HasForeignKey(a => a.BatchId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // PaymentTransactions
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");
            entity.HasIndex(e => e.TransactionRef);
            entity.HasOne(p => p.Order)
                  .WithMany(o => o.PaymentTransactions)
                  .HasForeignKey(p => p.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
