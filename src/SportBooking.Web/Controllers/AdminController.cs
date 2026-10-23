using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _um;
    public AdminController(AppDbContext db, UserManager<ApplicationUser> um) { _db = db; _um = um; }

    public async Task<IActionResult> Index()
    {
        var vm = new AdminDashboardVm
        {
            Users = await _db.Users.CountAsync(),
            Courts = await _db.Courts.CountAsync(),
            Bookings = await _db.Bookings.CountAsync(),
            Revenue = await _db.Bookings.Where(b => b.Status == BookingStatus.Confirmed).SumAsync(b => (decimal?)b.TotalPrice) ?? 0
        };
        foreach (var u in await _db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync())
            vm.UserList.Add((u.Email!, u.FullName, string.Join(", ", await _um.GetRolesAsync(u))));
        return View(vm);
    }
}
