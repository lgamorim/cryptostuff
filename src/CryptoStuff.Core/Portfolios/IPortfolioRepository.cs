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

    /// <summary>Marks a new portfolio to be inserted on the next save, which assigns its id.</summary>
    void Add(Portfolio portfolio);

    /// <summary>Marks a portfolio, with its transactions, to be deleted on the next save.</summary>
    void Remove(Portfolio portfolio);

    /// <summary>Writes every pending change, assigning ids to new portfolios and new transactions.</summary>
    /// <exception cref="PersistenceConflictException">
    /// The write broke the unique-name rule or lost a race with a concurrent
    /// write, detected through <see cref="Portfolio.Version"/>.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
