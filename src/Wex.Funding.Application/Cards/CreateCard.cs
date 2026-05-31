using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Application.Cards;

public sealed record CreateCardInput(decimal CreditLimitAmount, string CreditLimitCurrency);

public sealed record CreateCardOutput(Guid Id, decimal CreditLimitAmount, string CreditLimitCurrency, DateTime CreatedAt);

public sealed class CreateCard(ICardRepository cardRepository)
{
    public async Task<CreateCardOutput> ExecuteAsync(
        CreateCardInput input,
        CancellationToken cancellationToken = default)
    {
        var currency = new CurrencyCode(input.CreditLimitCurrency);
        var creditLimit = new Money(input.CreditLimitAmount, currency);
        var card = Card.Create(creditLimit);

        await cardRepository.AddAsync(card, cancellationToken);

        return new CreateCardOutput(
            card.Id.Value,
            card.CreditLimit.Amount,
            card.CreditLimit.Currency.Value,
            card.CreatedAt);
    }
}
