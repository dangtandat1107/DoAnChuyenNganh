using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _um;
    private readonly SignInManager<ApplicationUser> _sm;
    private readonly AppDbContext _db;

    public ProfileController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, AppDbContext db)
    { _um = um; _sm = sm; _db = db; }

    public async Task<IActionResult> Index(string? tab)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(await BuildAsync(user, tab == "password" ? "password" : tab == "upcoming" ? "upcoming" : "info"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Edit")] UpdateProfileVm vm)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
        {
            var page = await BuildAsync(user, "info");
            page.Edit = vm;
            return View("Index", page);
        }

        user.FullName = vm.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim();
        var r = await _um.UpdateAsync(user);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            var page = await BuildAsync(user, "info");
            page.Edit = vm;
            return View("Index", page);
        }

        await _sm.RefreshSignInAsync(user);   // cập nhật tên trên thanh menu
        TempData["Msg"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordVm vm)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();

        if (ModelState.IsValid)
        {
            var r = await _um.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword);
            if (r.Succeeded)
            {
                await _sm.RefreshSignInAsync(user);   // giữ đăng nhập sau khi đổi mật khẩu
                TempData["Msg"] = "Đổi mật khẩu thành công.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
        }
        return View("Index", await BuildAsync(user, "password"));
    }

    private async Task<ProfilePageVm> BuildAsync(ApplicationUser user, string tab)
    {
        var role = (await _um.GetRolesAsync(user)).FirstOrDefault() ?? "Customer";
        var vm = new ProfilePageVm
        {
            Email = user.Email ?? "", Role = role, FullName = user.FullName, ActiveTab = tab,
            Edit = new UpdateProfileVm { FullName = user.FullName, PhoneNumber = user.PhoneNumber }
        };
        var now = DateTime.Now;

        if (role == "Customer")
        {
            var mine = _db.Bookings.AsNoTracking().Where(b => b.CustomerId == user.Id && b.Status != BookingStatus.Cancelled);
            var spent = await mine.Where(b => b.Status == BookingStatus.Confirmed).SumAsync(b => (decimal?)b.TotalPrice) ?? 0;
            vm.Stats.Add(new("Tổng lượt đặt", (await mine.CountAsync()).ToString()));
            vm.Stats.Add(new("Sắp diễn ra", (await mine.CountAsync(b => b.EndTime > now)).ToString()));
            vm.Stats.Add(new("Đã chi (đã duyệt)", spent.ToString("N0") + " đ"));
            vm.Upcoming = await _db.Bookings.AsNoTracking().Include(b => b.Court)
                .Where(b => b.CustomerId == user.Id && b.Status != BookingStatus.Cancelled && b.StartTime >= now)
                .OrderBy(b => b.StartTime).Take(5).ToListAsync();
        }
        else if (role == "Owner")
        {
            vm.Stats.Add(new("Số sân của tôi", (await _db.Courts.CountAsync(c => c.OwnerId == user.Id)).ToString()));
            vm.Stats.Add(new("Lịch chờ duyệt", (await _db.Bookings.CountAsync(b => b.Court!.OwnerId == user.Id && b.Status == BookingStatus.Pending)).ToString()));
        }
        return vm;
    }
}
