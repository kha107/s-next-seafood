using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SNextSeafood.Application.Facades;
using SNextSeafood.Application.Strategies;
using SNextSeafood.Domain.Interfaces;
using SNextSeafood.Infrastructure.Data;
using SNextSeafood.Infrastructure.Repositories;
using SNextSeafood.Web.Helpers;

// 0. Tự động nạp biến môi trường từ file .env (nếu có)
EnvFileLoader.LoadFromSolutionRoot();

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context (SQL Server - EF Core Code First)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=SNextSeafoodDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<SNextDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Design Pattern #1: Repository & Unit of Work Pattern
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IWarehouseBatchRepository, WarehouseBatchRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 3. Design Pattern #2: Strategy Pattern (IPaymentGatewayStrategy)
builder.Services.AddScoped<IPaymentGatewayStrategy, VnPayStrategy>();
builder.Services.AddScoped<IPaymentGatewayStrategy, MoMoStrategy>();
builder.Services.AddScoped<IPaymentGatewayStrategy, CodStrategy>();
builder.Services.AddScoped<IPaymentStrategyFactory, PaymentStrategyFactory>();

// 4. Design Pattern #3: Facade Pattern (WarehouseFefoFacade & OrderCheckoutFacade)
builder.Services.AddScoped<IWarehouseFefoFacade, WarehouseFefoFacade>();
builder.Services.AddScoped<IOrderCheckoutFacade, OrderCheckoutFacade>();

// 5. Authentication & Authorization (Cookie Auth bảo vệ khu vực Quản trị Admin)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SNextSeafood.AdminAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/snext-portal/login";
        options.AccessDeniedPath = "/snext-portal/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// 6. MVC + RESTful Web API + Session Cart
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 7. Swagger / OpenAPI Documentation (Ẩn khỏi giao diện người dùng, chỉ dành cho lập trình viên)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "S-NEXT Seafood E-Commerce & FEFO WMS RESTful API",
        Version = "v1.0",
        Description = "Tài liệu OpenAPI cho hệ thống thương mại điện tử CÔNG TY TNHH S-NEXT (MST: 0313778856).",
        Contact = new OpenApiContact
        {
            Name = "Công ty TNHH S-NEXT",
            Email = "contact@snextseafood.vn"
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 8. Auto-Migrate & Run DataSeeder on Startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SNextDbContext>();
    await DbInitializer.SeedAsync(dbContext);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "S-NEXT Seafood FEFO WMS & Payment API v1.0");
    options.DocumentTitle = "Swagger UI - S-NEXT Seafood API";
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
