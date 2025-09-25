// tests/TravelAgency.UiTests/LoginWithEnvTests.cs
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace TravelAgency.UiTests;

public class LoginWithEnvTests : IAsyncLifetime
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
        _browser = await _pw.Chromium.LaunchAsync(new() { Headless = !headed, SlowMo = slowMoMs });
        _context = await _browser.NewContextAsync(new() { BaseURL = _baseUrl, IgnoreHTTPSErrors = true, ViewportSize = new ViewportSize { Width = 1280, Height = 800 } });
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        try { await _page.CloseAsync(); } catch { }
        try { await _context.CloseAsync(); } catch { }
        try { await _browser.CloseAsync(); } catch { }
        try { _pw.Dispose(); } catch { }
    }

    [Fact(DisplayName = "Login succeeds with USER_EMAIL/USER_PASSWORD (soft-skip if not set)")]
    public async Task Login_With_Env()
    {
        var email = Environment.GetEnvironmentVariable("USER_EMAIL");
        var password = Environment.GetEnvironmentVariable("USER_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Skipping: set USER_EMAIL and USER_PASSWORD to enable this test.");
            return;
        }

        await _page.GotoAsync("/Identity/Account/Login");
        await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
        await _page.FillAsync("input[name='Input.Password'], input[name='Password']", password);
        await _page.RunAndWaitForNavigationAsync(async () => await _page.ClickAsync("button[type=submit], input[type=submit]"));

        // reveal Logout if hidden
        var toggler = _page.Locator("button.navbar-toggler").First;
        if (await toggler.IsVisibleAsync()) await toggler.ClickAsync();
        var profileToggle = _page.Locator("button.dropdown-toggle, a.dropdown-toggle, [data-bs-toggle='dropdown']").First;
        if (await profileToggle.IsVisibleAsync()) await profileToggle.ClickAsync();

        await Expect(_page.Locator("a[href*='/Identity/Account/Logout' i], a:has-text('Logout'), button:has-text('Logout')").First)
            .ToBeVisibleAsync();
    }
}
