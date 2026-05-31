using Microsoft.EntityFrameworkCore;
using Wex.Funding.Application.Cards;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Infrastructure.Persistence.Repositories;

internal sealed class CardRepository(WexDbContext db) : ICardRepository
{
    public async Task AddAsync(Card card, CancellationToken cancellationToken = default)
    {
        await db.Cards.AddAsync(card, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Card?> GetByIdAsync(CardId id, CancellationToken cancellationToken = default) =>
        db.Cards.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Money> SumTransactionsAsync(CardId cardId, CancellationToken cancellationToken = default)
    {
        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            ?? throw new KeyNotFoundException($"Card {cardId} not found.");

        var total = await db.Transactions
            .Where(t => t.CardId == cardId)
            .SumAsync(t => t.Amount.Amount, cancellationToken);

        return new Money(total, card.CreditLimit.Currency);
    }
}
