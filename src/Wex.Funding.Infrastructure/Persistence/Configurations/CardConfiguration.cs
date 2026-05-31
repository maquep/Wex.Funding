using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Currencies;

namespace Wex.Funding.Infrastructure.Persistence.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CardId(value));

        builder.ComplexProperty(c => c.CreditLimit, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("credit_limit_amount")
                .HasColumnType("numeric(19,4)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("credit_limit_currency")
                .HasMaxLength(3)
                .HasConversion(c => c.Value, v => new CurrencyCode(v))
                .IsRequired();
        });

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
