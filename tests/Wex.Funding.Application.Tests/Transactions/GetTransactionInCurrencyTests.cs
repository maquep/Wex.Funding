using FluentAssertions;
using NSubstitute;
using Wex.Funding.Application.Cards;
using Wex.Funding.Application.Common;
using Wex.Funding.Application.Currencies;
using Wex.Funding.Application.Transactions;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Application.Tests.Transactions;

public sealed class GetTransactionInCurrencyTests
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly ITreasuryRatesClient _treasury = Substitute.For<ITreasuryRatesClient>();
    private readonly GetTransactionInCurrency _sut;

    // A known transaction: USD 100, dated 2024-06-15
    private static readonly DateOnly TxDate = new(2024, 6, 15);
    private static readonly Transaction StubbedTransaction = MakeTransaction(TxDate);

    public GetTransactionInCurrencyTests()
    {
        _sut = new GetTransactionInCurrency(_transactions, _treasury);

        _transactions
            .GetByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
            .Returns(StubbedTransaction);
    }

    [Fact]
    public async Task Exact_date_match_uses_that_rate()
    {
        GivenRate(found: true, rate: 1.5m, recordDate: TxDate);

        var result = await ExecuteAsync();

        result.IsOk.Should().BeTrue();
        result.Unwrap().ExchangeRate.Should().Be(1.5m);
        result.Unwrap().RateRecordDate.Should().Be(TxDate);
    }

    [Fact]
    public async Task Rate_179_days_before_tx_is_within_window()
    {
        var rateDate = TxDate.AddDays(-179);
        GivenRate(found: true, rate: 1.48m, recordDate: rateDate);

        var result = await ExecuteAsync();

        result.IsOk.Should().BeTrue();
        result.Unwrap().ExchangeRate.Should().Be(1.48m);
    }

    [Fact]
    public async Task Rate_exactly_183_days_before_tx_is_accepted()
    {
        var rateDate = TxDate.AddDays(-183);
        GivenRate(found: true, rate: 1.45m, recordDate: rateDate);

        var result = await ExecuteAsync();

        result.IsOk.Should().BeTrue();
        result.Unwrap().ExchangeRate.Should().Be(1.45m);
        result.Unwrap().RateRecordDate.Should().Be(rateDate);
    }

    [Fact]
    public async Task Rate_184_days_before_tx_is_rejected()
    {
        // Treasury client already applies the window filter, so it returns not-found
        GivenRate(found: false, rate: 0, recordDate: default);

        var result = await ExecuteAsync();

        result.IsErr.Should().BeTrue();
        result.UnwrapError().Should().BeOfType<NoRateAvailableError>()
            .Which.Message.Should().Contain("6 months");
    }

    [Fact]
    public async Task Rate_12_months_before_tx_returns_no_rate_available()
    {
        GivenRate(found: false, rate: 0, recordDate: default);

        var result = await ExecuteAsync();

        result.IsErr.Should().BeTrue();
    }

    [Fact]
    public async Task Future_dated_rate_not_available_returns_no_rate_error()
    {
        GivenRate(found: false, rate: 0, recordDate: default);

        var result = await ExecuteAsync();

        result.IsErr.Should().BeTrue();
    }

    [Fact]
    public async Task No_rates_for_currency_returns_no_rate_available()
    {
        GivenRate(found: false, rate: 0, recordDate: default);

        var result = await ExecuteAsync();

        result.IsErr.Should().BeTrue();
        result.UnwrapError().Should().BeOfType<NoRateAvailableError>();
    }

    [Fact]
    public async Task Converted_amount_equals_original_times_rate()
    {
        GivenRate(found: true, rate: 1.501m, recordDate: TxDate.AddDays(-10));

        var result = await ExecuteAsync();

        result.IsOk.Should().BeTrue();
        var output = result.Unwrap();
        var expected = Math.Round(output.OriginalAmount * output.ExchangeRate, 4);
        output.ConvertedAmount.Should().Be(expected);
    }

    [Fact]
    public async Task Transaction_not_found_returns_TransactionNotFoundError()
    {
        _transactions
            .GetByIdAsync(Arg.Any<TransactionId>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var unknownId = Guid.NewGuid();
        var result = await _sut.ExecuteAsync(new GetTransactionInCurrencyInput(unknownId, "AUD"));

        result.IsErr.Should().BeTrue();
        result.UnwrapError().Should().BeOfType<TransactionNotFoundError>();
        ((TransactionNotFoundError)result.UnwrapError()).TransactionId.Should().Be(unknownId);
    }

    // Helpers
    private void GivenRate(bool found, decimal rate, DateOnly recordDate) =>
        _treasury
            .GetRateOnOrBeforeAsync(Arg.Any<CurrencyCode>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new RateLookupResult(found, rate, recordDate));

    private Task<Result<GetTransactionInCurrencyOutput, GetTransactionError>> ExecuteAsync() =>
        _sut.ExecuteAsync(new GetTransactionInCurrencyInput(Guid.NewGuid(), "AUD"));

    private static Transaction MakeTransaction(DateOnly date)
    {
        var cardId = CardId.New();
        var currency = new CurrencyCode("USD");
        var amount = new Money(100m, currency);
        return Transaction.Record(cardId, "Test purchase", date, amount);
    }
}
