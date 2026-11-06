using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence.Alerts;

/// <summary>Stores price alerts in the SQLite database through <see cref="CryptoStuffDbContext"/>.</summary>
public sealed class AlertRepository(CryptoStuffDbContext context) : IAlertRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceAlert>> ListAsync(CancellationToken cancellationToken) =>
        await context.Alerts.OrderBy(a => a.Id).ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Filters on the state token column, a text equality SQLite handles
    /// exactly; ordering is by the integer id, never a decimal or timestamp.
    /// </remarks>
    public async Task<IReadOnlyList<PriceAlert>> ListArmedAsync(CancellationToken cancellationToken) =>
        await context.Alerts
            .Where(a => a.State == AlertState.Armed)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PriceAlert?> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Alerts.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public void Add(PriceAlert alert) => context.Alerts.Add(alert);

    /// <inheritdoc />
    public void Remove(PriceAlert alert) => context.Alerts.Remove(alert);

    /// <inheritdoc />
    /// <remarks>
    /// Detaches the alert from the context. A save that failed on it leaves the
    /// change tracked and pending, so without this every later save would fail
    /// on the same stale alert.
    /// </remarks>
    public void Discard(PriceAlert alert) => context.Entry(alert).State = EntityState.Detached;

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (SqliteConflict.IsConflict(exception))
        {
            throw new PersistenceConflictException("The alert change conflicts with stored state.", exception);
        }
    }
}
