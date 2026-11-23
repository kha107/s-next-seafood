# S-NEXT Seafood — Website Thương Mại Điện Tử Thủy Hải Sản Tích Hợp Thanh Toán Trực Tuyến & Quản Lý Kho Theo Hạn Sử Dụng (FEFO)

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC%20%2B%20Web%20API-0B4F6C)
![EF Core](https://img.shields.io/badge/Entity%20Framework%20Core-Code%20First-2E7D32)
![SQL Server](https://img.shields.io/badge/SQL%20Server-LocalDB%20%2F%20Express-CC2927?logo=microsoftsqlserver&logoColor=white)
![ClosedXML](https://img.shields.io/badge/Excel%20Reports-ClosedXML%20(.xlsx)-1D6F42)
![Swagger](https://img.shields.io/badge/OpenAPI-Swagger%20UI-85EA2D?logo=swagger&logoColor=black)

> **Đề tài Thực tập Tốt nghiệp (Ngành Công nghệ Phần mềm — Khoa CNTT, HUFLIT)**  
> **Đơn vị thực tập:** Công ty TNHH S-NEXT (Mã số thuế: `0313778856`)  
> **Địa chỉ:** 266/78/47 Tô Hiến Thành, Phường Hòa Hưng, TP. Hồ Chí Minh  
> **Lĩnh vực:** Phân phối Thủy hải sản đông lạnh nhập khẩu & Đặc sản đóng gói chuẩn chuỗi lạnh IQF `-18°C`

---

## 1. Giới Thiệu Hệ Thống

**S-NEXT Seafood** là nền tảng thương mại điện tử chuyên biệt cho ngành thủy hải sản đông lạnh và thực phẩm chuỗi lạnh, giải quyết đồng thời hai bài toán nghiệp vụ cốt lõi:

1. **Quản lý kho lạnh theo lô & tự động phân bổ xuất kho theo hạn sử dụng (FEFO — First Expired, First Out):**
   - Kiểm soát tồn kho thực tế (`RemainingQuantity`), tồn đang giữ chỗ chờ thanh toán (`ReservedQuantity`) và tồn khả dụng thời gian thực (`AvailableQuantity`).
   - Tự động chọn và tách lô theo thứ tự ưu tiên hạn sử dụng gần nhất (`ExpiryDate ASC, ImportDate ASC`), lưu vết chính xác từng gói hàng thuộc mã lô nào (`OrderItemBatchAllocations`) để truy xuất nguồn gốc thực phẩm.
   - Cảnh báo lô **sắp hết hạn (< 15 ngày)**, **đã hết hạn**, **sắp hết tồn kho**, lập phiếu nhập lô mới đính kèm cập nhật ảnh minh họa sản phẩm, lập phiếu xuất hủy hao hụt và theo dõi đầy đủ lịch sử biến động kho.
2. **Tích hợp thanh toán trực tuyến & cơ chế giữ hàng tạm thời (Stock Reservation):**
   - Hỗ trợ đa phương thức thanh toán: **Chuyển khoản Ngân hàng / VNPay QR (HMAC-SHA512)**, **Ví điện tử MoMo / VietQR** và **COD Nội thành TP.HCM**.
   - Hiển thị mã QR thanh toán kèm **số tiền chính xác** và **nội dung chuyển khoản riêng biệt cho từng đơn hàng** giúp đối soát nhanh chóng.
   - Khi tạo đơn thanh toán trực tuyến, hệ thống mở giao dịch cơ sở dữ liệu nguyên tử (`Database Transaction`) để tạm giữ tồn kho trên các lô FEFO; khi thanh toán thành công sẽ trừ tồn kho chính thức, ngược lại khi hủy/thất bại sẽ tự động hoàn trả lượng hàng giữ chỗ về kho.

---

## 2. Kiến Trúc & Design Patterns Áp Dụng

Dự án được tổ chức theo **kiến trúc 4 tầng (Multi-Project Solution)**:

```text
SNextSeafood.sln
├── src/
│   ├── SNextSeafood.Domain/          # Entities, Enums, Repository & UnitOfWork Interfaces
│   ├── SNextSeafood.Application/     # DTOs, Facades (FEFO & Checkout), Payment Strategies
│   ├── SNextSeafood.Infrastructure/  # SNextDbContext, EF Core Migrations, Repositories, DbInitializer
│   └── SNextSeafood.Web/             # ASP.NET Core MVC Views, RESTful Web API, Cookie Auth, Excel Export
├── .env.example                      # Mẫu biến môi trường (cần sao chép thành .env)
├── .gitignore                        # Cấu hình bỏ qua secrets, bin/obj, file tạm
└── README.md                         # Tài liệu hướng dẫn cài đặt & sử dụng
```

### Các Design Patterns thể hiện rõ trong mã nguồn:
| Design Pattern | Thành Phần Triển Khai | Vai Trò Nghiệp Vụ |
| :--- | :--- | :--- |
| **Repository & Unit of Work** | `IGenericRepository<T>`, `IProductRepository`, `IWarehouseBatchRepository`, `IOrderRepository`, `UnitOfWork` | Đảm bảo tính nguyên tử (ACID Transaction) khi đồng thời tạo đơn hàng, giữ/trừ tồn kho theo lô và ghi nhận lịch sử kho. |
| **Strategy Pattern** | `IPaymentGatewayStrategy`, `VnPayStrategy`, `MoMoStrategy`, `CodStrategy`, `PaymentStrategyFactory` | Đa hình hóa xử lý thanh toán, ký số & xác thực chữ ký điện tử `HMAC-SHA512`. |
| **Facade Pattern** | `WarehouseFefoFacade`, `OrderCheckoutFacade` | Đóng gói quy trình phức tạp: kiểm tra tồn khả dụng, tách lô FEFO, áp dụng Voucher, tạo đơn, sinh URL thanh toán và xử lý Callback/IPN. |

---

## 3. Tính Năng Chính

### Phân Hệ Khách Hàng (Public Storefront)
- **Trang Chủ & Danh Mục Sản Phẩm (`/`):** Tìm kiếm, lọc danh mục, hiển thị nhãn điều kiện bảo quản (`Đông lạnh -18°C`), quy cách (`500g`, `1kg`), tồn kho khả dụng thời gian thực và hạn sử dụng lô hiện tại.
- **Chi Tiết Sản Phẩm (`/Catalog/Details/{slug}`):** Chọn biến thể trọng lượng, xem hạn sử dụng lô đang xuất và cam kết chuỗi lạnh.
- **Giỏ Hàng & Thanh Toán (`/Cart`):** Áp dụng mã ưu đãi (`SNEXT50`, `XAHANG25`, `VIPSEAFOOD`), dự kiến phân bổ lô giao hàng, chọn hình thức thanh toán.
- **Thanh Toán QR Trực Tuyến (`/Payment/VnPayGateway`, `/Payment/MoMoGateway`):** Hiển thị mã QR tương ứng, đồng hồ giữ đơn 15 phút, số tiền & nội dung chuyển khoản duy nhất theo từng đơn có nút sao chép nhanh.
- **Tra Cứu Đơn Hàng & Hóa Đơn Điện Tử (`/Order/Track`, `/Order/Invoice/{orderCode}`):** Bảo mật thông tin khách hàng (yêu cầu xác thực Mã đơn hàng + Số điện thoại), in hóa đơn điện tử kèm chứng nhận nguồn gốc xuất xứ lô hàng (`Mã lô`, `NSX`, `HSD`, `Nhà cung cấp`).

### Phân Hệ Quản Trị Nội Bộ (Bảo Mật Phân Quyền Cookie Auth)
- **Cổng Đăng Nhập Ẩn (`/snext-portal/login`):** Ẩn hoàn toàn nút quản trị khỏi trang khách hàng, bảo vệ toàn bộ route `/Admin/*` và API kho bằng `[Authorize(Roles = "Admin")]`.
- **Tổng Quan & Báo Cáo (`/Admin/Dashboard`):** Biểu đồ Chart.js doanh thu theo phương thức thanh toán, biểu đồ cơ cấu tình trạng lô hàng, chỉ số tỷ lệ hao hụt và **Xuất báo cáo Microsoft Excel (`.xlsx`)** có định dạng tiêu đề, màu trạng thái và công thức tổng `SUM`.
- **Quản Lý Tồn Kho Lạnh Theo Lô (`/Admin/Warehouse`):** Bộ lọc lô sắp hết hạn (< 15 ngày), đã quá hạn, sắp hết hàng, đang giữ chỗ và thao tác lập phiếu xuất hủy.
- **Lập Phiếu Nhập Kho & Cập Nhật Ảnh Sản Phẩm (`/Admin/Warehouse/Import`):** Nhập lô hàng mới từ Nhà cung cấp, tải lên / cập nhật hình ảnh minh họa sản phẩm trực tiếp, và tra cứu sổ lịch sử biến động kho.
- **Quản Lý Đơn Hàng & Hóa Đơn (`/Admin/Orders`):** Lọc trạng thái đơn hàng, xem hóa đơn điện tử nội bộ & nguồn gốc lô xuất kho.

---

## 4. Yêu Cầu Môi Trường & Hướng Dẫn Cài Đặt

### Yêu cầu hệ thống
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) trở lên
- **SQL Server** (SQL Server LocalDB / SQL Server Express / Docker)

### Bước 1: Clone repository
```bash
git clone https://github.com/kha107/s-next-seafood.git
cd s-next-seafood
```

### Bước 2: Cấu hình biến môi trường (`.env`)
Sao chép file mẫu `.env.example` thành `.env` tại thư mục gốc của Solution và cập nhật thông tin phù hợp với máy của bạn:

```powershell
Copy-Item .env.example .env
```

Mở file `.env` và thiết lập các biến quan trọng:
```env
ConnectionStrings__DefaultConnection=Server=(localdb)\mssqllocaldb;Database=SNextSeafoodDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
AdminAuth__Username=admin@snextseafood.vn
AdminAuth__Password=YourStrongAdminPassword
VnPay__TmnCode=YOUR_VNPAY_TMN_CODE
VnPay__HashSecret=YOUR_VNPAY_HASH_SECRET
MoMo__PartnerCode=YOUR_MOMO_PARTNER_CODE
MoMo__SecretKey=YOUR_MOMO_SECRET_KEY
```

> Ứng dụng tích hợp sẵn `EnvFileLoader` tự động nạp `.env` khi khởi chạy. File `.env` đã được đưa vào `.gitignore` để không bao giờ lộ khóa bảo mật lên GitHub.

### Bước 3: Khôi phục gói thư viện, Build và Chạy ứng dụng
```powershell
dotnet restore SNextSeafood.sln
dotnet build SNextSeafood.sln
dotnet run --project src/SNextSeafood.Web/SNextSeafood.Web.csproj --urls "http://localhost:5188"
```

> Khi khởi động lần đầu, `DbInitializer.SeedAsync()` sẽ **tự động chạy EF Core Migration** tạo cơ sở dữ liệu `SNextSeafoodDb` và nạp sẵn dữ liệu mẫu thực tế của Công ty TNHH S-NEXT (3 Nhà cung cấp, 8 Sản phẩm / 16 Biến thể, 20 Lô hàng đa trạng thái, Mã giảm giá và Đơn hàng mẫu).

---

## 5. Đường Dẫn Truy Cập Nhanh

| Khu Vực | Đường Dẫn | Ghi Chú |
| :--- | :--- | :--- |
| **Trang chủ & Danh mục Khách hàng** | `http://localhost:5188/` | Công khai |
| **Giỏ hàng & Thanh toán** | `http://localhost:5188/Cart` | Công khai |
| **Tra cứu Đơn hàng Khách hàng** | `http://localhost:5188/Order/Track` | Xác thực Mã đơn + SĐT |
| **Đăng nhập Quản trị viên (Ẩn)** | `http://localhost:5188/snext-portal/login` | Tài khoản cấu hình trong `.env` |
| **Admin — Tổng quan & Báo cáo Excel** | `http://localhost:5188/Admin/Dashboard` | Yêu cầu đăng nhập Admin |
| **Admin — Quản lý Kho Lô** | `http://localhost:5188/Admin/Warehouse` | Yêu cầu đăng nhập Admin |
| **Admin — Nhập lô & Ảnh sản phẩm** | `http://localhost:5188/Admin/Warehouse/Import` | Yêu cầu đăng nhập Admin |
| **Admin — Quản lý Đơn hàng & Hóa đơn** | `http://localhost:5188/Admin/Orders` | Yêu cầu đăng nhập Admin |
| **Swagger OpenAPI (Dành cho Dev)** | `http://localhost:5188/swagger` | Tài liệu & kiểm thử RESTful API |

---

## 6. Danh Sách RESTful API Chính

- `GET /api/catalog/products` — Danh sách sản phẩm kèm biến thể & tồn kho khả dụng theo thời gian thực.
- `POST /api/orders/checkout` — Tạo đơn hàng, giữ tồn kho tạm thời theo FEFO và sinh URL thanh toán.
- `GET /api/payments/ipn` — Endpoint Webhook IPN xác thực chữ ký `HMAC-SHA512` và cập nhật trạng thái đơn/kho tự động.
- `GET /api/warehouse/batches` *(Admin)* — Danh sách lô hàng theo bộ lọc (`nearexpiry`, `expired`, `lowstock`, `reserved`).
- `GET /api/warehouse/simulate-fefo` *(Admin)* — Mô phỏng thuật toán tách lô xuất kho theo hạn sử dụng.
- `POST /api/warehouse/import-batch` *(Admin)* — Lập phiếu nhập kho lô hàng mới.
- `POST /api/warehouse/discard-batch` *(Admin)* — Lập phiếu xuất hủy lô hàng quá hạn/hư hỏng.
- `GET /api/orders/{orderCode}/traceability` *(Admin)* — Truy xuất nguồn gốc lô hàng theo mã đơn.

---

## 7. Bản Quyền & Liên Hệ

Dự án được phát triển phục vụ Báo cáo Thực tập Tốt nghiệp Khoa Công nghệ Thông tin (HUFLIT) tại **Công ty TNHH S-NEXT**.
