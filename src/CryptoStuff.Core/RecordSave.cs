namespace CryptoStuff.Core;

/// <summary>
/// The save every feature service makes of the one record it changed, given
/// as that record's repository operations so each service keeps its own
/// repository interface.
/// </summary>
internal static class RecordSave
{
    /// <summary>
    /// Saves the pending change. On a <see cref="PersistenceConflictException"/>
    /// it discards that change, so nothing stale stays pending, and tells a
    /// record another writer deleted from any other conflict. Any other
    /// exception, including cancellation and
    /// <see cref="PersistenceBusyException"/>, propagates.
    /// </summary>
    /// <param name="saveChangesAsync">Writes every pending change.</param>
    /// <param name="discard">Drops the record's pending change.</param>
    /// <param name="id">Reads the record's id, once its change is discarded.</param>
    /// <param name="existsAsync">Whether a record with the given id is stored.</param>
    /// <param name="cancellationToken">Cancels the save and the existence check.</param>
    public static async Task<SaveOutcome> SaveAsync(
        Func<CancellationToken, Task> saveChangesAsync,
        Action discard,
        Func<int> id,
        Func<int, CancellationToken, Task<bool>> existsAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            await saveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (PersistenceConflictException)
        {
            discard();

            // An id of zero is a record never saved, which no one can have deleted.
            var recordId = id();
            return recordId != 0 && !await existsAsync(recordId, cancellationToken)
                ? SaveOutcome.Deleted
                : SaveOutcome.Conflicted;
        }
    }
}
