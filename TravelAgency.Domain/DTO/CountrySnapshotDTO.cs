namespace TravelAgency.Domain.DTO;

public class CountrySnapshotDTO
{
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PrimaryLanguage { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public double PopulationMillions { get; set; }
    public string FlagUrl { get; set; } = string.Empty;

    public CountrySnapshotDTO() { }

    public CountrySnapshotDTO(string name, string region, string primaryLanguage,
                              string currencyCode, double populationMillions, string flagUrl)
    {
        Name = name;
        Region = region;
        PrimaryLanguage = primaryLanguage;
        CurrencyCode = currencyCode;
        PopulationMillions = populationMillions;
        FlagUrl = flagUrl;
    }
}
