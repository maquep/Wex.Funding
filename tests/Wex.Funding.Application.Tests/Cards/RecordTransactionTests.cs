using FluentAssertions;
using NSubstitute;
using Wex.Funding.Application.Cards;
using Wex.Funding.Application.Transactions;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Exceptions;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Application.Tests.Cards;

public sealed class RecordTransactionTests
{
    private readonly ICardRepository _cards = Substitute.For<ICardRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly RecordTransaction _sut;

    private static readonly Card UsdCard = Card.Create(new Money(5000m, new CurrencyCode("USD")));
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public RecordTransactionTests()
    {
        _sut = new RecordTransaction(_cards, _transactions);
        _cards.GetByIdAsync(UsdCard.Id, Arg.Any<CancellationToken>()).Returns(UsdCard);
    }

    [Fact]
    public async Task Execute_records_transaction_and_returns_output()
    {
        var inputTransaction = new RecordTransactionInput(UsdCard.Id.Value, "Coffee", Today, 5m, "USD");

        var outputTransaction = await _sut.ExecuteAsync(inputTransaction);

        outputTransaction.Id.Should().NotBe(Guid.Empty);
        outputTransaction.CardId.Should().Be(UsdCard.Id.Value);
        outputTransaction.Amount.Should().Be(5m);
        outputTransaction.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Execute_persists_transaction_via_repository()
    {
        await _sut.ExecuteAsync(new RecordTransactionInput(UsdCard.Id.Value, "Lunch", Today, 20m, "USD"));

        await _transactions.Received(1).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Currency_mismatch_between_card_and_transaction_throws()
    {
        var act = async () => await _sut.ExecuteAsync(
            new RecordTransactionInput(UsdCard.Id.Value, "Foreign", Today, 50m, "AUD"));

        await act.Should().ThrowAsync<CurrencyMismatchException>();
    }

    [Fact]
    public async Task Card_not_found_throws_KeyNotFoundException()
    {
        _cards.GetByIdAsync(Arg.Any<CardId>(), Arg.Any<CancellationToken>()).Returns((Card?)null);

        var act = async () => await _sut.ExecuteAsync(
            new RecordTransactionInput(Guid.NewGuid(), "Test", Today, 10m, "USD"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
