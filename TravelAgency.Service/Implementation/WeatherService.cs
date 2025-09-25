using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;
using System.Text.Json;
using TravelAgency.Domain.DTO;
using TravelAgency.Service.Interface;
using System.Globalization;

namespace TravelAgency.Service.Implementation;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public WeatherService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<WeatherWindowDTO?> GetWeatherWindowAsync(
            double latitude,
            double longitude,
            DateOnly from,
            DateOnly to,
            CancellationToken ct = default)
    {
        var latStr = latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var lonStr = longitude.ToString("0.######", CultureInfo.InvariantCulture);

        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={latStr}&longitude={lonStr}" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
            "&timezone=auto" +
            $"&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}";

        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<OpenMeteoDailyResponse>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ct);

        if (data?.daily?.time == null || data.daily.temperature_2m_max == null || data.daily.temperature_2m_min == null)
            return null;

        var days = data.daily.time.Length;
        if (days == 0) return null;

        double avgMax = data.daily.temperature_2m_max.Average();
        double avgMin = data.daily.temperature_2m_min.Average();
        double avgPrecProb = data.daily.precipitation_probability_max?.Average() ?? 0;

        var recommendation =
            avgPrecProb <= 30 && avgMax >= 18 && avgMax <= 32 ? "Good window" :
            avgPrecProb <= 50 ? "Mixed" : "Rainy/Unstable";

        return new WeatherWindowDTO
        {
            StartDate = from,
            EndDate = to,
            AverageMaxTempC = Math.Round(avgMax, 1),
            AverageMinTempC = Math.Round(avgMin, 1),
            AveragePrecipitationProbability = Math.Round(avgPrecProb, 0),
            Recommendation = recommendation
        };
    }

    private class OpenMeteoDailyResponse
    {
        public Daily? daily { get; set; }
    }
    private class Daily
    {
        public string[]? time { get; set; }
        public double[]? temperature_2m_max { get; set; }
        public double[]? temperature_2m_min { get; set; }
        public double[]? precipitation_probability_max { get; set; }
    }
}

