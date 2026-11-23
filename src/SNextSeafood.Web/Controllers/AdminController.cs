using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNextSeafood.Application.DTOs;
using SNextSeafood.Application.Facades;
using SNextSeafood.Domain.Enums;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Web.Models;

namespace SNextSeafood.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWarehouseFefoFacade _fefoFacade;
    private readonly IWebHostEnvironment _env;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public AdminController(
        IUnitOfWork unitOfWork,
        IWarehouseFefoFacade fefoFacade,
        IWebHostEnvironment env)
    {
        _unitOfWork = unitOfWork;
        _fefoFacade = fefoFacade;
        _env = env;
    }

    [HttpGet("")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [HttpGet("Warehouse")]
    public async Task<IActionResult> Warehouse(string filter = "all")
    {
        await _fefoFacade.RefreshAllBatchStatusesAsync();

        var batches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync(filter == "all" ? null : filter);
        var summary = await BuildWmsDashboardSummaryAsync();
        var suppliers = await _unitOfWork.WarehouseBatches.GetAllSuppliersAsync();
        var products = await _unitOfWork.Products.GetCatalogProductsWithBatchesAsync();

        var vm = new AdminWarehouseViewModel
        {
            ActiveFilter = filter.ToLowerInvariant(),
            Batches = batches,
            Summary = summary,
            Suppliers = suppliers,
            Products = products
        };

        return View(vm);
    }

    [HttpPost("Warehouse/DiscardBatch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscardBatch(int batchId, int quantityToDiscard, string? reason)
    {
        try
        {
            var tx = await _fefoFacade.DiscardSpoiledOrExpiredBatchAsync(new DiscardBatchRequestDto
            {
                BatchId = batchId,
                QuantityToDiscard = quantityToDiscard,
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? "Xuất hủy lô hàng quá hạn / cận hạn theo quy trình kiểm soát chất lượng chuỗi lạnh S-NEXT"
                    : reason,
                PerformedBy = User.Identity?.Name ?? "Trần Minh Tâm (Admin Kho WMS)"
            });

            TempData["SuccessMessage"] = $"Đã lập phiếu xuất hủy [{tx.ReceiptCode}] thành công: {tx.Note}";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Warehouse));
    }

    [HttpGet("Warehouse/Import")]
    public async Task<IActionResult> Import(int? variantId = null)
    {
        var suppliers = await _unitOfWork.WarehouseBatches.GetAllSuppliersAsync();
        var products = await _unitOfWork.Products.GetCatalogProductsWithBatchesAsync();
        var transactions = await _unitOfWork.WarehouseBatches.GetRecentInventoryTransactionsAsync(100);
        var receipts = await _unitOfWork.WarehouseReceipts.GetAllAsync();

        var defaultVariantId = variantId
            ?? products.SelectMany(p => p.Variants).OrderBy(v => v.VariantId).FirstOrDefault()?.VariantId
            ?? 1;

        var vm = new AdminImportAndTransactionsViewModel
        {
            SelectedVariantId = defaultVariantId,
            Suppliers = suppliers,
            Products = products,
            Transactions = transactions,
            Receipts = receipts.OrderByDescending(r => r.CreatedAt).ToList(),
            ImportForm = new ImportBatchRequestDto
            {
                ProductVariantId = defaultVariantId,
                CustomBatchCode = $"LOT-{DateTime.UtcNow:yyyyMM}-{Random.Shared.Next(100, 999)}",
                ManufactureDate = DateTime.UtcNow.Date.AddDays(-2),
                ExpiryDate = DateTime.UtcNow.Date.AddMonths(6),
                ImportUnitPrice = 215000m,
                InitialQuantity = 40,
                StorageRack = "KHO-LANH-A02",
                PerformedBy = User.Identity?.Name ?? "Trần Minh Tâm (Quản lý kho)",
                Note = "Nhập bổ sung lô thủy hải sản cấp đông -18°C phục vụ kinh doanh"
            }
        };

        return View(vm);
    }

    [HttpPost("Warehouse/Import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        ImportBatchRequestDto request,
        IFormFile? productImageFile,
        string? productImageUrl)
    {
        try
        {
            var newBatch = await _fefoFacade.ImportNewBatchAsync(request);
            var imageUpdatedMsg = string.Empty;

            // Tích hợp cập nhật hình ảnh minh họa sản phẩm ngay khi nhập lô mới / nhập thêm
            if ((productImageFile is not null && productImageFile.Length > 0) || !string.IsNullOrWhiteSpace(productImageUrl))
            {
                var variant = await _unitOfWork.Products.GetVariantWithProductAndBatchesAsync(request.ProductVariantId);
                if (variant?.Product is not null)
                {
                    var savedUrl = await SaveOrResolveProductImageAsync(variant.Product.Slug, productImageFile, productImageUrl);
                    if (!string.IsNullOrWhiteSpace(savedUrl))
                    {
                        var trackedProduct = await _unitOfWork.Products.GetByIdAsync(variant.ProductId);
                        if (trackedProduct is not null)
                        {
                            trackedProduct.ImageUrl = savedUrl;
                            _unitOfWork.Products.Update(trackedProduct);
                            await _unitOfWork.SaveChangesAsync();
                            imageUpdatedMsg = " Đồng thời đã cập nhật hình ảnh minh họa mới cho sản phẩm!";
                        }
                    }
                }
            }

            TempData["SuccessMessage"] = $"Đã lập phiếu nhập kho lô mới [{newBatch.BatchCode}] với số lượng {newBatch.InitialQuantity} đơn vị (HSD: {newBatch.ExpiryDate:dd/MM/yyyy})!{imageUpdatedMsg}";
            return RedirectToAction(nameof(Import), new { variantId = request.ProductVariantId });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Lỗi nhập kho: {ex.Message}";
            return RedirectToAction(nameof(Import), new { variantId = request.ProductVariantId });
        }
    }

    [HttpPost("Products/UpdateImage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProductImage(int productId, IFormFile? productImageFile, string? productImageUrl)
    {
        try
        {
            var product = await _unitOfWork.Products.GetByIdAsync(productId);
            if (product is null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm cần cập nhật ảnh.";
                return RedirectToAction(nameof(Import));
            }

            var newUrl = await SaveOrResolveProductImageAsync(product.Slug, productImageFile, productImageUrl);
            if (string.IsNullOrWhiteSpace(newUrl))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn tệp ảnh (.jpg, .png, .webp) hoặc nhập đường dẫn URL ảnh hợp lệ.";
                return RedirectToAction(nameof(Import));
            }

            product.ImageUrl = newUrl;
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật hình ảnh minh họa cho sản phẩm '{product.Name}' thành công!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Lỗi cập nhật hình ảnh: {ex.Message}";
        }

        return RedirectToAction(nameof(Import));
    }

    [HttpGet("Orders")]
    public async Task<IActionResult> Orders(string status = "all")
    {
        var allOrders = await _unitOfWork.Orders.GetAllOrdersWithDetailsAsync();
        var filtered = status.ToLowerInvariant() switch
        {
            "pending" => allOrders.Where(o => o.OrderStatus == OrderStatus.PendingPayment).ToList(),
            "paid" => allOrders.Where(o => o.PaymentStatus == PaymentStatus.Paid).ToList(),
            "cancelled" => allOrders.Where(o => o.OrderStatus == OrderStatus.Cancelled).ToList(),
            _ => allOrders.ToList()
        };

        var vm = new AdminOrdersViewModel
        {
            StatusFilter = status.ToLowerInvariant(),
            Orders = filtered,
            Summary = await BuildWmsDashboardSummaryAsync()
        };

        return View(vm);
    }

    [HttpGet("Orders/Invoice/{orderCode}")]
    public async Task<IActionResult> OrderInvoice(string orderCode)
    {
        var order = await _unitOfWork.Orders.GetOrderWithFullTraceabilityAsync(orderCode);
        if (order is null)
        {
            TempData["ErrorMessage"] = $"Không tìm thấy đơn hàng {orderCode}.";
            return RedirectToAction(nameof(Orders));
        }

        ViewData["IsAdminView"] = true;
        var vm = new OrderInvoiceViewModel
        {
            Order = order,
            CallbackResult = null
        };

        return View("~/Views/Payment/Invoice.cshtml", vm);
    }

    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var summary = await BuildWmsDashboardSummaryAsync();
        var orders = await _unitOfWork.Orders.GetAllOrdersWithDetailsAsync();
        var allBatches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync();
        var recentTx = await _unitOfWork.WarehouseBatches.GetRecentInventoryTransactionsAsync(15);

        var now = DateTime.UtcNow;
        var alertBatches = allBatches
            .Where(b => b.RemainingQuantity > 0 && (b.ExpiryDate <= now.AddDays(15) || b.AvailableQuantity <= (b.ProductVariant?.SafetyStockThreshold ?? 15)))
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        var vm = new AdminDashboardViewModel
        {
            Summary = summary,
            RecentOrders = orders.Take(10).ToList(),
            AlertBatches = alertBatches,
            RecentTransactions = recentTx
        };

        return View(vm);
    }

    [HttpGet("Dashboard/ExportInventoryCsv")]
    [HttpGet("Dashboard/ExportInventoryExcel")]
    public async Task<IActionResult> ExportInventoryCsv()
    {
        var batches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync();
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Báo Cáo Tồn Kho");

        // Header công ty & tiêu đề báo cáo
        ws.Range("A1:O1").Merge().Value = "CÔNG TY TNHH S-NEXT — BÁO CÁO TỒN KHO LẠNH THEO LÔ HÀNG";
        ws.Range("A1:O1").Style
            .Font.SetBold(true).Font.SetFontSize(14).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#071E3D"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Row(1).Height = 32;

        ws.Range("A2:O2").Merge().Value = $"Mã số thuế: 0313778856 | Địa chỉ: 266/78/47 Tô Hiến Thành, P. Hòa Hưng, TP. Hồ Chí Minh | Ngày xuất báo cáo: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
        ws.Range("A2:O2").Style
            .Font.SetItalic(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#334155"))
            .Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(2).Height = 22;

        // Tiêu đề cột
        var headers = new[]
        {
            "STT", "Mã Lô Hàng", "Mã SKU", "Tên Sản Phẩm", "Quy Cách", "Nhà Cung Cấp",
            "Ngày SX", "Hạn Sử Dụng", "Ngày Còn Lại", "Giá Vốn Nhập",
            "Tồn Thực Tế", "Đang Giữ Chỗ", "Tồn Khả Dụng", "Giá Trị Tồn Kho", "Tình Trạng"
        };

        const int headerRow = 4;
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.SetBold(true).Font.SetFontColor(XLColor.White);
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0B4F6C"));
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.White;
        }
        ws.Row(headerRow).Height = 26;

        int row = headerRow + 1;
        int stt = 1;
        foreach (var b in batches)
        {
            var inventoryValue = b.RemainingQuantity * b.ImportUnitPrice;
            var statusVi = b.BatchStatus switch
            {
                BatchStatus.NearExpiry => "Sắp hết hạn (< 15 ngày)",
                BatchStatus.Expired => "Đã quá hạn",
                BatchStatus.LowStock => "Sắp hết hàng",
                BatchStatus.Depleted => "Đã xuất hết",
                _ => "Đạt chuẩn"
            };

            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = b.BatchCode;
            ws.Cell(row, 3).Value = b.ProductVariant?.Sku ?? "";
            ws.Cell(row, 4).Value = b.ProductVariant?.Product?.Name ?? "";
            ws.Cell(row, 5).Value = b.ProductVariant?.PackagingSpec ?? "";
            ws.Cell(row, 6).Value = b.Supplier?.Name ?? "";
            ws.Cell(row, 7).Value = b.ManufactureDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 8).Value = b.ExpiryDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 9).Value = b.DaysUntilExpiry;
            ws.Cell(row, 10).Value = b.ImportUnitPrice;
            ws.Cell(row, 11).Value = b.RemainingQuantity;
            ws.Cell(row, 12).Value = b.ReservedQuantity;
            ws.Cell(row, 13).Value = b.AvailableQuantity;
            ws.Cell(row, 14).Value = inventoryValue;
            ws.Cell(row, 15).Value = statusVi;

            var dataRange = ws.Range(row, 1, row, 15);
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CBD5E1");
            dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
            dataRange.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            if (b.BatchStatus == BatchStatus.Expired || b.DaysUntilExpiry <= 0)
            {
                dataRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEE2E2"));
            }
            else if (b.BatchStatus == BatchStatus.NearExpiry || (b.DaysUntilExpiry > 0 && b.DaysUntilExpiry <= 15 && b.RemainingQuantity > 0))
            {
                dataRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEF3C7"));
            }
            else if (row % 2 == 0)
            {
                dataRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFC"));
            }

            ws.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0 \"đ\"";
            ws.Cell(row, 11).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 12).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 13).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0 \"đ\"";
            ws.Cell(row, 15).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 22;
            row++;
        }

        // Dòng tổng cộng
        ws.Range(row, 1, row, 10).Merge().Value = "TỔNG CỘNG TOÀN KHO";
        ws.Range(row, 1, row, 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        ws.Cell(row, 11).FormulaA1 = $"SUM(K5:K{row - 1})";
        ws.Cell(row, 12).FormulaA1 = $"SUM(L5:L{row - 1})";
        ws.Cell(row, 13).FormulaA1 = $"SUM(M5:M{row - 1})";
        ws.Cell(row, 14).FormulaA1 = $"SUM(N5:N{row - 1})";
        ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0 \"đ\"";
        ws.Cell(row, 15).Value = $"{batches.Count} lô hàng";

        var totalRange = ws.Range(row, 1, row, 15);
        totalRange.Style.Font.SetBold(true).Font.SetFontColor(XLColor.FromHtml("#071E3D"));
        totalRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E0F2FE"));
        totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        totalRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Cell(row, 11).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell(row, 12).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell(row, 13).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell(row, 15).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(row).Height = 26;

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 6;
        ws.Column(4).Width = 34;
        ws.Column(6).Width = 28;
        ws.Column(14).Width = 18;
        ws.Column(15).Width = 22;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"BaoCaoTonKho_SNEXT_{DateTime.UtcNow.AddHours(7):yyyyMMdd_HHmm}.xlsx");
    }

    [HttpGet("Dashboard/ExportRevenueCsv")]
    [HttpGet("Dashboard/ExportRevenueExcel")]
    public async Task<IActionResult> ExportRevenueCsv()
    {
        var orders = await _unitOfWork.Orders.GetAllOrdersWithDetailsAsync();
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Báo Cáo Doanh Thu");

        // Header doanh nghiệp
        ws.Range("A1:N1").Merge().Value = "CÔNG TY TNHH S-NEXT — BÁO CÁO DOANH THU & ĐƠN HÀNG";
        ws.Range("A1:N1").Style
            .Font.SetBold(true).Font.SetFontSize(14).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#071E3D"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Row(1).Height = 32;

        ws.Range("A2:N2").Merge().Value = $"Mã số thuế: 0313778856 | Địa chỉ: 266/78/47 Tô Hiến Thành, P. Hòa Hưng, TP. Hồ Chí Minh | Ngày lập: {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
        ws.Range("A2:N2").Style
            .Font.SetItalic(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#334155"))
            .Fill.SetBackgroundColor(XLColor.FromHtml("#F1F5F9"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(2).Height = 22;

        // Khối tóm tắt nhanh ở dòng 3
        var paidOrders = orders.Where(o => o.PaymentStatus == PaymentStatus.Paid).ToList();
        ws.Range("A3:D3").Merge().Value = $"Tổng số đơn hàng: {orders.Count} đơn";
        ws.Range("E3:I3").Merge().Value = $"Đơn đã thu tiền: {paidOrders.Count} đơn (VNPay: {paidOrders.Where(o => o.PaymentMethod == PaymentMethodType.VNPay).Sum(o => o.FinalAmount):N0} đ | MoMo: {paidOrders.Where(o => o.PaymentMethod == PaymentMethodType.MoMo).Sum(o => o.FinalAmount):N0} đ | COD: {paidOrders.Where(o => o.PaymentMethod == PaymentMethodType.COD).Sum(o => o.FinalAmount):N0} đ)";
        ws.Range("J3:N3").Merge().Value = $"Tổng doanh thu thực thu: {paidOrders.Sum(o => o.FinalAmount):N0} đ";
        ws.Range("A3:N3").Style
            .Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.FromHtml("#0F5132"))
            .Fill.SetBackgroundColor(XLColor.FromHtml("#D1E7DD"))
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Range("J3:N3").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        ws.Row(3).Height = 24;

        var headers = new[]
        {
            "STT", "Mã Đơn Hàng", "Tên Khách Hàng", "Số Điện Thoại", "Khu Vực Giao Hàng",
            "Phương Thức Thanh Toán", "Trạng Thái Đơn", "Trạng Thái Thanh Toán",
            "Tạm Tính", "Giảm Giá", "Phí Giao Hàng", "Thành Tiền", "Mã Lô Xuất Kho", "Ngày Đặt Hàng"
        };

        const int headerRow = 5;
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.SetBold(true).Font.SetFontColor(XLColor.White);
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0B4F6C"));
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.White;
        }
        ws.Row(headerRow).Height = 26;

        int row = headerRow + 1;
        int stt = 1;
        foreach (var o in orders)
        {
            var methodVi = o.PaymentMethod switch
            {
                PaymentMethodType.VNPay => "VNPay / Chuyển khoản QR",
                PaymentMethodType.MoMo => "Ví MoMo",
                _ => "Thanh toán khi nhận hàng (COD)"
            };
            var orderStatusVi = o.OrderStatus switch
            {
                OrderStatus.PendingPayment => "Chờ thanh toán",
                OrderStatus.Processing => "Đang chuẩn bị hàng",
                OrderStatus.Shipping => "Đang giao hàng",
                OrderStatus.Completed => "Hoàn thành",
                OrderStatus.Cancelled => "Đã hủy",
                _ => o.OrderStatus.ToString()
            };
            var payStatusVi = o.PaymentStatus switch
            {
                PaymentStatus.Paid => "Đã thanh toán",
                PaymentStatus.Failed => "Thất bại / Đã hủy",
                PaymentStatus.Refunded => "Đã hoàn tiền",
                _ => "Chưa thanh toán"
            };
            var allocatedBatches = string.Join(", ", o.OrderItems
                .SelectMany(i => i.BatchAllocations)
                .Select(a => $"{a.BatchCode} (SL: {a.AllocatedQuantity})"));

            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = o.OrderCode;
            ws.Cell(row, 3).Value = o.CustomerName;
            ws.Cell(row, 4).Value = "'" + o.CustomerPhone; // Giữ nguyên số 0 đầu của SĐT
            ws.Cell(row, 5).Value = o.District;
            ws.Cell(row, 6).Value = methodVi;
            ws.Cell(row, 7).Value = orderStatusVi;
            ws.Cell(row, 8).Value = payStatusVi;
            ws.Cell(row, 9).Value = o.TotalAmount;
            ws.Cell(row, 10).Value = o.DiscountAmount;
            ws.Cell(row, 11).Value = o.ShippingFee;
            ws.Cell(row, 12).Value = o.FinalAmount;
            ws.Cell(row, 13).Value = allocatedBatches;
            ws.Cell(row, 14).Value = o.CreatedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm");

            var dataRange = ws.Range(row, 1, row, 14);
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CBD5E1");
            dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
            dataRange.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

            if (row % 2 == 0)
            {
                dataRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F8FAFC"));
            }

            // Highlight màu trạng thái thanh toán dễ đọc
            if (o.PaymentStatus == PaymentStatus.Paid)
            {
                ws.Cell(row, 8).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DCFCE7"))
                    .Font.SetFontColor(XLColor.FromHtml("#166534")).Font.SetBold(true);
            }
            else if (o.PaymentStatus == PaymentStatus.Failed || o.OrderStatus == OrderStatus.Cancelled)
            {
                ws.Cell(row, 8).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEE2E2"))
                    .Font.SetFontColor(XLColor.FromHtml("#991B1B")).Font.SetBold(true);
            }
            else
            {
                ws.Cell(row, 8).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEF3C7"))
                    .Font.SetFontColor(XLColor.FromHtml("#92400E")).Font.SetBold(true);
            }

            ws.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 2).Style.Font.SetBold(true);
            ws.Cell(row, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Cell(row, 9).Style.NumberFormat.Format = "#,##0 \"đ\"";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0 \"đ\"";
            ws.Cell(row, 11).Style.NumberFormat.Format = "#,##0 \"đ\"";
            ws.Cell(row, 12).Style.NumberFormat.Format = "#,##0 \"đ\"";
            ws.Cell(row, 12).Style.Font.SetBold(true).Font.SetFontColor(XLColor.FromHtml("#B91C1C"));
            ws.Cell(row, 14).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 23;
            row++;
        }

        // Dòng tổng cộng
        ws.Range(row, 1, row, 8).Merge().Value = "TỔNG CỘNG TẤT CẢ ĐƠN HÀNG";
        ws.Range(row, 1, row, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        ws.Cell(row, 9).FormulaA1 = $"SUM(I6:I{row - 1})";
        ws.Cell(row, 10).FormulaA1 = $"SUM(J6:J{row - 1})";
        ws.Cell(row, 11).FormulaA1 = $"SUM(K6:K{row - 1})";
        ws.Cell(row, 12).FormulaA1 = $"SUM(L6:L{row - 1})";
        ws.Range(row, 9, row, 12).Style.NumberFormat.Format = "#,##0 \"đ\"";
        ws.Range(row, 13, row, 14).Merge().Value = $"Thực thu: {paidOrders.Sum(o => o.FinalAmount):N0} đ";
        ws.Range(row, 13, row, 14).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        var totalRange = ws.Range(row, 1, row, 14);
        totalRange.Style.Font.SetBold(true).Font.SetFontColor(XLColor.FromHtml("#071E3D"));
        totalRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E0F2FE"));
        totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        totalRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Row(row).Height = 26;

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 6;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 26;
        ws.Column(6).Width = 26;
        ws.Column(8).Width = 20;
        ws.Column(12).Width = 18;
        ws.Column(13).Width = 34;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"BaoCaoDoanhThu_SNEXT_{DateTime.UtcNow.AddHours(7):yyyyMMdd_HHmm}.xlsx");
    }

    private async Task<string?> SaveOrResolveProductImageAsync(string productSlug, IFormFile? imageFile, string? imageUrl)
    {
        if (imageFile is not null && imageFile.Length > 0)
        {
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
            {
                throw new InvalidOperationException("Định dạng ảnh không hợp lệ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp.");
            }
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("Dung lượng ảnh vượt quá giới hạn 5MB.");
            }

            var folderPath = Path.Combine(_env.WebRootPath, "images", "products");
            Directory.CreateDirectory(folderPath);

            var safeSlug = string.Concat(productSlug.Where(c => char.IsLetterOrDigit(c) || c == '-'));
            var fileName = $"{safeSlug}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var fullPath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/images/products/{fileName}";
        }

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var trimmed = imageUrl.Trim();
            if (trimmed.StartsWith("/", StringComparison.Ordinal) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
            throw new InvalidOperationException("Đường dẫn ảnh phải bắt đầu bằng / hoặc http(s)://.");
        }

        return null;
    }

    private async Task<WmsDashboardSummaryDto> BuildWmsDashboardSummaryAsync()
    {
        var allBatches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync();
        var allOrders = await _unitOfWork.Orders.GetAllOrdersWithDetailsAsync();
        var allTx = await _unitOfWork.WarehouseBatches.GetRecentInventoryTransactionsAsync(500);
        var now = DateTime.UtcNow;

        var paidOrders = allOrders.Where(o => o.PaymentStatus == PaymentStatus.Paid).ToList();
        var totalInitialUnits = allBatches.Sum(b => b.InitialQuantity);
        var expiredUnits = allBatches.Where(b => b.ExpiryDate <= now).Sum(b => b.RemainingQuantity);
        var discardedUnits = allTx.Where(t => t.TransactionType == InventoryTransactionType.SpoilageDiscard).Sum(t => Math.Abs(t.QuantityChanged));
        var totalSpoiledUnits = expiredUnits + discardedUnits;

        var spoilageRate = totalInitialUnits > 0
            ? Math.Round((decimal)totalSpoiledUnits * 100m / totalInitialUnits, 2)
            : 0m;

        var spoilageCost = allBatches.Where(b => b.ExpiryDate <= now).Sum(b => b.RemainingQuantity * b.ImportUnitPrice)
            + allTx.Where(t => t.TransactionType == InventoryTransactionType.SpoilageDiscard)
                   .Sum(t => Math.Abs(t.QuantityChanged) * (t.Batch?.ImportUnitPrice ?? 0m));

        return new WmsDashboardSummaryDto
        {
            TotalActiveBatches = allBatches.Count(b => b.RemainingQuantity > 0 && b.ExpiryDate > now),
            NearExpiryBatchCount = allBatches.Count(b => b.RemainingQuantity > 0 && b.ExpiryDate > now && b.ExpiryDate <= now.AddDays(15)),
            ExpiredBatchCount = allBatches.Count(b => b.ExpiryDate <= now || b.BatchStatus == BatchStatus.Expired),
            LowStockBatchCount = allBatches.Count(b => b.RemainingQuantity > 0 && b.ExpiryDate > now && b.AvailableQuantity <= (b.ProductVariant?.SafetyStockThreshold ?? 15)),
            TotalRemainingUnits = allBatches.Sum(b => b.RemainingQuantity),
            TotalReservedUnits = allBatches.Sum(b => b.ReservedQuantity),
            TotalAvailableUnits = allBatches.Sum(b => b.AvailableQuantity),
            TotalInventoryCostValue = allBatches.Sum(b => b.RemainingQuantity * b.ImportUnitPrice),
            TotalRevenuePaid = paidOrders.Sum(o => o.FinalAmount),
            VnPayRevenue = paidOrders.Where(o => o.PaymentMethod == PaymentMethodType.VNPay).Sum(o => o.FinalAmount),
            MoMoRevenue = paidOrders.Where(o => o.PaymentMethod == PaymentMethodType.MoMo).Sum(o => o.FinalAmount),
            CodRevenue = paidOrders.Where(o => o.PaymentMethod == PaymentMethodType.COD).Sum(o => o.FinalAmount),
            TotalOrdersCount = allOrders.Count,
            PendingPaymentOrdersCount = allOrders.Count(o => o.OrderStatus == OrderStatus.PendingPayment),
            SpoilageRatePercent = spoilageRate,
            SpoilageCostValue = spoilageCost
        };
    }
}
