using System.ComponentModel.DataAnnotations;

namespace SNextSeafood.Domain.Enums;

public enum BatchStatus
{
    [Display(Name = "Đang hoạt động (Active)")]
    Active = 0,

    [Display(Name = "Sắp hết tồn kho (LowStock)")]
    LowStock = 1,

    [Display(Name = "Cận hạn sử dụng (< 15 ngày)")]
    NearExpiry = 2,

    [Display(Name = "Đã hết hạn (Expired)")]
    Expired = 3,

    [Display(Name = "Đã xuất hết (Depleted)")]
    Depleted = 4
}

public enum InventoryTransactionType
{
    [Display(Name = "Nhập kho lô mới (Import)")]
    Import = 0,

    [Display(Name = "Xuất bán đơn hàng (SaleExport)")]
    SaleExport = 1,

    [Display(Name = "Giữ hàng chờ thanh toán (Reservation)")]
    Reservation = 2,

    [Display(Name = "Hoàn giữ hàng (ReleaseReservation)")]
    ReleaseReservation = 3,

    [Display(Name = "Xuất hủy hết hạn/hao hụt (SpoilageDiscard)")]
    SpoilageDiscard = 4
}

public enum OrderStatus
{
    [Display(Name = "Chờ thanh toán trực tuyến")]
    PendingPayment = 0,

    [Display(Name = "Đang đóng gói lạnh")]
    Processing = 1,

    [Display(Name = "Đang giao hàng (Xe lạnh)")]
    Shipping = 2,

    [Display(Name = "Hoàn thành")]
    Completed = 3,

    [Display(Name = "Đã hủy")]
    Cancelled = 4
}

public enum PaymentStatus
{
    [Display(Name = "Chưa thanh toán")]
    Unpaid = 0,

    [Display(Name = "Đã thanh toán")]
    Paid = 1,

    [Display(Name = "Thanh toán thất bại")]
    Failed = 2,

    [Display(Name = "Đã hoàn tiền")]
    Refunded = 3
}

public enum PaymentMethodType
{
    [Display(Name = "VNPay Online (ATM/QR/Thẻ Quốc Tế)")]
    VNPay = 0,

    [Display(Name = "Ví MoMo / VietQR")]
    MoMo = 1,

    [Display(Name = "COD Nội thành TP.HCM (Nhận hàng kiểm tra lạnh)")]
    COD = 2
}
