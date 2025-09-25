using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Interface;
using TravelAgency.Service.Interface;
using Microsoft.EntityFrameworkCore;



namespace TravelAgency.Service.Implementation
{
    public class PackageService : IPackageService
    {
        private readonly IRepository<Package> _repo;
        private readonly IRepository<Booking> _bookings;
        private readonly IWeatherService _weather;
        private readonly IExchangeRateService _fx;
        private readonly IPublicHolidayService _holidays;

        public PackageService(
            IRepository<Package> repo,
            IRepository<Booking> bookings,
            IWeatherService weather,
            IExchangeRateService fx,
            IPublicHolidayService holidays)
        {
            _repo = repo;
            _bookings = bookings;
            _weather = weather;
            _fx = fx;
            _holidays = holidays;
        }

        public Package Create(Package p)
        {
            return _repo.Insert(p);
        }

        public Package Update(Package p)
        {
            return _repo.Update(p);
        }

        public Package Delete(Package p)
        {
            return _repo.Delete(p);
        }

        public Package? Get(Guid id)
        {
            return _repo.Get(x => x, x => x.Id == id);
        }

        public IEnumerable<Package> All()
        {
            return _repo.GetAll(x => x);
        }


        public int RemainingSeats(Guid packageId)
        {
            var pkg = Get(packageId);
            if (pkg == null)
            {
                return 0;
            }

            var reserved = _bookings.GetAll(b => b.PeopleCount,
                predicate: b => b.PackageId == packageId && b.Status != "Cancelled").Sum();

            var remaining = pkg.AvailableSeats - reserved;
            return remaining < 0 ? 0 : remaining;
        }

        public async Task<WeatherWindowDTO?> GetWeatherWindowAsync(Guid packageId, CancellationToken ct = default)
        {
            var pkg = _repo.Get(
                selector: x => x,
                predicate: x => x.Id == packageId,
                include: q => q.Include(p => p.Destination)
            );

            if (pkg == null || pkg.Destination == null) return null;

            var lat = pkg.Destination.Latitude;
            var lon = pkg.Destination.Longitude;

            return await _weather.GetWeatherWindowAsync(lat, lon, pkg.StartDate, pkg.EndDate, ct);
        }


        public async Task<IReadOnlyList<PublicHolidayDTO>> GetHolidaysAsync(Guid packageId, CancellationToken ct = default)
        {
            var pkg = _repo.Get(
                selector: x => x,
                predicate: x => x.Id == packageId,
                include: q => q.Include(p => p.Destination)
            );

            if (pkg == null || pkg.Destination == null || string.IsNullOrWhiteSpace(pkg.Destination.IsoCode))
                return Array.Empty<PublicHolidayDTO>();

            return await _holidays.GetHolidaysInRangeAsync(
                pkg.Destination.IsoCode,
                pkg.StartDate,
                pkg.EndDate,
                ct
            );
        }

        public async Task<PriceQuoteDTO?> GetPriceQuoteAsync(Guid packageId, string toCurrency, CancellationToken ct = default)
        {
            var pkg = _repo.Get(x => x, x => x.Id == packageId, include: q => q.Include(p => p.Destination));
            if (pkg == null) return null;

            var from = "USD";
            var to = (toCurrency ?? "EUR").Trim().ToUpperInvariant();

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                return new PriceQuoteDTO
                {
                    FromCurrency = from,
                    ToCurrency = to,
                    Rate = 1m,
                    AmountBase = pkg.BasePrice,   
                    AmountConverted = pkg.BasePrice,
                    TimestampUtc = DateTime.UtcNow
                };
            }

            return await _fx.ConvertAsync(from, to, pkg.BasePrice, ct);
        }

        public async Task<int> GetRemainingSeatsAsync(Guid packageId, CancellationToken ct = default)
        {
            var pkg = _repo.Get(x => x, x => x.Id == packageId);
            return pkg?.AvailableSeats ?? 0;
        }

    }
}
