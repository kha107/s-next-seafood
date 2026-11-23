using Microsoft.EntityFrameworkCore;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Enums;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Infrastructure.Data;

namespace SNextSeafood.Infrastructure.Repositories;

public class WarehouseBatchRepository : GenericRepository<WarehouseBatch>, IWarehouseBatchRepository
{
    public WarehouseBatchRepository(SNextDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<WarehouseBatch>> GetFefoEligibleBatchesAsync(
        int productVariantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _context.WarehouseBatches
            .Include(b => b.Supplier)
            .Include(b => b.ProductVariant)
                .ThenInclude(v => v!.Product)
            .Where(b => b.ProductVariantId == productVariantId
                        && b.ExpiryDate > now
                        && b.BatchStatus != BatchStatus.Expired
                        && b.BatchStatus != BatchStatus.Depleted
                        && (b.RemainingQuantity - b.ReservedQuantity) > 0)
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.ImportDate)
            .ThenBy(b => b.BatchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseBatch>> GetAllBatchesWithDetailsAsync(
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var nearExpiryCutoff = now.AddDays(15);

        var query = _context.WarehouseBatches
            .Include(b => b.ProductVariant)
                .ThenInclude(v => v!.Product)
                    .ThenInclude(p => p!.Category)
            .Include(b => b.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            switch (filter.Trim().ToLowerInvariant())
            {
                case "nearexpiry":
                    query = query.Where(b => b.RemainingQuantity > 0 && b.ExpiryDate > now && b.ExpiryDate <= nearExpiryCutoff);
                    break;
                case "expired":
                    query = query.Where(b => b.ExpiryDate <= now || b.BatchStatus == BatchStatus.Expired);
                    break;
                case "lowstock":
                    query = query.Where(b => b.RemainingQuantity > 0 && b.ExpiryDate > now
                        && (b.RemainingQuantity - b.ReservedQuantity) <= b.ProductVariant!.SafetyStockThreshold);
                    break;
                case "reserved":
                    query = query.Where(b => b.ReservedQuantity > 0);
                    break;
            }
        }

        return await query
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.ImportDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseBatch?> GetBatchWithDetailsAsync(int batchId, CancellationToken cancellationToken = default)
    {
        return await _context.WarehouseBatches
            .Include(b => b.ProductVariant)
                .ThenInclude(v => v!.Product)
            .Include(b => b.Supplier)
            .FirstOrDefaultAsync(b => b.BatchId == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryTransaction>> GetRecentInventoryTransactionsAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryTransactions
            .Include(t => t.Batch)
                .ThenInclude(b => b!.ProductVariant)
                    .ThenInclude(v => v!.Product)
            .Include(t => t.Batch)
                .ThenInclude(b => b!.Supplier)
            .Include(t => t.Order)
            .Include(t => t.WarehouseReceipt)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.TransactionId)
            .Take(count)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Supplier>> GetAllSuppliersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Suppliers
            .OrderBy(s => s.SupplierCode)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
