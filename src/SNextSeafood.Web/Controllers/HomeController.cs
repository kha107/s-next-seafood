using Microsoft.AspNetCore.Mvc;
using SNextSeafood.Application.Facades;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Web.Models;

namespace SNextSeafood.Web.Controllers;

public class HomeController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseFefoFacade _fefoFacade;

    public HomeController(IUnitOfWork unitOfWork, IWarehouseFefoFacade fefoFacade)
    {
        _unitOfWork = unitOfWork;
        _fefoFacade = fefoFacade;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId = null, string? search = null)
    {
        var categories = await _unitOfWork.Products.GetAllCategoriesAsync();
        var products = await _unitOfWork.Products.GetCatalogProductsWithBatchesAsync(categoryId, search);
        var vouchers = await _unitOfWork.Orders.GetAllActiveVouchersAsync();
        var nearExpiryBatches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync("nearexpiry");

        var vm = new CatalogIndexViewModel
        {
            Categories = categories,
            Products = products,
            ActiveVouchers = vouchers,
            SelectedCategoryId = categoryId,
            SearchQuery = search,
            NearExpiryBatchCount = nearExpiryBatches.Count
        };

        return View(vm);
    }

    [HttpGet("Catalog/Details/{slug}")]
    public async Task<IActionResult> Details(string slug, int? variantId = null, int previewQty = 3)
    {
        var product = await _unitOfWork.Products.GetProductBySlugWithDetailsAsync(slug);
        if (product is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var selectedVariant = variantId.HasValue
            ? product.Variants.FirstOrDefault(v => v.VariantId == variantId.Value) ?? product.Variants.First()
            : product.Variants.First();

        var fefoPreview = await _fefoFacade.SimulateFefoAllocationAsync(selectedVariant.VariantId, Math.Max(1, previewQty));
        var allProducts = await _unitOfWork.Products.GetCatalogProductsWithBatchesAsync(product.CategoryId);
        var vouchers = await _unitOfWork.Orders.GetAllActiveVouchersAsync();

        var vm = new ProductDetailViewModel
        {
            Product = product,
            SelectedVariant = selectedVariant,
            FefoPreview = fefoPreview,
            RelatedProducts = allProducts.Where(p => p.ProductId != product.ProductId).Take(4).ToList(),
            ActiveVouchers = vouchers
        };

        return View(vm);
    }
}
