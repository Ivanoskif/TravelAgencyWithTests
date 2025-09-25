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
using Microsoft.EntityFrameworkCore.Query;

using Xunit;

namespace TravelAgency.UnitTests.Services
{
    public class PackageServiceTests
    {
        private readonly IRepository<Package> _repo;
        private readonly IRepository<Booking> _bookings;
        private readonly IWeatherService _weather;
        private readonly IExchangeRateService _fx;
        private readonly IPublicHolidayService _holidays;

        private readonly PackageService _sut;

        public PackageServiceTests()
        {
            _repo = Substitute.For<IRepository<Package>>();
            _bookings = Substitute.For<IRepository<Booking>>();
            _weather = Substitute.For<IWeatherService>();
            _fx = Substitute.For<IExchangeRateService>();
            _holidays = Substitute.For<IPublicHolidayService>();

            _sut = new PackageService(_repo, _bookings, _weather, _fx, _holidays);
        }

        [Fact]
        public void Create_ShouldInsert()
        {
            var p = NewPackage();
            _repo.Insert(p).Returns(p);

            var result = _sut.Create(p);

            result.Should().BeSameAs(p);
            _repo.Received(1).Insert(p);
        }

        [Fact]
        public void Update_ShouldUpdate()
        {
            var p = NewPackage();
            _repo.Update(p).Returns(p);

            var result = _sut.Update(p);

            result.Should().BeSameAs(p);
            _repo.Received(1).Update(p);
        }

        [Fact]
        public void Delete_ShouldDelete()
        {
            var p = NewPackage();
            _repo.Delete(p).Returns(p);

            var result = _sut.Delete(p);

            result.Should().BeSameAs(p);
            _repo.Received(1).Delete(p);
        }

        [Fact]
        public void Get_ShouldFetchById()
        {
            var id = Guid.NewGuid();
            var p = NewPackage(id);

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(p);

            var result = _sut.Get(id);
            result.Should().Be(p);
        }

        [Fact]
        public void All_ShouldReturnAll()
        {
            var list = new List<Package> { NewPackage(), NewPackage() };
            _repo.GetAll(
                Arg.Any<Expression<Func<Package, Package>>>(),
                null, null, null
            ).Returns(list);

            var result = _sut.All().ToList();
            result.Should().HaveCount(2);
        }

        [Fact]
        public void RemainingSeats_ShouldReturnZero_WhenPackageNotFound()
        {
            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns((Package?)null);

            var result = _sut.RemainingSeats(Guid.NewGuid());

            result.Should().Be(0);
            _bookings.DidNotReceive()
                     .GetAll<int>(
                         Arg.Any<Expression<Func<Booking, int>>>(),
                         Arg.Any<Expression<Func<Booking, bool>>>(),
                         Arg.Any<Func<IQueryable<Booking>, IOrderedQueryable<Booking>>>(),
                         Arg.Any<Func<IQueryable<Booking>, IIncludableQueryable<Booking, object>>>()
                     );

        }

        [Fact]
        public void RemainingSeats_ShouldCalculateAndClampToZero()
        {
            var id = Guid.NewGuid();
            var pkg = NewPackage(id, availableSeats: 10);

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(pkg);

            var data = new List<Booking>
            {
                new() { PackageId = id, PeopleCount = 6, Status = "Confirmed" },
                new() { PackageId = id, PeopleCount = 8, Status = "Paid" },
                new() { PackageId = id, PeopleCount = 3, Status = "Cancelled" }
            };

            _bookings.GetAll<int>(
                Arg.Any<Expression<Func<Booking, int>>>(),
                Arg.Any<Expression<Func<Booking, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Booking, int>>>(0).Compile();
                var predicate = ci.ArgAt<Expression<Func<Booking, bool>>>(1).Compile();
                return data.Where(predicate).Select(selector).ToList();
            });

            var result = _sut.RemainingSeats(id);
            result.Should().Be(0);
        }

        [Fact]
        public void RemainingSeats_ShouldReturnPositive_WhenSeatsLeft()
        {
            var id = Guid.NewGuid();
            var pkg = NewPackage(id, availableSeats: 12);

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(pkg);

            var data = new List<Booking>
            {
                new() { PackageId = id, PeopleCount = 3, Status = "Confirmed" },
                new() { PackageId = id, PeopleCount = 5, Status = "Paid" }
            };

            _bookings.GetAll<int>(
                Arg.Any<Expression<Func<Booking, int>>>(),
                Arg.Any<Expression<Func<Booking, bool>>>(),
                null, null
            ).Returns(ci =>
            {
                var selector = ci.ArgAt<Expression<Func<Booking, int>>>(0).Compile();
                var predicate = ci.ArgAt<Expression<Func<Booking, bool>>>(1).Compile();
                return data.Where(predicate).Select(selector).ToList();
            });

            var result = _sut.RemainingSeats(id);
            result.Should().Be(4);
        }

        [Fact]
        public async Task GetWeatherWindowAsync_ShouldReturnNull_WhenPackageMissingOrNoDestination()
        {
            var id = Guid.NewGuid();

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns((Package?)null);

            var r1 = await _sut.GetWeatherWindowAsync(id);
            r1.Should().BeNull();

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(new Package { Destination = null! });

            var r2 = await _sut.GetWeatherWindowAsync(id);
            r2.Should().BeNull();

            await _weather.DidNotReceiveWithAnyArgs()
                          .GetWeatherWindowAsync(default, default, default, default, default);
        }

