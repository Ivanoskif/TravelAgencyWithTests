using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.Models;
using TravelAgency.IntegrationTests.Infrastructure;
using TravelAgency.Repository.Interface;
using TravelAgency.Repository.Data;
using TravelAgency.Service.Interface;
using Xunit;

namespace TravelAgency.IntegrationTests.Controllers
{
    public class CartControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public CartControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithMocks(
            out IRepository<Package> packRepo,
            out IPackageService packageSvc,
            out IBookingService bookingSvc,
            out IRepository<Customer> customerRepo,
            out UserManager<ApplicationUser> userManager,
            string role = "User",
            string? email = "user@example.com")
        {
            var packRepoLocal = Substitute.For<IRepository<Package>>();
            var packageSvcLocal = Substitute.For<IPackageService>();
            var bookingSvcLocal = Substitute.For<IBookingService>();
            var customerRepoLocal = Substitute.For<IRepository<Customer>>();

            var userStore = Substitute.For<IUserStore<ApplicationUser>>();
            var idOpts = Substitute.For<IOptions<IdentityOptions>>();
            idOpts.Value.Returns(new IdentityOptions());
            var pwdHasher = Substitute.For<IPasswordHasher<ApplicationUser>>();
            var userValidators = Enumerable.Empty<IUserValidator<ApplicationUser>>();
            var pwdValidators = Enumerable.Empty<IPasswordValidator<ApplicationUser>>();
            var normalizer = Substitute.For<ILookupNormalizer>();
            var errDesc = new IdentityErrorDescriber();
            var sp = Substitute.For<IServiceProvider>();
            var logger = Substitute.For<ILogger<UserManager<ApplicationUser>>>();

            var userManagerLocal = Substitute.For<UserManager<ApplicationUser>>(
                userStore, idOpts, pwdHasher, userValidators, pwdValidators, normalizer, errDesc, sp, logger);

            userManagerLocal.GetUserAsync(Arg.Any<ClaimsPrincipal>())
                            .Returns(ci => Task.FromResult(new ApplicationUser { Email = email })!);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(IRepository<Package>) ||
                        d.ServiceType == typeof(IPackageService) ||
                        d.ServiceType == typeof(IBookingService) ||
                        d.ServiceType == typeof(IRepository<Customer>) ||
                        d.ServiceType == typeof(UserManager<ApplicationUser>)
                    ).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddSingleton(packRepoLocal);
                    services.AddSingleton(packageSvcLocal);
                    services.AddSingleton(bookingSvcLocal);
                    services.AddSingleton(customerRepoLocal);
                    services.AddSingleton(userManagerLocal);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-User-Role", role);
            if (!string.IsNullOrWhiteSpace(email))
                client.DefaultRequestHeaders.Add("X-User-Email", email);

            packRepo = packRepoLocal;
            packageSvc = packageSvcLocal;
            bookingSvc = bookingSvcLocal;
            customerRepo = customerRepoLocal;
            userManager = userManagerLocal;

