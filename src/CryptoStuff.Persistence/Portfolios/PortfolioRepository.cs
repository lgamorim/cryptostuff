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
    public void Add(Portfolio portfolio) => context.Portfolios.Add(portfolio);

    /// <inheritdoc />
    public void Remove(Portfolio portfolio) => context.Portfolios.Remove(portfolio);

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
    }

    // A transaction row refers to its portfolio, and that is the only foreign
    // key a portfolio save can break. EF inserts a new portfolio together with
    // its transactions, so a violation means the portfolio row was deleted
    // after it was read: a lost race, whose version check the transaction's
    // insert reached too late to report.
    private static bool IsOwnerDeletedConcurrently(DbUpdateException exception) =>
        SqliteConflict.IsForeignKeyViolation(exception);
}
