using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;

namespace TravelAgency.Service.Interface
{

    public class CountryImport
    {
        public string Name { get; set; } = string.Empty;
        public string IsoCode { get; set; } = string.Empty;
        public string? Capital { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Region { get; set; } = string.Empty;
        public string FlagUrl { get; set; } = string.Empty;
        public string PrimaryLanguage { get; set; } = string.Empty;
        public double PopulationMillions { get; set; }
    }

    public interface IExternalCountryService
    {
        Task<CountrySnapshotDTO?> GetCountrySnapshotAsync(string countryOrIso, CancellationToken ct = default);
        Task<IEnumerable<CountryImport>> GetAllForImportAsync(CancellationToken ct = default);
    }
}
