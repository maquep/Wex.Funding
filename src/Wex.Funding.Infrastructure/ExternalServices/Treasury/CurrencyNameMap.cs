namespace Wex.Funding.Infrastructure.ExternalServices.Treasury;

public static class CurrencyNameMap
{
    // Maps ISO 4217 codes to Treasury API descriptive names (USD-to-X rates).
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

    public static bool IsKnown(string isoCode) => _knownCurrencies.Contains(isoCode);

    public static bool TryGet(string isoCode, out string treasuryName) =>
        _map.TryGetValue(isoCode, out treasuryName!);
}
