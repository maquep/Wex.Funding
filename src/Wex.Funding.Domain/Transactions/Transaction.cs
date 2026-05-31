using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Events;
using Wex.Funding.Domain.Exceptions;

namespace Wex.Funding.Domain.Transactions;

public sealed class Transaction
{
    public TransactionId Id { get; private set; }
    public CardId CardId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateOnly TransactionDate { get; private set; }
    public Money Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<object> _domainEvents = [];
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    private Transaction() { }

    public static Transaction Record(
        CardId cardId,
        string description,
        DateOnly date,
        Money amount)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (amount.Amount <= 0)
        {
            throw new InvalidAmountException("Transaction amount must be greater than zero.");
        }

        if (date > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Transaction date cannot be in the future.", nameof(date));
        }

        var transaction = new Transaction
        {
            Id = TransactionId.New(),
            CardId = cardId,
            Description = description,
            TransactionDate = date,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        };

        transaction._domainEvents.Add(new TransactionRecorded(
            transaction.Id,
            cardId,
            amount,
            date,
            transaction.CreatedAt));

        return transaction;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
