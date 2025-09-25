using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
using TravelAgency.Repository.Data;
using TravelAgency.IntegrationTests.Infrastructure;
using Xunit;

namespace TravelAgency.IntegrationTests.Controllers
{
    public class AdminControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AdminControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithMocks(
            out UserManager<ApplicationUser> userManager,
            out RoleManager<IdentityRole<Guid>> roleManager,
            string role = "Admin",
            string email = "admin@travel.local")
        {
            var userStore = Substitute.For<IUserStore<ApplicationUser>>();
            var roleStore = Substitute.For<IRoleStore<IdentityRole<Guid>>>();

            var um = Substitute.For<UserManager<ApplicationUser>>(
                userStore,
                null, null, null, null, null, null, null, null
            );

            var rm = Substitute.For<RoleManager<IdentityRole<Guid>>>(
                roleStore,
                Array.Empty<IRoleValidator<IdentityRole<Guid>>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null
            );

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var removeUM = services.Where(d => d.ServiceType == typeof(UserManager<ApplicationUser>)).ToList();
                    foreach (var d in removeUM) services.Remove(d);

                    var removeRM = services.Where(d => d.ServiceType == typeof(RoleManager<IdentityRole<Guid>>)).ToList();
                    foreach (var d in removeRM) services.Remove(d);

                    services.AddSingleton(um);
                    services.AddSingleton(rm);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-User-Role", role);
            client.DefaultRequestHeaders.Add("X-User-Email", email);

            userManager = um;
            roleManager = rm;
            um.ClearReceivedCalls();
            rm.ClearReceivedCalls();

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
            token.Should().NotBeNull("anti-forgery token input must be present in the form");

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
        private static void AssertRedirectWithEmail(HttpResponseMessage resp, string email)
        {
            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var loc = resp.Headers.Location!.ToString();
            loc.Should().StartWith("/Admin?email=");

            var actual = loc.Substring("/Admin?email=".Length);
            var encoded = WebUtility.UrlEncode(email);
            actual.Should().BeOneOf(email, encoded);
        }

        // GET /Admin
        [Fact]
        public async Task Index_Admin_ShouldReturnOk()
        {
            var client = CreateClientWithMocks(out _, out _);
            var resp = await client.GetAsync("/Admin");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Index_NonAdmin_ShouldBeForbidden()
        {
            var client = CreateClientWithMocks(out _, out _, role: "User");
            var resp = await client.GetAsync("/Admin");
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // POST /Admin/Find
        [Fact]
        public async Task Find_EmptyEmail_ShouldRedirectToIndex_WithError()
        {
            var client = CreateClientWithMocks(out _, out _);
            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/Find",
                new[] { new KeyValuePair<string, string>("email", "") }, token);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().StartWith("/Admin");
        }

        [Fact]
        public async Task Find_UserNotFound_ShouldRedirectToIndex_AndCallFindByEmail()
        {
            var client = CreateClientWithMocks(out var um, out _);
            const string email = "unknown@mail.com";
            um.FindByEmailAsync(email).Returns((ApplicationUser?)null);

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/Find",
                new[] { new KeyValuePair<string, string>("email", email) }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);
            await um.Received(1).FindByEmailAsync(email);
        }

        [Fact]
        public async Task Find_UserFound_ShouldRedirectToIndex_AndLoadRoles()
        {
            var client = CreateClientWithMocks(out var um, out _);

            const string email = "filip@mail.com";
            var appUser = new ApplicationUser { Id = Guid.NewGuid(), Email = email };
            um.FindByEmailAsync(email).Returns(appUser);
            um.GetRolesAsync(appUser).Returns(new List<string> { "User", "Agent" });

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/Find",
                new[] { new KeyValuePair<string, string>("email", email) }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);

            await um.Received(1).FindByEmailAsync(email);
            await um.Received(1).GetRolesAsync(appUser);
        }

