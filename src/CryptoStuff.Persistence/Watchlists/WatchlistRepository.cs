using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence.Watchlists;

/// <summary>Stores watchlists in the SQLite database through <see cref="CryptoStuffDbContext"/>.</summary>
public sealed class WatchlistRepository(CryptoStuffDbContext context) : IWatchlistRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Watchlist>> ListAsync(CancellationToken cancellationToken) =>
        await context.Watchlists.OrderBy(w => w.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Watchlist?> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Watchlists.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    /// <remarks>The comparison runs under the name column's <c>NOCASE</c> collation.</remarks>
    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken) =>
        context.Watchlists.AnyAsync(
            w => w.Name == name && (excludingId == null || w.Id != excludingId),
            cancellationToken);

    /// <inheritdoc />
    public void Add(Watchlist watchlist) => context.Watchlists.Add(watchlist);

    /// <inheritdoc />
    public void Remove(Watchlist watchlist) => context.Watchlists.Remove(watchlist);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqliteConflict.IsConflict(exception))
        {
            throw new PersistenceConflictException("The watchlist change conflicts with stored state.", exception);
        }
    }
}
