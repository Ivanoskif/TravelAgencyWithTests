using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using TravelAgency.Domain.DTO;
using TravelAgency.Service.Interface;

namespace TravelAgency.Service.Implementation;

public class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _http;

    public ExchangeRateService(HttpClient http)
    {
        _http = http;
    }

    public async Task<decimal?> GetRateAsync(string from, string to, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return null;
        from = from.Trim().ToUpperInvariant();
        to = to.Trim().ToUpperInvariant();

        var url = $"latest?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var raw = await resp.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<FrankLatest>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (data?.Rates != null && data.Rates.TryGetValue(to, out var rate)) return rate;
        return null;
    }

    public async Task<PriceQuoteDTO?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return null;
        from = from.Trim().ToUpperInvariant();
        to = to.Trim().ToUpperInvariant();

        var rate = await GetRateAsync(from, to, ct);
        if (rate is null) return null;

        var converted = Math.Round(amount * rate.Value, 2, MidpointRounding.AwayFromZero);

        return new PriceQuoteDTO
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = rate.Value,
            AmountBase = amount,
            AmountConverted = converted,
            TimestampUtc = DateTime.UtcNow
        };
    }

    private class FrankLatest
    {
        public string? Base { get; set; }
        public string? Date { get; set; }
        public System.Collections.Generic.Dictionary<string, decimal>? Rates { get; set; }
    }
}
