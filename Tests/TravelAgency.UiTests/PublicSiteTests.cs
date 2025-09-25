using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace TravelAgency.UiTests
{
    public class PublicSiteTests : IAsyncLifetime
    {
        private IPlaywright _pw = default!;
        private IBrowser _browser = default!;
        private IBrowserContext _context = default!;
        private IPage _page = default!;

        private readonly string _baseUrl =
            Environment.GetEnvironmentVariable("BASE_URL")?.TrimEnd('/')
            ?? "https://travelagencyweb20250924043952-chaxgpehczdgbgg3.francecentral-01.azurewebsites.net";

        public async Task InitializeAsync()
        {
            _pw = await Playwright.CreateAsync();

            var headed = string.Equals(Environment.GetEnvironmentVariable("HEADED"), "1", StringComparison.OrdinalIgnoreCase);
            _ = int.TryParse(Environment.GetEnvironmentVariable("SLOWMO"), out var slowMoMs);

            _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = !headed,
                SlowMo = slowMoMs
            });

            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _baseUrl,
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                RecordVideoDir = "videos"
            });

            await _context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });

            _page = await _context.NewPageAsync();
            _page.Console += (_, m) => Console.WriteLine($"[console:{m.Type}] {m.Text}");
            _page.PageError += (_, e) => Console.WriteLine($"[pageerror] {e}");

            var resp = await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            Console.WriteLine($"[init] url={_page.Url} status={resp?.Status}");
            resp?.Ok.Should().BeTrue("home page should respond with 200");
        }

        public async Task DisposeAsync()
        {
            try { await _context.Tracing.StopAsync(new TracingStopOptions { Path = "playwright-trace.zip" }); } catch { }
            try { await _page.CloseAsync(); } catch { }
            try { await _context.CloseAsync(); } catch { }
            try { await _browser.CloseAsync(); } catch { }
            try { _pw.Dispose(); } catch { }
        }

        [Fact(DisplayName = "Home/Packages page shows packages table and at least one row")]
        public async Task Home_Shows_Packages()
        {
            var resp = await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            resp?.Ok.Should().BeTrue();

            await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Travel Packages" }).First)
                .ToBeVisibleAsync();

            await Expect(_page.Locator("table").First).ToBeVisibleAsync();

            var details = _page.GetByRole(AriaRole.Link, new() { Name = "Details" });
            (await details.CountAsync()).Should().BeGreaterThan(0, "there should be at least one details link");
            await Expect(details.First).ToBeVisibleAsync();
        }

        [Fact(DisplayName = "Destinations page renders list with Details links")]
        public async Task Destinations_List_Renders()
        {
            var resp = await _page.GotoAsync("/Destinations", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            resp?.Ok.Should().BeTrue();

            await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Destinations" }).First)
                .ToBeVisibleAsync();

            var details = _page.GetByRole(AriaRole.Link, new() { Name = "Details" });
            (await details.CountAsync()).Should().BeGreaterThan(0, "destinations should list items with Details links");
            await Expect(details.First).ToBeVisibleAsync();
        }

        [Fact(DisplayName = "Bookings redirects anonymous users to Login")]
        public async Task Bookings_Requires_Login()
        {
            var resp = await _page.GotoAsync("/Bookings/UserBookings", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            resp?.Ok.Should().BeTrue(); 

            _page.Url.Should().Contain("/Identity/Account/Login", "anonymous users should be redirected to login for Bookings");
            _page.Url.Should().Contain("ReturnUrl=%2FBookings%2FUserBookings");

            await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true }).First)
                .ToBeVisibleAsync();
        }

        [Fact(DisplayName = "Register page shows validation when submitting empty form")]
        public async Task Register_Shows_Validation_On_Empty_Submit()
        {
            await _page.GotoAsync("/Identity/Account/Register", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });

            await _page.ClickAsync("button[type=submit], input[type=submit]");

            var alert = _page.GetByRole(AriaRole.Alert).First;
            if (await alert.CountAsync() > 0)
            {
                await Expect(alert).ToBeVisibleAsync(new() { Timeout = 10000 });
            }
            else
            {
                var emailError = _page.Locator("span[data-valmsg-for='Input.Email'].field-validation-error, span[data-valmsg-for='Input.Email'].text-danger").First;
                await Expect(emailError).ToBeVisibleAsync(new() { Timeout = 10000 });
            }
        }

        [Fact(DisplayName = "Login succeeds with provided credentials (set USER_EMAIL & USER_PASSWORD)"),
         System.Diagnostics.CodeAnalysis.SuppressMessage("xUnit", "xUnit1004")]
        public async Task Login_With_Env_Credentials()
        {
            var email = Environment.GetEnvironmentVariable("USER_EMAIL");
            var password = Environment.GetEnvironmentVariable("USER_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Skipping login test: set USER_EMAIL and USER_PASSWORD to enable.");
                return;
            }

            await _page.GotoAsync("/Identity/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
            await _page.FillAsync("input[name='Input.Password'], input[name='Password']", password);

            await _page.RunAndWaitForNavigationAsync(
                async () => await _page.ClickAsync("button[type=submit], input[type=submit]"),
                new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 }
            );

            var navbarToggler = _page.Locator("button.navbar-toggler");
            if (await navbarToggler.IsVisibleAsync()) await navbarToggler.ClickAsync();

            var profileToggle = _page.Locator("button.dropdown-toggle, a.dropdown-toggle, [data-bs-toggle='dropdown']").First;
            if (await profileToggle.IsVisibleAsync()) await profileToggle.ClickAsync();

            var logout = _page.Locator("a[href*='/Identity/Account/Logout' i], a:has-text('Logout'), button:has-text('Logout')").First;
            await Expect(logout).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
    }
}
