using FluentAssertions;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Tests;

public sealed class MoneyTests
{
    private static readonly CurrencyCode Usd = new("USD");
    private static readonly CurrencyCode Aud = new("AUD");

    [Fact]
    public void Add_same_currency_returns_sum()
    {
        var a = new Money(100m, Usd);
        var b = new Money(50m, Usd);
        a.Add(b).Should().Be(new Money(150m, Usd));
    }

    [Fact]
    public void Subtract_same_currency_returns_difference()
    {
        var a = new Money(100m, Usd);
        var b = new Money(30m, Usd);
        a.Subtract(b).Should().Be(new Money(70m, Usd));
    }

    [Fact]
    public void Add_different_currencies_throws_CurrencyMismatchException()
    {
        var usd = new Money(100m, Usd);
        var aud = new Money(50m, Aud);
        var act = () => usd.Add(aud);
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Subtract_different_currencies_throws_CurrencyMismatchException()
    {
        var usd = new Money(100m, Usd);
        var aud = new Money(50m, Aud);
        var act = () => usd.Subtract(aud);
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Same_amount_and_currency_are_equal()
    {
        var a = new Money(100m, Usd);
        var b = new Money(100m, Usd);
        a.Should().Be(b);
    }

    [Fact]
    public void Different_amounts_are_not_equal()
    {
        var a = new Money(100m, Usd);
        var b = new Money(99m, Usd);
        a.Should().NotBe(b);
    }
}
