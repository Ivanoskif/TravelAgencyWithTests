using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;

namespace TravelAgency.Service.Interface
{
    public interface IPackageService
    {
        
        Package Create(Package p);
        Package Update(Package p);
        Package Delete(Package p);
        Package? Get(Guid id);
        IEnumerable<Package> All();


        int RemainingSeats(Guid packageId);
        Task<WeatherWindowDTO?> GetWeatherWindowAsync(Guid packageId, CancellationToken ct = default);
        Task<IReadOnlyList<PublicHolidayDTO>> GetHolidaysAsync(Guid packageId, CancellationToken ct = default);
        Task<PriceQuoteDTO?> GetPriceQuoteAsync(Guid packageId, string toCurrency, CancellationToken ct = default);
        Task<int> GetRemainingSeatsAsync(Guid packageId, CancellationToken ct = default);
    }
}

