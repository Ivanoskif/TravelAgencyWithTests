using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Xunit;
using TravelAgency.Web; // Program

namespace TravelAgency.UiTests.Support;

public class UiTestFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program>? Factory { get; private set; }
    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;
    public IBrowserContext Context { get; private set; } = default!;
    public IPage Page { get; private set; } = default!;
    public string BaseUrl { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        // 0) Prefer a live URL when provided (best for headed/debugging)
        var envBase = Environment.GetEnvironmentVariable("BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBase))
        {
            BaseUrl = envBase.TrimEnd('/');
        }
        else
        {
            // 1) Spin up in-memory test server with the Web project's content root
            // so views/static files resolve.
            Factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment(Environments.Development);

                    // Point content root to the Web project folder
                    // Adjust the relative path if your solution layout differs.
                    var webProj = Path.GetFullPath(Path.Combine(
                        AppContext.BaseDirectory, "..", "..", "..", "..", "TravelAgency.Web"));
                    builder.UseContentRoot(webProj);

                    // Enable static web assets in tests (ASP.NET Core 7+/8+)
                    builder.UseStaticWebAssets();
                });

            BaseUrl = Factory.Server.BaseAddress!.ToString().TrimEnd('/');
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Headed / slow-mo / browser channel from env
        var headed = string.Equals(Environment.GetEnvironmentVariable("HEADED"), "1", StringComparison.OrdinalIgnoreCase);
        _ = int.TryParse(Environment.GetEnvironmentVariable("SLOWMO"), out var slowMoMs);
        var channel = Environment.GetEnvironmentVariable("BROWSER_CHANNEL");
        var channelOpt = string.IsNullOrWhiteSpace(channel) ? null : channel;

        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !headed,
            SlowMo = slowMoMs,
            Channel = channelOpt
        });

        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            RecordVideoDir = "videos" // optional; creates videos/ per test
        });

        Page = await Context.NewPageAsync();

        // Helpful diagnostics: log console + JS errors into test output
        Page.Console += (_, msg) => Console.WriteLine($"[console:{msg.Type}] {msg.Text}");
        Page.PageError += (_, err) => Console.WriteLine($"[pageerror] {err}");

        // Tracing (very useful to debug blank pages)
        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        // Smoke-navigate to base URL once; fail early if not OK.
        var resp = await Page.GotoAsync("/");
        if (resp is null || !resp.Ok)
        {
            var status = resp?.Status ?? -1;
            var url = resp?.Url ?? $"{BaseUrl}/";
            var body = resp is null ? "(no response)" : await resp.TextAsync().ConfigureAwait(false);
            throw new Exception($"Startup navigation failed: {status} at {url}\n{body}");
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await Context.Tracing.StopAsync(new TracingStopOptions { Path = "playwright-trace.zip" });
        }
        catch { /* ignore */ }

        try { await Page.CloseAsync(); } catch { }
        try { await Context.CloseAsync(); } catch { }
        try { await Browser.CloseAsync(); } catch { }
        try { Playwright.Dispose(); } catch { }
        try { Factory?.Dispose(); } catch { }
    }
}
