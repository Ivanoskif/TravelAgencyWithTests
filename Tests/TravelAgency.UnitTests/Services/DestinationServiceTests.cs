using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Interface;
using TravelAgency.Service.Implementation;
using TravelAgency.Service.Interface;
using Xunit;

namespace TravelAgency.UnitTests.Services
{
    public class DestinationServiceTests
    {
        private readonly IRepository<Destination> _repo;
        private readonly IExternalCountryService _countries;
        private readonly DestinationService _sut;

        public DestinationServiceTests()
        {
            _repo = Substitute.For<IRepository<Destination>>();
            _countries = Substitute.For<IExternalCountryService>();
            _sut = new DestinationService(_repo, _countries);
        }

        [Fact]
        public void Create_ShouldInsertEntity()
        {
            var d = new Destination
            {
                Id = Guid.NewGuid(),
                CountryName = "Italy",
                City = "Rome",
                IsoCode = "IT",
                DefaultCurrency = "EUR"
            };

            _repo.Insert(d).Returns(d);

            var result = _sut.Create(d);

            result.Should().BeSameAs(d);
            _repo.Received(1).Insert(d);
        }

        [Fact]
        public void Update_ShouldCallRepoUpdate()
        {
            var d = new Destination
            {
                Id = Guid.NewGuid(),
                CountryName = "France",
                City = "Paris",
                IsoCode = "FR",
                DefaultCurrency = "EUR"
            };

            _repo.Update(d).Returns(d);

            var result = _sut.Update(d);

            result.Should().BeSameAs(d);
            _repo.Received(1).Update(d);
        }

        [Fact]
        public void Delete_ShouldCallRepoDelete()
        {
            var d = new Destination
            {
                Id = Guid.NewGuid(),
                CountryName = "Spain",
                City = "Madrid",
                IsoCode = "ES",
                DefaultCurrency = "EUR"
            };

            _repo.Delete(d).Returns(d);

            var result = _sut.Delete(d);

            result.Should().BeSameAs(d);
            _repo.Received(1).Delete(d);
        }

        [Fact]
        public void Get_ShouldFetchById()
        {
            var id = Guid.NewGuid();
            var d = new Destination { Id = id, CountryName = "Italy", City = "Rome", IsoCode = "IT", DefaultCurrency = "EUR" };

            _repo.Get<Destination>(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(d);

            var result = _sut.Get(id);

            result.Should().Be(d);
        }

        [Fact]
        public void All_ShouldReturnAll()
        {
            var list = new List<Destination>
            {
                new() { Id = Guid.NewGuid(), CountryName = "Italy", City = "Rome",  IsoCode = "IT", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "France", City = "Paris", IsoCode = "FR", DefaultCurrency = "EUR" }
            };

            _repo.GetAll(Arg.Any<Expression<Func<Destination, Destination>>>(), null, null, null)
                 .Returns(list);

            var result = _sut.All().ToList();

            result.Should().HaveCount(2);
            result.Select(x => x.City).Should().Contain(new[] { "Rome", "Paris" });
        }

        [Fact]
        public void Find_WithNoFilters_ShouldReturnAll()
        {
            var data = new List<Destination>
            {
                new() { Id = Guid.NewGuid(), CountryName = "Italy",  City = "Rome",   IsoCode = "IT", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "France", City = "Paris",  IsoCode = "FR", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "Italy",  City = "Milan",  IsoCode = "IT", DefaultCurrency = "EUR" },
            };

            _repo.GetAll(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Destination, Destination>>>(0).Compile();
                var predicate = ci.ArgAt<Expression<Func<Destination, bool>>>(1).Compile();
                return data.Where(predicate).Select(selector).ToList();
            });

            var result = _sut.Find(null, null).ToList();

            result.Should().BeEquivalentTo(data);
        }

        [Fact]
        public void Find_ByCountry_ShouldFilterExactMatch_CaseSensitive()
        {
            var data = new List<Destination>
            {
                new() { Id = Guid.NewGuid(), CountryName = "Italy", City = "Rome",  IsoCode = "IT", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "Italy", City = "Milan", IsoCode = "IT", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "France", City = "Paris", IsoCode = "FR", DefaultCurrency = "EUR" }
            };

            _repo.GetAll(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Destination, Destination>>>(0).Compile();
                var predicate = ci.ArgAt<Expression<Func<Destination, bool>>>(1).Compile();
                return data.Where(predicate).Select(selector).ToList();
            });

            _sut.Find(country: "Italy").Should().HaveCount(2);
            _sut.Find(country: "ITALY").Should().BeEmpty();
        }

        [Fact]
        public void Find_ByCity_ShouldFilterExactMatch_CaseSensitive()
        {
            var data = new List<Destination>
            {
                new() { Id = Guid.NewGuid(), CountryName = "Italy", City = "Rome",  IsoCode = "IT", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "Italy", City = "Milan", IsoCode = "IT", DefaultCurrency = "EUR" },
            };

            _repo.GetAll(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Destination, Destination>>>(0).Compile();
                var predicate = ci.ArgAt<Expression<Func<Destination, bool>>>(1).Compile();
                return data.Where(predicate).Select(selector).ToList();
            });

