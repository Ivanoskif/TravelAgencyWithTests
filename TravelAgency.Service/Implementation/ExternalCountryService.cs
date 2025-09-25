using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Service.Interface;

namespace TravelAgency.Service.Implementation
{
    public class ExternalCountryService : IExternalCountryService
    {
        private readonly HttpClient _http;

        public ExternalCountryService(HttpClient http)
        {
            _http = http;
        }

        public async Task<CountrySnapshotDTO?> GetCountrySnapshotAsync(string countryOrIso, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(countryOrIso)) return null;
            
            var isIso = countryOrIso.Trim().Length <= 3;
            var url = isIso
                ? $"alpha/{Uri.EscapeDataString(countryOrIso)}?fields=name,region,languages,currencies,population,flags"
                : $"name/{Uri.EscapeDataString(countryOrIso)}?fullText=true&fields=name,region,languages,currencies,population,flags";

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var items = await JsonSerializer.DeserializeAsync<List<RestCountry>>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            var rc = items?.FirstOrDefault();
            if (rc == null) return null;

            return new CountrySnapshotDTO
            {
                Name = rc.name?.common ?? rc.name?.official ?? "N/A",
                Region = rc.region ?? "",
                PrimaryLanguage = rc.languages != null && rc.languages.Count > 0
                    ? rc.languages.Values.FirstOrDefault() ?? ""
                    : "",
                CurrencyCode = rc.currencies != null && rc.currencies.Count > 0
                    ? rc.currencies.Keys.FirstOrDefault() ?? ""
                    : "",
                PopulationMillions = rc.population > 0 ? Math.Round(rc.population / 1_000_000.0, 2) : 0,
                FlagUrl = rc.flags?.png ?? rc.flags?.svg ?? ""
            };
        }

        public async Task<IEnumerable<CountryImport>> GetAllForImportAsync(CancellationToken ct = default)
        {
            const string url = "all?fields=name,cca2,capital,currencies,latlng,region,languages,population,flags";

            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<List<RestCountry>>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct)
                ?? new List<RestCountry>();

            return data.Select(rc =>
            {
                var name = rc.name?.common ?? rc.name?.official ?? "N/A";
                var currency = rc.currencies != null && rc.currencies.Count > 0
                    ? rc.currencies.Keys.FirstOrDefault() ?? ""
                    : "";
                var lang = rc.languages != null && rc.languages.Count > 0
                    ? rc.languages.Values.FirstOrDefault() ?? ""
                    : "";
                double lat = 0, lon = 0;
                if (rc.latlng != null && rc.latlng.Count >= 2) { lat = rc.latlng[0]; lon = rc.latlng[1]; }

                return new CountryImport
                {
                    Name = name,
                    IsoCode = rc.cca2 ?? "",
                    Capital = rc.capital?.FirstOrDefault(),
                    CurrencyCode = currency,
                    Latitude = lat,
                    Longitude = lon,
                    Region = rc.region ?? "",
                    FlagUrl = rc.flags?.png ?? rc.flags?.svg ?? "",
                    PrimaryLanguage = lang,
                    PopulationMillions = rc.population > 0 ? Math.Round(rc.population / 1_000_000.0, 2) : 0
                };
            });
        }

        private class RestCountry
        {
            public NameObj? name { get; set; }
            public string? cca2 { get; set; }
            public List<string>? capital { get; set; }
            public Dictionary<string, CurrencyObj>? currencies { get; set; }
            public List<double>? latlng { get; set; }
            public string? region { get; set; }
            public Dictionary<string, string>? languages { get; set; }
            public long population { get; set; }
            public FlagsObj? flags { get; set; }
        }
        private class NameObj { public string? common { get; set; } public string? official { get; set; } }
        private class CurrencyObj { public string? name { get; set; } public string? symbol { get; set; } }
        private class FlagsObj { public string? png { get; set; } public string? svg { get; set; } }
    }
}
