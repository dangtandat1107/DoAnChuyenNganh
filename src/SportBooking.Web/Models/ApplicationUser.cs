using Microsoft.AspNetCore.Identity;
namespace SportBooking.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
}
