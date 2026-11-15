using CryptoStuff.Core;
using Microsoft.Data.Sqlite;

namespace CryptoStuff.Persistence;

/// <summary>
/// Recognises a save that failed because another connection held the
/// database's lock for longer than the provider waits, and turns it into
/// Core's <see cref="PersistenceBusyException"/>. The provider reports it as
/// SQLite's BUSY or LOCKED result, wrapped by EF when a statement failed and
/// unwrapped when the save's own transaction could not begin, so the whole
/// chain of inner exceptions is searched.
/// </summary>
internal static class SqliteBusy
{
    // SQLite primary result codes: https://www.sqlite.org/rescode.html
    private const int Busy = 5;     // SQLITE_BUSY
    private const int Locked = 6;   // SQLITE_LOCKED

    public static bool IsBusy(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: Busy or Locked })
            {
                return true;
            }
        }

        return false;
    }

    public static PersistenceBusyException ToPersistenceBusy(Exception exception) =>
        new("The database is busy with another write; try again shortly.", exception);
}
