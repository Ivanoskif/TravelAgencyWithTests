using TravelAgency.Domain.DTO;

namespace TravelAgency.Service.Interface;

public interface IWeatherService
{
    Task<WeatherWindowDTO?> GetWeatherWindowAsync(
    double latitude,
    double longitude,
    DateOnly from,
    DateOnly to,
    CancellationToken ct = default);
}
