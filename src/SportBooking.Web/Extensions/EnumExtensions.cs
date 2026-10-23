using System.ComponentModel.DataAnnotations;
using System.Reflection;
namespace SportBooking.Web.Extensions;

public static class EnumExtensions
{
    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.Name ?? value.ToString();
    }
}
