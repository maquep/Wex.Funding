using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Wex.Funding.Infrastructure.Persistence;

namespace Wex.Funding.Api.Tests;

public sealed class WexApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private WireMockServer? _wireMock;

    public HttpClient ApiClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _wireMock = WireMockServer.Start();
        StubTreasuryRate("Australia-Dollar", "1.5010", "2024-03-15");

        ApiClient = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WexDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        _wireMock?.Stop();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the DbContext registration with the Testcontainers connection string
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<WexDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<WexDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString())
                    .UseSnakeCaseNamingConvention());
        });

        builder.UseSetting("TreasuryApi:BaseUrl",
            $"{_wireMock?.Url}/services/api/fiscal_service/v1/accounting/od/rates_of_exchange");
        builder.UseSetting("TreasuryApi:TimeoutSeconds", "10");
        builder.UseSetting("TreasuryApi:RetryCount", "0");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
    }

    private void StubTreasuryRate(string currencyDesc, string rate, string recordDate)
    {
        _wireMock!.Given(
            Request.Create()
                .WithPath("/services/api/fiscal_service/v1/accounting/od/rates_of_exchange")
                .UsingGet())
        .RespondWith(
            Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                {
                  "data": [
                    {
                      "country_currency_desc": "{{currencyDesc}}",
                      "exchange_rate": "{{rate}}",
                      "record_date": "{{recordDate}}"
                    }
                  ]
                }
                """));
    }
}
