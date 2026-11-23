using Microsoft.EntityFrameworkCore.Storage;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Infrastructure.Data;

namespace SNextSeafood.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SNextDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public IProductRepository Products { get; }
    public IWarehouseBatchRepository WarehouseBatches { get; }
    public IOrderRepository Orders { get; }
    public IGenericRepository<WarehouseReceipt> WarehouseReceipts { get; }
    public IGenericRepository<InventoryTransaction> InventoryTransactions { get; }
    public IGenericRepository<OrderItemBatchAllocation> BatchAllocations { get; }
    public IGenericRepository<PaymentTransaction> PaymentTransactions { get; }
    public IGenericRepository<Supplier> Suppliers { get; }
    public IGenericRepository<Voucher> Vouchers { get; }

    public UnitOfWork(SNextDbContext context)
    {
        _context = context;
        Products = new ProductRepository(_context);
        WarehouseBatches = new WarehouseBatchRepository(_context);
        Orders = new OrderRepository(_context);
        WarehouseReceipts = new GenericRepository<WarehouseReceipt>(_context);
        InventoryTransactions = new GenericRepository<InventoryTransaction>(_context);
        BatchAllocations = new GenericRepository<OrderItemBatchAllocation>(_context);
        PaymentTransactions = new GenericRepository<PaymentTransaction>(_context);
        Suppliers = new GenericRepository<Supplier>(_context);
        Vouchers = new GenericRepository<Voucher>(_context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            return;
        }
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (_currentTransaction is not null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
