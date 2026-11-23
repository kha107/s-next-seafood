using SNextSeafood.Application.DTOs;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Enums;
using SNextSeafood.Domain.Interfaces;

namespace SNextSeafood.Application.Facades;

public interface IWarehouseFefoFacade
{
    Task<int> GetAvailableQuantityAsync(int productVariantId, CancellationToken cancellationToken = default);
    Task<FefoSimulationResultDto> SimulateFefoAllocationAsync(int productVariantId, int requestedQuantity, CancellationToken cancellationToken = default);
    Task<List<OrderItemBatchAllocation>> AllocateFefoForOrderItemAsync(
        Order order,
        OrderItem orderItem,
        bool isOnlineReservation,
        CancellationToken cancellationToken = default);
    Task ConfirmSaleExportFromReservationAsync(Order order, string performedBy, CancellationToken cancellationToken = default);
    Task ReleaseReservationForCancelledOrderAsync(Order order, string performedBy, string reason, CancellationToken cancellationToken = default);
    Task<WarehouseBatch> ImportNewBatchAsync(ImportBatchRequestDto request, CancellationToken cancellationToken = default);
    Task<InventoryTransaction> DiscardSpoiledOrExpiredBatchAsync(DiscardBatchRequestDto request, CancellationToken cancellationToken = default);
    Task RefreshAllBatchStatusesAsync(CancellationToken cancellationToken = default);
    BatchStatus EvaluateBatchStatus(WarehouseBatch batch, int safetyStockThreshold);
}

/// <summary>
/// FACADE PATTERN: WarehouseFefoFacade
/// Đóng gói thuật toán Quản lý Kho & Phân bổ Lô hàng theo Hạn sử dụng (FEFO - First Expired, First Out),
/// cơ chế Giữ hàng tạm thời (Stock Reservation), Nhập kho lô mới và Xuất hủy hao hụt (SpoilageDiscard).
/// </summary>
public class WarehouseFefoFacade : IWarehouseFefoFacade
{
    private readonly IUnitOfWork _unitOfWork;

