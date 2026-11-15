namespace CryptoStuff.Core.Watchlists;

/// <summary>
/// Stores watchlists. A unit of work: <see cref="Add"/>, <see cref="Remove"/>,
/// and changes to a watchlist returned by <see cref="ListAsync"/> or
/// <see cref="FindAsync"/> are written only by <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IWatchlistRepository
{
    /// <summary>Gets every watchlist, ordered by id.</summary>
    Task<IReadOnlyList<Watchlist>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets the watchlist with the given id, or null when none has it.</summary>
    Task<Watchlist?> FindAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a saved watchlist other than <paramref name="excludingId"/> already
    /// has <paramref name="name"/>, compared case-insensitively. Pass a null
    /// <paramref name="excludingId"/> to check every watchlist.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a watchlist with the given id is stored, read from the database
    /// rather than from the watchlists this repository has already returned, so
    /// it notices a watchlist another writer deleted since it was read.
    /// </summary>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);

    /// <summary>Marks a new watchlist to be inserted on the next save, which assigns its id.</summary>
    void Add(Watchlist watchlist);

    /// <summary>Marks a watchlist to be deleted on the next save.</summary>
    void Remove(Watchlist watchlist);

    /// <summary>
    /// Forgets a watchlist returned by this repository and any change made to it,
    /// that has not been saved, so no later save writes it. A save that
    /// threw a <see cref="PersistenceConflictException"/> leaves the change that
    /// conflicted pending, so the next save would fail on it again.
    /// </summary>
    void Discard(Watchlist watchlist);

    /// <summary>Writes every pending change.</summary>
    /// <exception cref="PersistenceConflictException">
    /// The write broke the unique-name rule or lost a race with a concurrent write.
    /// </exception>
    /// <exception cref="PersistenceBusyException">
    /// The database stayed locked by another writer longer than the provider
    /// waits; nothing was written.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
