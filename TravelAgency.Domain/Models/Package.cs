namespace TravelAgency.Domain.Models;
public class Package : BaseEntity
{
    public Guid DestinationId { get; set; }
    public Destination Destination { get; set; } = default!;

    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal BasePrice { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int AvailableSeats { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
