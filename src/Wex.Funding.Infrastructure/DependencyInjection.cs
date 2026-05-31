using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Wex.Funding.Application.Cards;
using Wex.Funding.Application.Currencies;
using Wex.Funding.Application.Transactions;
using Wex.Funding.Infrastructure.Configuration;
using Wex.Funding.Infrastructure.ExternalServices.Treasury;
using Wex.Funding.Infrastructure.Persistence;
using Wex.Funding.Infrastructure.Persistence.Repositories;

namespace Wex.Funding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<WexDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("Postgres"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        services.Configure<TreasuryApiOptions>(
            configuration.GetSection(TreasuryApiOptions.SectionName));

        var treasuryOptions = configuration
            .GetSection(TreasuryApiOptions.SectionName)
            .Get<TreasuryApiOptions>() ?? new TreasuryApiOptions();

        services.AddMemoryCache();

        services.AddHttpClient<ITreasuryRatesClient, TreasuryRatesClient>(client =>
        {
            client.BaseAddress = new Uri(treasuryOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(treasuryOptions.TimeoutSeconds);
        })
        .AddPolicyHandler(GetRetryPolicy(treasuryOptions.RetryCount))
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
