using Microsoft.EntityFrameworkCore;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Infrastructure.Data;

namespace SNextSeafood.Infrastructure.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(SNextDbContext context) : base(context)
    {
    }

    public async Task<Order?> GetOrderWithFullTraceabilityAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.BatchAllocations)
                    .ThenInclude(a => a.Batch)
                        .ThenInclude(b => b!.Supplier)
            .Include(o => o.PaymentTransactions)
            .Include(o => o.InventoryTransactions)
            .FirstOrDefaultAsync(o => o.OrderCode == orderCode, cancellationToken);
    }

    public async Task<Order?> GetOrderByIdWithAllocationsAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.BatchAllocations)
                    .ThenInclude(a => a.Batch)
                        .ThenInclude(b => b!.Supplier)
            .Include(o => o.PaymentTransactions)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetAllOrdersWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v!.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.BatchAllocations)
            .Include(o => o.PaymentTransactions)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Voucher?> GetActiveVoucherByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == normalized && v.IsActive && v.ExpiryDate > DateTime.UtcNow, cancellationToken);
    }

    public async Task<IReadOnlyList<Voucher>> GetAllActiveVouchersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Vouchers
            .Where(v => v.IsActive && v.ExpiryDate > DateTime.UtcNow)
            .OrderByDescending(v => v.IsClearanceVoucher)
            .ThenByDescending(v => v.DiscountPercent)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
