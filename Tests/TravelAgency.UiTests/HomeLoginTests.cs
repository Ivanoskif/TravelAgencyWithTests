using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace TravelAgency.UiTests
{
    public class HomeLoginTests : IAsyncLifetime
    {
        private IPlaywright _pw = default!;
        private IBrowser _browser = default!;
        private IBrowserContext _context = default!;
        private IPage _page = default!;
        private readonly string _baseUrl;

        public HomeLoginTests()
        {
            _baseUrl = Environment.GetEnvironmentVariable("BASE_URL")?.TrimEnd('/')
                       ?? "http://localhost:5274";
        }

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
            Console.WriteLine($"[nav:init] url={_page.Url} status={resp?.Status}");
            resp?.Ok.Should().BeTrue("the home page should return 200 on initial navigation");
        }

        public async Task DisposeAsync()
        {
            try { await _context.Tracing.StopAsync(new TracingStopOptions { Path = "playwright-trace.zip" }); } catch { }
            try { await _page.CloseAsync(); } catch { }
            try { await _context.CloseAsync(); } catch { }
            try { await _browser.CloseAsync(); } catch { }
            try { _pw.Dispose(); } catch { }
        }

        [Fact(DisplayName = "Home page renders")]
        public async Task Home_Should_Render()
        {
            var resp = await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            Console.WriteLine($"[nav:home] url={_page.Url} status={resp?.Status}");
            resp?.Ok.Should().BeTrue("home should respond with 200");

            var title = await _page.TitleAsync();
            title.Should().NotBeNullOrWhiteSpace();

            (title.Contains("Travel", StringComparison.OrdinalIgnoreCase) ||
             await _page.IsVisibleAsync("text=Travel"))
            .Should().BeTrue("home should show something travel-related in title or body");
        }

        [Fact(DisplayName = "Can open login page")]
        public async Task LoginPage_Should_Open()
        {
            var resp = await _page.GotoAsync("/Identity/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            if (resp is null || !resp.Ok)
                resp = await _page.GotoAsync("/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });

            Console.WriteLine($"[nav:login] url={_page.Url} status={resp?.Status}");
            resp?.Ok.Should().BeTrue("login page should respond with 200");

            (await _page.IsVisibleAsync("input[name='Input.Email'], input[name='Email']")).Should().BeTrue("email field should be visible");
            (await _page.IsVisibleAsync("input[name='Input.Password'], input[name='Password']")).Should().BeTrue("password field should be visible");
        }

        [Fact(DisplayName = "Admin can log in")]
        public async Task Admin_Login_Should_Succeed()
        {
            var email = "admin@travel.local";
            var password = "Admin!12345";

            await _page.GotoAsync("/Identity/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });

            await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
            await _page.FillAsync("input[name='Input.Password'], input[name='Password']", password);

            await _page.RunAndWaitForNavigationAsync(
                async () => await _page.ClickAsync("button[type=submit], input[type=submit]"),
                new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 }
            );


            _page.Url.Should().NotContain("/Identity/Account/Login", "should leave the login page after successful sign-in");

            var navbarToggler = _page.Locator("button.navbar-toggler");
            if (await navbarToggler.IsVisibleAsync())
            {
                Console.WriteLine("[ui] Clicking navbar toggler");
                await navbarToggler.ClickAsync();
            }

            var profileTrigger = _page.Locator("button.dropdown-toggle, a.dropdown-toggle, [data-bs-toggle='dropdown']");
            if (await profileTrigger.IsVisibleAsync())
            {
                Console.WriteLine("[ui] Opening user/profile dropdown");
                await profileTrigger.ClickAsync();
            }

            var logout = _page.Locator(
                "a[href*='/Identity/Account/Logout' i], " +
                "a:has-text('Logout'), " +
                "button:has-text('Logout')"
            ).First;

            await Expect(logout).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
    }
}
