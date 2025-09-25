using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;

namespace TravelAgency.Service.Interface
{
    public interface IDestinationService
    {
        
        Destination Create(Destination d);
        Destination Update(Destination d);
        Destination Delete(Destination d);
        Destination? Get(Guid id);
        IEnumerable<Destination> All();

       
        IEnumerable<Destination> Find(string? country = null, string? city = null);
        Destination? GetByCityCountry(string city, string country);
        Task<CountrySnapshotDTO?> GetCountrySnapshotAsync(Guid destinationId, CancellationToken ct = default);
        Task<int> ImportCountriesAsync(CancellationToken ct = default);
    }
}