            _sut.Find(city: "Rome").Should().HaveCount(1);
            _sut.Find(city: "ROME").Should().BeEmpty();
        }

        [Fact]
        public void GetByCityCountry_ShouldReturnExactMatch()
        {
            var rome = new Destination { Id = Guid.NewGuid(), CountryName = "Italy", City = "Rome", IsoCode = "IT", DefaultCurrency = "EUR" };

            _repo.Get<Destination>(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var predicate = ci.ArgAt<Expression<Func<Destination, bool>>>(1).Compile();
                return predicate(rome) ? rome : null;
            });

            _sut.GetByCityCountry("Rome", "Italy").Should().Be(rome);
            _sut.GetByCityCountry("ROME", "Italy").Should().BeNull();
        }

        [Fact]
        public async Task GetCountrySnapshotAsync_ShouldReturnNull_WhenDestinationNotFound()
        {
            _repo.Get<Destination>(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns((Destination?)null);

            var result = await _sut.GetCountrySnapshotAsync(Guid.NewGuid());

            result.Should().BeNull();
            await _countries.DidNotReceiveWithAnyArgs().GetCountrySnapshotAsync(default!, default);
        }

        [Fact]
        public async Task GetCountrySnapshotAsync_ShouldAskExternalService_ForCountryName()
        {
            var dest = new Destination
            {
                Id = Guid.NewGuid(),
                CountryName = "Italy",
                City = "Rome",
                IsoCode = "IT",
                DefaultCurrency = "EUR"
            };

            _repo.Get<Destination>(
                Arg.Any<Expression<Func<Destination, Destination>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(dest);

            var snapshot = new CountrySnapshotDTO();
            _countries.GetCountrySnapshotAsync("Italy", Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult<CountrySnapshotDTO?>(snapshot));

            var result = await _sut.GetCountrySnapshotAsync(dest.Id);

            result.Should().BeSameAs(snapshot);
            await _countries.Received(1).GetCountrySnapshotAsync("Italy", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ImportCountriesAsync_ShouldInsert_UpToFive_NewCountries()
        {
            var imports = Enumerable.Range(1, 6).Select(i => new CountryImport
            {
                Name = $"Country{i}",
                IsoCode = $"C{i:00}",
                Capital = $"Capital{i}",
                CurrencyCode = "EUR",
                Latitude = 1.1 * i,
                Longitude = 2.2 * i
            }).ToList();

            _repo.GetAll(
                Arg.Any<Expression<Func<Destination, string>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                return new List<string>();
            });

            _countries.GetAllForImportAsync(Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult<IEnumerable<CountryImport>>(imports));

            var count = await _sut.ImportCountriesAsync();

            count.Should().Be(5);
            _repo.Received(5).Insert(Arg.Is<Destination>(d =>
                d.CountryName.StartsWith("Country") &&
                d.City.StartsWith("Capital") &&
                d.IsoCode.StartsWith("C") &&
                d.DefaultCurrency == "EUR"
            ));
        }

        [Fact]
        public async Task ImportCountriesAsync_ShouldSkip_ExistingIsoCodes()
        {
            var imports = new List<CountryImport>
            {
                new() { Name = "Italy",  IsoCode = "IT", Capital = "Rome",    CurrencyCode = "EUR" },
                new() { Name = "France", IsoCode = "FR", Capital = "Paris",   CurrencyCode = "EUR" },
                new() { Name = "Spain",  IsoCode = "ES", Capital = "Madrid",  CurrencyCode = "EUR" },
            };

            var existing = new List<Destination>
            {
                new() { Id = Guid.NewGuid(), CountryName = "Italy",  City = "Rome",  IsoCode = "IT", DefaultCurrency = "EUR" },
                new() { Id = Guid.NewGuid(), CountryName = "France", City = "Paris", IsoCode = "FR", DefaultCurrency = "EUR" }
            };

            _repo.GetAll(
                Arg.Any<Expression<Func<Destination, string>>>(),   
                Arg.Any<Expression<Func<Destination, bool>>>(),      
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Destination, string>>>(0).Compile();
                var predicate = ci.ArgAt<Expression<Func<Destination, bool>>>(1).Compile();
                return existing.Where(predicate).Select(selector).ToList();
            });

            _countries.GetAllForImportAsync(Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult<IEnumerable<CountryImport>>(imports));

            var count = await _sut.ImportCountriesAsync();

            count.Should().Be(1);
            _repo.Received(1).Insert(Arg.Is<Destination>(d => d.IsoCode == "ES"));
            _repo.DidNotReceive().Insert(Arg.Is<Destination>(d => d.IsoCode == "IT" || d.IsoCode == "FR"));
        }

        [Fact]
        public async Task ImportCountriesAsync_ShouldDefaultCurrency_ToUSD_WhenMissing_AndCityToNA_WhenNull()
        {
            var imports = new List<CountryImport>
            {
                new() { Name = "Xland", IsoCode = "XL", Capital = null, CurrencyCode = "", Latitude = 0, Longitude = 0 }
            };

            _repo.GetAll(
                Arg.Any<Expression<Func<Destination, string>>>(),
                Arg.Any<Expression<Func<Destination, bool>>>(),
                null, null
            ).Returns(ci => new List<string>());

            _countries.GetAllForImportAsync(Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult<IEnumerable<CountryImport>>(imports));

            var count = await _sut.ImportCountriesAsync();

            count.Should().Be(1);
            _repo.Received(1).Insert(Arg.Is<Destination>(d =>
                d.CountryName == "Xland" &&
                d.City == "N/A" &&
                d.IsoCode == "XL" &&
                d.DefaultCurrency == "USD"
            ));
        }

    }


}
