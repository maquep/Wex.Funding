using FluentAssertions;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Tests;

public sealed class CurrencyCodeTests
{
    [Theory]
    [InlineData("USD")]
    [InlineData("AUD")]
    [InlineData("EUR")]
    public void Valid_iso_code_creates_successfully(string code)
    {
        var currency = new CurrencyCode(code);
        currency.Value.Should().Be(code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("us")]
    [InlineData("USDT")]
    [InlineData("123")]
    [InlineData("usd")]
    public void Invalid_code_throws_InvalidCurrencyException(string code)
    {
        var act = () => new CurrencyCode(code);
        act.Should().Throw<InvalidCurrencyException>();
    }

    [Fact]
    public void Two_same_codes_are_equal()
    {
        var a = new CurrencyCode("USD");
        var b = new CurrencyCode("USD");
        a.Should().Be(b);
    }

    [Fact]
    public void Different_codes_are_not_equal()
    {
        var a = new CurrencyCode("USD");
        var b = new CurrencyCode("AUD");
        a.Should().NotBe(b);
    }
}
