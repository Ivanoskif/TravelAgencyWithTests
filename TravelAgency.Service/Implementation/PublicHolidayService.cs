using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Service.Interface;

namespace TravelAgency.Service.Implementation
{
    public class PublicHolidayService : IPublicHolidayService
    {
        private readonly HttpClient _http;
        public PublicHolidayService(HttpClient http) { _http = http; }

        public async Task<IReadOnlyList<PublicHolidayDTO>> GetHolidaysInRangeAsync(string iso2, DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(iso2)) return Array.Empty<PublicHolidayDTO>();

            var years = (from.Year == to.Year) ? new[] { from.Year } : new[] { from.Year, to.Year };
            var all = new List<PublicHolidayDTO>();

            foreach (var y in years)
            {
                var resp = await _http.GetAsync($"PublicHolidays/{y}/{iso2}", ct);
                if (!resp.IsSuccessStatusCode) continue;

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                var items = await JsonSerializer.DeserializeAsync<List<ApiHoliday>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct)
                           ?? new List<ApiHoliday>();

                all.AddRange(items.Select(x => new PublicHolidayDTO
                {
                    Date = DateOnly.FromDateTime(x.Date),
                    LocalName = x.LocalName ?? "",
                    Name = x.Name ?? ""
                }));
            }

            return all.Where(h => h.Date >= from && h.Date <= to)
                      .OrderBy(h => h.Date)
                      .ToList();
        }

        private class ApiHoliday
        {
            public DateTime Date { get; set; }
            public string? LocalName { get; set; }
            public string? Name { get; set; }
        }
    }
}
