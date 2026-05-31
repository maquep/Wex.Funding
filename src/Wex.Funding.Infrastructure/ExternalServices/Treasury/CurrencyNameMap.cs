namespace Wex.Funding.Infrastructure.ExternalServices.Treasury;

public static class CurrencyNameMap
{
    // Maps ISO 4217 codes to Treasury API descriptive names (USD-to-X rates).
    // USD itself is the base currency and has no entry here — it cannot be a conversion target.
    // A production implementation would maintain this from a reference dataset or RateService endpoint.
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AUD"] = "Australia-Dollar",
        ["CAD"] = "Canada-Dollar",
        ["EUR"] = "Euro Zone-Euro",
        ["GBP"] = "United Kingdom-Pound",
        ["JPY"] = "Japan-Yen",
        ["NZD"] = "New Zealand-Dollar",
        ["CHF"] = "Switzerland-Franc",
        ["HKD"] = "Hong Kong-Dollar",
        ["SGD"] = "Singapore-Dollar",
        ["SEK"] = "Sweden-Krona",
        ["NOK"] = "Norway-Krone",
        ["DKK"] = "Denmark-Krone",
        ["MXN"] = "Mexico-Peso",
        ["BRL"] = "Brazil-Real",
        ["INR"] = "India-Rupee",
        ["CNY"] = "China-Renminbi",
        ["KRW"] = "South Korea-Won",
        ["ZAR"] = "South Africa-Rand",
    };

    // All recognised currencies: the conversion map plus USD (base currency).
    private static readonly HashSet<string> _knownCurrencies =
        new(_map.Keys, StringComparer.OrdinalIgnoreCase) { "USD" };

    /// <summary>Returns true if the code is a currency known to this system (valid for storage).</summary>
    public static bool IsKnown(string isoCode) => _knownCurrencies.Contains(isoCode);

    /// <summary>Returns true if the code can be used as a conversion target via the Treasury API.</summary>
    public static bool TryGet(string isoCode, out string treasuryName) =>
        _map.TryGetValue(isoCode, out treasuryName!);
}
