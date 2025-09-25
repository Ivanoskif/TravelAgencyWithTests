using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Domain.Models;

namespace TravelAgency.Repository.Data;

public class AppDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Destination>(e =>
        {
            e.Property(x => x.CountryName).HasMaxLength(120).IsRequired();
            e.Property(x => x.City).HasMaxLength(120).IsRequired();
            e.Property(x => x.IsoCode).HasMaxLength(3).IsRequired();
            e.Property(x => x.DefaultCurrency).HasMaxLength(3).IsRequired();
            e.HasIndex(x => new { x.CountryName, x.City }).IsUnique();
        });

        b.Entity<Package>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.BasePrice).HasPrecision(18, 2);
            e.HasOne(x => x.Destination).WithMany(d => d.Packages).HasForeignKey(x => x.DestinationId);
        });


        b.Entity<Customer>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email);
        });

        b.Entity<Booking>(e =>
        {
            e.Property(x => x.TotalBasePrice).HasPrecision(18, 2);
            e.HasOne(x => x.Package).WithMany(p => p.Bookings).HasForeignKey(x => x.PackageId);
            e.HasOne(x => x.Customer).WithMany(c => c.Bookings).HasForeignKey(x => x.CustomerId);
        });
    }
}
