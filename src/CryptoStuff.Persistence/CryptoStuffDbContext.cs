using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence;

/// <summary>
/// The application's SQLite database. Each entity's mapping lives in its own
/// <see cref="IEntityTypeConfiguration{TEntity}"/> beside its repository and is
/// applied from this assembly, so <see cref="OnModelCreating"/> holds no
/// entity mapping of its own.
/// </summary>
public sealed class CryptoStuffDbContext(DbContextOptions<CryptoStuffDbContext> options) : DbContext(options)
{
    /// <summary>The stored portfolios, each with its transactions.</summary>
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    /// <summary>The stored watchlists.</summary>
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CryptoStuffDbContext).Assembly);
}
