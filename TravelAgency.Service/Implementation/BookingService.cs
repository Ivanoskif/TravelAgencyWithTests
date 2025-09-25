using Microsoft.EntityFrameworkCore;
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
    public class BookingService : IBookingService
    {
        private readonly IRepository<Booking> _repo;
        private readonly IRepository<Package> _packages;
        private readonly IExchangeRateService _fx;

        public BookingService(
            IRepository<Booking> repo,
            IRepository<Package> packages,
            IExchangeRateService fx)
        {
            _repo = repo;
            _packages = packages;
            _fx = fx;
        }

        public Booking Create(Booking b)
        {
            return _repo.Insert(b);
        }

        public Booking Update(Booking b)
        {
            return _repo.Update(b);
        }

        public Booking Delete(Booking b)
        {
            return _repo.Delete(b);
        }

        public Booking? Get(Guid id)
        {
            return _repo.Get(x => x, x => x.Id == id);
        }

        public IEnumerable<Booking> All()
        {
            return _repo.GetAll(
                selector: b => b,
                predicate: null,
                orderBy: q => q.OrderByDescending(b => b.CreatedAtUtc),
                include: q => q
                    .Include(b => b.Package)
                    .Include(b => b.Customer)
            );
        }

        public async Task<PriceQuoteDTO?> ConvertTotalAsync(Guid bookingId, string targetCurrency, CancellationToken ct = default)
        {
            var b = Get(bookingId);
            if (b == null)
            {
                return null;
            }

            var pkg = _packages.Get(x => x, x => x.Id == b.PackageId);
            var baseCur = pkg?.Destination?.DefaultCurrency ?? "EUR";
            var to = string.IsNullOrWhiteSpace(targetCurrency) ? baseCur : targetCurrency.ToUpperInvariant();

            return await _fx.ConvertAsync(baseCur, to, b.TotalBasePrice, ct);
        }

        public Task<int> CountBookedSeatsAsync(Guid packageId, CancellationToken ct = default)
        {
            var total = _repo
                .GetAll(b => b.PeopleCount,
                    b => b.PackageId == packageId && b.Status.ToString() != BookingStatus.Cancelled.ToString())
                .Sum();
            return Task.FromResult(total);
        }

        public async Task<(bool Ok, string? Error)> CreateBookingAsync(
    Guid customerId, Guid packageId, int peopleCount, CancellationToken ct = default)
        {
            if (peopleCount <= 0) return (false, "PeopleCount must be > 0");

            var pkg = _packages.Get(x => x, x => x.Id == packageId);
            if (pkg == null) return (false, "Package not found.");

            var remaining = pkg.AvailableSeats;
            if (remaining < peopleCount)
                return (false, $"Not enough seats. Remaining: {remaining}.");

            var totalBase = pkg.BasePrice * peopleCount;

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                PackageId = packageId,
                CustomerId = customerId,
                PeopleCount = peopleCount,
                TotalBasePrice = totalBase,
                CreatedAtUtc = DateTime.UtcNow,
                Status = BookingStatus.Confirmed.ToString(),
            };

            _repo.Insert(booking);        
            pkg.AvailableSeats -= peopleCount; 
            if (pkg.AvailableSeats < 0) pkg.AvailableSeats = 0;
            _packages.Update(pkg);            

            return (true, null);
        }

        public Task<IReadOnlyList<Booking>> GetByCustomerEmailAsync(string email, CancellationToken ct = default)
        {
            var list = _repo.GetAll(
                selector: b => b,
                predicate: b => b.Customer != null
                             && b.Customer.Email != null
                             && b.Customer.Email.ToLower() == email,
                orderBy: q => q.OrderByDescending(b => b.CreatedAtUtc),
                include: q => q
                    .Include(b => b.Package)
                    .Include(b => b.Customer)
            ).ToList();

            return Task.FromResult((IReadOnlyList<Booking>)list);
        }


    }
}
