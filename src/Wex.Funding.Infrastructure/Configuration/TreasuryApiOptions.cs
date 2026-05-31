namespace Wex.Funding.Infrastructure.Configuration;

public sealed class TreasuryApiOptions
{
    public const string SectionName = "TreasuryApi";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;
    public int RetryCount { get; set; } = 3;
}
