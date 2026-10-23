using System.ComponentModel.DataAnnotations;
namespace SportBooking.Web.Models;

public enum SportType
{
    [Display(Name = "Cầu lông")] Badminton,
    [Display(Name = "Bóng đá")] Football,
    [Display(Name = "Tennis")] Tennis
}

public enum CourtStatus
{
    [Display(Name = "Hoạt động")] Active,
    [Display(Name = "Bảo trì")] Maintenance
}

public enum BookingStatus
{
    [Display(Name = "Chờ xác nhận")] Pending,
    [Display(Name = "Đã xác nhận")] Confirmed,
    [Display(Name = "Đã hủy")] Cancelled
}
