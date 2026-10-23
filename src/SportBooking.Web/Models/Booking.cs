namespace SportBooking.Web.Models;

public class Booking
{
    public int Id { get; set; }
    public int CourtId { get; set; }
    public Court? Court { get; set; }
    public string CustomerId { get; set; } = "";
    public ApplicationUser? Customer { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
