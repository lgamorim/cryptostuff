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

    /// <summary>Gets the alert with the given id, or null when none has it.</summary>
    Task<PriceAlert?> FindAsync(int id, CancellationToken cancellationToken);

    /// <summary>Marks a new alert to be inserted on the next save, which assigns its id.</summary>
    void Add(PriceAlert alert);

    /// <summary>Marks an alert to be deleted on the next save.</summary>
    void Remove(PriceAlert alert);

    /// <summary>Writes every pending change, assigning ids to new alerts.</summary>
    /// <exception cref="PersistenceConflictException">
    /// The write lost a race with a concurrent write, detected through
    /// <see cref="PriceAlert.Version"/>, or the alert it changes or removes was
    /// deleted since it was read.
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
