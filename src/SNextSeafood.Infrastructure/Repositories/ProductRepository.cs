using Microsoft.EntityFrameworkCore;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Infrastructure.Data;

namespace SNextSeafood.Infrastructure.Repositories;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(SNextDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Product>> GetCatalogProductsWithBatchesAsync(
        int? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.WarehouseBatches)
                    .ThenInclude(b => b.Supplier)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.ShortDescription.ToLower().Contains(term) ||
                p.Origin.ToLower().Contains(term) ||
                p.Variants.Any(v => v.Sku.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.ProductId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetProductBySlugWithDetailsAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
                .ThenInclude(v => v.WarehouseBatches)
                    .ThenInclude(b => b.Supplier)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);
    }

    public async Task<ProductVariant?> GetVariantWithProductAndBatchesAsync(int variantId, CancellationToken cancellationToken = default)
    {
        return await _context.ProductVariants
            .Include(v => v.Product)
                .ThenInclude(p => p!.Category)
            .Include(v => v.WarehouseBatches)
                .ThenInclude(b => b.Supplier)
            .FirstOrDefaultAsync(v => v.VariantId == variantId, cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .OrderBy(c => c.DisplayOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
