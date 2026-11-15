namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// Stores portfolios with their transactions. A unit of work:
/// <see cref="Add"/>, <see cref="Remove"/>, and changes to a portfolio returned by
/// <see cref="ListAsync"/> or <see cref="FindAsync"/>, are written only by
/// <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IPortfolioRepository
{
    /// <summary>Gets every portfolio with its transactions, ordered by id.</summary>
    Task<IReadOnlyList<Portfolio>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets the portfolio with the given id and its transactions, or null when none has it.</summary>
    Task<Portfolio?> FindAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a saved portfolio other than <paramref name="excludingId"/> already
    /// has <paramref name="name"/>, compared case-insensitively. Pass a null
    /// <paramref name="excludingId"/> to check every portfolio.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a portfolio with the given id is stored, read from the database
    /// rather than from the portfolios this repository has already returned, so
    /// it notices a portfolio another writer deleted since it was read.
    /// </summary>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);

    /// <summary>Marks a new portfolio to be inserted on the next save, which assigns its id.</summary>
    void Add(Portfolio portfolio);

    /// <summary>Marks a portfolio, with its transactions, to be deleted on the next save.</summary>
    void Remove(Portfolio portfolio);

    /// <summary>
    /// Forgets a portfolio returned by this repository and any change made to it,
    /// its transactions included, that has not been saved, so no later save writes it. A save that
    /// threw a <see cref="PersistenceConflictException"/> leaves the change that
    /// conflicted pending, so the next save would fail on it again.
    /// </summary>
    void Discard(Portfolio portfolio);

    /// <summary>Writes every pending change, assigning ids to new portfolios and new transactions.</summary>
    /// <exception cref="PersistenceConflictException">
    /// The write broke the unique-name rule or lost a race with a concurrent
    /// write, detected through <see cref="Portfolio.Version"/> or, when both
    /// writes added a transaction, through the two taking the same
    /// <see cref="PortfolioTransaction.Sequence"/>.
    /// </exception>
    /// <exception cref="PersistenceBusyException">
    /// The database stayed locked by another writer longer than the provider
    /// waits; nothing was written.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
