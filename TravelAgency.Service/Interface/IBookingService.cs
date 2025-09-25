using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TravelAgency.Domain.DTO;
using TravelAgency.Domain.Models;

namespace TravelAgency.Service.Interface
{
    public interface IBookingService
    {
        
        Booking Create(Booking b);
        Booking Update(Booking b);
        Booking Delete(Booking b);
        Booking? Get(Guid id);
        IEnumerable<Booking> All();

        
        Task<PriceQuoteDTO?> ConvertTotalAsync(Guid bookingId, string targetCurrency, CancellationToken ct = default);
        Task<int> CountBookedSeatsAsync(Guid packageId, CancellationToken ct = default);
        Task<(bool Ok, string? Error)> CreateBookingAsync(Guid customerId, Guid packageId, int peopleCount, CancellationToken ct = default);
        Task<IReadOnlyList<Booking>> GetByCustomerEmailAsync(string email, CancellationToken ct = default);
    }
}
