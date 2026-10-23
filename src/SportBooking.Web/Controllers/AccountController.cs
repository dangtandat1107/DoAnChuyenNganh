using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

public class AccountController : Controller
{
    private const string ResetPurpose = "ResetPassword";
    private const int MaxSendPer15Min = 3;
    private const int MaxVerifyTries = 5;

    private readonly UserManager<ApplicationUser> _um;
    private readonly SignInManager<ApplicationUser> _sm;
    private readonly IAppEmailSender _email;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AccountController> _log;

    public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm,
        IAppEmailSender email, IMemoryCache cache, ILogger<AccountController> log)
    { _um = um; _sm = sm; _email = email; _cache = cache; _log = log; }

    // ---------------- Đăng nhập ----------------
    [HttpGet] public IActionResult Login(string? returnUrl) { ViewData["ReturnUrl"] = returnUrl; return View(new LoginVm()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl)
    {
        if (!ModelState.IsValid) return View(vm);
        var r = await _sm.PasswordSignInAsync(vm.Email, vm.Password, true, lockoutOnFailure: true);
        if (r.Succeeded) return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
        ModelState.AddModelError("", r.IsLockedOut ? "Tài khoản bị khóa tạm thời." : "Sai email hoặc mật khẩu.");
        return View(vm);
    }

    // ---------------- Đăng ký ----------------
    [HttpGet] public IActionResult Register() => View(new RegisterVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (vm.Role is not ("Customer" or "Owner")) ModelState.AddModelError(nameof(vm.Role), "Vai trò không hợp lệ.");
        if (!ModelState.IsValid) return View(vm);

        var u = new ApplicationUser { UserName = vm.Email, Email = vm.Email, FullName = vm.FullName, EmailConfirmed = true };
        var r = await _um.CreateAsync(u, vm.Password);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await _um.AddToRoleAsync(u, vm.Role);
        await _sm.SignInAsync(u, true);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await _sm.SignOutAsync(); return RedirectToAction("Index", "Home"); }

    public IActionResult Denied() => View();

    // ---------------- Quên mật khẩu: bước 1 - nhập email, gửi mã ----------------
    [HttpGet] public IActionResult ForgotPassword() => View(new ForgotPasswordVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Giới hạn số lần yêu cầu gửi mã (tính cả email không tồn tại để không lộ thông tin)
        var sendKey = $"fp:send:{vm.Email.Trim().ToLowerInvariant()}";
        _cache.TryGetValue(sendKey, out int sent);
        if (sent >= MaxSendPer15Min)
        {
            ModelState.AddModelError("", "Bạn đã yêu cầu quá nhiều lần. Vui lòng thử lại sau 15 phút.");
            return View(vm);
        }
        _cache.Set(sendKey, sent + 1, TimeSpan.FromMinutes(15));

        var user = await _um.FindByEmailAsync(vm.Email.Trim());
        if (user != null)
        {
            // Mã 6 chữ số do Identity sinh ra, gắn với tài khoản và hết hạn sau vài phút
            var code = await _um.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose);
            var body = $@"<div style='font-family:Arial,sans-serif'>
                <h2>FSS - Friendly Sport Space</h2>
                <p>Xin chào {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
                <p>Mã xác nhận đặt lại mật khẩu của bạn là:</p>
                <p style='font-size:32px;font-weight:bold;letter-spacing:6px'>{code}</p>
                <p>Mã có hiệu lực trong vài phút. Nếu bạn không yêu cầu, hãy bỏ qua email này.</p></div>";
            try { await _email.SendAsync(user.Email!, "Mã đặt lại mật khẩu FSS", body); }
            catch (Exception ex)
            {
                _log.LogError(ex, "Gửi email đặt lại mật khẩu thất bại");
                ModelState.AddModelError("", "Không gửi được email lúc này. Vui lòng thử lại sau.");
                return View(vm);
            }
        }

        // Luôn trả lời giống nhau dù email có tồn tại hay không
        TempData["Msg"] = "Nếu email tồn tại trong hệ thống, mã xác nhận đã được gửi. Hãy kiểm tra hộp thư (cả mục Spam).";
        return RedirectToAction(nameof(ResetPassword), new { email = vm.Email.Trim() });
    }

    // ---------------- Quên mật khẩu: bước 2 - nhập mã + mật khẩu mới ----------------
    [HttpGet] public IActionResult ResetPassword(string? email) => View(new ResetPasswordVm { Email = email ?? "" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var tryKey = $"fp:try:{vm.Email.Trim().ToLowerInvariant()}";
        _cache.TryGetValue(tryKey, out int tries);
        if (tries >= MaxVerifyTries)
        {
            ModelState.AddModelError("", "Bạn nhập sai mã quá nhiều lần. Vui lòng yêu cầu mã mới sau 15 phút.");
            return View(vm);
        }
        _cache.Set(tryKey, tries + 1, TimeSpan.FromMinutes(15));

        var user = await _um.FindByEmailAsync(vm.Email.Trim());
        var ok = user != null &&
                 await _um.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose, vm.Code.Trim());
        if (!ok)
        {
            ModelState.AddModelError(nameof(vm.Code), "Mã không đúng hoặc đã hết hạn.");
            return View(vm);
        }

        var token = await _um.GeneratePasswordResetTokenAsync(user!);
        var r = await _um.ResetPasswordAsync(user!, token, vm.NewPassword);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        _cache.Remove(tryKey);
        await _um.SetLockoutEndDateAsync(user!, null);
        await _um.ResetAccessFailedCountAsync(user!);
        _log.LogInformation("User {Email} đã đặt lại mật khẩu", user!.Email);

        TempData["Msg"] = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }
}
