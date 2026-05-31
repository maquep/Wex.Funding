using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Events;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Cards;

public sealed class Card
{
    public CardId Id { get; private set; }
    public Money CreditLimit { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<object> _domainEvents = [];
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    private Card() { }

    public static Card Create(Money creditLimit)
    {
        if (creditLimit.Amount < 0)
            throw new InvalidAmountException("Credit limit must be greater than or equal to zero.");

        var card = new Card
        {
            Id = CardId.New(),
            CreditLimit = creditLimit,
            CreatedAt = DateTime.UtcNow
        };

        card._domainEvents.Add(new CardCreated(card.Id, creditLimit, card.CreatedAt));
        return card;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
