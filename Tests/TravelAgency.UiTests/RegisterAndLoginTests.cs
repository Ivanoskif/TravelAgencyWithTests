// tests/TravelAgency.UiTests/RegisterAndLoginTests.cs
using System;
using System.Linq; // <-- added
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace TravelAgency.UiTests;

public class RegisterAndLoginTests : IAsyncLifetime
{
    private IPlaywright _pw = default!;
    private IBrowser _browser = default!;
    private IBrowserContext _context = default!;
    private IPage _page = default!;
    private readonly string _baseUrl =
        Environment.GetEnvironmentVariable("BASE_URL")?.TrimEnd('/')
        ?? "https://travelagencyweb20250924043952-chaxgpehczdgbgg3.francecentral-01.azurewebsites.net";

    private readonly string _password = "Test.123";

    public async Task InitializeAsync()
    {
        _pw = await Playwright.CreateAsync();
        var headed = string.Equals(Environment.GetEnvironmentVariable("HEADED"), "1", StringComparison.OrdinalIgnoreCase);
        _ = int.TryParse(Environment.GetEnvironmentVariable("SLOWMO"), out var slowMoMs);

        _browser = await _pw.Chromium.LaunchAsync(new() { Headless = !headed, SlowMo = slowMoMs });
        _context = await _browser.NewContextAsync(new()
        {
            BaseURL = _baseUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            RecordVideoDir = "videos"
        });
        await _context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        _page = await _context.NewPageAsync();
        await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
    }

    public async Task DisposeAsync()
    {
        try { await _context.Tracing.StopAsync(new() { Path = "playwright-trace.zip" }); } catch { }
        try { await _page.CloseAsync(); } catch { }
        try { await _context.CloseAsync(); } catch { }
        try { await _browser.CloseAsync(); } catch { }
        try { _pw.Dispose(); } catch { }
    }

    [Fact(DisplayName = "Register a new user, then log in (works with/without email confirmation)")]
    public async Task Register_Then_Login()
    {
        var unique = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var email = $"{unique}@example.com";

        await _page.GotoAsync("/Identity/Account/Register", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
        await _page.FillAsync("input[name='Input.Password'], input[name='Password']", _password);
        await _page.FillAsync("input[name='Input.ConfirmPassword'], input[name='ConfirmPassword']", _password);
        await _page.RunAndWaitForNavigationAsync(
            async () => await _page.ClickAsync("button[type=submit], input[type=submit]"),
            new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });

        var headings = await _page.GetByRole(AriaRole.Heading).AllInnerTextsAsync();
        var requiresConfirmation =
            _page.Url.Contains("RegisterConfirmation", StringComparison.OrdinalIgnoreCase) ||
            headings.Any(t => t.Contains("Confirm", StringComparison.OrdinalIgnoreCase) ||
                              t.Contains("Email", StringComparison.OrdinalIgnoreCase));

        if (requiresConfirmation)
        {
            await _page.GotoAsync("/Identity/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
            await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
            await _page.FillAsync("input[name='Input.Password'], input[name='Password']", _password);
            await _page.ClickAsync("button[type=submit], input[type=submit]");

            var alert = _page.GetByRole(AriaRole.Alert).First;
            if (await alert.CountAsync() > 0)
            {
                await Expect(alert).ToBeVisibleAsync();
            }
            else
            {
                await Expect(_page.Locator("span[data-valmsg-for='Input.Email'], span[data-valmsg-for='Input.Password']").First)
                    .ToBeVisibleAsync();
            }
        }
        else
        {
            if (!await LogoutVisibleAsync())
            {
                await _page.GotoAsync("/Identity/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
                await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
                await _page.FillAsync("input[name='Input.Password'], input[name='Password']", _password);
                await _page.RunAndWaitForNavigationAsync(
                    async () => await _page.ClickAsync("button[type=submit], input[type=submit]"),
                    new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });

                (await LogoutVisibleAsync()).Should().BeTrue("user should be logged in and see Logout");
            }

            await TryLogoutAsync();
        }
    }

    private async Task<bool> LogoutVisibleAsync()
    {
        var toggler = _page.Locator("button.navbar-toggler").First;
        if (await toggler.IsVisibleAsync()) await toggler.ClickAsync();

        var profileToggle = _page.Locator("button.dropdown-toggle, a.dropdown-toggle, [data-bs-toggle='dropdown']").First;
        if (await profileToggle.IsVisibleAsync()) await profileToggle.ClickAsync();

        var logout = _page.Locator("a[href*='/Identity/Account/Logout' i], a:has-text('Logout'), button:has-text('Logout')").First;
        try { await Expect(logout).ToBeVisibleAsync(new() { Timeout = 4000 }); return true; } catch { return false; }
    }

    private async Task TryLogoutAsync()
    {
        var toggler = _page.Locator("button.navbar-toggler").First;
        if (await toggler.IsVisibleAsync()) await toggler.ClickAsync();

        var profileToggle = _page.Locator("button.dropdown-toggle, a.dropdown-toggle, [data-bs-toggle='dropdown']").First;
        if (await profileToggle.IsVisibleAsync()) await profileToggle.ClickAsync();

        var logout = _page.Locator("a[href*='/Identity/Account/Logout' i], a:has-text('Logout'), button:has-text('Logout')").First;
        if (await logout.IsVisibleAsync())
        {
            await _page.RunAndWaitForNavigationAsync(async () => await logout.ClickAsync(),
                new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        }
    }
}
