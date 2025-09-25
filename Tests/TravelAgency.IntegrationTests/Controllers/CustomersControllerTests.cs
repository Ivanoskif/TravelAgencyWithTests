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
using System.Threading.Tasks;
using TravelAgency.Domain.Models;
using TravelAgency.IntegrationTests.Infrastructure;
using TravelAgency.Service.Interface;
using Xunit;

namespace TravelAgency.IntegrationTests.Controllers
{
    public class CustomersControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public CustomersControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithMock(out ICustomerService customers, string role)
        {
            var customersLocal = Substitute.For<ICustomerService>();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var toRemove = services.Where(d => d.ServiceType == typeof(ICustomerService)).ToList();
                    foreach (var d in toRemove) services.Remove(d);

                    services.AddSingleton(customersLocal);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-User-Role", role);

            customers = customersLocal;
            return client;
        }

        // Anti-forgery helpers 
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

        // GET Index 
        [Fact]
        public async Task Index_Anonymous_NoQuery_ShouldReturnOk_AndCallAll()
        {
            var client = CreateClientWithMock(out var svc, role: "User");

            svc.All().Returns(new[]
            {
                new Customer { Id = Guid.NewGuid(), Email = "filip@mail.com",    FirstName = "filip",  LastName = "ivanoski" },
                new Customer { Id = Guid.NewGuid(), Email = "user@mail.com",     FirstName = "second", LastName = "user" }
            });

            var resp = await client.GetAsync("/Customers");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            svc.Received(1).All();
            svc.DidNotReceiveWithAnyArgs().Search(default!);
        }

        [Fact]
        public async Task Index_Anonymous_WithQuery_ShouldReturnOk_AndCallSearch()
        {
            var client = CreateClientWithMock(out var svc, role: "User");

            svc.Search("filip").Returns(new[]
            {
                new Customer { Id = Guid.NewGuid(), Email = "filip@mail.com", FirstName = "filip", LastName = "ivanoski" }
            });

            var resp = await client.GetAsync("/Customers?q=filip");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            svc.Received(1).Search("filip");
            svc.DidNotReceive().All();
        }

