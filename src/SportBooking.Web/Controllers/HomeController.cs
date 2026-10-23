using Microsoft.AspNetCore.Mvc;
namespace SportBooking.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
