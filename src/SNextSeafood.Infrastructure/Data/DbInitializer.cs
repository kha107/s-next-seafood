using Microsoft.EntityFrameworkCore;
using SNextSeafood.Domain.Entities;
using SNextSeafood.Domain.Enums;

namespace SNextSeafood.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(SNextDbContext context)
    {
        await context.Database.MigrateAsync();

        var localProductImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ca-hoi-nauy-phi-le-cap-dong"] = "/images/products/salmon-fillet.jpg",
            ["tom-su-bien-cap-dong-iqf"] = "/images/products/tiger-prawn.jpg",
            ["coi-so-diep-nhat-ban"] = "/images/products/japanese-scallop.jpg",
            ["muc-ong-trung-cap-dong"] = "/images/products/egg-squid.jpg",
            ["cha-muc-gia-tay-ha-long-hut-chan-khong"] = "/images/products/halong-squid-cake.jpg",
            ["sau-rieng-ri6-cap-dong-tach-mui"] = "/images/products/ri6-durian.jpg",
            ["ca-tuyet-dai-tay-duong"] = "/images/products/atlantic-cod.jpg",
            ["bach-tuoc-baby-cap-dong"] = "/images/products/baby-octopus.jpg"
        };

        if (await context.Products.AnyAsync())
        {
            // Tự động cập nhật lại đường dẫn ảnh minh họa chính xác nếu DB cũ vẫn đang dùng URL Unsplash
            var existingProducts = await context.Products.ToListAsync();
            var updatedAny = false;
            foreach (var prod in existingProducts)
            {
                if (localProductImages.TryGetValue(prod.Slug, out var localPath) &&
                    (string.IsNullOrWhiteSpace(prod.ImageUrl) || prod.ImageUrl.StartsWith("https://images.unsplash.com/", StringComparison.OrdinalIgnoreCase)))
                {
                    prod.ImageUrl = localPath;
                    updatedAny = true;
                }
            }
            if (updatedAny)
            {
                await context.SaveChangesAsync();
            }
            return;
        }

        var now = DateTime.UtcNow;

        // 1. Seed Categories
        var categories = new List<Category>
        {
            new()
            {
                Name = "Thủy Hải Sản Nhập Khẩu Cao Cấp",
                Slug = "thuy-hai-san-nhap-khau",
                Description = "Cá hồi Nauy, Cá tuyết Đại Tây Dương, Cồi sò điệp Hokkaido cấp đông sâu -18°C chuẩn IQF.",
                IconClass = "bi-tsunami",
                DisplayOrder = 1
            },
            new()
            {
                Name = "Hải Sản Biển Việt Nam Cấp Đông IQF",
                Slug = "hai-san-bien-viet-nam",
                Description = "Tôm sú biển tự nhiên, Mực ống trứng, Bạch tuộc baby đánh bắt từ vùng biển Côn Đảo & Phú Quốc.",
                IconClass = "bi-water",
                DisplayOrder = 2
            },
            new()
            {
                Name = "Đặc Sản Chế Biến & Nông Sản Đông Lạnh",
                Slug = "dac-san-che-bien-nong-san",
                Description = "Chả mực giã tay Hạ Long hút chân không và Sầu riêng Ri6 tách múi cấp đông Nitơ lỏng.",
                IconClass = "bi-box-seam",
                DisplayOrder = 3
            }
        };
        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        // 2. Seed 3 Suppliers (Theo đặc tả Công ty TNHH S-NEXT)
        var suppliers = new List<Supplier>
        {
            new()
            {
                SupplierCode = "SUP-MN01",
                Name = "Kho Lạnh Thủy Sản Miền Nam",
                ContactPerson = "Ông Trần Quốc Bảo",
                Phone = "0903881234",
                Email = "supply@kholanhmiennam.vn",
                Address = "Lô B12, KCN Hiệp Phước, Nhà Bè, TP. Hồ Chí Minh",
                ColdStorageStandard = "ISO 22000:2018 & HACCP (-22°C đến -18°C)"
            },
            new()
            {
                SupplierCode = "SUP-LT02",
                Name = "Nông Sản Xuất Khẩu Long Tiên",
                ContactPerson = "Bà Nguyễn Thị Mai Hương",
                Phone = "0918223456",
                Email = "export@longtienagri.vn",
                Address = "Ấp Mỹ Thạnh, Xã Long Tiên, Huyện Cai Lậy, Tiền Giang",
                ColdStorageStandard = "GlobalG.A.P & IQF Liquid Nitrogen (-35°C)"
            },
            new()
            {
                SupplierCode = "SUP-CD03",
                Name = "Hải Sản Côn Đảo S-NEXT",
                ContactPerson = "Ông Lê Hoàng Hải",
                Phone = "0989776543",
                Email = "condao.fleet@snextseafood.vn",
                Address = "Cảng Cá Bến Đầm, Huyện Côn Đảo, Bà Rịa - Vũng Tàu (VP: 266/78/47 Tô Hiến Thành, P. Hòa Hưng, TP.HCM)",
                ColdStorageStandard = "Tàu Cấp Đông Trên Biển & Chuỗi Lạnh S-NEXT (-18°C)"
            }
        };
        await context.Suppliers.AddRangeAsync(suppliers);
        await context.SaveChangesAsync();

        // 3. Seed 8 Signature Products (each with 2 Variants: 500g and 1kg = 16 Variants)
        var products = new List<Product>
        {
            new()
            {
                CategoryId = categories[0].CategoryId,
                Name = "Cá hồi Nauy phi lê cấp đông",
                Slug = "ca-hoi-nauy-phi-le-cap-dong",
                ShortDescription = "Phi lê cá hồi Salmo Salar nhập khẩu Na Uy, cấp đông sâu -18°C giữ trọn Omega-3 và vân mỡ tươi béo.",
                Description = "Cá hồi Nauy phi lê tại S-NEXT Seafood được nhập khẩu chính ngạch từ vùng biển lạnh Bắc Âu, pha lóc trong phòng lạnh tiêu chuẩn HACCP và hút chân không từng khay. Thích hợp chế biến áp chảo sốt bơ chanh, nướng lò hoặc nấu cháo dinh dưỡng.",
                Origin = "Na Uy (Norway)",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Đóng gói hút chân không",
                ImageUrl = "/images/products/salmon-fillet.jpg",
                IsFeatured = true,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-SALMON-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 295000m, OriginalPrice = 330000m, SafetyStockThreshold = 15 },
                    new() { Sku = "SNEXT-SALMON-1KG", PackagingSpec = "Túi 1kg (2 Khay)", WeightGrams = 1000, Price = 569000m, OriginalPrice = 640000m, SafetyStockThreshold = 10 }
                }
            },
            new()
            {
                CategoryId = categories[1].CategoryId,
                Name = "Tôm sú biển cấp đông IQF",
                Slug = "tom-su-bien-cap-dong-iqf",
                ShortDescription = "Tôm sú biển tự nhiên size đại (15-20 con/kg), cấp đông rời từng con (IQF) không mạ băng dày.",
                Description = "Tôm sú biển đánh bắt tự nhiên tại vùng biển Côn Đảo - Cà Mau, được cấp đông siêu tốc bằng công nghệ IQF ngay sau khi lên tàu. Thịt tôm săn chắc, ngọt đậm đà, tiện lợi khi rã đông từng phần theo nhu cầu bữa ăn gia đình.",
                Origin = "Côn Đảo - Cà Mau",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Hộp khóa nhiệt IQF & Hút chân không",
                ImageUrl = "/images/products/tiger-prawn.jpg",
                IsFeatured = true,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-TIGERPRAWN-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 245000m, OriginalPrice = 275000m, SafetyStockThreshold = 20 },
                    new() { Sku = "SNEXT-TIGERPRAWN-1KG", PackagingSpec = "Hộp 1kg", WeightGrams = 1000, Price = 470000m, OriginalPrice = 530000m, SafetyStockThreshold = 15 }
                }
            },
            new()
            {
                CategoryId = categories[0].CategoryId,
                Name = "Cồi sò điệp Nhật Bản",
                Slug = "coi-so-diep-nhat-ban",
                ShortDescription = "Cồi sò điệp Hokkaido Nhật Bản chuẩn Sashimi, trắng ngần, ngọt thanh và mềm tan.",
                Description = "Cồi sò điệp Hokkaido được thu hoạch từ vùng biển lạnh phía Bắc Nhật Bản, tách vỏ và cấp đông proton giữ nguyên cấu trúc tế bào. Có thể dùng trực tiếp làm Sashimi, áp chảo bơ tỏi hoặc nấu súp hải sản cao cấp.",
                Origin = "Hokkaido, Nhật Bản",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Đóng gói hút chân không IQF",
                ImageUrl = "/images/products/japanese-scallop.jpg",
                IsFeatured = true,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-SCALLOP-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 385000m, OriginalPrice = 420000m, SafetyStockThreshold = 12 },
                    new() { Sku = "SNEXT-SCALLOP-1KG", PackagingSpec = "Túi 1kg", WeightGrams = 1000, Price = 740000m, OriginalPrice = 810000m, SafetyStockThreshold = 10 }
                }
            },
            new()
            {
                CategoryId = categories[1].CategoryId,
                Name = "Mực ống trứng cấp đông",
                Slug = "muc-ong-trung-cap-dong",
                ShortDescription = "Mực ống trứng Côn Đảo căng tròn 95% đầy trứng, da bóng hồng, cấp đông ngay trên tàu.",
                Description = "Mực ống trứng câu đêm tại vùng biển Côn Đảo, tuyển chọn những con mực ôm đầy trứng béo ngậy, cấp đông sâu ở -18°C. Hấp hành gừng, chiên nước mắm hoặc nướng muối ớt đều giữ được độ giòn ngọt đặc trưng.",
                Origin = "Côn Đảo, Việt Nam",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Khay hút chân không 2 lớp",
                ImageUrl = "/images/products/egg-squid.jpg",
                IsFeatured = true,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-SQUID-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 189000m, OriginalPrice = 215000m, SafetyStockThreshold = 18 },
                    new() { Sku = "SNEXT-SQUID-1KG", PackagingSpec = "Túi 1kg (2 Khay)", WeightGrams = 1000, Price = 365000m, OriginalPrice = 410000m, SafetyStockThreshold = 12 }
                }
            },
            new()
            {
                CategoryId = categories[2].CategoryId,
                Name = "Chả mực giã tay Hạ Long hút chân không",
                Slug = "cha-muc-gia-tay-ha-long-hut-chan-khong",
                ShortDescription = "Chả mực nang giã tay truyền thống Hạ Long, giòn sần sật từng miếng mực tươi, không pha bột độn.",
                Description = "Chế biến từ 100% mực nang tươi đánh bắt tại Vịnh Bắc Bộ, giã tay thủ công kết hợp tiêu sọ Phú Quốc và nước mắm cốt. Đóng gói hút chân không chuẩn vệ sinh ATTP, bảo quản lạnh sâu giữ trọn hương vị truyền thống.",
                Origin = "Hạ Long, Quảng Ninh",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Đóng gói hút chân không",
                ImageUrl = "/images/products/halong-squid-cake.jpg",
                IsFeatured = true,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-SQUIDCAKE-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 235000m, OriginalPrice = 260000m, SafetyStockThreshold = 15 },
                    new() { Sku = "SNEXT-SQUIDCAKE-1KG", PackagingSpec = "Túi 1kg (2 Khay)", WeightGrams = 1000, Price = 450000m, OriginalPrice = 500000m, SafetyStockThreshold = 10 }
                }
            },
            new()
            {
                CategoryId = categories[2].CategoryId,
                Name = "Sầu riêng Ri6 cấp đông tách múi",
                Slug = "sau-rieng-ri6-cap-dong-tach-mui",
                ShortDescription = "Sầu riêng Ri6 chín cây Tiền Giang tách múi, cấp đông Nitơ lỏng giữ nguyên độ béo dẻo vàng ươm.",
                Description = "Sản phẩm hợp tác độc quyền cùng Nông Sản Xuất Khẩu Long Tiên: Sầu riêng Ri6 chín tự nhiên đạt độ đường Brix chuẩn xuất khẩu, tách múi trong phòng sạch và cấp đông Nitơ lỏng ở -35°C, bảo quản ở -18°C.",
                Origin = "Cai Lậy, Tiền Giang",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Hộp hút chân không màng Seal",
                ImageUrl = "/images/products/ri6-durian.jpg",
                IsFeatured = true,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-DURIAN-500G", PackagingSpec = "Hộp 500g", WeightGrams = 500, Price = 195000m, OriginalPrice = 225000m, SafetyStockThreshold = 15 },
                    new() { Sku = "SNEXT-DURIAN-1KG", PackagingSpec = "Hộp 1kg", WeightGrams = 1000, Price = 375000m, OriginalPrice = 430000m, SafetyStockThreshold = 10 }
                }
            },
            new()
            {
                CategoryId = categories[0].CategoryId,
                Name = "Cá tuyết Đại Tây Dương",
                Slug = "ca-tuyet-dai-tay-duong",
                ShortDescription = "Thịt cá tuyết trắng ngần, thớ thịt dày mềm, giàu DHA & Protein tinh khiết, không xương dăm.",
                Description = "Cá tuyết Đại Tây Dương (Atlantic Cod) được mệnh danh là 'vàng trắng của biển cả', cắt khoanh/phi lê tiêu chuẩn và hút chân không bảo quản ở -18°C. Rất được ưa chuộng cho thực đơn ăn dặm cao cấp và nhà hàng Âu.",
                Origin = "Bắc Đại Tây Dương",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Đóng gói hút chân không",
                ImageUrl = "/images/products/atlantic-cod.jpg",
                IsFeatured = false,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-CODFISH-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 445000m, OriginalPrice = 490000m, SafetyStockThreshold = 10 },
                    new() { Sku = "SNEXT-CODFISH-1KG", PackagingSpec = "Túi 1kg", WeightGrams = 1000, Price = 860000m, OriginalPrice = 950000m, SafetyStockThreshold = 8 }
                }
            },
            new()
            {
                CategoryId = categories[1].CategoryId,
                Name = "Bạch tuộc baby cấp đông",
                Slug = "bach-tuoc-baby-cap-dong",
                ShortDescription = "Bạch tuộc baby nguyên con làm sạch nội tạng, cấp đông IQF giòn ngọt, sẵn sàng nướng sa tế hoặc nhúng lẩu.",
                Description = "Bạch tuộc baby thu hoạch từ vùng biển Côn Đảo, đã được làm sạch, quay muối tạo độ giòn tự nhiên và cấp đông IQF. Đóng gói khay 500g và túi 1kg tiện lợi cho các bữa tiệc BBQ cuối tuần.",
                Origin = "Côn Đảo, Việt Nam",
                StorageCondition = "Đông lạnh -18°C",
                PackagingMethod = "Khay hút chân không IQF",
                ImageUrl = "/images/products/baby-octopus.jpg",
                IsFeatured = false,
                Variants = new List<ProductVariant>
                {
                    new() { Sku = "SNEXT-OCTOPUS-500G", PackagingSpec = "Khay 500g", WeightGrams = 500, Price = 155000m, OriginalPrice = 180000m, SafetyStockThreshold = 20 },
                    new() { Sku = "SNEXT-OCTOPUS-1KG", PackagingSpec = "Túi 1kg", WeightGrams = 1000, Price = 295000m, OriginalPrice = 340000m, SafetyStockThreshold = 15 }
                }
            }
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();

        var variantsBySku = await context.ProductVariants.ToDictionaryAsync(v => v.Sku);

        // 4. Seed Vouchers (Bao gồm mã xả hàng cận date & mã ưu đãi S-NEXT)
        var vouchers = new List<Voucher>
        {
            new()
            {
                Code = "SNEXT50",
                Description = "Giảm 10% (tối đa 50.000đ) cho đơn hàng thủy hải sản S-NEXT từ 400.000đ",
                DiscountPercent = 10,
                MaxDiscountAmount = 50000m,
                MinOrderAmount = 400000m,
                ExpiryDate = now.AddMonths(3),
                IsActive = true,
                IsClearanceVoucher = false
            },
            new()
            {
                Code = "XAHANG25",
                Description = "Ưu đãi Xả Kho Cận Hạn: Giảm 25% (tối đa 120.000đ) cho các lô thủy hải sản ưu đãi trong tuần",
                DiscountPercent = 25,
                MaxDiscountAmount = 120000m,
                MinOrderAmount = 250000m,
                ExpiryDate = now.AddDays(15),
                IsActive = true,
                IsClearanceVoucher = true
            },
            new()
            {
                Code = "VIPSEAFOOD",
                Description = "Ưu đãi Khách Hàng Thân Thiết: Giảm 15% (tối đa 150.000đ) cho đơn từ 1.000.000đ",
                DiscountPercent = 15,
                MaxDiscountAmount = 150000m,
                MinOrderAmount = 1000000m,
                ExpiryDate = now.AddMonths(6),
                IsActive = true,
                IsClearanceVoucher = false
            }
        };
        await context.Vouchers.AddRangeAsync(vouchers);
        await context.SaveChangesAsync();

        // 5. Seed Warehouse Receipts
        var receipts = new List<WarehouseReceipt>
        {
            new()
            {
                ReceiptCode = "NK-202606-01",
                ReceiptType = "Import",
                SupplierId = suppliers[0].SupplierId,
                TotalCost = 42500000m,
                CreatedAt = now.AddDays(-170),
                PerformedBy = "Nguyễn Văn Khoa (Thủ kho)",
                Note = "Nhập lô Cá hồi & Sò điệp đợt tháng 06 (Hiện tại đang cận hạn sử dụng)"
            },
            new()
            {
                ReceiptCode = "NK-202610-02",
                ReceiptType = "Import",
                SupplierId = suppliers[2].SupplierId,
                TotalCost = 68400000m,
                CreatedAt = now.AddDays(-25),
                PerformedBy = "Nguyễn Văn Khoa (Thủ kho)",
                Note = "Nhập tàu hải sản Côn Đảo (Tôm sú IQF, Mực ống trứng, Bạch tuộc baby)"
            },
            new()
            {
                ReceiptCode = "NK-202611-01",
                ReceiptType = "Import",
                SupplierId = suppliers[0].SupplierId,
                TotalCost = 95200000m,
                CreatedAt = now.AddDays(-5),
                PerformedBy = "Trần Minh Tâm (Quản lý WMS)",
                Note = "Nhập lô mới hạn sử dụng dài (6 tháng) chuẩn bị cao điểm cuối năm"
            },
            new()
            {
                ReceiptCode = "NK-202611-02",
                ReceiptType = "Import",
                SupplierId = suppliers[1].SupplierId,
                TotalCost = 34000000m,
                CreatedAt = now.AddDays(-4),
                PerformedBy = "Trần Minh Tâm (Quản lý WMS)",
                Note = "Nhập Sầu riêng Ri6 cấp đông tách múi & Chả mực Hạ Long"
            }
        };
        await context.WarehouseReceipts.AddRangeAsync(receipts);
        await context.SaveChangesAsync();

        // 6. Seed 12 Core WarehouseBatches (+ 6 supplementary batches so all 16 variants have stock)
        // Đặc tả: Có cả lô hạn sử dụng còn dài (6 tháng), lô cận date (còn 7 ngày để hiển thị cảnh báo vàng trên UI),
        // và lô có đơn hàng đang giữ chỗ (ReservedQuantity > 0).
        var batches = new List<WarehouseBatch>
        {
            // Lô 1: Cá hồi 500g - CẬN DATE (Còn 7 ngày!) + Đang giữ chỗ 2 khay -> Ưu tiên xuất số 1 theo FEFO
            new()
            {
                BatchCode = "LOT-202611-001",
                ProductVariantId = variantsBySku["SNEXT-SALMON-500G"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-173),
                ExpiryDate = now.AddDays(7), // Cận date 7 ngày (Cảnh báo vàng!)
                ImportDate = now.AddDays(-170),
                ImportUnitPrice = 210000m,
                InitialQuantity = 40,
                RemainingQuantity = 8,
                ReservedQuantity = 2, // Đang giữ chỗ cho đơn chờ thanh toán VNPay
                StorageRack = "KHO-LANH-A01",
                BatchStatus = BatchStatus.NearExpiry
            },
            // Lô 2: Cá hồi 500g - Hạn dài (Còn 175 ngày ~ 6 tháng) -> Xuất nối tiếp khi Lô 1 hết theo FEFO
            new()
            {
                BatchCode = "LOT-202611-002",
                ProductVariantId = variantsBySku["SNEXT-SALMON-500G"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-7),
                ExpiryDate = now.AddDays(175),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 215000m,
                InitialQuantity = 60,
                RemainingQuantity = 52,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-A02",
                BatchStatus = BatchStatus.Active
            },
            // Lô 3: Cá hồi 1kg - Hạn dài (6 tháng)
            new()
            {
                BatchCode = "LOT-202611-003",
                ProductVariantId = variantsBySku["SNEXT-SALMON-1KG"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-7),
                ExpiryDate = now.AddDays(175),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 410000m,
                InitialQuantity = 35,
                RemainingQuantity = 28,
                ReservedQuantity = 1,
                StorageRack = "KHO-LANH-A03",
                BatchStatus = BatchStatus.Active
            },
            // Lô 4: Tôm sú biển 500g - CẬN DATE (Còn 11 ngày < 15 ngày)
            new()
            {
                BatchCode = "LOT-202611-004",
                ProductVariantId = variantsBySku["SNEXT-TIGERPRAWN-500G"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-169),
                ExpiryDate = now.AddDays(11),
                ImportDate = now.AddDays(-165),
                ImportUnitPrice = 165000m,
                InitialQuantity = 50,
                RemainingQuantity = 12,
                ReservedQuantity = 3,
                StorageRack = "KHO-LANH-B01",
                BatchStatus = BatchStatus.NearExpiry
            },
            // Lô 5: Tôm sú biển 500g - Hạn dài (160 ngày)
            new()
            {
                BatchCode = "LOT-202611-005",
                ProductVariantId = variantsBySku["SNEXT-TIGERPRAWN-500G"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-26),
                ExpiryDate = now.AddDays(160),
                ImportDate = now.AddDays(-25),
                ImportUnitPrice = 170000m,
                InitialQuantity = 60,
                RemainingQuantity = 55,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B02",
                BatchStatus = BatchStatus.Active
            },
            // Lô 6: Cồi sò điệp Nhật Bản 500g - Sắp hết tồn kho (LowStock: Còn 6 khay < SafetyStockThreshold 12)
            new()
            {
                BatchCode = "LOT-202611-006",
                ProductVariantId = variantsBySku["SNEXT-SCALLOP-500G"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-30),
                ExpiryDate = now.AddDays(150),
                ImportDate = now.AddDays(-25),
                ImportUnitPrice = 275000m,
                InitialQuantity = 30,
                RemainingQuantity = 6,
                ReservedQuantity = 1,
                StorageRack = "KHO-LANH-A04",
                BatchStatus = BatchStatus.LowStock
            },
            // Lô 7: Mực ống trứng 500g - CẬN DATE (Còn 7 ngày!)
            new()
            {
                BatchCode = "LOT-202611-007",
                ProductVariantId = variantsBySku["SNEXT-SQUID-500G"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-173),
                ExpiryDate = now.AddDays(7),
                ImportDate = now.AddDays(-170),
                ImportUnitPrice = 125000m,
                InitialQuantity = 45,
                RemainingQuantity = 9,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B03",
                BatchStatus = BatchStatus.NearExpiry
            },
            // Lô 8: Mực ống trứng 500g - Hạn dài (170 ngày)
            new()
            {
                BatchCode = "LOT-202611-008",
                ProductVariantId = variantsBySku["SNEXT-SQUID-500G"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-10),
                ExpiryDate = now.AddDays(170),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 130000m,
                InitialQuantity = 50,
                RemainingQuantity = 46,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B04",
                BatchStatus = BatchStatus.Active
            },
            // Lô 9: Chả mực giã tay Hạ Long 500g - Hạn dài (180 ngày)
            new()
            {
                BatchCode = "LOT-202611-009",
                ProductVariantId = variantsBySku["SNEXT-SQUIDCAKE-500G"].VariantId,
                SupplierId = suppliers[1].SupplierId,
                ManufactureDate = now.AddDays(-5),
                ExpiryDate = now.AddDays(180),
                ImportDate = now.AddDays(-4),
                ImportUnitPrice = 165000m,
                InitialQuantity = 45,
                RemainingQuantity = 38,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-C01",
                BatchStatus = BatchStatus.Active
            },
            // Lô 10: Sầu riêng Ri6 cấp đông 500g - Hạn dài (180 ngày)
            new()
            {
                BatchCode = "LOT-202611-010",
                ProductVariantId = variantsBySku["SNEXT-DURIAN-500G"].VariantId,
                SupplierId = suppliers[1].SupplierId,
                ManufactureDate = now.AddDays(-6),
                ExpiryDate = now.AddDays(180),
                ImportDate = now.AddDays(-4),
                ImportUnitPrice = 135000m,
                InitialQuantity = 50,
                RemainingQuantity = 42,
                ReservedQuantity = 2,
                StorageRack = "KHO-LANH-C02",
                BatchStatus = BatchStatus.Active
            },
            // Lô 11: Cá tuyết Đại Tây Dương 500g - Sắp hết tồn kho (LowStock: Còn 5 khay < ngưỡng 10)
            new()
            {
                BatchCode = "LOT-202611-011",
                ProductVariantId = variantsBySku["SNEXT-CODFISH-500G"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-20),
                ExpiryDate = now.AddDays(165),
                ImportDate = now.AddDays(-15),
                ImportUnitPrice = 320000m,
                InitialQuantity = 25,
                RemainingQuantity = 5,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-A05",
                BatchStatus = BatchStatus.LowStock
            },
            // Lô 12: Bạch tuộc baby cấp đông 500g - ĐÃ HẾT HẠN (Quá hạn 2 ngày - Phục vụ demo lập phiếu xuất hủy SpoilageDiscard)
            new()
            {
                BatchCode = "LOT-202605-012",
                ProductVariantId = variantsBySku["SNEXT-OCTOPUS-500G"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-185),
                ExpiryDate = now.AddDays(-2), // Đã quá hạn 2 ngày -> Không tính vào AvailableQuantity!
                ImportDate = now.AddDays(-180),
                ImportUnitPrice = 105000m,
                InitialQuantity = 30,
                RemainingQuantity = 4,
                ReservedQuantity = 0,
                StorageRack = "KHO-CACH-LY-X01",
                BatchStatus = BatchStatus.Expired
            },
            // Các lô bổ sung cho biến thể 1kg & Bạch tuộc 500g mới để khách đặt mua mọi biến thể mượt mà
            new()
            {
                BatchCode = "LOT-202611-013",
                ProductVariantId = variantsBySku["SNEXT-OCTOPUS-500G"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-8),
                ExpiryDate = now.AddDays(172),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 108000m,
                InitialQuantity = 50,
                RemainingQuantity = 44,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B05",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-014",
                ProductVariantId = variantsBySku["SNEXT-TIGERPRAWN-1KG"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-12),
                ExpiryDate = now.AddDays(168),
                ImportDate = now.AddDays(-10),
                ImportUnitPrice = 330000m,
                InitialQuantity = 35,
                RemainingQuantity = 30,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B06",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-015",
                ProductVariantId = variantsBySku["SNEXT-SCALLOP-1KG"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-10),
                ExpiryDate = now.AddDays(170),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 530000m,
                InitialQuantity = 25,
                RemainingQuantity = 22,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-A06",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-016",
                ProductVariantId = variantsBySku["SNEXT-SQUID-1KG"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-9),
                ExpiryDate = now.AddDays(171),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 250000m,
                InitialQuantity = 30,
                RemainingQuantity = 27,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B07",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-017",
                ProductVariantId = variantsBySku["SNEXT-SQUIDCAKE-1KG"].VariantId,
                SupplierId = suppliers[1].SupplierId,
                ManufactureDate = now.AddDays(-5),
                ExpiryDate = now.AddDays(175),
                ImportDate = now.AddDays(-4),
                ImportUnitPrice = 315000m,
                InitialQuantity = 25,
                RemainingQuantity = 21,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-C03",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-018",
                ProductVariantId = variantsBySku["SNEXT-DURIAN-1KG"].VariantId,
                SupplierId = suppliers[1].SupplierId,
                ManufactureDate = now.AddDays(-6),
                ExpiryDate = now.AddDays(174),
                ImportDate = now.AddDays(-4),
                ImportUnitPrice = 260000m,
                InitialQuantity = 30,
                RemainingQuantity = 26,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-C04",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-019",
                ProductVariantId = variantsBySku["SNEXT-CODFISH-1KG"].VariantId,
                SupplierId = suppliers[0].SupplierId,
                ManufactureDate = now.AddDays(-14),
                ExpiryDate = now.AddDays(166),
                ImportDate = now.AddDays(-10),
                ImportUnitPrice = 610000m,
                InitialQuantity = 20,
                RemainingQuantity = 16,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-A07",
                BatchStatus = BatchStatus.Active
            },
            new()
            {
                BatchCode = "LOT-202611-020",
                ProductVariantId = variantsBySku["SNEXT-OCTOPUS-1KG"].VariantId,
                SupplierId = suppliers[2].SupplierId,
                ManufactureDate = now.AddDays(-8),
                ExpiryDate = now.AddDays(172),
                ImportDate = now.AddDays(-5),
                ImportUnitPrice = 205000m,
                InitialQuantity = 35,
                RemainingQuantity = 31,
                ReservedQuantity = 0,
                StorageRack = "KHO-LANH-B08",
                BatchStatus = BatchStatus.Active
            }
        };

        await context.WarehouseBatches.AddRangeAsync(batches);
        await context.SaveChangesAsync();

        var batchByCode = await context.WarehouseBatches.ToDictionaryAsync(b => b.BatchCode);

        // 7. Seed Sample Orders (Paid VNPay, Paid MoMo, Paid COD, and 1 PendingPayment VNPay with Stock Reservation)
        var order1 = new Order
        {
            OrderCode = "ORD-20261115-1001",
            CustomerId = "CUST-001",
            CustomerName = "Phạm Minh Tuấn",
            CustomerPhone = "0909123456",
            CustomerEmail = "tuan.pham@gmail.com",
            ShippingAddress = "268 Lý Thường Kiệt, Phường 14, Quận 10, TP. Hồ Chí Minh",
            District = "Quận 10, TP. Hồ Chí Minh",
            DeliveryNote = "Giao giờ hành chính, đóng thùng xốp đá gel giữ lạnh.",
            AppliedVoucherCode = "SNEXT50",
            TotalAmount = 1080000m,
            DiscountAmount = 50000m,
            ShippingFee = 0m,
            FinalAmount = 1030000m,
            PaymentMethod = PaymentMethodType.VNPay,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now.AddDays(-2).AddMinutes(4)
        };

        var order1Item1 = new OrderItem
        {
            Order = order1,
            ProductVariantId = variantsBySku["SNEXT-SALMON-500G"].VariantId,
            Quantity = 2,
            UnitPrice = 295000m,
            LineTotal = 590000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-001"].BatchId,
                    BatchCode = "LOT-202611-001",
                    AllocatedQuantity = 2,
                    ManufactureDate = batchByCode["LOT-202611-001"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-001"].ExpiryDate,
                    ImportUnitPrice = 210000m,
                    IsExportConfirmed = true
                }
            }
        };

        var order1Item2 = new OrderItem
        {
            Order = order1,
            ProductVariantId = variantsBySku["SNEXT-TIGERPRAWN-500G"].VariantId,
            Quantity = 2,
            UnitPrice = 245000m,
            LineTotal = 490000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-004"].BatchId,
                    BatchCode = "LOT-202611-004",
                    AllocatedQuantity = 2,
                    ManufactureDate = batchByCode["LOT-202611-004"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-004"].ExpiryDate,
                    ImportUnitPrice = 165000m,
                    IsExportConfirmed = true
                }
            }
        };
        order1.OrderItems.Add(order1Item1);
        order1.OrderItems.Add(order1Item2);
        order1.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentMethod = "VNPay",
            TransactionRef = "ORD-20261115-1001-VNP",
            GatewayTransactionNo = "14589201",
            Amount = 1030000m,
            ResponseCode = "00",
            SecureHashVerified = true,
            CallbackPayload = "vnp_Amount=103000000&vnp_BankCode=NCB&vnp_ResponseCode=00&vnp_TransactionNo=14589201&vnp_TxnRef=ORD-20261115-1001-VNP",
            CreatedAt = now.AddDays(-2),
            PaidAt = now.AddDays(-2).AddMinutes(3)
        });

        // Order 2: Paid via MoMo QR
        var order2 = new Order
        {
            OrderCode = "ORD-20261115-1002",
            CustomerId = "CUST-002",
            CustomerName = "Lê Ngọc Thảo Vy",
            CustomerPhone = "0938765432",
            CustomerEmail = "thaovy.le@outlook.com",
            ShippingAddress = "828 Sư Vạn Hạnh, Phường 13, Quận 10, TP. Hồ Chí Minh",
            District = "Quận 10, TP. Hồ Chí Minh",
            DeliveryNote = "Giao tới sảnh chung cư, gọi trước 15 phút.",
            TotalAmount = 775000m,
            DiscountAmount = 0m,
            ShippingFee = 0m,
            FinalAmount = 775000m,
            PaymentMethod = PaymentMethodType.MoMo,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1).AddMinutes(2)
        };
        order2.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-SCALLOP-500G"].VariantId,
            Quantity = 1,
            UnitPrice = 385000m,
            LineTotal = 385000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-006"].BatchId,
                    BatchCode = "LOT-202611-006",
                    AllocatedQuantity = 1,
                    ManufactureDate = batchByCode["LOT-202611-006"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-006"].ExpiryDate,
                    ImportUnitPrice = 275000m,
                    IsExportConfirmed = true
                }
            }
        });
        order2.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-DURIAN-500G"].VariantId,
            Quantity = 2,
            UnitPrice = 195000m,
            LineTotal = 390000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-010"].BatchId,
                    BatchCode = "LOT-202611-010",
                    AllocatedQuantity = 2,
                    ManufactureDate = batchByCode["LOT-202611-010"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-010"].ExpiryDate,
                    ImportUnitPrice = 135000m,
                    IsExportConfirmed = true
                }
            }
        });
        order2.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentMethod = "MoMo",
            TransactionRef = "ORD-20261115-1002-MOMO",
            GatewayTransactionNo = "MOMO99283741",
            Amount = 775000m,
            ResponseCode = "00",
            SecureHashVerified = true,
            CallbackPayload = "partnerCode=SNEXTMOMO&orderId=ORD-20261115-1002-MOMO&resultCode=0&transId=MOMO99283741",
            CreatedAt = now.AddDays(-1),
            PaidAt = now.AddDays(-1).AddMinutes(2)
        });

        // Order 3: Paid via COD Nội thành TP.HCM
        var order3 = new Order
        {
            OrderCode = "ORD-20261116-1003",
            CustomerId = "CUST-003",
            CustomerName = "Nhà Hàng Hải Sản Biển Đông",
            CustomerPhone = "0912345678",
            CustomerEmail = "beptruong@biendongrestaurant.vn",
            ShippingAddress = "145 Nguyễn Thị Minh Khai, Quận 1, TP. Hồ Chí Minh",
            District = "Quận 1, TP. Hồ Chí Minh",
            DeliveryNote = "Xuất hóa đơn VAT Công ty TNHH S-NEXT.",
            AppliedVoucherCode = "VIPSEAFOOD",
            TotalAmount = 1239000m,
            DiscountAmount = 150000m,
            ShippingFee = 0m,
            FinalAmount = 1089000m,
            PaymentMethod = PaymentMethodType.COD,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = now.AddHours(-12),
            UpdatedAt = now.AddHours(-6)
        };
        order3.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-SQUIDCAKE-1KG"].VariantId,
            Quantity = 2,
            UnitPrice = 450000m,
            LineTotal = 900000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-017"].BatchId,
                    BatchCode = "LOT-202611-017",
                    AllocatedQuantity = 2,
                    ManufactureDate = batchByCode["LOT-202611-017"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-017"].ExpiryDate,
                    ImportUnitPrice = 315000m,
                    IsExportConfirmed = true
                }
            }
        });
        order3.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-SQUID-500G"].VariantId,
            Quantity = 1,
            UnitPrice = 189000m,
            LineTotal = 189000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-007"].BatchId,
                    BatchCode = "LOT-202611-007",
                    AllocatedQuantity = 1,
                    ManufactureDate = batchByCode["LOT-202611-007"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-007"].ExpiryDate,
                    ImportUnitPrice = 125000m,
                    IsExportConfirmed = true
                }
            }
        });
        order3.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentMethod = "COD",
            TransactionRef = "ORD-20261116-1003-COD",
            GatewayTransactionNo = "COD-SHIP-8812",
            Amount = 1089000m,
            ResponseCode = "00",
            SecureHashVerified = true,
            CallbackPayload = "COD_COLLECTED_BY_COLD_CHAIN_SHIPPER",
            CreatedAt = now.AddHours(-12),
            PaidAt = now.AddHours(-6)
        });

        // Order 4: PendingPayment VNPay (Đang giữ chỗ tồn kho tạm thời Stock Reservation!)
        var order4 = new Order
        {
            OrderCode = "ORD-20261116-1004",
            CustomerId = "CUST-004",
            CustomerName = "Đặng Hoàng Phúc",
            CustomerPhone = "0977889900",
            CustomerEmail = "phuc.dang@huflit.edu.vn",
            ShippingAddress = "266/78/47 Tô Hiến Thành, Phường Hòa Hưng, Quận 10, TP. Hồ Chí Minh",
            District = "Quận 10, TP. Hồ Chí Minh",
            DeliveryNote = "Khách đang thực hiện thanh toán trên cổng VNPay Sandbox (Giữ hàng FEFO 15 phút).",
            AppliedVoucherCode = "XAHANG25",
            TotalAmount = 2669000m,
            DiscountAmount = 120000m,
            ShippingFee = 0m,
            FinalAmount = 2549000m,
            PaymentMethod = PaymentMethodType.VNPay,
            OrderStatus = OrderStatus.PendingPayment,
            PaymentStatus = PaymentStatus.Unpaid,
            CreatedAt = now.AddMinutes(-8)
        };
        order4.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-SALMON-500G"].VariantId,
            Quantity = 2,
            UnitPrice = 295000m,
            LineTotal = 590000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-001"].BatchId,
                    BatchCode = "LOT-202611-001",
                    AllocatedQuantity = 2,
                    ManufactureDate = batchByCode["LOT-202611-001"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-001"].ExpiryDate,
                    ImportUnitPrice = 210000m,
                    IsExportConfirmed = false
                }
            }
        });
        order4.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-SALMON-1KG"].VariantId,
            Quantity = 1,
            UnitPrice = 569000m,
            LineTotal = 569000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-003"].BatchId,
                    BatchCode = "LOT-202611-003",
                    AllocatedQuantity = 1,
                    ManufactureDate = batchByCode["LOT-202611-003"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-003"].ExpiryDate,
                    ImportUnitPrice = 410000m,
                    IsExportConfirmed = false
                }
            }
        });
        order4.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-TIGERPRAWN-500G"].VariantId,
            Quantity = 3,
            UnitPrice = 245000m,
            LineTotal = 735000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-004"].BatchId,
                    BatchCode = "LOT-202611-004",
                    AllocatedQuantity = 3,
                    ManufactureDate = batchByCode["LOT-202611-004"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-004"].ExpiryDate,
                    ImportUnitPrice = 165000m,
                    IsExportConfirmed = false
                }
            }
        });
        order4.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-SCALLOP-500G"].VariantId,
            Quantity = 1,
            UnitPrice = 385000m,
            LineTotal = 385000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-006"].BatchId,
                    BatchCode = "LOT-202611-006",
                    AllocatedQuantity = 1,
                    ManufactureDate = batchByCode["LOT-202611-006"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-006"].ExpiryDate,
                    ImportUnitPrice = 275000m,
                    IsExportConfirmed = false
                }
            }
        });
        order4.OrderItems.Add(new OrderItem
        {
            ProductVariantId = variantsBySku["SNEXT-DURIAN-500G"].VariantId,
            Quantity = 2,
            UnitPrice = 195000m,
            LineTotal = 390000m,
            BatchAllocations = new List<OrderItemBatchAllocation>
            {
                new()
                {
                    BatchId = batchByCode["LOT-202611-010"].BatchId,
                    BatchCode = "LOT-202611-010",
                    AllocatedQuantity = 2,
                    ManufactureDate = batchByCode["LOT-202611-010"].ManufactureDate,
                    ExpiryDate = batchByCode["LOT-202611-010"].ExpiryDate,
                    ImportUnitPrice = 135000m,
                    IsExportConfirmed = false
                }
            }
        });
        order4.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentMethod = "VNPay",
            TransactionRef = "ORD-20261116-1004-VNP",
            Amount = 2549000m,
            ResponseCode = "PENDING",
            SecureHashVerified = false,
            CreatedAt = now.AddMinutes(-8)
        });

        await context.Orders.AddRangeAsync(order1, order2, order3, order4);
        await context.SaveChangesAsync();

        // 8. Seed InventoryTransactions Log
        var inventoryTx = new List<InventoryTransaction>
        {
            new()
            {
                ReceiptCode = "NK-202606-01",
                WarehouseReceiptId = receipts[0].ReceiptId,
                BatchId = batchByCode["LOT-202611-001"].BatchId,
                TransactionType = InventoryTransactionType.Import,
                QuantityChanged = 40,
                RemainingQuantityAfter = 40,
                ReservedQuantityAfter = 0,
                CreatedAt = now.AddDays(-170),
                PerformedBy = "Nguyễn Văn Khoa (Thủ kho)",
                Note = "Nhập kho ban đầu lô Cá hồi Nauy 500g LOT-202611-001"
            },
            new()
            {
                ReceiptCode = "NK-202611-01",
                WarehouseReceiptId = receipts[2].ReceiptId,
                BatchId = batchByCode["LOT-202611-002"].BatchId,
                TransactionType = InventoryTransactionType.Import,
                QuantityChanged = 60,
                RemainingQuantityAfter = 60,
                ReservedQuantityAfter = 0,
                CreatedAt = now.AddDays(-5),
                PerformedBy = "Trần Minh Tâm (Quản lý WMS)",
                Note = "Nhập kho lô mới Cá hồi Nauy 500g LOT-202611-002 (HSD 6 tháng)"
            },
            new()
            {
                ReceiptCode = "XK-202611-01",
                BatchId = batchByCode["LOT-202611-001"].BatchId,
                OrderId = order1.OrderId,
                TransactionType = InventoryTransactionType.SaleExport,
                QuantityChanged = -2,
                RemainingQuantityAfter = 8,
                ReservedQuantityAfter = 0,
                CreatedAt = now.AddDays(-2),
                PerformedBy = "VNPay IPN Webhook (FEFO)",
                Note = "Xuất bán tự động theo FEFO cho đơn ORD-20261115-1001"
            },
            new()
            {
                ReceiptCode = "XK-202611-02",
                BatchId = batchByCode["LOT-202611-004"].BatchId,
                OrderId = order1.OrderId,
                TransactionType = InventoryTransactionType.SaleExport,
                QuantityChanged = -2,
                RemainingQuantityAfter = 12,
                ReservedQuantityAfter = 0,
                CreatedAt = now.AddDays(-2),
                PerformedBy = "VNPay IPN Webhook (FEFO)",
                Note = "Xuất bán tự động theo FEFO cho đơn ORD-20261115-1001"
            },
            new()
            {
                ReceiptCode = "XK-202611-03",
                BatchId = batchByCode["LOT-202611-006"].BatchId,
                OrderId = order2.OrderId,
                TransactionType = InventoryTransactionType.SaleExport,
                QuantityChanged = -1,
                RemainingQuantityAfter = 6,
                ReservedQuantityAfter = 0,
                CreatedAt = now.AddDays(-1),
                PerformedBy = "MoMo Callback (FEFO)",
                Note = "Xuất bán tự động theo FEFO cho đơn ORD-20261115-1002"
            },
            new()
            {
                ReceiptCode = "HOLD-202611-04",
                BatchId = batchByCode["LOT-202611-001"].BatchId,
                OrderId = order4.OrderId,
                TransactionType = InventoryTransactionType.Reservation,
                QuantityChanged = 2,
                RemainingQuantityAfter = 8,
                ReservedQuantityAfter = 2,
                CreatedAt = now.AddMinutes(-8),
                PerformedBy = "OrderCheckoutFacade (Stock Reservation)",
                Note = "Tạm giữ 2 khay từ lô cận date LOT-202611-001 chờ khách thanh toán VNPay đơn ORD-20261116-1004"
            },
            new()
            {
                ReceiptCode = "HOLD-202611-05",
                BatchId = batchByCode["LOT-202611-004"].BatchId,
                OrderId = order4.OrderId,
                TransactionType = InventoryTransactionType.Reservation,
                QuantityChanged = 3,
                RemainingQuantityAfter = 12,
                ReservedQuantityAfter = 3,
                CreatedAt = now.AddMinutes(-8),
                PerformedBy = "OrderCheckoutFacade (Stock Reservation)",
                Note = "Tạm giữ 3 khay Tôm sú từ lô LOT-202611-004 chờ thanh toán VNPay đơn ORD-20261116-1004"
            }
        };

        await context.InventoryTransactions.AddRangeAsync(inventoryTx);
        await context.SaveChangesAsync();
    }
}
