using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

// Thêm claim "FullName" vào cookie để thanh menu hiện tên mà không cần truy vấn DB mỗi trang
public class AppClaimsFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsFactory(UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm, IOptions<IdentityOptions> opt)
        : base(um, rm, opt) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim("FullName", user.FullName ?? ""));
        return id;
    }
}