        // GET Details 
        [Fact]
        public async Task Details_Anonymous_NullId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out _, role: "User");
            var resp = await client.GetAsync("/Customers/Details");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_Anonymous_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            svc.Get(Arg.Any<Guid>()).Returns((Customer?)null);

            var resp = await client.GetAsync($"/Customers/Details/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Details_Anonymous_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Customer
            {
                Id = id,
                Email = "filip@mail.com",
                FirstName = "filip",
                LastName = "ivanoski"
            });

            var resp = await client.GetAsync($"/Customers/Details/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            svc.Received(1).Get(id);
        }

        // Create 
        [Fact]
        public async Task Create_Get_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out _, role: "Admin");
            var resp = await client.GetAsync("/Customers/Create");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_Post_Valid_ShouldRedirect_AndCallService()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");

            svc.GetByEmail("new@mail.com").Returns((Customer?)null);
            var token = await GetAntiForgeryAsync(client, "/Customers/Create");

            var form = new[]
            {
                new KeyValuePair<string, string>("FirstName", "new"),
                new KeyValuePair<string, string>("LastName",  "user"),
                new KeyValuePair<string, string>("Email",     "new@mail.com"),
                new KeyValuePair<string, string>("Phone",     "123"),
                new KeyValuePair<string, string>("Id",        Guid.Empty.ToString())
            };

            var req = BuildPostForm("/Customers/Create", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Customers");

            svc.Received(1).Create(Arg.Is<Customer>(c => c.Email == "new@mail.com" && c.FirstName == "new"));
        }

        [Fact]
        public async Task Create_Post_DuplicateEmail_ShouldStayOnView_AndNotCallCreate()
        {
            var client = CreateClientWithMock(out var svc, role: "Agent");

            svc.GetByEmail("user@mail.com")
               .Returns(new Customer { Id = Guid.NewGuid(), Email = "user@mail.com" });

            var token = await GetAntiForgeryAsync(client, "/Customers/Create");

            var form = new[]
            {
                new KeyValuePair<string, string>("FirstName", "second"),
                new KeyValuePair<string, string>("LastName",  "user"),
                new KeyValuePair<string, string>("Email",     "user@mail.com"),
                new KeyValuePair<string, string>("Phone",     "123"),
                new KeyValuePair<string, string>("Id",        Guid.Empty.ToString())
            };

            var req = BuildPostForm("/Customers/Create", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            await resp.Content.ReadAsStringAsync().ContinueWith(t =>
            {
                t.Result.Should().Contain("Email already exists.");
            });

            svc.DidNotReceiveWithAnyArgs().Create(default!);
        }

        // Edit 
        [Fact]
        public async Task Edit_Get_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            svc.Get(Arg.Any<Guid>()).Returns((Customer?)null);

            var resp = await client.GetAsync($"/Customers/Edit/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Edit_Get_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Customer
            {
                Id = id,
                Email = "ivanoski@mail.com",
                FirstName = "filip",
                LastName = "ivanoski"
            });

            var resp = await client.GetAsync($"/Customers/Edit/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Edit_Post_DuplicateEmail_ShouldStayOnView_AndNotCallUpdate()
        {
            var client = CreateClientWithMock(out var svc, role: "Agent");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Customer { Id = id, Email = "user@mail.com", FirstName = "second", LastName = "user" });
            svc.GetByEmail("filip@mail.com").Returns(new Customer { Id = Guid.NewGuid(), Email = "filip@mail.com" });

            var token = await GetAntiForgeryAsync(client, $"/Customers/Edit/{id}");

            var form = new[]
            {
                new KeyValuePair<string, string>("Id",        id.ToString()),
                new KeyValuePair<string, string>("FirstName", "second"),
                new KeyValuePair<string, string>("LastName",  "user"),
                new KeyValuePair<string, string>("Email",     "filip@mail.com"),
                new KeyValuePair<string, string>("Phone",     "000")
            };

            var req = BuildPostForm($"/Customers/Edit/{id}", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            var html = await resp.Content.ReadAsStringAsync();
            html.Should().Contain("Email already in use by another customer.");

            svc.DidNotReceiveWithAnyArgs().Update(default!);
        }

        [Fact]
        public async Task Edit_Post_Valid_ShouldRedirect_AndCallUpdate()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Customer { Id = id, Email = "user@mail.com", FirstName = "new", LastName = "user" });
            svc.GetByEmail("newuser@mail.com").Returns((Customer?)null);

            var token = await GetAntiForgeryAsync(client, $"/Customers/Edit/{id}");

            var form = new[]
            {
                new KeyValuePair<string, string>("Id",        id.ToString()),
                new KeyValuePair<string, string>("FirstName", "new"),
                new KeyValuePair<string, string>("LastName",  "user"),
                new KeyValuePair<string, string>("Email",     "newuser@mail.com"),
                new KeyValuePair<string, string>("Phone",     "321")
            };

            var req = BuildPostForm($"/Customers/Edit/{id}", form, token);
            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Customers");

            svc.Received(1).Update(Arg.Is<Customer>(c => c.Id == id && c.Email == "newuser@mail.com"));
        }

        // Delete 
        [Fact]
        public async Task Delete_Get_UnknownId_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            svc.Get(Arg.Any<Guid>()).Returns((Customer?)null);

            var resp = await client.GetAsync($"/Customers/Delete/{Guid.NewGuid()}");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_Get_KnownId_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Customer { Id = id, Email = "user@mail.com", FirstName = "second", LastName = "user" });

            var resp = await client.GetAsync($"/Customers/Delete/{id}");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DeleteConfirmed_Post_ShouldRedirect_AndCallDelete()
        {
            var client = CreateClientWithMock(out var svc, role: "Admin");
            var id = Guid.NewGuid();

            svc.Get(id).Returns(new Customer { Id = id, Email = "user@mail.com" });

            var token = await GetAntiForgeryAsync(client, $"/Customers/Delete/{id}");

            var req = BuildPostForm($"/Customers/Delete/{id}", new[]
            {
                new KeyValuePair<string, string>("id", id.ToString())
            }, token);

            var resp = await client.SendAsync(req);

            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().Be("/Customers");

            svc.Received(1).Delete(Arg.Is<Customer>(c => c.Id == id));
        }

        // GetByEmail and Search endpoints 
        [Fact]
        public async Task GetByEmail_Anonymous_Empty_ShouldReturn400()
        {
            var client = CreateClientWithMock(out _, role: "User");
            var resp = await client.GetAsync("/Customers/GetByEmail?email=");
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetByEmail_Anonymous_NotFound_ShouldReturn404()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            svc.GetByEmail("newuser@mail.com").Returns((Customer?)null);

            var resp = await client.GetAsync("/Customers/GetByEmail?email=newuser@mail.com");
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetByEmail_Anonymous_Found_ShouldReturnOk()
        {
            var client = CreateClientWithMock(out var svc, role: "User");
            var id = Guid.NewGuid();

            svc.GetByEmail("filip@mail.com").Returns(new Customer
            {
                Id = id,
                Email = "filip@mail.com",
                FirstName = "filip",
                LastName = "ivanoski"
            });

            var resp = await client.GetAsync("/Customers/GetByEmail?email=filip@mail.com");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Search_Anonymous_ShouldReturnOk_AndCallService()
        {
            var client = CreateClientWithMock(out var svc, role: "User");

            svc.Search("filip").Returns(new[]
            {
                new Customer { Id = Guid.NewGuid(), Email = "filip@mail.com", FirstName = "filip", LastName = "ivanoski" }
            });

            var resp = await client.GetAsync("/Customers/Search?q=filip");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            svc.Received(1).Search("filip");
        }
    }
}
