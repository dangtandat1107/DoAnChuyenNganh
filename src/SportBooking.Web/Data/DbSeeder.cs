using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Models;
namespace SportBooking.Web.Data;

public static class DbSeeder
{
    public static readonly string[] Roles = { "Admin", "Owner", "Customer" };

    public static async Task InitAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILogger<AppDbContext>>();

        // SQL Server trong Docker khởi động chậm -> thử lại tối đa 10 lần
        for (var i = 1; ; i++)
        {
            try { await db.Database.MigrateAsync(); break; }
            catch (Exception ex) when (i < 10)
            {
                log.LogWarning("DB chưa sẵn sàng (lần {Try}): {Msg}", i, ex.Message);
                await Task.Delay(3000);
            }
        }

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles)
            if (!await roleMgr.RoleExistsAsync(r)) await roleMgr.CreateAsync(new IdentityRole(r));

        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUser(userMgr, "admin@demo.com", "Quản trị viên", "Admin@123", "Admin");
        var owner = await EnsureUser(userMgr, "owner@demo.com", "Chủ sân Demo", "Owner@123", "Owner");
        await EnsureUser(userMgr, "customer@demo.com", "Khách Demo", "Customer@123", "Customer");

        if (!await db.Courts.AnyAsync())
        {
            db.Courts.AddRange(
                new Court { Name = "Sân cầu lông A1", SportType = SportType.Badminton, District = "Quận 1", Address = "12 Nguyễn Huệ", PricePerHour = 80000, OwnerId = owner.Id },
                new Court { Name = "Sân cầu lông A2", SportType = SportType.Badminton, District = "Quận 1", Address = "12 Nguyễn Huệ", PricePerHour = 80000, OwnerId = owner.Id },
                new Court { Name = "Sân bóng đá mini B1", SportType = SportType.Football, District = "Quận 7", Address = "45 Nguyễn Thị Thập", PricePerHour = 350000, OwnerId = owner.Id },
                new Court { Name = "Sân tennis C1", SportType = SportType.Tennis, District = "Quận 3", Address = "88 Võ Văn Tần", PricePerHour = 200000, OwnerId = owner.Id });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUser(UserManager<ApplicationUser> um, string email, string name, string pwd, string role)
    {
        var u = await um.FindByEmailAsync(email);
        if (u != null) return u;
        u = new ApplicationUser { UserName = email, Email = email, FullName = name, EmailConfirmed = true };
        var r = await um.CreateAsync(u, pwd);
        if (!r.Succeeded) throw new Exception(string.Join("; ", r.Errors.Select(e => e.Description)));
        await um.AddToRoleAsync(u, role);
        return u;
    }
}
