using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Wex.Funding.Application.Currencies;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Infrastructure.ExternalServices.Treasury;

internal sealed class TreasuryRatesClient(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<TreasuryRatesClient> logger) : ITreasuryRatesClient
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private const int SixMonthWindowDays = 183;

    public async Task<RateLookupResult> GetRateOnOrBeforeAsync(
        CurrencyCode currencyCode,
        DateOnly transactionDate,
        CancellationToken cancellationToken = default)
    {
        if (currencyCode.Value.Equals("USD", StringComparison.OrdinalIgnoreCase))
            return new RateLookupResult(true, 1m, transactionDate);

        if (!CurrencyNameMap.TryGet(currencyCode.Value, out var treasuryName))
        {
            logger.LogWarning("No Treasury API mapping for currency {Currency}", currencyCode.Value);
            return new RateLookupResult(false, 0, default);
        }

        var cacheKey = $"rate:{currencyCode.Value}:on-or-before:{transactionDate:yyyy-MM-dd}";

        if (cache.TryGetValue<RateLookupResult>(cacheKey, out var cached))
        {
            return cached!;
        }

        var windowStart = transactionDate.AddDays(-SixMonthWindowDays);
        var url = BuildUrl(treasuryName, windowStart, transactionDate, pageSize: 1);

        logger.LogInformation("Getting Treasury rate for {Currency} on or before {Date}", currencyCode.Value, transactionDate);

        var response = await httpClient.GetFromJsonAsync<TreasuryApiResponse>(url, cancellationToken);
        var record = response?.Data?.FirstOrDefault();

        RateLookupResult result;

        if (record is null || !decimal.TryParse(record.ExchangeRate, out var rate))
        {
            result = new RateLookupResult(false, 0, default);
        }
        else
        {
            var recordDate = DateOnly.Parse(record.RecordDate);

            // 6-month boundary validation
            var daysBack = transactionDate.DayNumber - recordDate.DayNumber;
            result = daysBack <= SixMonthWindowDays
                ? new RateLookupResult(true, rate, recordDate)
                : new RateLookupResult(false, 0, default);
        }

        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<RateLookupResult> GetLatestRateAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken = default)
    {
        if (currencyCode.Value.Equals("USD", StringComparison.OrdinalIgnoreCase))
            return new RateLookupResult(true, 1m, DateOnly.FromDateTime(DateTime.UtcNow));

        if (!CurrencyNameMap.TryGet(currencyCode.Value, out var treasuryName))
        {
            logger.LogWarning("No Treasury API mapping for currency {Currency}", currencyCode.Value);
            return new RateLookupResult(false, 0, default);
        }

        var cacheKey = $"rate:{currencyCode.Value}:latest";

        if (cache.TryGetValue<RateLookupResult>(cacheKey, out var cached))
        {
            return cached!;
        }

        var url = $"?fields=country_currency_desc,exchange_rate,record_date" +
                  $"&filter=country_currency_desc:eq:{Uri.EscapeDataString(treasuryName)}" +
                  $"&sort=-record_date" +
                  $"&page[size]=1";

        logger.LogInformation("Getting latest Treasury rate for {Currency}", currencyCode.Value);

        var response = await httpClient.GetFromJsonAsync<TreasuryApiResponse>(url, cancellationToken);
        var record = response?.Data?.FirstOrDefault();

        RateLookupResult result;

        if (record is null || !decimal.TryParse(record.ExchangeRate, out var rate))
        {
            result = new RateLookupResult(false, 0, default);
        }
        else
        {
            result = new RateLookupResult(true, rate, DateOnly.Parse(record.RecordDate));
        }

        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private static string BuildUrl(string treasuryName, DateOnly from, DateOnly to, int pageSize) =>
        $"?fields=country_currency_desc,exchange_rate,record_date" +
        $"&filter=country_currency_desc:eq:{Uri.EscapeDataString(treasuryName)}" +
        $",record_date:lte:{to:yyyy-MM-dd}" +
        $",record_date:gte:{from:yyyy-MM-dd}" +
        $"&sort=-record_date" +
        $"&page[size]={pageSize}";
}
