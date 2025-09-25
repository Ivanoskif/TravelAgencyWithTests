using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace TravelAgency.UiTests;

public class PackageDetailsActionsTests : IAsyncLifetime
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
    }

    public async Task DisposeAsync()
    {
        try { await _context.Tracing.StopAsync(new() { Path = "playwright-trace.zip" }); } catch { }
        try { await _page.CloseAsync(); } catch { }
        try { await _context.CloseAsync(); } catch { }
        try { await _browser.CloseAsync(); } catch { }
        try { _pw.Dispose(); } catch { }
    }

    // Tests

    [Fact(DisplayName = "Details → Holidays (API) shows results (modal, inline section, or new page)")]
    public async Task Package_Holidays_API_Works()
    {
        await OpenFirstPackageDetailsAsync();

        var clicked =
            await ClickByNamesAsync(new[] { "View holidays", "Holidays (API)", "Holidays", "Public holidays" });
        clicked.Should().BeTrue("the Details page should have a Holidays action");

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var dialog = _page.GetByRole(AriaRole.Dialog).First;
        if (await dialog.CountAsync() > 0)
        {
            await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 10000 });
            return;
        }

        var inline =
            _page.Locator("[data-test=holidays], #holidays, .holidays, table:has(th:has-text('Holiday'))").First;
        if (await inline.CountAsync() > 0)
        {
            await Expect(inline).ToBeVisibleAsync(new() { Timeout = 10000 });
            return;
        }

        if (_page.Url.Contains("holiday", StringComparison.OrdinalIgnoreCase))
            return;

        await Expect(_page.Locator("text=/Holiday|Holidays/i").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact(DisplayName = "Details → Weather window (API) shows results (modal, inline, or new page)")]
    public async Task Package_Weather_API_Works()
    {
        await OpenFirstPackageDetailsAsync();

        var clicked =
            await ClickByNamesAsync(new[] { "Weather window", "Wheater window", "Weather (API)", "Weather" });
        clicked.Should().BeTrue("the Details page should have a Weather action");

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var dialog = _page.GetByRole(AriaRole.Dialog).First;
        if (await dialog.CountAsync() > 0)
        {
            await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 10000 });
            return;
        }

        var inline = _page.Locator("[data-test=weather], #weather, .weather, section:has-text('Weather')").First;
        if (await inline.CountAsync() > 0)
        {
            await Expect(inline).ToBeVisibleAsync(new() { Timeout = 10000 });
            return;
        }

        if (_page.Url.Contains("weather", StringComparison.OrdinalIgnoreCase))
            return;

        await Expect(_page.Locator("text=/Weather|°C|°F|Humidity|Wind/i").First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact(DisplayName = "Details → Convert price: choose CAD and convert")]
    public async Task Package_ConvertPrice_Works()
    {
        await OpenFirstPackageDetailsAsync();

        var clicked = await ClickByNamesAsync(new[] { "Convert price", "Convert", "Converte" });
        clicked.Should().BeTrue("the Details page should have a Convert action");

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var select = _page.Locator("select:has(option:has-text('CAD')), select:has(option[value='CAD'])").First;
        if (await select.CountAsync() == 0)
        {
            select = _page.GetByLabel("Currency", new() { Exact = false }).Filter(new() { Has = _page.Locator("select") }).First;
            if (await select.CountAsync() == 0)
                select = _page.Locator("select[name*='Currency' i], select[id*='Currency' i]").First;
        }
        (await select.CountAsync()).Should().BeGreaterThan(0, "a currency <select> should exist on the convert UI");

        await select.SelectOptionAsync(new[] { new SelectOptionValue { Label = "CAD", Value = "CAD" } });

        var didConvert = await ClickByNamesAsync(new[] { "Convert", "Converte" });
        didConvert.Should().BeTrue("there should be a Convert button on the convert UI");

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var result = _page.Locator("[data-test=convert-result], #converted, #result, .conversion-result").First;
        if (await result.CountAsync() > 0)
        {
            await Expect(result).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
        else
        {
            var cadText = _page.Locator("xpath=//*[contains(normalize-space(.),'CAD') and not(self::option)]").First;
            await Expect(cadText).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
    }

    // helpers

    private async Task OpenFirstPackageDetailsAsync()
    {
        var resp = await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        resp?.Ok.Should().BeTrue();

        var detailsLinks = _page.GetByRole(AriaRole.Link, new() { Name = "Details" });
        (await detailsLinks.CountAsync()).Should().BeGreaterThan(0, "home/packages page should list at least one package");
        await detailsLinks.First.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task<bool> ClickByNamesAsync(string[] names)
    {
        foreach (var name in names)
        {
            var byRole = _page.GetByRole(AriaRole.Button, new() { Name = name, Exact = false }).First;
            if (await TryClickAsync(byRole)) return true;

            var byLink = _page.GetByRole(AriaRole.Link, new() { Name = name, Exact = false }).First;
            if (await TryClickAsync(byLink)) return true;

            var fallback = _page.Locator($"button:has-text('{name}'), a:has-text('{name}')").First;
            if (await TryClickAsync(fallback)) return true;
        }
        return false;
    }

    private static async Task<bool> TryClickAsync(ILocator locator)
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
}
