using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TravelAgency.Domain.Models;
using TravelAgency.IntegrationTests.Infrastructure;
using TravelAgency.Service.Interface;
using Xunit;

namespace TravelAgency.IntegrationTests.Controllers
{
    public class PackagesControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public PackagesControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithMocks(
            out IPackageService pkg,
            out IDestinationService dest,
            string role
        )
        {
            var pkgLocal = Substitute.For<IPackageService>();
            var destLocal = Substitute.For<IDestinationService>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(IPackageService) ||
                        d.ServiceType == typeof(IDestinationService)).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddSingleton(pkgLocal);
                    services.AddSingleton(destLocal);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-User-Role", role);

            pkg = pkgLocal;
            dest = destLocal;
            return client;
        }

        // TESTS

        [Fact]
        public async Task Index_Anonymous_ShouldReturnOk_AndCallAll()
        {
            var client = CreateClientWithMocks(out var pkg, out var dest, role: "User");

            pkg.All().Returns(new[]
            {
                new Package { Id = Guid.NewGuid(), Title = "A", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date) },
                new Package { Id = Guid.NewGuid(), Title = "B", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)) },
            });

            dest.All().Returns(Array.Empty<Destination>());

            var resp = await client.GetAsync("/Packages");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            pkg.Received(1).All();
            dest.Received(1).All();
        }

        [Fact]
        public async Task Details_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMocks(out var pkg, out _, role: "User");

            pkg.Get(Arg.Any<Guid>()).Returns((Package?)null);

            var resp = await client.GetAsync($"/Packages/Details/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out var pkg, out _, role: "User");

            var id = Guid.NewGuid();
            pkg.Get(id).Returns(new Package
            {
                Id = id,
                Title = "Trip",
                BasePrice = 100,
                AvailableSeats = 5,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2))
            });

            var resp = await client.GetAsync($"/Packages/Details/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            pkg.Received(1).Get(id);
        }

        [Fact]
        public async Task Create_Get_Admin_ShouldReturnOk_AndPopulateDestinations()
        {
            var client = CreateClientWithMocks(out var pkg, out var dest, role: "Admin");

            dest.All().Returns(new[]
            {
                new Destination { Id = Guid.NewGuid(), City = "Rome", CountryName = "Italy" }
            });

            var resp = await client.GetAsync("/Packages/Create");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            dest.Received(1).All();
        }

        [Fact]
        public async Task Edit_Get_Admin_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out var pkg, out var dest, role: "Admin");

            var id = Guid.NewGuid();
            pkg.Get(id).Returns(new Package
            {
                Id = id,
                Title = "Existing",
                BasePrice = 120,
                AvailableSeats = 4,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                DestinationId = Guid.NewGuid()
            });

            dest.All().Returns(new[]
            {
                new Destination { Id = Guid.NewGuid(), City = "Paris", CountryName = "France" }
            });

            var resp = await client.GetAsync($"/Packages/Edit/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            pkg.Received(1).Get(id);
            dest.Received(1).All();
        }

        [Fact]
        public async Task Edit_Get_Admin_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMocks(out var pkg, out _, role: "Admin");

            pkg.Get(Arg.Any<Guid>()).Returns((Package?)null);

            var resp = await client.GetAsync($"/Packages/Edit/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_Get_Admin_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out var pkg, out _, role: "Admin");

            var id = Guid.NewGuid();
            pkg.Get(id).Returns(new Package
            {
                Id = id,
                Title = "Del",
                BasePrice = 90,
                AvailableSeats = 2,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1))
            });

            var resp = await client.GetAsync($"/Packages/Delete/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            pkg.Received(1).Get(id);
        }

        [Fact]
        public async Task Delete_Get_Admin_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMocks(out var pkg, out _, role: "Admin");

            pkg.Get(Arg.Any<Guid>()).Returns((Package?)null);

            var resp = await client.GetAsync($"/Packages/Delete/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
