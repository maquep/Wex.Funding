using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Application.Transactions;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> ListByCardIdAsync(CardId cardId, CancellationToken cancellationToken = default);
}
