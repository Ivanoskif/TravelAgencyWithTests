using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TravelAgency.Service.Interface
{
    public class PublicHolidayDTO
    {
        public DateOnly Date { get; set; }
        public string LocalName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public interface IPublicHolidayService
    {
        Task<IReadOnlyList<PublicHolidayDTO>> GetHolidaysInRangeAsync(string iso2, DateOnly from, DateOnly to, CancellationToken ct = default);
    }
}
