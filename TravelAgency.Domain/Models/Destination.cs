namespace TravelAgency.Domain.Models;
public class Destination : BaseEntity
{
    public string CountryName { get; set; } = default!;
    public string City { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string IsoCode { get; set; } = default!;
    public string DefaultCurrency { get; set; } = "EUR";

    public ICollection<Package> Packages { get; set; } = new List<Package>();
}
