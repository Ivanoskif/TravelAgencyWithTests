using TravelAgency.Domain.DTO;

namespace TravelAgency.Service.Interface;

public interface IExchangeRateService
{
    Task<decimal?> GetRateAsync(string from, string to, CancellationToken ct = default);
    Task<PriceQuoteDTO?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct = default);
}
