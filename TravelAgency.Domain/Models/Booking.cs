namespace TravelAgency.Domain.Models;
public class Booking : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public int PeopleCount { get; set; }
    public decimal TotalBasePrice { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
}
