using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoStuff.Persistence;

/// <summary>
/// Recognises the save failures a repository reports as a
/// <see cref="Core.PersistenceConflictException"/>: a unique or primary-key
/// violation, or a concurrency check that found the row changed or gone. Other
/// constraint failures, foreign keys among them, are programming errors rather
/// than conflicts and propagate unchanged, unless a repository knows its only
/// foreign key points at rows that another writer can delete, and so asks
/// <see cref="IsForeignKeyViolation"/> as well.
/// </summary>
internal static class SqliteConflict
{
    // SQLite extended result codes: https://www.sqlite.org/rescode.html
    private const int UniqueViolation = 2067;      // SQLITE_CONSTRAINT_UNIQUE
    private const int PrimaryKeyViolation = 1555;  // SQLITE_CONSTRAINT_PRIMARYKEY
    private const int ForeignKeyViolation = 787;   // SQLITE_CONSTRAINT_FOREIGNKEY

    public static bool IsConflict(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is SqliteException { SqliteExtendedErrorCode: UniqueViolation or PrimaryKeyViolation };

    public static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: ForeignKeyViolation };
}
