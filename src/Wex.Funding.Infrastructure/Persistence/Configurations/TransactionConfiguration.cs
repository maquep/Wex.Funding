using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new TransactionId(value));

        builder.Property(t => t.CardId)
            .HasColumnName("card_id")
            .HasConversion(id => id.Value, value => new CardId(value))
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .HasColumnName("transaction_date")
            .HasColumnType("date")
            .IsRequired();

        builder.ComplexProperty(t => t.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(19,4)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("amount_currency")
                .HasMaxLength(3)
                .HasConversion(c => c.Value, v => new CurrencyCode(v))
                .IsRequired();
        });

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(t => t.CardId)
            .HasDatabaseName("ix_transactions_card_id");

        builder.HasIndex(t => new { t.CardId, t.TransactionDate })
            .HasDatabaseName("ix_transactions_card_id_date");
    }
}
