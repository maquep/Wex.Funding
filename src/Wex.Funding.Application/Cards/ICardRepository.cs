using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Application.Cards;

public interface ICardRepository
{
    Task AddAsync(Card card, CancellationToken cancellationToken = default);
    Task<Card?> GetByIdAsync(CardId id, CancellationToken cancellationToken = default);
    Task<Money> SumTransactionsAsync(CardId cardId, CancellationToken cancellationToken = default);
}
