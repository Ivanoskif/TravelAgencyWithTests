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
    public class DestinationRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryDb _db = default!;
        private IRepository<Destination> _repo = default!;

        public async Task InitializeAsync()
        {
            _db = new SqliteInMemoryDb();
            _repo = new Repository<Destination>(_db.Context);
            await Task.CompletedTask;
        }

        public async Task DisposeAsync() => await _db.DisposeAsync();

        private static Destination NewDestination() => new Destination
        {
            CountryName = "Spain",
            City = "Barcelona",
            IsoCode = "ES",
            DefaultCurrency = "EUR",
            Latitude = 41.3851,
            Longitude = 2.1734
        };

        [Fact]
        public void Insert_And_Get_ShouldWork()
        {
            var d = NewDestination();

            var inserted = _repo.Insert(d);
            inserted.Id.Should().NotBeEmpty();

            var fromDb = _repo.Get(x => x, x => x.Id == inserted.Id);
            fromDb.Should().NotBeNull();
            fromDb!.City.Should().Be("Barcelona");
        }

        [Fact]
        public void Update_ShouldPersist()
        {
            var d = _repo.Insert(NewDestination());
            d.City = "Madrid";

            _repo.Update(d);

            var reloaded = _db.Context.Destinations.Single(x => x.Id == d.Id);
            reloaded.City.Should().Be("Madrid");
        }

        [Fact]
        public void Delete_ShouldRemove()
        {
            var d = _repo.Insert(NewDestination());

            _repo.Delete(d);

            _db.Context.Destinations.Any(x => x.Id == d.Id).Should().BeFalse();
        }

        [Fact]
        public void GetAll_WithOrderAndProjection_ShouldReturnOrderedCities()
        {
            _repo.Insert(new Destination { CountryName = "Italy", City = "Rome", IsoCode = "IT", DefaultCurrency = "EUR" });
            _repo.Insert(new Destination { CountryName = "Italy", City = "Milan", IsoCode = "IT", DefaultCurrency = "EUR" });
            _repo.Insert(new Destination { CountryName = "Italy", City = "Florence", IsoCode = "IT", DefaultCurrency = "EUR" });

            var cities = _repo.GetAll(
                selector: d => d.City,
                predicate: d => d.CountryName == "Italy",
                orderBy: q => q.OrderBy(d => d.City),
                include: null
            ).ToList();

            cities.Should().ContainInOrder("Florence", "Milan", "Rome");
        }
    }
}
