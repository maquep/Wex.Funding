using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Application.Currencies;

public sealed record RateLookupResult(
    bool Found,
    decimal Rate,
    DateOnly RecordDate);

public interface ITreasuryRatesClient
{
    Task<RateLookupResult> GetRateOnOrBeforeAsync(
        CurrencyCode target,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default);

    Task<RateLookupResult> GetLatestRateAsync(
        CurrencyCode target,
        CancellationToken cancellationToken = default);
}
