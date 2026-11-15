using CryptoStuff.Core.Alerts;
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
    /// <summary>The stored price alerts.</summary>
    public DbSet<PriceAlert> Alerts => Set<PriceAlert>();

    /// <summary>The stored portfolios, each with its transactions.</summary>
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    /// <summary>The stored watchlists.</summary>
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();

    /// <inheritdoc />
    /// <remarks>
    /// Registers <see cref="UnicodeNoCaseCollation"/> on every connection this
    /// context opens, the migration's included, since the name columns and
    /// their unique indexes are built on it.
    /// </remarks>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(UnicodeNoCaseCollation.Registration);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CryptoStuffDbContext).Assembly);
}
