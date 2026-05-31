using FluentAssertions;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Events;
using Wex.Funding.Domain.Exceptions;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Domain.Tests;

public sealed class TransactionTests
{
    private static readonly CardId AnyCardId = CardId.New();
    private static readonly CurrencyCode Usd = new("USD");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void Record_assigns_new_identity()
    {
        var tx = Transaction.Record(AnyCardId, "Coffee", Today, new Money(5m, Usd));
        tx.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Record_raises_TransactionRecorded_domain_event()
    {
        var tx = Transaction.Record(AnyCardId, "Coffee", Today, new Money(5m, Usd));
        tx.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TransactionRecorded>();
    }

    [Fact]
    public void Record_with_zero_amount_throws_InvalidAmountException()
    {
        var act = () => Transaction.Record(AnyCardId, "Coffee", Today, new Money(0m, Usd));
        act.Should().Throw<InvalidAmountException>();
    }

    [Fact]
    public void Record_with_negative_amount_throws_InvalidAmountException()
    {
        var act = () => Transaction.Record(AnyCardId, "Refund", Today, new Money(-10m, Usd));
        act.Should().Throw<InvalidAmountException>();
    }

    [Fact]
    public void Record_with_empty_description_throws_ArgumentException()
    {
        var act = () => Transaction.Record(AnyCardId, "", Today, new Money(10m, Usd));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_with_whitespace_description_throws_ArgumentException()
    {
        var act = () => Transaction.Record(AnyCardId, "   ", Today, new Money(10m, Usd));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_with_future_date_throws_ArgumentException()
    {
        var tomorrow = Today.AddDays(1);
        var act = () => Transaction.Record(AnyCardId, "Future", tomorrow, new Money(10m, Usd));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_with_today_date_succeeds()
    {
        var act = () => Transaction.Record(AnyCardId, "Today's purchase", Today, new Money(10m, Usd));
        act.Should().NotThrow();
    }

    [Fact]
    public void Record_with_past_date_succeeds()
    {
        var pastDate = Today.AddDays(-30);
        var act = () => Transaction.Record(AnyCardId, "Old purchase", pastDate, new Money(10m, Usd));
        act.Should().NotThrow();
    }
}
