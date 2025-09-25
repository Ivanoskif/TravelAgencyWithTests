using System;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Implementation;
using TravelAgency.Repository.Interface;
using TravelAgency.IntegrationTests.Infrastructure;
using Xunit;

namespace TravelAgency.IntegrationTests.Repositories
{
    public class BookingRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryDb _db = default!;
        private IRepository<Booking> _repo = default!;
        private Destination _dest = default!;
        private Package _pkg = default!;
        private Customer _cust = default!;

        public async Task InitializeAsync()
        {
            _db = new SqliteInMemoryDb();
            _repo = new Repository<Booking>(_db.Context);

            _dest = new Destination
            {
                Id = Guid.NewGuid(),
                CountryName = "Greece",
                City = "Thessaloniki",
                IsoCode = "GR",
                DefaultCurrency = "EUR"
            };
            _db.Context.Destinations.Add(_dest);

            _pkg = new Package
            {
                Id = Guid.NewGuid(),
                Title = "Beach Escape",
                Description = "All inclusive 5 days",
                BasePrice = 250,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(20)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(25)),
                AvailableSeats = 15,
                DestinationId = _dest.Id
            };
            _db.Context.Packages.Add(_pkg);

            _cust = new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "Kostovska",
                Email = "ana@test.mk"
            };
            _db.Context.Customers.Add(_cust);

            await _db.Context.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _db.DisposeAsync();

        [Fact]
        public void Insert_Then_Get_WithIncludes_ShouldReturnGraph()
        {
            var b = new Booking
            {
                PackageId = _pkg.Id,
                CustomerId = _cust.Id,
                PeopleCount = 2,
                TotalBasePrice = 500m,
                Status = "Confirmed",
                CreatedAtUtc = DateTime.UtcNow
            };

            _repo.Insert(b);

            var fromDb = _repo.Get(
                selector: x => x,
                predicate: x => x.Id == b.Id,
                orderBy: null,
                include: q => q
                    .Include(x => x.Package)
                    .ThenInclude(p => p.Destination)
                    .Include(x => x.Customer)
            );

            fromDb.Should().NotBeNull();
            fromDb!.Package.Should().NotBeNull();
            fromDb.Customer.Should().NotBeNull();

            fromDb.Package!.Title.Should().Be("Beach Escape");
            fromDb.Package.Destination!.IsoCode.Should().Be("GR");
            fromDb.Customer!.Email.Should().Be("ana@test.mk");
        }

        [Fact]
        public void Delete_ShouldRemove()
        {
            var b = _repo.Insert(new Booking
            {
                PackageId = _pkg.Id,
                CustomerId = _cust.Id,
                PeopleCount = 3,
                TotalBasePrice = 750m,
                Status = "Paid",
                CreatedAtUtc = DateTime.UtcNow
            });

            _repo.Delete(b);

            _db.Context.Bookings.Any(x => x.Id == b.Id).Should().BeFalse();
        }
    }
}
