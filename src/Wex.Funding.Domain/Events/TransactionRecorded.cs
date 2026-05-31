using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Domain.Events;

public sealed record TransactionRecorded(
    TransactionId TransactionId,
    CardId CardId,
    Money Amount,
    DateOnly TransactionDate,
    DateTime OccurredAt);
