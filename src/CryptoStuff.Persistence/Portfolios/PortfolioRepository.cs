using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence.Portfolios;

/// <summary>
/// Stores portfolios in the SQLite database through <see cref="CryptoStuffDbContext"/>.
/// A portfolio is always loaded with all of its transactions, so the ledger's
/// replay and any valuation run in memory; no query orders or sums a decimal
/// or timestamp column.
/// </summary>
public sealed class PortfolioRepository(CryptoStuffDbContext context) : IPortfolioRepository
{
    // The shadow foreign key each owned transaction row holds to its portfolio.
    private const string OwnerKey = "PortfolioId";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Portfolio>> ListAsync(CancellationToken cancellationToken) =>
        await context.Portfolios.OrderBy(p => p.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Portfolio?> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Portfolios.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    /// <remarks>The comparison runs under the name column's <c>NOCASE</c> collation.</remarks>
    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken) =>
        context.Portfolios.AnyAsync(
            p => p.Name == name && (excludingId == null || p.Id != excludingId),
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken) =>
        context.Portfolios.AnyAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public void Add(Portfolio portfolio) => context.Portfolios.Add(portfolio);

    /// <inheritdoc />
    public void Remove(Portfolio portfolio) => context.Portfolios.Remove(portfolio);

    /// <inheritdoc />
    /// <remarks>
    /// Detaches the portfolio and every transaction tracked as its own.
    /// Detaching the portfolio also detaches the transactions still in its
    /// ledger, but not the ones removed from it, which stay tracked for
    /// deletion: a later read through this context would put those stale
    /// copies back in place of the stored rows, so they are found by their
    /// foreign key and detached first. Only a saved portfolio can have removed
    /// transactions, so its own <see cref="Portfolio.Id"/> is the key to match.
    /// </remarks>
    public void Discard(Portfolio portfolio)
    {
        var removedTransactions = context.ChangeTracker.Entries<PortfolioTransaction>()
            .Where(entry => Equals(entry.Property(OwnerKey).CurrentValue, portfolio.Id))
            .ToList();
        foreach (var transaction in removedTransactions)
        {
            transaction.State = EntityState.Detached;
        }

        context.Entry(portfolio).State = EntityState.Detached;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqliteConflict.IsConflict(exception) || IsOwnerDeletedConcurrently(exception))
        {
            throw new PersistenceConflictException("The portfolio change conflicts with stored state.", exception);
        }
        catch (Exception exception) when (SqliteBusy.IsBusy(exception))
        {
            throw SqliteBusy.ToPersistenceBusy(exception);
        }
    }

    // A transaction row refers to its portfolio, and that is the only foreign
    // key a portfolio save can break. EF inserts a new portfolio together with
    // its transactions, so a violation means the portfolio row was deleted
    // after it was read: a lost race, whose version check the transaction's
    // insert reached too late to report.
    private static bool IsOwnerDeletedConcurrently(DbUpdateException exception) =>
        SqliteConflict.IsForeignKeyViolation(exception);
}
