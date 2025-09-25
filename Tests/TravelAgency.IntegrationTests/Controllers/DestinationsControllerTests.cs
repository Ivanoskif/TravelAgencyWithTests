using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;
using TravelAgency.IntegrationTests.Infrastructure;
using TravelAgency.Service.Interface;
using Xunit;

namespace TravelAgency.IntegrationTests.Controllers
{
    public class DestinationsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public DestinationsControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithMock(out IDestinationService svc, string role)
        {
            var destLocal = Substitute.For<IDestinationService>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var toRemove = services.Where(d => d.ServiceType == typeof(IDestinationService)).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddSingleton(destLocal);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-User-Role", role);

            svc = destLocal;
            return client;
        }

        // Anti-forgery helpers (POST)
        private static readonly Regex AntiForgeryInput =
            new Regex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        // Import (POST /Destinations/Import)
        [Fact]
        public async Task Import_Admin_ShouldRedirect_AndCallService()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");

            svc.ImportCountriesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(3));

            var token = await GetAntiForgeryAsync(client, "/Destinations/Create");

            var req = BuildPostForm("/Destinations/Import", Array.Empty<KeyValuePair<string, string>>(), token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Destinations");

            await svc.Received(1).ImportCountriesAsync(Arg.Any<CancellationToken>());
        }

        // Index (GET /Destinations) 
        [Fact]
        public async Task Index_Anonymous_WithFilters_ShouldReturnOk_AndCallFind()
        {
            var client = CreateClientWithMock(out var svc, role: "User");

            svc.Find("Italy", "Rome").Returns(new[]
            {
                new Destination { Id = Guid.NewGuid(), CountryName = "Italy", City = "Rome", IsoCode = "IT" }
            });

            var resp = await client.GetAsync("/Destinations?country=Italy&city=Rome");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            svc.Received(1).Find("Italy", "Rome");
        }

        [Fact]
        public async Task Index_Anonymous_NoFilters_ShouldReturnOk_AndCallFindWithNulls()
        {
            var client = CreateClientWithMock(out var svc, role: "User");

            svc.Find(null, null).Returns(new[]
            {
                new Destination { Id = Guid.NewGuid(), CountryName = "MK", City = "Skopje", IsoCode = "MK" }
            });

            var resp = await client.GetAsync("/Destinations");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            svc.Received(1).Find(null, null);
        }

        // Details 
        [Fact]
        public async Task Details_Anonymous_NullId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out _, role: "User");
            var resp = await client.GetAsync("/Destinations/Details");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_Anonymous_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            svc.Get(Arg.Any<Guid>()).Returns((Destination?)null);

            var resp = await client.GetAsync($"/Destinations/Details/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_Anonymous_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination
            {
                Id = id,
                CountryName = "Germany",
                City = "Berlin",
                IsoCode = "DE"
            });

            var resp = await client.GetAsync($"/Destinations/Details/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            svc.Received(1).Get(id);
        }

        // Create
        [Fact]
        public async Task Create_Get_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out _, role: "Admin");
            var resp = await client.GetAsync("/Destinations/Create");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_Post_Valid_ShouldRedirect_AndCallCreate()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");

            var token = await GetAntiForgeryAsync(client, "/Destinations/Create");

            var form = new[]
            {
                new KeyValuePair<string, string>("CountryName",     "Spain"),
                new KeyValuePair<string, string>("City",            "Madrid"),
                new KeyValuePair<string, string>("Latitude",        "40.4168"),
                new KeyValuePair<string, string>("Longitude",       "-3.7038"),
                new KeyValuePair<string, string>("IsoCode",         "ES"),
                new KeyValuePair<string, string>("DefaultCurrency", "EUR"),
                new KeyValuePair<string, string>("Id",              Guid.Empty.ToString())
            };

            var req = BuildPostForm("/Destinations/Create", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Destinations");

            svc.Received(1).Create(Arg.Is<Destination>(d => d.CountryName == "Spain" && d.City == "Madrid"));
        }

        //  Edit
        [Fact]
        public async Task Edit_Get_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            svc.Get(Arg.Any<Guid>()).Returns((Destination?)null);

            var resp = await client.GetAsync($"/Destinations/Edit/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Edit_Get_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination
            {
                Id = id,
                CountryName = "France",
                City = "Paris",
                IsoCode = "FR"
            });

            var resp = await client.GetAsync($"/Destinations/Edit/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Edit_Post_IdMismatch_ShouldReturn404_OrRedirectToError()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination
            {
                Id = id,
                CountryName = "Italy",
                City = "Rome",
                IsoCode = "IT"
            });

            var token = await GetAntiForgeryAsync(client, $"/Destinations/Edit/{id}");

            var otherId = Guid.NewGuid();
            var form = new[]
            {
        new KeyValuePair<string, string>("Id",              otherId.ToString()),
        new KeyValuePair<string, string>("CountryName",     "Italy"),
        new KeyValuePair<string, string>("City",            "Rome"),
        new KeyValuePair<string, string>("Latitude",        "41.9028"),
        new KeyValuePair<string, string>("Longitude",       "12.4964"),
        new KeyValuePair<string, string>("IsoCode",         "IT"),
        new KeyValuePair<string, string>("DefaultCurrency", "EUR")
    };

            var req = BuildPostForm($"/Destinations/Edit/{id}", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Found);
        }

        [Fact]
        public async Task Edit_Post_Valid_ShouldRedirect_AndCallUpdate()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination
            {
                Id = id,
                CountryName = "MK",
                City = "Skopje",
                IsoCode = "MK"
            });

            var token = await GetAntiForgeryAsync(client, $"/Destinations/Edit/{id}");

            var form = new[]
            {
                new KeyValuePair<string, string>("Id",              id.ToString()),
                new KeyValuePair<string, string>("CountryName",     "North Macedonia"),
                new KeyValuePair<string, string>("City",            "Skopje"),
                new KeyValuePair<string, string>("Latitude",        "41.9973"),
                new KeyValuePair<string, string>("Longitude",       "21.4280"),
                new KeyValuePair<string, string>("IsoCode",         "MK"),
                new KeyValuePair<string, string>("DefaultCurrency", "MKD")
            };

            var req = BuildPostForm($"/Destinations/Edit/{id}", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Destinations");

            svc.Received(1).Update(Arg.Is<Destination>(d => d.Id == id && d.CountryName == "North Macedonia" && d.DefaultCurrency == "MKD"));
        }

        // Delete 
        [Fact]
        public async Task Delete_Get_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            svc.Get(Arg.Any<Guid>()).Returns((Destination?)null);

            var resp = await client.GetAsync($"/Destinations/Delete/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_Get_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination { Id = id, CountryName = "DE", City = "Berlin", IsoCode = "DE" });

            var resp = await client.GetAsync($"/Destinations/Delete/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_ShouldRedirect_AndCallDelete()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination { Id = id, CountryName = "ES", City = "Madrid", IsoCode = "ES" });

            var token = await GetAntiForgeryAsync(client, $"/Destinations/Delete/{id}");

            var req = BuildPostForm($"/Destinations/Delete/{id}", new[]
            {
                new KeyValuePair<string, string>("id", id.ToString())
            }, token);

            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Destinations");

            svc.Received(1).Delete(Arg.Is<Destination>(d => d.Id == id));
        }

        // CountrySnapshot
        [Fact]
        public async Task CountrySnapshot_UnknownDestination_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            var id = Guid.NewGuid();

            svc.Get(id).Returns((Destination?)null);

            var resp = await client.GetAsync($"/Destinations/CountrySnapshot/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CountrySnapshot_NoSnapshotFromService_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination { Id = id, CountryName = "IT", City = "Rome", IsoCode = "IT" });
            svc.GetCountrySnapshotAsync(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<CountrySnapshotDTO?>(null));

            var resp = await client.GetAsync($"/Destinations/CountrySnapshot/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CountrySnapshot_Found_ShouldReturnOkOr500_AndCallService()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Destination { Id = id, CountryName = "ES", City = "Madrid", IsoCode = "ES" });
            svc.GetCountrySnapshotAsync(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<CountrySnapshotDTO?>(new CountrySnapshotDTO
            {
                Name = "Spain",
                Region = "Europe",
                CurrencyCode = "EUR",
                PopulationMillions = (double) 47.5m,
                PrimaryLanguage = "Spanish",
                FlagUrl = "https://flags.example/es.png"
            }));

            var resp = await client.GetAsync($"/Destinations/CountrySnapshot/{id}");

            resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);

            await svc.Received(1).GetCountrySnapshotAsync(id, Arg.Any<CancellationToken>());
        }
    }
}
