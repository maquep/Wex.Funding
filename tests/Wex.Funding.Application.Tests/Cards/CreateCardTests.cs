using FluentAssertions;
using NSubstitute;
using Wex.Funding.Application.Cards;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Application.Tests.Cards;

public sealed class CreateCardTests
{
    private readonly ICardRepository _repo = Substitute.For<ICardRepository>();
    private readonly CreateCard _sut;

    public CreateCardTests() => _sut = new CreateCard(_repo);

    [Fact]
    public async Task Execute_creates_card_and_returns_output()
    {
        var input = new CreateCardInput(1000m, "USD");

        var output = await _sut.ExecuteAsync(input);

        output.Id.Should().NotBe(Guid.Empty);
        output.CreditLimitAmount.Should().Be(1000m);
        output.CreditLimitCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task Execute_persists_card_via_repository()
    {
        await _sut.ExecuteAsync(new CreateCardInput(500m, "AUD"));

        await _repo.Received(1).AddAsync(Arg.Any<Card>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_with_invalid_currency_throws()
    {
        var act = async () => await _sut.ExecuteAsync(new CreateCardInput(500m, "xx"));

        await act.Should().ThrowAsync<InvalidCurrencyException>();
    }

    [Fact]
    public async Task Execute_with_negative_limit_throws_InvalidAmountException()
    {
        var act = async () => await _sut.ExecuteAsync(new CreateCardInput(-1m, "USD"));

        await act.Should().ThrowAsync<InvalidAmountException>();
    }
}
