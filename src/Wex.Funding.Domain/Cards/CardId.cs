namespace Wex.Funding.Domain.Cards;

public readonly record struct CardId
{
    public Guid Value { get; }

    public CardId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("CardId must not be empty.", nameof(value));
        }
            
        Value = value;
    }

    public static CardId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
