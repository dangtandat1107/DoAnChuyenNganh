using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
namespace SportBooking.Web.Controllers;

[Authorize]
public class BookingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IBookingService _svc;
    public BookingsController(AppDbContext db, IBookingService svc) { _db = db; _svc = svc; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int courtId, DateTime date, int hour, int hours = 1)
    {
        var r = await _svc.CreateAsync(UserId, courtId, date.Date.AddHours(hour), hours);
        if (r.Success) { TempData["Msg"] = "Đặt sân thành công, đang chờ chủ sân xác nhận."; return RedirectToAction(nameof(My)); }
        TempData["Err"] = r.Error;
        return RedirectToAction("Details", "Courts", new { id = courtId, date = date.ToString("yyyy-MM-dd") });
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> My() =>
        View(await _db.Bookings.AsNoTracking().Include(b => b.Court)
            .Where(b => b.CustomerId == UserId).OrderByDescending(b => b.StartTime).ToListAsync());

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == UserId);
        if (b == null) return NotFound();
        if (b.Status == BookingStatus.Cancelled) return RedirectToAction(nameof(My));
        if (b.StartTime < DateTime.Now.AddHours(2)) { TempData["Err"] = "Chỉ được hủy trước giờ chơi ít nhất 2 giờ."; return RedirectToAction(nameof(My)); }
        b.Status = BookingStatus.Cancelled;
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Đã hủy lịch.";
        return RedirectToAction(nameof(My));
    }

    // ---------- Chủ sân duyệt lịch ----------
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Manage()
    {
        var q = _db.Bookings.AsNoTracking().Include(b => b.Court).Include(b => b.Customer).AsQueryable();
        if (!User.IsInRole("Admin")) q = q.Where(b => b.Court!.OwnerId == UserId);
        return View(await q.OrderByDescending(b => b.StartTime).ToListAsync());
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, BookingStatus status)
    {
        var b = await _db.Bookings.Include(x => x.Court)
            .FirstOrDefaultAsync(x => x.Id == id && (User.IsInRole("Admin") || x.Court!.OwnerId == UserId));
        if (b == null) return NotFound();
        if (status is BookingStatus.Confirmed or BookingStatus.Cancelled) { b.Status = status; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Manage));
    }
}