        // POST /Admin/AddRole
        [Fact]
        public async Task AddRole_MissingFields_ShouldRedirectToIndex()
        {
            var client = CreateClientWithMocks(out _, out _);
            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/AddRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", ""),
                    new KeyValuePair<string, string>("role", "")
                }, token);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().StartWith("/Admin");
        }

        [Fact]
        public async Task AddRole_UserNotFound_ShouldRedirect()
        {
            var client = CreateClientWithMocks(out var um, out _);
            const string email = "test@mail.com";
            um.FindByEmailAsync(email).Returns((ApplicationUser?)null);

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/AddRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", email),
                    new KeyValuePair<string, string>("role", "Agent")
                }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);

            await um.Received(1).FindByEmailAsync(email);
        }

        [Fact]
        public async Task AddRole_RoleNotExists_ShouldCreate_ThenAdd_AndRedirect()
        {
            var client = CreateClientWithMocks(out var um, out var rm);

            const string email = "ivanoski@mail.com";
            var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email };
            um.FindByEmailAsync(email).Returns(user);

            rm.RoleExistsAsync("Agent").Returns(false);
            rm.CreateAsync(Arg.Any<IdentityRole<Guid>>()).Returns(IdentityResult.Success);

            um.AddToRoleAsync(user, "Agent").Returns(IdentityResult.Success);

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/AddRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", email),
                    new KeyValuePair<string, string>("role", "Agent")
                }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);

            await rm.Received().CreateAsync(Arg.Is<IdentityRole<Guid>>(r => r.Name == "Agent"));
            await um.Received(1).AddToRoleAsync(user, "Agent");
        }

        [Fact]
        public async Task AddRole_RoleExists_ShouldAdd_AndRedirect()
        {
            var client = CreateClientWithMocks(out var um, out var rm);

            const string email = "filip@mail.com";
            var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email };
            um.FindByEmailAsync(email).Returns(user);

            rm.RoleExistsAsync("Admin").Returns(true);
            um.AddToRoleAsync(user, "Admin").Returns(IdentityResult.Success);

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/AddRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", email),
                    new KeyValuePair<string, string>("role", "Admin")
                }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);

            await um.Received(1).AddToRoleAsync(user, "Admin");
        }

        // POST /Admin/RemoveRole
        [Fact]
        public async Task RemoveRole_MissingFields_ShouldRedirect()
        {
            var client = CreateClientWithMocks(out _, out _);
            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/RemoveRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", ""),
                    new KeyValuePair<string, string>("role", "")
                }, token);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
            resp.Headers.Location!.ToString().Should().StartWith("/Admin");
        }

        [Fact]
        public async Task RemoveRole_UserNotFound_ShouldRedirect()
        {
            var client = CreateClientWithMocks(out var um, out _);
            const string email = "ghost@mail.com";
            um.FindByEmailAsync(email).Returns((ApplicationUser?)null);

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/RemoveRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", email),
                    new KeyValuePair<string, string>("role", "Agent")
                }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);

            await um.Received(1).FindByEmailAsync(email);
        }

        [Fact]
        public async Task RemoveRole_Success_ShouldCallRemove_AndRedirect()
        {
            var client = CreateClientWithMocks(out var um, out _);

            const string email = "test@mail.com";
            var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email };
            um.FindByEmailAsync(email).Returns(user);
            um.RemoveFromRoleAsync(user, "Agent").Returns(IdentityResult.Success);

            var token = await GetAntiForgeryAsync(client, "/Admin");

            var req = BuildPostForm("/Admin/RemoveRole",
                new[]
                {
                    new KeyValuePair<string, string>("email", email),
                    new KeyValuePair<string, string>("role", "Agent")
                }, token);

            var resp = await client.SendAsync(req);
            AssertRedirectWithEmail(resp, email);

            await um.Received(1).RemoveFromRoleAsync(user, "Agent");
        }
    }
}
