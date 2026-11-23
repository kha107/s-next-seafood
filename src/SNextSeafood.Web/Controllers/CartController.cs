using Microsoft.AspNetCore.Mvc;
using SNextSeafood.Application.DTOs;
using SNextSeafood.Application.Facades;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Web.Helpers;
using SNextSeafood.Web.Models;

namespace SNextSeafood.Web.Controllers;

public class CartController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseFefoFacade _fefoFacade;
    private readonly IOrderCheckoutFacade _checkoutFacade;

    public CartController(
        IUnitOfWork unitOfWork,
        IWarehouseFefoFacade fefoFacade,
        IOrderCheckoutFacade checkoutFacade)
    {
        _unitOfWork = unitOfWork;
        _fefoFacade = fefoFacade;
        _checkoutFacade = checkoutFacade;
    }

    [HttpGet("Cart")]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildCartCheckoutViewModelAsync();
        return View(vm);
    }

    [HttpPost("Cart/Add")]
    public async Task<IActionResult> Add(int variantId, int quantity = 1, bool buyNow = false)
    {
        var variant = await _unitOfWork.Products.GetVariantWithProductAndBatchesAsync(variantId);
        if (variant is null || variant.Product is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy biến thể sản phẩm.";
            return RedirectToAction("Index", "Home");
        }

        var available = await _fefoFacade.GetAvailableQuantityAsync(variantId);
        if (available <= 0)
        {
            TempData["ErrorMessage"] = $"Sản phẩm {variant.Product.Name} ({variant.PackagingSpec}) hiện đã hết tồn kho khả dụng.";
            return RedirectToAction("Details", "Home", new { slug = variant.Product.Slug, variantId });
        }

        var cart = SessionCartHelper.GetCart(HttpContext.Session);
        var existing = cart.FirstOrDefault(c => c.VariantId == variantId);

        var eligibleBatches = await _unitOfWork.WarehouseBatches.GetFefoEligibleBatchesAsync(variantId);
        var earliestBatch = eligibleBatches.FirstOrDefault();

        if (existing is not null)
        {
            existing.Quantity = Math.Min(available, existing.Quantity + Math.Max(1, quantity));
            existing.AvailableStock = available;
            existing.EarliestBatchCode = earliestBatch?.BatchCode;
            existing.EarliestBatchExpiry = earliestBatch?.ExpiryDate;
        }
        else
        {
            cart.Add(new CartItemDto
            {
                ProductId = variant.ProductId,
                VariantId = variant.VariantId,
                ProductName = variant.Product.Name,
                Slug = variant.Product.Slug,
                Sku = variant.Sku,
                PackagingSpec = variant.PackagingSpec,
                StorageCondition = variant.Product.StorageCondition,
                ImageUrl = variant.Product.ImageUrl,
                UnitPrice = variant.Price,
                Quantity = Math.Min(available, Math.Max(1, quantity)),
                AvailableStock = available,
                EarliestBatchCode = earliestBatch?.BatchCode,
                EarliestBatchExpiry = earliestBatch?.ExpiryDate
            });
        }

        SessionCartHelper.SaveCart(HttpContext.Session, cart);
        TempData["SuccessMessage"] = $"Đã thêm {variant.Product.Name} ({variant.PackagingSpec}) vào giỏ hàng lạnh S-NEXT!";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Cart/UpdateQuantity")]
    public async Task<IActionResult> UpdateQuantity(int variantId, int quantity)
    {
        var cart = SessionCartHelper.GetCart(HttpContext.Session);
        var item = cart.FirstOrDefault(c => c.VariantId == variantId);
        if (item is not null)
        {
            if (quantity <= 0)
            {
                cart.Remove(item);
            }
            else
            {
                var available = await _fefoFacade.GetAvailableQuantityAsync(variantId);
                item.Quantity = Math.Min(available, quantity);
                item.AvailableStock = available;
            }
            SessionCartHelper.SaveCart(HttpContext.Session, cart);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Cart/Remove")]
    public IActionResult Remove(int variantId)
    {
        var cart = SessionCartHelper.GetCart(HttpContext.Session);
        cart.RemoveAll(c => c.VariantId == variantId);
        SessionCartHelper.SaveCart(HttpContext.Session, cart);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Cart/ApplyVoucher")]
    public async Task<IActionResult> ApplyVoucher(string? voucherCode)
    {
        var cart = SessionCartHelper.GetCart(HttpContext.Session);
        var subtotal = cart.Sum(c => c.LineTotal);

        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            SessionCartHelper.SetAppliedVoucher(HttpContext.Session, null);
            TempData["SuccessMessage"] = "Đã gỡ mã giảm giá.";
            return RedirectToAction(nameof(Index));
        }

        var (discount, voucher, message) = await _checkoutFacade.CalculateVoucherDiscountAsync(voucherCode, subtotal);
        if (voucher is not null && discount > 0)
        {
            SessionCartHelper.SetAppliedVoucher(HttpContext.Session, voucher.Code);
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Checkout/PlaceOrder")]
    public async Task<IActionResult> PlaceOrder(CheckoutRequestDto input)
    {
        var cart = SessionCartHelper.GetCart(HttpContext.Session);
        if (cart.Count == 0)
        {
            TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
            return RedirectToAction(nameof(Index));
        }

        input.VoucherCode = string.IsNullOrWhiteSpace(input.VoucherCode)
            ? SessionCartHelper.GetAppliedVoucher(HttpContext.Session)
            : input.VoucherCode;

        input.Items = cart.Select(c => new CheckoutItemInputDto
        {
            VariantId = c.VariantId,
            Quantity = c.Quantity
        }).ToList();

        var hostBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        var result = await _checkoutFacade.PlaceOrderAndInitiatePaymentAsync(input, hostBaseUrl, clientIp);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        SessionCartHelper.ClearCart(HttpContext.Session);
        SessionCartHelper.AuthorizeOrderInSession(HttpContext.Session, result.OrderCode);

        if (result.RequiresOnlineRedirect && !string.IsNullOrWhiteSpace(result.PaymentRedirectUrl))
        {
            return Redirect(result.PaymentRedirectUrl);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction("Invoice", "Payment", new { orderCode = result.OrderCode });
    }

    private async Task<CartCheckoutViewModel> BuildCartCheckoutViewModelAsync()
    {
        var cart = SessionCartHelper.GetCart(HttpContext.Session);
        var fefoMap = new Dictionary<int, FefoSimulationResultDto>();

        foreach (var item in cart)
        {
            var available = await _fefoFacade.GetAvailableQuantityAsync(item.VariantId);
            item.AvailableStock = available;
            var sim = await _fefoFacade.SimulateFefoAllocationAsync(item.VariantId, item.Quantity);
            fefoMap[item.VariantId] = sim;
        }

        var subtotal = cart.Sum(c => c.LineTotal);
        var voucherCode = SessionCartHelper.GetAppliedVoucher(HttpContext.Session);
        var (discount, _, voucherMsg) = await _checkoutFacade.CalculateVoucherDiscountAsync(voucherCode, subtotal);
        var shippingFee = cart.Count == 0 ? 0m : (subtotal >= 500000m ? 0m : 30000m);
        var finalAmount = Math.Max(0m, subtotal - discount + shippingFee);
        var activeVouchers = await _unitOfWork.Orders.GetAllActiveVouchersAsync();

        var vm = new CartCheckoutViewModel
        {
            CartItems = cart,
            Subtotal = subtotal,
            DiscountAmount = discount,
            ShippingFee = shippingFee,
            FinalAmount = finalAmount,
            AppliedVoucherCode = discount > 0 ? voucherCode : null,
            VoucherMessage = voucherMsg,
            AvailableVouchers = activeVouchers,
            FefoAllocationsByVariant = fefoMap
        };
        vm.CheckoutInput.VoucherCode = vm.AppliedVoucherCode;

        return vm;
    }
}
