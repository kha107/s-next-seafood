using System.Linq.Expressions;
using SNextSeafood.Domain.Entities;

namespace SNextSeafood.Domain.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IProductRepository : IGenericRepository<Product>
{
    Task<IReadOnlyList<Product>> GetCatalogProductsWithBatchesAsync(int? categoryId = null, string? search = null, CancellationToken cancellationToken = default);
    Task<Product?> GetProductBySlugWithDetailsAsync(string slug, CancellationToken cancellationToken = default);
    Task<ProductVariant?> GetVariantWithProductAndBatchesAsync(int variantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
}

public interface IWarehouseBatchRepository : IGenericRepository<WarehouseBatch>
{
    /// <summary>
    /// Lấy danh sách các lô hàng khả dụng theo thuật toán FEFO:
    /// RemainingQuantity - ReservedQuantity > 0, ExpiryDate > UtcNow, sắp xếp ExpiryDate ASC, ImportDate ASC.
    /// </summary>
    Task<IReadOnlyList<WarehouseBatch>> GetFefoEligibleBatchesAsync(int productVariantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarehouseBatch>> GetAllBatchesWithDetailsAsync(string? filter = null, CancellationToken cancellationToken = default);
    Task<WarehouseBatch?> GetBatchWithDetailsAsync(int batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryTransaction>> GetRecentInventoryTransactionsAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Supplier>> GetAllSuppliersAsync(CancellationToken cancellationToken = default);
}

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetOrderWithFullTraceabilityAsync(string orderCode, CancellationToken cancellationToken = default);
    Task<Order?> GetOrderByIdWithAllocationsAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetAllOrdersWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<Voucher?> GetActiveVoucherByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Voucher>> GetAllActiveVouchersAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWork : IDisposable
{
    IProductRepository Products { get; }
    IWarehouseBatchRepository WarehouseBatches { get; }
    IOrderRepository Orders { get; }
    IGenericRepository<WarehouseReceipt> WarehouseReceipts { get; }
    IGenericRepository<InventoryTransaction> InventoryTransactions { get; }
    IGenericRepository<OrderItemBatchAllocation> BatchAllocations { get; }
    IGenericRepository<PaymentTransaction> PaymentTransactions { get; }
    IGenericRepository<Supplier> Suppliers { get; }
    IGenericRepository<Voucher> Vouchers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
