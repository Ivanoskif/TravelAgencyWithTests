using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace TravelAgency.UiTests;

public class BookPackageTests : IAsyncLifetime
{
    private IPlaywright _pw = default!;
    private IBrowser _browser = default!;
    private IBrowserContext _context = default!;
    private IPage _page = default!;

    private readonly string _baseUrl =
        Environment.GetEnvironmentVariable("BASE_URL")?.TrimEnd('/') ??
        "https://travelagencyweb20250924043952-chaxgpehczdgbgg3.francecentral-01.azurewebsites.net";

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
        _page.Console += (_, m) => Console.WriteLine($"[console:{m.Type}] {m.Text}");
        _page.PageError += (_, e) => Console.WriteLine($"[pageerror] {e}");
    }

    public async Task DisposeAsync()
    {
        try { await _context.Tracing.StopAsync(new() { Path = "playwright-trace.zip" }); } catch { }
        try { await _page.CloseAsync(); } catch { }
        try { await _context.CloseAsync(); } catch { }
        try { await _browser.CloseAsync(); } catch { }
        try { _pw.Dispose(); } catch { }
    }

    [Fact(DisplayName = "Book first package as logged-in user (requires USER_EMAIL/PASSWORD)")]
    public async Task Book_First_Package()
    {
        var email = Environment.GetEnvironmentVariable("USER_EMAIL");
        var password = Environment.GetEnvironmentVariable("USER_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Skipping: set USER_EMAIL and USER_PASSWORD to enable booking test.");
            return;
        }

        await EnsureLoggedInAsync(email!, password!);

        await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        var detailsLinks = _page.GetByRole(AriaRole.Link, new() { Name = "Details" });
        (await detailsLinks.CountAsync()).Should().BeGreaterThan(0, "there should be at least one package to view");
        await detailsLinks.First.ClickAsync();

        var peopleFilled =
            await FillIfExistsAsync(_page.GetByLabel("People", new() { Exact = false }).First, "2") ||
            await FillIfExistsAsync(_page.Locator("input[name='People'], input[id*='People']").First, "2");
        peopleFilled.Should().BeTrue("the booking details page should have a People field");

        var submitted =
            await TryClickAnyAsync(_page.GetByRole(AriaRole.Button, new() { Name = "Add to cart" }).First) ||
            await TryClickAnyAsync(_page.Locator("button:has-text('Add to cart'), a:has-text('Add to cart')").First) ||
            await TryClickAnyAsync(_page.Locator("button:has-text('Book Now'), a:has-text('Book Now'), button:has-text('Book'), a:has-text('Book')").First);

        submitted.Should().BeTrue("a submit button like 'Add to cart' should exist on the booking form");

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await _page.GotoAsync("/Bookings/UserBookings", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        _page.Url.Should().Contain("/Bookings/UserBookings");

        var table = _page.Locator("table").First;
        await Expect(table).ToBeVisibleAsync(new() { Timeout = 10000 });

        var rows = _page.Locator("table tbody tr");
        (await rows.CountAsync()).Should().BeGreaterThan(0, "user bookings table should have at least one entry");
    }

    // helpers

    private async Task EnsureLoggedInAsync(string email, string password)
    {
        if (await LogoutVisibleAsync()) return;

        await _page.GotoAsync("/Identity/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        await _page.FillAsync("input[name='Input.Email'], input[name='Email']", email);
        await _page.FillAsync("input[name='Input.Password'], input[name='Password']", password);
        await _page.RunAndWaitForNavigationAsync(
            async () => await _page.ClickAsync("button[type=submit], input[type=submit]"),
            new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 }
        );

        (await LogoutVisibleAsync()).Should().BeTrue("login should succeed so Logout becomes visible");
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

    private static async Task<bool> TryClickAnyAsync(ILocator locator)
    {
        try
        {
            if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
            {
                await locator.First.ClickAsync();
                return true;
            }
        }
        catch {  }
        return false;
    }

    private static async Task<bool> FillIfExistsAsync(ILocator locator, string value)
    {
        try
        {
            if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
            {
                await locator.First.FillAsync(value);
                return true;
            }
        }
        catch {  }
        return false;
    }
}
