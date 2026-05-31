using FluentAssertions;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Events;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Tests;

public sealed class CardTests
{
    private static readonly CurrencyCode Usd = new("USD");

    [Fact]
    public void Create_assigns_new_identity()
    {
        var card = Card.Create(new Money(1000m, Usd));
        card.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_stores_credit_limit()
    {
        var limit = new Money(5000m, Usd);
        var card = Card.Create(limit);
        card.CreditLimit.Should().Be(limit);
    }

    [Fact]
    public void Create_raises_CardCreated_domain_event()
    {
        var card = Card.Create(new Money(1000m, Usd));
        card.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CardCreated>();
    }

    [Fact]
    public void Create_with_zero_limit_succeeds()
    {
        var act = () => Card.Create(new Money(0m, Usd));
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_with_negative_limit_throws_InvalidAmountException()
    {
        var act = () => Card.Create(new Money(-1m, Usd));
        act.Should().Throw<InvalidAmountException>();
    }
}
