using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Implementation;
using TravelAgency.Repository.Interface;
using TravelAgency.IntegrationTests.Infrastructure;
using Xunit;

namespace TravelAgency.IntegrationTests.Repositories
{
    public class PackageRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryDb _db = default!;
        private IRepository<Package> _repo = default!;

        public async Task InitializeAsync()
        {
            _db = new SqliteInMemoryDb();
            _repo = new Repository<Package>(_db.Context);

            var dest = new Destination
            {
                Id = Guid.NewGuid(),
                CountryName = "Italy",
                City = "Rome",
                IsoCode = "IT",
                DefaultCurrency = "EUR"
            };
            _db.Context.Destinations.Add(dest);
            await _db.Context.SaveChangesAsync();
        }

        public async Task DisposeAsync() => await _db.DisposeAsync();

        [Fact]
        public void Insert_Then_GetById_ShouldReturnEntity()
        {
            var dest = _db.Context.Destinations.First();
            var pkg = new Package
            {
                Title = "City Break",
                Description = "Test description",
                BasePrice = 100m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(33)),
                AvailableSeats = 10,
                DestinationId = dest.Id
            };

            _repo.Insert(pkg);

            var fromDb = _repo.Get(p => p, p => p.Id == pkg.Id,
                include: q => q.Include(x => x.Destination));

            fromDb.Should().NotBeNull();
            fromDb!.Destination.Should().NotBeNull();
            fromDb.Destination!.City.Should().Be("Rome");
            fromDb.Title.Should().Be("City Break");
            fromDb.AvailableSeats.Should().Be(10);
            fromDb.StartDate.Should().BeOnOrAfter(pkg.StartDate);
            fromDb.EndDate.Should().BeOnOrAfter(pkg.EndDate);
        }

        [Fact]
        public void Update_ShouldPersistChanges()
        {
            var dest = _db.Context.Destinations.First();
            var pkg = new Package
            {
                Title = "Weekend",
                Description = "Test description",
                BasePrice = 120m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(12)),
                AvailableSeats = 5,
                DestinationId = dest.Id
            };
            _repo.Insert(pkg);

            pkg.Title = "Weekend Plus";
            pkg.AvailableSeats = 4;
            _repo.Update(pkg);

            var reloaded = _db.Context.Packages.Single(p => p.Id == pkg.Id);
            reloaded.Title.Should().Be("Weekend Plus");
            reloaded.AvailableSeats.Should().Be(4);
        }

        [Fact]
        public void Delete_ShouldRemoveRow()
        {
            var dest = _db.Context.Destinations.First();
            var pkg = new Package
            {
                Title = "DeleteMe",
                Description = "Test description",
                BasePrice = 90m,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
                AvailableSeats = 2,
                DestinationId = dest.Id
            };
            _repo.Insert(pkg);

            _repo.Delete(pkg);

            var exists = _db.Context.Packages.Any(p => p.Id == pkg.Id);
            exists.Should().BeFalse();
        }
    }
}