        [Fact]
        public async Task GetWeatherWindowAsync_ShouldCallWeather_AndReturnValue()
        {
            var id = Guid.NewGuid();
            var pkg = NewPackage(id);
            pkg.Destination = new Destination { Latitude = 41.9981, Longitude = 21.4254 };

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(pkg);

            var expected = new WeatherWindowDTO
            {
                StartDate = pkg.StartDate,
                EndDate = pkg.EndDate,
                AverageMaxTempC = 25,
                AverageMinTempC = 15,
                AveragePrecipitationProbability = 10,
                Recommendation = "Good window"
            };

            _weather.GetWeatherWindowAsync(pkg.Destination.Latitude, pkg.Destination.Longitude,
                                           pkg.StartDate, pkg.EndDate, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<WeatherWindowDTO?>(expected));

            var result = await _sut.GetWeatherWindowAsync(id);

            result.Should().BeSameAs(expected);
            await _weather.Received(1).GetWeatherWindowAsync(
                pkg.Destination.Latitude, pkg.Destination.Longitude,
                pkg.StartDate, pkg.EndDate, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetHolidaysAsync_ShouldReturnEmpty_WhenNoPackage_NoDestination_OrEmptyIso()
        {
            var id = Guid.NewGuid();

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns((Package?)null);

            (await _sut.GetHolidaysAsync(id)).Should().BeEmpty();

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(new Package { Destination = null! });

            (await _sut.GetHolidaysAsync(id)).Should().BeEmpty();

            var pkg = NewPackage(id);
            pkg.Destination = new Destination { IsoCode = "" };

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(pkg);

            (await _sut.GetHolidaysAsync(id)).Should().BeEmpty();

            await _holidays.DidNotReceiveWithAnyArgs()
                           .GetHolidaysInRangeAsync(default!, default, default, default);
        }

        [Fact]
        public async Task GetHolidaysAsync_ShouldAskProvider_AndReturnList()
        {
            var id = Guid.NewGuid();
            var pkg = NewPackage(id);
            pkg.Destination = new Destination { IsoCode = "MK" };

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(pkg);

            var providerList = new List<PublicHolidayDTO>
            {
                new() { Date = pkg.StartDate, Name = "Holiday1", LocalName = "Praznik1" },
                new() { Date = pkg.EndDate, Name = "Holiday2", LocalName = "Praznik2" }
            };

            _holidays.GetHolidaysInRangeAsync("MK", pkg.StartDate, pkg.EndDate, Arg.Any<CancellationToken>())
                     .Returns(Task.FromResult<IReadOnlyList<PublicHolidayDTO>>(providerList));

            var result = await _sut.GetHolidaysAsync(id);

            result.Should().BeEquivalentTo(providerList);
            await _holidays.Received(1).GetHolidaysInRangeAsync("MK", pkg.StartDate, pkg.EndDate, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetPriceQuoteAsync_ShouldReturnNull_WhenPackageNotFound()
        {
            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns((Package?)null);

            var result = await _sut.GetPriceQuoteAsync(Guid.NewGuid(), "EUR");

            result.Should().BeNull();
            await _fx.DidNotReceiveWithAnyArgs().ConvertAsync(default!, default!, default, default);
        }

        [Fact]
        public async Task GetPriceQuoteAsync_ShouldReturnIdentity_WhenFromEqualsTo()
        {
            var p = NewPackage(basePrice: 123.45m);

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(p);

            var result = await _sut.GetPriceQuoteAsync(Guid.NewGuid(), "usd");

            result.Should().NotBeNull();
            result!.FromCurrency.Should().Be("USD");
            result.ToCurrency.Should().Be("USD");
            result.Rate.Should().Be(1m);
            result.AmountBase.Should().Be(123.45m);
            result.AmountConverted.Should().Be(123.45m);
        }

        [Fact]
        public async Task GetPriceQuoteAsync_ShouldCallFx_WhenDifferentCurrency()
        {
            var p = NewPackage(basePrice: 200m);

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null,
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(p);

            var expected = new PriceQuoteDTO
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.9m,
                AmountBase = 200m,
                AmountConverted = 180m,
                TimestampUtc = DateTime.UtcNow
            };

            _fx.ConvertAsync("USD", "EUR", 200m, Arg.Any<CancellationToken>())
               .Returns(Task.FromResult<PriceQuoteDTO?>(expected));

            var result = await _sut.GetPriceQuoteAsync(Guid.NewGuid(), "EUR");

            result.Should().BeSameAs(expected);
            await _fx.Received(1).ConvertAsync("USD", "EUR", 200m, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetRemainingSeatsAsync_ShouldReturnSeatsOrZero()
        {
            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns((Package?)null);

            var z = await _sut.GetRemainingSeatsAsync(Guid.NewGuid());
            z.Should().Be(0);

            var p = NewPackage(availableSeats: 7);

            _repo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns(p);

            var n = await _sut.GetRemainingSeatsAsync(Guid.NewGuid());
            n.Should().Be(7);
        }

        private static Package NewPackage(Guid? id = null, int availableSeats = 10, decimal basePrice = 100m)
        {
            return new Package
            {
                Id = id ?? Guid.NewGuid(),
                Title = "Test",
                Description = "Desc",
                BasePrice = basePrice,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(15)),
                AvailableSeats = availableSeats,
                Destination = new Destination { IsoCode = "MK", Latitude = 41.99, Longitude = 21.43 }
            };
        }
    }
}
