using System.Text.Json.Serialization;

namespace Wex.Funding.Infrastructure.ExternalServices.Treasury;

internal sealed record TreasuryApiResponse(
    [property: JsonPropertyName("data")] List<TreasuryRateRecord>? Data);

internal sealed record TreasuryRateRecord(
    [property: JsonPropertyName("country_currency_desc")] string CountryCurrencyDesc,
    [property: JsonPropertyName("exchange_rate")] string ExchangeRate,
    [property: JsonPropertyName("record_date")] string RecordDate);
