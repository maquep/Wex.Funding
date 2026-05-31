using FluentAssertions;
using NSubstitute;
using Wex.Funding.Application.Cards;
using Wex.Funding.Application.Common;
using Wex.Funding.Application.Currencies;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Application.Tests.Cards;

public sealed class GetCardBalanceInCurrencyTests
{
    private readonly ICardRepository _cards = Substitute.For<ICardRepository>();
    private readonly ITreasuryRatesClient _treasury = Substitute.For<ITreasuryRatesClient>();
    private readonly GetCardBalanceInCurrency _sut;

    private static readonly Card UsdCard = Card.Create(new Money(1000m, new CurrencyCode("USD")));
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public GetCardBalanceInCurrencyTests()
    {
        _sut = new GetCardBalanceInCurrency(_cards, _treasury);
        _cards.GetByIdAsync(UsdCard.Id, Arg.Any<CancellationToken>()).Returns(UsdCard);
        _cards.SumTransactionsAsync(UsdCard.Id, Arg.Any<CancellationToken>())
            .Returns(new Money(200m, new CurrencyCode("USD")));
    }

    [Fact]
    public async Task Happy_path_returns_converted_balance()
    {
        _treasury.GetLatestRateAsync(Arg.Any<CurrencyCode>(), Arg.Any<CancellationToken>())
            .Returns(new RateLookupResult(true, 1.5m, Today));

        var result = await _sut.ExecuteAsync(new GetCardBalanceInCurrencyInput(UsdCard.Id.Value, "AUD"));

        result.IsOk.Should().BeTrue();
        var getCardBalance = result.Unwrap();
        getCardBalance.CreditLimitAmount.Should().Be(1000m);
        getCardBalance.TotalSpentAmount.Should().Be(200m);
        getCardBalance.AvailableBalanceAmount.Should().Be(800m);
        getCardBalance.ConvertedAvailableBalance.Should().BeApproximately(800m * 1.5m, 0.0001m);
        getCardBalance.TargetCurrency.Should().Be("AUD");
    }

    [Fact]
    public async Task Card_not_found_returns_CardNotFoundError()
    {
        _cards.GetByIdAsync(Arg.Any<CardId>(), Arg.Any<CancellationToken>()).Returns((Card?)null);

        var result = await _sut.ExecuteAsync(new GetCardBalanceInCurrencyInput(Guid.NewGuid(), "AUD"));

        result.IsErr.Should().BeTrue();
        result.UnwrapError().Should().BeOfType<CardNotFoundError>();
    }

    [Fact]
    public async Task Rate_not_available_returns_BalanceRateNotAvailableError_not_CardNotFoundError()
    {
        _treasury.GetLatestRateAsync(Arg.Any<CurrencyCode>(), Arg.Any<CancellationToken>())
            .Returns(new RateLookupResult(false, 0, default));

        var result = await _sut.ExecuteAsync(new GetCardBalanceInCurrencyInput(UsdCard.Id.Value, "AUD"));

        result.IsErr.Should().BeTrue();
        result.UnwrapError().Should().BeOfType<BalanceRateNotAvailableError>(
            "rate no available condition is differenr from missing card and must produce a distinct error type");
    }
}
