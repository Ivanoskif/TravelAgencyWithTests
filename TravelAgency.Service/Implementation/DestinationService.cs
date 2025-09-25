using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Interface;
using TravelAgency.Service.Interface;

namespace TravelAgency.Service.Implementation
{
    public class DestinationService : IDestinationService
    {
        private readonly IRepository<Destination> _repo;
        private readonly IExternalCountryService _countries;

        public DestinationService(IRepository<Destination> repo, IExternalCountryService countries)
        {
            _repo = repo;
            _countries = countries;
        }

        public Destination Create(Destination d)
        {
            return _repo.Insert(d);
        }

        public Destination Update(Destination d)
        {
            return _repo.Update(d);
        }

        public Destination Delete(Destination d)
        {
            return _repo.Delete(d);
        }

        public Destination? Get(Guid id)
        {
            return _repo.Get(x => x, x => x.Id == id);
        }

        public IEnumerable<Destination> All()
        {
            return _repo.GetAll(x => x);
        }

        public IEnumerable<Destination> Find(string? country = null, string? city = null)
        {
            return _repo.GetAll(x => x,
                predicate: x =>
                    (string.IsNullOrWhiteSpace(country) || x.CountryName == country) &&
                    (string.IsNullOrWhiteSpace(city) || x.City == city));
        }

        public Destination? GetByCityCountry(string city, string country)
        {
            return _repo.Get(x => x, x => x.City == city && x.CountryName == country);
        }

        public async Task<CountrySnapshotDTO?> GetCountrySnapshotAsync(Guid destinationId, CancellationToken ct = default)
        {
            var dest = Get(destinationId);
            if (dest == null)
            {
                return null;
            }
            return await _countries.GetCountrySnapshotAsync(dest.CountryName, ct);
        }

        public async Task<int> ImportCountriesAsync(CancellationToken ct = default)
        {
            var countries = await _countries.GetAllForImportAsync(ct);
            int count = 0;

            foreach (var c in countries.Take(5))
            {
                bool exists = _repo.GetAll(x => x.IsoCode, x => x.IsoCode == c.IsoCode).Any();
                if (exists) continue;

                var dest = new Destination
                {
                    Id = Guid.NewGuid(),
                    CountryName = c.Name,
                    City = c.Capital ?? "N/A",
                    IsoCode = c.IsoCode,
                    DefaultCurrency = string.IsNullOrWhiteSpace(c.CurrencyCode) ? "USD" : c.CurrencyCode,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude
                };

                _repo.Insert(dest);
                count++;
            }
            return count;
        }
    }
}
