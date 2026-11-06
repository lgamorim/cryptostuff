namespace CryptoStuff.Core.Alerts;

/// <summary>
/// Stores price alerts. A unit of work: <see cref="Add"/>, <see cref="Remove"/>,
/// and changes to an alert returned by <see cref="ListAsync"/> or
/// <see cref="FindAsync"/>, are written only by <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IAlertRepository
{
    /// <summary>Gets every alert, ordered by id.</summary>
    Task<IReadOnlyList<PriceAlert>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets every alert that is <see cref="AlertState.Armed"/>, ordered by id: the
    /// ones an evaluation has to check against current prices.
    /// </summary>
    Task<IReadOnlyList<PriceAlert>> ListArmedAsync(CancellationToken cancellationToken);

    /// <summary>Gets the alert with the given id, or null when none has it.</summary>
    Task<PriceAlert?> FindAsync(int id, CancellationToken cancellationToken);

    /// <summary>Marks a new alert to be inserted on the next save, which assigns its id.</summary>
    void Add(PriceAlert alert);

    /// <summary>Marks an alert to be deleted on the next save.</summary>
    void Remove(PriceAlert alert);

    /// <summary>
    /// Forgets an alert returned by this repository and any change made to it
    /// that has not been saved, so no later save writes it. A save that threw a
    /// <see cref="PersistenceConflictException"/> leaves the change that
    /// conflicted pending, so the next save would fail on it again; discarding
    /// that alert lets the rest of the work be saved.
    /// </summary>
    void Discard(PriceAlert alert);

    /// <summary>Writes every pending change, assigning ids to new alerts.</summary>
    /// <exception cref="PersistenceConflictException">
    /// The write lost a race with a concurrent write, detected through
    /// <see cref="PriceAlert.Version"/>, or the alert it changes or removes was
    /// deleted since it was read.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
