using System;
using System.Collections.Generic;
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
    public class BookingServiceTests
    {
        private readonly IRepository<Booking> _repo;
        private readonly IRepository<Package> _packages;
        private readonly IExchangeRateService _fx;
        private readonly BookingService _sut;

        public BookingServiceTests()
        {
            _repo = Substitute.For<IRepository<Booking>>();
            _packages = Substitute.For<IRepository<Package>>();
            _fx = Substitute.For<IExchangeRateService>();

            _sut = new BookingService(_repo, _packages, _fx);
        }

        [Fact]
        public void Create_ShouldInsertBooking()
        {
            var booking = new Booking { Id = Guid.NewGuid() };
            _repo.Insert(booking).Returns(booking);

            var result = _sut.Create(booking);

            result.Should().BeSameAs(booking);
            _repo.Received(1).Insert(booking);
        }

        [Fact]
        public async Task ConvertTotalAsync_ShouldReturnNull_WhenBookingNotFound()
        {
            _repo.Get<Booking>(
                Arg.Any<Expression<Func<Booking, Booking>>>(),
                Arg.Any<Expression<Func<Booking, bool>>>(),
                null, null
            ).Returns((Booking?)null);

            var result = await _sut.ConvertTotalAsync(Guid.NewGuid(), "USD");

            result.Should().BeNull();
        }

        [Fact]
        public async Task ConvertTotalAsync_ShouldCallExchangeRateService()
        {
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, PackageId = Guid.NewGuid(), TotalBasePrice = 100 };
            var package = new Package
            {
                Id = booking.PackageId,
                Destination = new Destination { DefaultCurrency = "EUR" }
            };

            _repo.Get<Booking>(
                Arg.Any<Expression<Func<Booking, Booking>>>(),
                Arg.Any<Expression<Func<Booking, bool>>>(),
                null, null
            ).Returns(booking);

            _packages.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns(package);

            var expectedQuote = new PriceQuoteDTO("EUR", "USD", 1.1m, 100, 110, DateTime.UtcNow);

            _fx.ConvertAsync("EUR", "USD", 100, Arg.Any<CancellationToken>())
               .Returns(_ => Task.FromResult<PriceQuoteDTO?>(expectedQuote));

            var result = await _sut.ConvertTotalAsync(bookingId, "USD");

            result.Should().NotBeNull();
            result!.FromCurrency.Should().Be("EUR");
            result.ToCurrency.Should().Be("USD");
            result.AmountConverted.Should().Be(110);
        }

        [Fact]
        public async Task CountBookedSeatsAsync_ShouldSumPeopleCount()
        {
            _repo.GetAll<int>(
                Arg.Any<Expression<Func<Booking, int>>>(),
                Arg.Any<Expression<Func<Booking, bool>>>(),
                null, null
            ).Returns(new List<int> { 2, 3 });

            var result = await _sut.CountBookedSeatsAsync(Guid.NewGuid());

            result.Should().Be(5);
        }

        [Fact]
        public async Task CreateBookingAsync_ShouldFail_WhenPeopleCountIsZero()
        {
            var (ok, error) = await _sut.CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid(), 0);

            ok.Should().BeFalse();
            error.Should().Contain("PeopleCount");
        }

        [Fact]
        public async Task CreateBookingAsync_ShouldFail_WhenPackageNotFound()
        {
            _packages.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns((Package?)null);

            var (ok, error) = await _sut.CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid(), 2);

            ok.Should().BeFalse();
            error.Should().Contain("Package not found");
        }

        [Fact]
        public async Task CreateBookingAsync_ShouldFail_WhenNotEnoughSeats()
        {
            var package = new Package { Id = Guid.NewGuid(), BasePrice = 100, AvailableSeats = 1 };
            _packages.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns(package);

            var (ok, error) = await _sut.CreateBookingAsync(Guid.NewGuid(), package.Id, 2);

            ok.Should().BeFalse();
            error.Should().Contain("Not enough seats");
        }

        [Fact]
        public async Task CreateBookingAsync_ShouldCreateBookingAndUpdatePackage()
        {
            var pkg = new Package { Id = Guid.NewGuid(), BasePrice = 100, AvailableSeats = 10 };
            _packages.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                null, null
            ).Returns(pkg);

            var (ok, error) = await _sut.CreateBookingAsync(Guid.NewGuid(), pkg.Id, 2);

            ok.Should().BeTrue();
            error.Should().BeNull();

            _repo.Received(1).Insert(Arg.Is<Booking>(b => b.TotalBasePrice == 200 && b.PeopleCount == 2));
            _packages.Received(1).Update(Arg.Is<Package>(p => p.AvailableSeats == 8));
        }
    }
}
