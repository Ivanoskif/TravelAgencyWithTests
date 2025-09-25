namespace TravelAgency.Domain.DTO
{
    public class PriceQuoteDTO
    {
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;

        public decimal Rate { get; set; }
        public decimal AmountBase { get; set; }
        public decimal AmountConverted { get; set; }

        public DateTime TimestampUtc { get; set; }

        public PriceQuoteDTO() { }

        public PriceQuoteDTO(string fromCurrency, string toCurrency,
                             decimal rate, decimal amountBase, decimal amountConverted,
                             DateTime timestampUtc)
        {
            FromCurrency = fromCurrency;
            ToCurrency = toCurrency;
            Rate = rate;
            AmountBase = amountBase;
            AmountConverted = amountConverted;
            TimestampUtc = timestampUtc;
        }
    }
}
