using Microsoft.EntityFrameworkCore;
using Wex.Funding.Application.Transactions;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(WexDbContext db) : ITransactionRepository
{
    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await db.Transactions.AddAsync(transaction, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Transaction?> GetByIdAsync(TransactionId id, CancellationToken cancellationToken = default) =>
        db.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> ListByCardIdAsync(CardId cardId, CancellationToken cancellationToken = default)
    {
        var results = await db.Transactions
            .Where(t => t.CardId == cardId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        return results.AsReadOnly();
    }
}
