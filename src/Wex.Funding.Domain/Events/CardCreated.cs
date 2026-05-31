using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Domain.Events;

public sealed record CardCreated(CardId CardId, Money CreditLimit, DateTime OccurredAt);