    public WarehouseFefoFacade(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> GetAvailableQuantityAsync(int productVariantId, CancellationToken cancellationToken = default)
    {
        var eligibleBatches = await _unitOfWork.WarehouseBatches.GetFefoEligibleBatchesAsync(productVariantId, cancellationToken);
        return eligibleBatches.Sum(b => Math.Max(0, b.RemainingQuantity - b.ReservedQuantity));
    }

    public async Task<FefoSimulationResultDto> SimulateFefoAllocationAsync(
        int productVariantId,
        int requestedQuantity,
        CancellationToken cancellationToken = default)
    {
        var variant = await _unitOfWork.Products.GetVariantWithProductAndBatchesAsync(productVariantId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy biến thể sản phẩm #{productVariantId}.");

        var eligibleBatches = await _unitOfWork.WarehouseBatches.GetFefoEligibleBatchesAsync(productVariantId, cancellationToken);
        var totalAvailable = eligibleBatches.Sum(b => Math.Max(0, b.RemainingQuantity - b.ReservedQuantity));

        var result = new FefoSimulationResultDto
        {
            ProductVariantId = variant.VariantId,
            ProductName = variant.Product?.Name ?? string.Empty,
            Sku = variant.Sku,
            PackagingSpec = variant.PackagingSpec,
            RequestedQuantity = requestedQuantity,
            TotalAvailableQuantity = totalAvailable
        };

        if (requestedQuantity <= 0)
        {
            result.CanFulfill = false;
            result.Message = "Số lượng yêu cầu phải lớn hơn 0.";
            return result;
        }

        if (totalAvailable < requestedQuantity)
        {
            result.CanFulfill = false;
            result.Message = $"Không đủ tồn kho khả dụng theo chuẩn FEFO (Yêu cầu: {requestedQuantity}, Khả dụng còn hạn: {totalAvailable}).";
            return result;
        }

        var remainingToAllocate = requestedQuantity;
        foreach (var batch in eligibleBatches)
        {
            if (remainingToAllocate <= 0) break;

            var batchAvailable = Math.Max(0, batch.RemainingQuantity - batch.ReservedQuantity);
            if (batchAvailable <= 0) continue;

            var takeQty = Math.Min(remainingToAllocate, batchAvailable);
            remainingToAllocate -= takeQty;

            result.Allocations.Add(new FefoBatchAllocationPreviewDto
            {
                BatchId = batch.BatchId,
                BatchCode = batch.BatchCode,
                AllocatedQuantity = takeQty,
                ManufactureDate = batch.ManufactureDate,
                ExpiryDate = batch.ExpiryDate,
                DaysUntilExpiry = batch.DaysUntilExpiry,
                ImportUnitPrice = batch.ImportUnitPrice,
                SupplierName = batch.Supplier?.Name ?? "Kho Lạnh S-NEXT",
                StorageRack = batch.StorageRack
            });
        }

        result.CanFulfill = true;
        result.Message = result.Allocations.Count > 1
            ? $"Đã tự động tách & phân bổ {requestedQuantity} đơn vị từ {result.Allocations.Count} lô hàng theo thứ tự ưu tiên hạn sử dụng gần nhất (FEFO)."
            : $"Đã phân bổ {requestedQuantity} đơn vị từ lô {result.Allocations[0].BatchCode} (HSD: {result.Allocations[0].ExpiryDate:dd/MM/yyyy}).";

        return result;
    }

    public async Task<List<OrderItemBatchAllocation>> AllocateFefoForOrderItemAsync(
        Order order,
        OrderItem orderItem,
        bool isOnlineReservation,
        CancellationToken cancellationToken = default)
    {
        var eligibleBatches = await _unitOfWork.WarehouseBatches.GetFefoEligibleBatchesAsync(orderItem.ProductVariantId, cancellationToken);
        var totalAvailable = eligibleBatches.Sum(b => Math.Max(0, b.RemainingQuantity - b.ReservedQuantity));

        if (totalAvailable < orderItem.Quantity)
        {
            var skuName = eligibleBatches.FirstOrDefault()?.ProductVariant?.Sku ?? $"Variant #{orderItem.ProductVariantId}";
            throw new InvalidOperationException(
                $"Sản phẩm [{skuName}] không đủ tồn kho khả dụng (Cần {orderItem.Quantity}, Kho khả dụng còn hạn: {totalAvailable}).");
        }

        var remainingToAllocate = orderItem.Quantity;
        var allocations = new List<OrderItemBatchAllocation>();

        foreach (var batch in eligibleBatches)
        {
            if (remainingToAllocate <= 0) break;

            var availableInBatch = Math.Max(0, batch.RemainingQuantity - batch.ReservedQuantity);
            if (availableInBatch <= 0) continue;

            var takeQty = Math.Min(remainingToAllocate, availableInBatch);
            remainingToAllocate -= takeQty;

            var safetyThreshold = batch.ProductVariant?.SafetyStockThreshold ?? 15;

            if (isOnlineReservation)
            {
                // Luồng B - Bước 1: Tăng ReservedQuantity để giữ hàng tạm thời chờ thanh toán trực tuyến
                batch.ReservedQuantity += takeQty;
                batch.BatchStatus = EvaluateBatchStatus(batch, safetyThreshold);
                _unitOfWork.WarehouseBatches.Update(batch);

                var allocation = new OrderItemBatchAllocation
                {
                    OrderItem = orderItem,
                    BatchId = batch.BatchId,
                    BatchCode = batch.BatchCode,
                    AllocatedQuantity = takeQty,
                    ManufactureDate = batch.ManufactureDate,
                    ExpiryDate = batch.ExpiryDate,
                    ImportUnitPrice = batch.ImportUnitPrice,
                    IsExportConfirmed = false,
                    IsReservationReleased = false
                };
                allocations.Add(allocation);
                orderItem.BatchAllocations.Add(allocation);

                await _unitOfWork.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    ReceiptCode = $"HOLD-{DateTime.UtcNow:yyyyMMdd}-{order.OrderCode[^4..]}",
                    BatchId = batch.BatchId,
                    Order = order,
                    TransactionType = InventoryTransactionType.Reservation,
                    QuantityChanged = takeQty,
                    RemainingQuantityAfter = batch.RemainingQuantity,
                    ReservedQuantityAfter = batch.ReservedQuantity,
                    CreatedAt = DateTime.UtcNow,
                    PerformedBy = "Hệ thống bán hàng S-NEXT",
                    Note = $"Giữ hàng tạm thời {takeQty} gói từ lô {batch.BatchCode} (HSD {batch.ExpiryDate:dd/MM/yyyy}) cho đơn {order.OrderCode}"
                }, cancellationToken);
            }
            else
            {
                // Đơn COD: Trừ trực tiếp RemainingQuantity và ghi nhận SaleExport theo FEFO
                batch.RemainingQuantity -= takeQty;
                batch.BatchStatus = EvaluateBatchStatus(batch, safetyThreshold);
                _unitOfWork.WarehouseBatches.Update(batch);

                var allocation = new OrderItemBatchAllocation
                {
                    OrderItem = orderItem,
                    BatchId = batch.BatchId,
                    BatchCode = batch.BatchCode,
                    AllocatedQuantity = takeQty,
                    ManufactureDate = batch.ManufactureDate,
                    ExpiryDate = batch.ExpiryDate,
                    ImportUnitPrice = batch.ImportUnitPrice,
                    IsExportConfirmed = true,
                    IsReservationReleased = false
                };
                allocations.Add(allocation);
                orderItem.BatchAllocations.Add(allocation);

                await _unitOfWork.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    ReceiptCode = $"XK-{DateTime.UtcNow:yyyyMMdd}-{order.OrderCode[^4..]}",
                    BatchId = batch.BatchId,
                    Order = order,
                    TransactionType = InventoryTransactionType.SaleExport,
                    QuantityChanged = -takeQty,
                    RemainingQuantityAfter = batch.RemainingQuantity,
                    ReservedQuantityAfter = batch.ReservedQuantity,
                    CreatedAt = DateTime.UtcNow,
                    PerformedBy = "Hệ thống bán hàng S-NEXT",
                    Note = $"Xuất bán {takeQty} gói từ lô {batch.BatchCode} (HSD {batch.ExpiryDate:dd/MM/yyyy}) cho đơn giao tận nơi {order.OrderCode}"
                }, cancellationToken);
            }
        }

        return allocations;
    }

    public async Task ConfirmSaleExportFromReservationAsync(
        Order order,
        string performedBy,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in order.OrderItems)
        {
            foreach (var allocation in item.BatchAllocations.Where(a => !a.IsExportConfirmed && !a.IsReservationReleased))
            {
                var batch = await _unitOfWork.WarehouseBatches.GetBatchWithDetailsAsync(allocation.BatchId, cancellationToken)
                    ?? throw new InvalidOperationException($"Không tìm thấy lô hàng #{allocation.BatchId}.");

                batch.RemainingQuantity = Math.Max(0, batch.RemainingQuantity - allocation.AllocatedQuantity);
                batch.ReservedQuantity = Math.Max(0, batch.ReservedQuantity - allocation.AllocatedQuantity);
                batch.BatchStatus = EvaluateBatchStatus(batch, batch.ProductVariant?.SafetyStockThreshold ?? 15);
                _unitOfWork.WarehouseBatches.Update(batch);

                allocation.IsExportConfirmed = true;
                _unitOfWork.BatchAllocations.Update(allocation);

                await _unitOfWork.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    ReceiptCode = $"XK-{DateTime.UtcNow:yyyyMMdd}-{order.OrderCode[^4..]}",
                    BatchId = batch.BatchId,
                    OrderId = order.OrderId,
                    TransactionType = InventoryTransactionType.SaleExport,
                    QuantityChanged = -allocation.AllocatedQuantity,
                    RemainingQuantityAfter = batch.RemainingQuantity,
                    ReservedQuantityAfter = batch.ReservedQuantity,
                    CreatedAt = DateTime.UtcNow,
                    PerformedBy = performedBy,
                    Note = $"Đã xác nhận thanh toán: Xuất kho {allocation.AllocatedQuantity} gói từ lô {batch.BatchCode} cho đơn {order.OrderCode}"
                }, cancellationToken);
            }
        }
    }

    public async Task ReleaseReservationForCancelledOrderAsync(
        Order order,
        string performedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in order.OrderItems)
        {
            foreach (var allocation in item.BatchAllocations.Where(a => !a.IsExportConfirmed && !a.IsReservationReleased))
            {
                var batch = await _unitOfWork.WarehouseBatches.GetBatchWithDetailsAsync(allocation.BatchId, cancellationToken);
                if (batch is null) continue;

                batch.ReservedQuantity = Math.Max(0, batch.ReservedQuantity - allocation.AllocatedQuantity);
                batch.BatchStatus = EvaluateBatchStatus(batch, batch.ProductVariant?.SafetyStockThreshold ?? 15);
                _unitOfWork.WarehouseBatches.Update(batch);

                allocation.IsReservationReleased = true;
                _unitOfWork.BatchAllocations.Update(allocation);

                await _unitOfWork.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    ReceiptCode = $"REL-{DateTime.UtcNow:yyyyMMdd}-{order.OrderCode[^4..]}",
                    BatchId = batch.BatchId,
                    OrderId = order.OrderId,
                    TransactionType = InventoryTransactionType.ReleaseReservation,
                    QuantityChanged = allocation.AllocatedQuantity,
                    RemainingQuantityAfter = batch.RemainingQuantity,
                    ReservedQuantityAfter = batch.ReservedQuantity,
                    CreatedAt = DateTime.UtcNow,
                    PerformedBy = performedBy,
                    Note = $"Hoàn trả giữ hàng {allocation.AllocatedQuantity} gói về lô {batch.BatchCode} ({reason})"
                }, cancellationToken);
            }
        }
    }

    public async Task<WarehouseBatch> ImportNewBatchAsync(
        ImportBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.InitialQuantity <= 0)
            throw new ArgumentException("Số lượng nhập kho phải lớn hơn 0.");

        if (request.ExpiryDate <= request.ManufactureDate)
            throw new ArgumentException("Hạn sử dụng (HSD) phải sau Ngày sản xuất (NSX).");

        var variant = await _unitOfWork.Products.GetVariantWithProductAndBatchesAsync(request.ProductVariantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy biến thể sản phẩm để nhập kho.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var timestampTag = DateTime.UtcNow.ToString("yyyyMM");
            var randomSuffix = Random.Shared.Next(100, 999);
            var batchCode = !string.IsNullOrWhiteSpace(request.CustomBatchCode)
                ? request.CustomBatchCode.Trim().ToUpperInvariant()
                : $"LOT-{timestampTag}-{randomSuffix}";

            var receiptCode = $"NK-{timestampTag}-{Random.Shared.Next(10, 99)}";

            var receipt = new WarehouseReceipt
            {
                ReceiptCode = receiptCode,
                ReceiptType = "Import",
                SupplierId = request.SupplierId,
                TotalCost = request.ImportUnitPrice * request.InitialQuantity,
                CreatedAt = DateTime.UtcNow,
                PerformedBy = request.PerformedBy,
                Note = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Nhập kho lô mới {batchCode} ({variant.Product?.Name} - {variant.PackagingSpec})"
                    : request.Note
            };
            await _unitOfWork.WarehouseReceipts.AddAsync(receipt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var newBatch = new WarehouseBatch
            {
                BatchCode = batchCode,
                ProductVariantId = request.ProductVariantId,
                SupplierId = request.SupplierId,
                ManufactureDate = request.ManufactureDate,
                ExpiryDate = request.ExpiryDate,
                ImportDate = DateTime.UtcNow,
                ImportUnitPrice = request.ImportUnitPrice,
                InitialQuantity = request.InitialQuantity,
                RemainingQuantity = request.InitialQuantity,
                ReservedQuantity = 0,
                StorageRack = string.IsNullOrWhiteSpace(request.StorageRack) ? "KHO-LANH-A01" : request.StorageRack.Trim().ToUpperInvariant()
            };
            newBatch.BatchStatus = EvaluateBatchStatus(newBatch, variant.SafetyStockThreshold);

            await _unitOfWork.WarehouseBatches.AddAsync(newBatch, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var importTx = new InventoryTransaction
            {
                ReceiptCode = receiptCode,
                WarehouseReceiptId = receipt.ReceiptId,
                BatchId = newBatch.BatchId,
                TransactionType = InventoryTransactionType.Import,
                QuantityChanged = request.InitialQuantity,
                RemainingQuantityAfter = newBatch.RemainingQuantity,
                ReservedQuantityAfter = 0,
                CreatedAt = DateTime.UtcNow,
                PerformedBy = request.PerformedBy,
                Note = $"Nhập kho {request.InitialQuantity} đơn vị vào lô {batchCode} (Giá vốn: {request.ImportUnitPrice:N0}đ)"
            };
            await _unitOfWork.InventoryTransactions.AddAsync(importTx, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return newBatch;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InventoryTransaction> DiscardSpoiledOrExpiredBatchAsync(
        DiscardBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var batch = await _unitOfWork.WarehouseBatches.GetBatchWithDetailsAsync(request.BatchId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lô hàng cần xuất hủy.");

        var discardableQty = Math.Max(0, batch.RemainingQuantity - batch.ReservedQuantity);
        var qtyToDiscard = request.QuantityToDiscard <= 0 ? discardableQty : Math.Min(request.QuantityToDiscard, discardableQty);

        if (qtyToDiscard <= 0)
            throw new InvalidOperationException("Lô hàng này không còn số lượng khả dụng để xuất hủy.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var receiptCode = $"XH-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10, 99)}";
            var receipt = new WarehouseReceipt
            {
                ReceiptCode = receiptCode,
                ReceiptType = "SpoilageDiscard",
                SupplierId = batch.SupplierId,
                TotalCost = qtyToDiscard * batch.ImportUnitPrice,
                CreatedAt = DateTime.UtcNow,
                PerformedBy = request.PerformedBy,
                Note = $"Phiếu xuất hủy lô {batch.BatchCode}: {request.Reason}"
            };
            await _unitOfWork.WarehouseReceipts.AddAsync(receipt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            batch.RemainingQuantity -= qtyToDiscard;
            batch.BatchStatus = EvaluateBatchStatus(batch, batch.ProductVariant?.SafetyStockThreshold ?? 15);
            _unitOfWork.WarehouseBatches.Update(batch);

            var discardTx = new InventoryTransaction
            {
                ReceiptCode = receiptCode,
                WarehouseReceiptId = receipt.ReceiptId,
                BatchId = batch.BatchId,
                TransactionType = InventoryTransactionType.SpoilageDiscard,
                QuantityChanged = -qtyToDiscard,
                RemainingQuantityAfter = batch.RemainingQuantity,
                ReservedQuantityAfter = batch.ReservedQuantity,
                CreatedAt = DateTime.UtcNow,
                PerformedBy = request.PerformedBy,
                Note = $"Xuất hủy {qtyToDiscard} đơn vị từ lô {batch.BatchCode}. Lý do: {request.Reason}"
            };
            await _unitOfWork.InventoryTransactions.AddAsync(discardTx, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return discardTx;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task RefreshAllBatchStatusesAsync(CancellationToken cancellationToken = default)
    {
        var batches = await _unitOfWork.WarehouseBatches.GetAllBatchesWithDetailsAsync(null, cancellationToken);
        foreach (var b in batches)
        {
            var tracked = await _unitOfWork.WarehouseBatches.GetByIdAsync(b.BatchId, cancellationToken);
            if (tracked is null) continue;
            var newStatus = EvaluateBatchStatus(tracked, b.ProductVariant?.SafetyStockThreshold ?? 15);
            if (tracked.BatchStatus != newStatus)
            {
                tracked.BatchStatus = newStatus;
                _unitOfWork.WarehouseBatches.Update(tracked);
            }
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public BatchStatus EvaluateBatchStatus(WarehouseBatch batch, int safetyStockThreshold)
    {
        var now = DateTime.UtcNow;
        if (batch.RemainingQuantity <= 0)
            return BatchStatus.Depleted;

        if (batch.ExpiryDate <= now)
            return BatchStatus.Expired;

        var daysLeft = (batch.ExpiryDate.Date - now.Date).TotalDays;
        if (daysLeft <= 15)
            return BatchStatus.NearExpiry;

        var available = Math.Max(0, batch.RemainingQuantity - batch.ReservedQuantity);
        if (available <= safetyStockThreshold)
            return BatchStatus.LowStock;

        return BatchStatus.Active;
    }
}