            return client;
        }

        // Anti-forgery helpers
        private static readonly System.Text.RegularExpressions.Regex AntiForgeryInput =
            new(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
              | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string? ExtractRequestVerificationToken(string html)
            => AntiForgeryInput.Match(html) is var m && m.Success ? m.Groups[1].Value : null;

        private static async Task<string> GetAntiForgeryAsync(HttpClient client, string getFormPath)
        {
            var get = await client.GetAsync(getFormPath);
            get.StatusCode.Should().Be(HttpStatusCode.OK);

            var html = await get.Content.ReadAsStringAsync();
            var token = ExtractRequestVerificationToken(html);
            token.Should().NotBeNull("anti-forgery token input must be present in form");
            return token!;
        }

        private static HttpRequestMessage BuildPostForm(
            string url,
            IEnumerable<KeyValuePair<string, string>> form,
            string antiForgeryToken)
        {
            var fields = new List<KeyValuePair<string, string>>(form)
            {
                new("__RequestVerificationToken", antiForgeryToken)
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(fields)
            };
            req.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            return req;
        }

        // Index 
        [Fact]
        public async Task Index_Anonymous_EmptyCart_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out _, out _, out _, out _, out _,
                                               role: "User", email: null);

            var resp = await client.GetAsync("/Cart");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Add
        [Fact]
        public async Task Add_Post_PackageMissing_ShouldReturn404()
        {
            var client = CreateClientWithMocks(out var packRepo, out _, out _, out _, out _,
                                               role: "Admin");

            packRepo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                Arg.Any<Func<IQueryable<Package>, IOrderedQueryable<Package>>>(),
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns((Package?)null);

            var token = await GetAntiForgeryAsync(client, "/Customers/Create");

            var req = BuildPostForm("/Cart/Add", new[]
            {
                new KeyValuePair<string, string>("packageId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("people", "2")
            }, token);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Add_Post_Valid_ShouldRedirectToIndex()
        {
            var client = CreateClientWithMocks(out var packRepo, out _, out _, out _, out _,
                                               role: "Admin");

            var id = Guid.NewGuid();

            packRepo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                Arg.Any<Func<IQueryable<Package>, IOrderedQueryable<Package>>>(),
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(ci => new Package
            {
                Id = id,
                Title = "Sunny Trip",
                BasePrice = 100m,
                AvailableSeats = 50
            });

            var token = await GetAntiForgeryAsync(client, "/Customers/Create");

            var req = BuildPostForm("/Cart/Add", new[]
            {
                new KeyValuePair<string, string>("packageId", id.ToString()),
                new KeyValuePair<string, string>("people", "3")
            }, token);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Cart");
        }

        // Remove 
        [Fact]
        public async Task Remove_Post_ExistingItem_ShouldRedirectToIndex()
        {
            var client = CreateClientWithMocks(out var packRepo, out _, out _, out _, out _,
                                               role: "Admin");

            var id = Guid.NewGuid();

            packRepo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                Arg.Any<Func<IQueryable<Package>, IOrderedQueryable<Package>>>(),
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(ci => new Package
            {
                Id = id,
                Title = "Short Break",
                BasePrice = 80m,
                AvailableSeats = 10
            });

            var token1 = await GetAntiForgeryAsync(client, "/Customers/Create");
            var addReq = BuildPostForm("/Cart/Add", new[]
            {
                new KeyValuePair<string, string>("packageId", id.ToString()),
                new KeyValuePair<string, string>("people", "1")
            }, token1);
            var addResp = await client.SendAsync(addReq);
            addResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

            var token2 = await GetAntiForgeryAsync(client, "/Customers/Create");
            var remReq = BuildPostForm("/Cart/Remove", new[]
            {
                new KeyValuePair<string, string>("packageId", id.ToString())
            }, token2);

            var remResp = await client.SendAsync(remReq);
            remResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            remResp.Headers.Location!.ToString().Should().Be("/Cart");
        }

        // Checkout 
        [Fact]
        public async Task Checkout_EmptyCart_ShouldRedirectToIndex()
        {
            var client = CreateClientWithMocks(
            out var packRepo, out var packageSvc, out var bookingSvc, out var customerRepo, out _,
            role: "Admin",
            email: "admin@travel.local" 
        );

            var token = await GetAntiForgeryAsync(client, "/Customers/Create");

            var req = BuildPostForm("/Cart/Checkout", Array.Empty<KeyValuePair<string, string>>(), token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Cart");
        }

        [Fact]
        public async Task Checkout_NotEnoughSeats_ShouldRedirectToIndex_AndNotCreateBooking()
        {
            var client = CreateClientWithMocks(
            out var packRepo, out var packageSvc, out var bookingSvc, out var customerRepo, out _,
            role: "Admin",
            email: "admin@travel.local" 
        );

            var pkgId = Guid.NewGuid();

            packRepo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                Arg.Any<Func<IQueryable<Package>, IOrderedQueryable<Package>>>(),
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(ci => new Package
            {
                Id = pkgId,
                Title = "City Tour",
                BasePrice = 120m,
                AvailableSeats = 2
            });

            var token1 = await GetAntiForgeryAsync(client, "/Customers/Create");
            var addReq = BuildPostForm("/Cart/Add", new[]
            {
                new KeyValuePair<string, string>("packageId", pkgId.ToString()),
                new KeyValuePair<string, string>("people", "3")
            }, token1);
            var addResp = await client.SendAsync(addReq);
            addResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

            var cust = new Customer { Id = Guid.NewGuid(), Email = "user@example.com", FirstName = "U" };

            customerRepo.Get<Customer>(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                Arg.Any<Func<IQueryable<Customer>, IOrderedQueryable<Customer>>>(),
                Arg.Any<Func<IQueryable<Customer>, IIncludableQueryable<Customer, object>>>()
            ).Returns(cust);

            packageSvc.GetRemainingSeatsAsync(pkgId, Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(1));

            var token2 = await GetAntiForgeryAsync(client, "/Customers/Create");
            var checkoutReq = BuildPostForm("/Cart/Checkout", Array.Empty<KeyValuePair<string, string>>(), token2);
            var checkoutResp = await client.SendAsync(checkoutReq);

            checkoutResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            checkoutResp.Headers.Location!.ToString().Should().Be("/Cart");

            await bookingSvc.DidNotReceiveWithAnyArgs().CreateBookingAsync(default, default, default, default);
        }

        [Fact]
        public async Task Checkout_Success_ShouldRedirectToIndex_AndCreateBooking()
        {
            var client = CreateClientWithMocks(
                out var packRepo, out var packageSvc, out var bookingSvc, out var customerRepo, out _,
                role: "Admin",
                email: "admin@travel.local"
            ); ;

            var pkgId = Guid.NewGuid();

            packRepo.Get<Package>(
                Arg.Any<Expression<Func<Package, Package>>>(),
                Arg.Any<Expression<Func<Package, bool>>>(),
                Arg.Any<Func<IQueryable<Package>, IOrderedQueryable<Package>>>(),
                Arg.Any<Func<IQueryable<Package>, IIncludableQueryable<Package, object>>>()
            ).Returns(ci => new Package
            {
                Id = pkgId,
                Title = "Beach Escape",
                BasePrice = 200m,
                AvailableSeats = 10
            });

            var token1 = await GetAntiForgeryAsync(client, "/Customers/Create");
            var addReq = BuildPostForm("/Cart/Add", new[]
            {
                new KeyValuePair<string, string>("packageId", pkgId.ToString()),
                new KeyValuePair<string, string>("people", "2")
            }, token1);
            var addResp = await client.SendAsync(addReq);
            addResp.StatusCode.Should().Be(HttpStatusCode.Redirect);

            var cust = new Customer { Id = Guid.NewGuid(), Email = "user@example.com", FirstName = "U" };

            customerRepo.Get<Customer>(
                Arg.Any<Expression<Func<Customer, Customer>>>(),
                Arg.Any<Expression<Func<Customer, bool>>>(),
                Arg.Any<Func<IQueryable<Customer>, IOrderedQueryable<Customer>>>(),
                Arg.Any<Func<IQueryable<Customer>, IIncludableQueryable<Customer, object>>>()
            ).Returns(cust);

            packageSvc.GetRemainingSeatsAsync(pkgId, Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(5));

            bookingSvc.CreateBookingAsync(cust.Id, pkgId, 2, Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult((true, (string?)null)));

            var token2 = await GetAntiForgeryAsync(client, "/Customers/Create");
            var checkoutReq = BuildPostForm("/Cart/Checkout", Array.Empty<KeyValuePair<string, string>>(), token2);
            var checkoutResp = await client.SendAsync(checkoutReq);

            checkoutResp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            checkoutResp.Headers.Location!.ToString().Should().Be("/Cart");

            await bookingSvc.Received(1).CreateBookingAsync(cust.Id, pkgId, 2, Arg.Any<CancellationToken>());
        }
    }
}
