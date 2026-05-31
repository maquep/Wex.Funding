using Wex.Funding.Application.Cards;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Exceptions;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Application.Transactions;

public sealed record RecordTransactionInput
    (
    Guid CardId,
    string Description,
    DateOnly TransactionDate,
    decimal Amount,
    string Currency);

public sealed record RecordTransactionOutput(
    Guid Id,
    Guid CardId,
    string Description,
    DateOnly TransactionDate,
    decimal Amount,
    string Currency,
    DateTime CreatedAt);

public sealed class RecordTransaction(ICardRepository cardRepository, ITransactionRepository transactionRepository)
{
    public async Task<RecordTransactionOutput> ExecuteAsync(RecordTransactionInput input,CancellationToken cancellationToken = default)
    {
        var cardId = new CardId(input.CardId);
        var card = await cardRepository.GetByIdAsync(cardId, cancellationToken);

        if (card is null)
        {
            throw new KeyNotFoundException($"Card {input.CardId} not found.");
        }

        var currency = new CurrencyCode(input.Currency);

        if (currency != card.CreditLimit.Currency)
        {
            throw new CurrencyMismatchException(
                currency.Value,
                card.CreditLimit.Currency.Value);
        }

        var amount = new Money(input.Amount, currency);
        var transaction = Transaction.Record(cardId, input.Description, input.TransactionDate, amount);

        await transactionRepository.AddAsync(transaction, cancellationToken);

        return new RecordTransactionOutput(
            transaction.Id.Value,
            transaction.CardId.Value,
            transaction.Description,
            transaction.TransactionDate,
            transaction.Amount.Amount,
            transaction.Amount.Currency.Value,
            transaction.CreatedAt);
    }
}
