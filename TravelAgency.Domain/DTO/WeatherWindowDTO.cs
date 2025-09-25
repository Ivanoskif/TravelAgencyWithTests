namespace TravelAgency.Domain.DTO
{
    public class WeatherWindowDTO
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public double AverageMaxTempC { get; set; }
        public double AverageMinTempC { get; set; }
        public double AveragePrecipitationProbability { get; set; }

        public string Recommendation { get; set; } = string.Empty;

        public WeatherWindowDTO() { }

        public WeatherWindowDTO(
            DateOnly startDate,
            DateOnly endDate,
            double averageMaxTempC,
            double averageMinTempC,
            double averagePrecipitationProbability,
            string recommendation)
        {
            StartDate = startDate;
            EndDate = endDate;
            AverageMaxTempC = averageMaxTempC;
            AverageMinTempC = averageMinTempC;
            AveragePrecipitationProbability = averagePrecipitationProbability;
            Recommendation = recommendation;
        }
    }
}
