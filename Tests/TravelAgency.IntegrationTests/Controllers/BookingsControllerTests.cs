using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.Models;
using TravelAgency.IntegrationTests.Infrastructure;
using TravelAgency.Repository.Data;
using TravelAgency.Service.Interface;
using Xunit;

namespace TravelAgency.IntegrationTests.Controllers
{
    public class BookingsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public BookingsControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // helper: client and mocks 
        private HttpClient CreateClientWithMocks(
            out IBookingService bookings,
            out IPackageService packages,
            out ICustomerService customers,
            out UserManager<ApplicationUser> userManager,
            string role
        )
        {
            var bookingsLocal = Substitute.For<IBookingService>();
            var packagesLocal = Substitute.For<IPackageService>();
            var customersLocal = Substitute.For<ICustomerService>();

            var store = Substitute.For<IUserStore<ApplicationUser>>();
            var userManagerLocal = Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null
            );

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(IBookingService) ||
                        d.ServiceType == typeof(IPackageService) ||
                        d.ServiceType == typeof(ICustomerService) ||
                        d.ServiceType == typeof(UserManager<ApplicationUser>)
                    ).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddSingleton(bookingsLocal);
                    services.AddSingleton(packagesLocal);
                    services.AddSingleton(customersLocal);
                    services.AddSingleton(userManagerLocal);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-User-Role", role);

            bookings = bookingsLocal;
            packages = packagesLocal;
            customers = customersLocal;
            userManager = userManagerLocal;
            return client;
        }

        // tests 

        [Fact]
        public async Task Index_ShouldReturnOk_AndCallAll()
        {
            var client = CreateClientWithMocks(out var bookings, out var packages, out var customers, out var userMgr, role: "User");

            bookings.All().Returns(new[]
            {
                new Booking { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow.AddHours(-2), PeopleCount = 2 },
                new Booking { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow, PeopleCount = 1 }
            });

            var resp = await client.GetAsync("/Bookings");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            bookings.Received(1).All();
        }

        [Fact]
        public async Task Details_MissingId_ShouldReturn404()
        {
            var client = CreateClientWithMocks(out var bookings, out _, out _, out _, role: "User");

            var resp = await client.GetAsync("/Bookings/Details");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMocks(out var bookings, out _, out _, out _, role: "User");

            bookings.Get(Arg.Any<Guid>()).Returns((Booking?)null);

            var unknownId = Guid.NewGuid();
            var resp = await client.GetAsync($"/Bookings/Details/{unknownId}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out var bookings, out _, out _, out _, role: "User");

            var id = Guid.NewGuid();
            bookings.Get(id).Returns(new Booking
            {
                Id = id,
                PackageId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                PeopleCount = 3,
                TotalBasePrice = 300m,
                CreatedAtUtc = DateTime.UtcNow
            });

            var resp = await client.GetAsync($"/Bookings/Details/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            bookings.Received(1).Get(id);
        }

        [Fact]
        public async Task UserBookings_NoEmailClaim_UsesUserManager_ThenReturnsOk()
        {
            var client = CreateClientWithMocks(out var bookings, out _, out _, out var userMgr, role: "User");

            var user = new ApplicationUser { Email = "buyer@example.com" };
            userMgr.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
            userMgr.GetEmailAsync(user).Returns("buyer@example.com");

            bookings.GetByCustomerEmailAsync("buyer@example.com", Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult((IReadOnlyList<Booking>)new[]
                    {
                        new Booking { Id = Guid.NewGuid(), PeopleCount = 2, TotalBasePrice = 250m },
                    }));

            var resp = await client.GetAsync("/Bookings/UserBookings");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            await bookings.Received(1).GetByCustomerEmailAsync("buyer@example.com", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task UserBookings_EmailFromUserManager_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out var bookings, out _, out _, out var userMgr, role: "User");

            var user = new ApplicationUser { Email = "alice@example.com" };
            userMgr.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
            userMgr.GetEmailAsync(user).Returns("alice@example.com");

            bookings.GetByCustomerEmailAsync("alice@example.com", Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult((IReadOnlyList<Booking>)new[]
                    {
                        new Booking { Id = Guid.NewGuid(), PeopleCount = 1, TotalBasePrice = 120m },
                    }));

            var resp = await client.GetAsync("/Bookings/UserBookings");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            await bookings.Received(1).GetByCustomerEmailAsync("alice@example.com", Arg.Any<CancellationToken>());
        }
    }
}
