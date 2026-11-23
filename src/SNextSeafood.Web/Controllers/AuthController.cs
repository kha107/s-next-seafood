using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SNextSeafood.Web.Controllers;

public class AuthController : Controller
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("/snext-portal/login")]
    [HttpGet("/Admin/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Dashboard", "Admin");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [AllowAnonymous]
    [HttpPost("/snext-portal/login")]
    [HttpPost("/Admin/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var expectedUser = _configuration["AdminAuth:Username"] ?? "admin@snextseafood.vn";
        var altUser = _configuration["AdminAuth:AltUsername"] ?? "admin";
        var expectedPassword = _configuration["AdminAuth:Password"];
        var displayName = _configuration["AdminAuth:DisplayName"] ?? "Quản trị viên S-NEXT";

        if (string.IsNullOrWhiteSpace(expectedPassword))
        {
            ViewData["ErrorMessage"] = "Chưa cấu hình mật khẩu quản trị (AdminAuth__Password) trong tệp .env.";
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Username"] = username;
            return View();
        }

        var inputUser = (username ?? string.Empty).Trim();
        var isValidUser = string.Equals(inputUser, expectedUser, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(inputUser, altUser, StringComparison.OrdinalIgnoreCase);

        if (!isValidUser || !string.Equals(password, expectedPassword, StringComparison.Ordinal))
        {
            ViewData["ErrorMessage"] = "Tên đăng nhập hoặc mật khẩu Quản trị viên không chính xác.";
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Username"] = inputUser;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, expectedUser),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {displayName} đến với Hệ thống Quản trị S-NEXT Seafood.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Dashboard", "Admin");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("/snext-portal/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "Bạn đã đăng xuất khỏi hệ thống quản trị an toàn.";
        return RedirectToAction("Index", "Home");
    }
}
