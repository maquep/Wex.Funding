using Microsoft.EntityFrameworkCore;
using Wex.Funding.Domain.Cards;
using Wex.Funding.Domain.Transactions;

namespace Wex.Funding.Infrastructure.Persistence;

public sealed class WexDbContext(DbContextOptions<WexDbContext> options) : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WexDbContext).Assembly);
    }
}
