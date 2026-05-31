using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Wex.Funding.Api.Tests;

/// <summary>
/// End-to-end integration test using WebApplicationFactory + Testcontainers (real Postgres)
/// + WireMock (stubbed Treasury API). Verifies the full request path: create card →
/// record transaction → retrieve in target currency.
/// </summary>
public sealed class EndToEndTests(WexApiFactory factory) : IClassFixture<WexApiFactory>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Full_flow_create_card_record_transaction_get_in_target_currency()
    {
        var client = factory.ApiClient;

        // 1. Create a card
        var createCardResponse = await client.PostAsJsonAsync("/api/cards", new
        {
            creditLimitAmount = 1000.00m,
            creditLimitCurrency = "USD"
        });

        createCardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var cardBody = await createCardResponse.Content.ReadAsStringAsync();
        var card = JsonDocument.Parse(cardBody).RootElement;
        var cardId = card.GetProperty("id").GetString();
        cardId.Should().NotBeNullOrEmpty();

        // 2. Record a transaction against that card
        var recordTxResponse = await client.PostAsJsonAsync($"/api/cards/{cardId}/transactions", new
        {
            description = "Hotel booking",
            transactionDate = "2024-03-15",
            amount = 200.00m,
            currency = "USD"
        });

        recordTxResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var txBody = await recordTxResponse.Content.ReadAsStringAsync();
        var tx = JsonDocument.Parse(txBody).RootElement;
        var txId = tx.GetProperty("id").GetString();
        txId.Should().NotBeNullOrEmpty();

        // 3. Retrieve the transaction in AUD (WireMock returns 1.501 rate)
        var getInCurrencyResponse = await client.GetAsync($"/api/transactions/{txId}?currency=AUD");

        getInCurrencyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var convertedBody = await getInCurrencyResponse.Content.ReadFromJsonAsync<ConvertedTransactionDto>(JsonOpts);

        convertedBody.Should().NotBeNull();
        convertedBody!.TargetCurrency.Should().Be("AUD");
        convertedBody.OriginalAmount.Should().Be(200.00m);
        convertedBody.ConvertedAmount.Should().BeApproximately(200m * 1.501m, 0.001m);
        convertedBody.ExchangeRate.Should().BeApproximately(1.501m, 0.0001m);
    }

    [Fact]
    public async Task Create_card_with_invalid_currency_format_returns_400()
    {
        var response = await factory.ApiClient.PostAsJsonAsync("/api/cards", new
        {
            creditLimitAmount = 1000m,
            creditLimitCurrency = "invalid"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_card_with_unsupported_currency_returns_400()
    {
        var response = await factory.ApiClient.PostAsJsonAsync("/api/cards", new
        {
            creditLimitAmount = 1000m,
            creditLimitCurrency = "ZZZ"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_transaction_with_unsupported_currency_returns_400()
    {
        var response = await factory.ApiClient.PostAsJsonAsync(
            $"/api/cards/{Guid.NewGuid()}/transactions",
            new
            {
                description = "Test",
                transactionDate = "2024-01-01",
                amount = 10m,
                currency = "ZZZ"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Record_transaction_for_unknown_card_returns_404()
    {
        var response = await factory.ApiClient.PostAsJsonAsync(
            $"/api/cards/{Guid.NewGuid()}/transactions",
            new
            {
                description = "Test",
                transactionDate = "2024-01-01",
                amount = 10m,
                currency = "USD"
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_transaction_for_unknown_id_returns_404()
    {
        var response = await factory.ApiClient.GetAsync($"/api/transactions/{Guid.NewGuid()}?currency=AUD");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_endpoint_returns_healthy()
    {
        var response = await factory.ApiClient.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record ConvertedTransactionDto(
        Guid Id,
        string Description,
        DateOnly TransactionDate,
        decimal OriginalAmount,
        string OriginalCurrency,
        decimal ExchangeRate,
        DateOnly RateRecordDate,
        decimal ConvertedAmount,
        string TargetCurrency);
}
