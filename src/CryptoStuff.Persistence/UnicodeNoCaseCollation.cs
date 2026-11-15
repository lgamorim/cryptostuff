using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CryptoStuff.Persistence;

/// <summary>
/// The collation the name columns use: case-insensitive across Unicode,
/// where SQLite's own <c>NOCASE</c> folds only the ASCII letters, so
/// <c>Ação</c> and <c>AÇÃO</c> are the same name. SQLite knows a custom
/// collation only on a connection that registered it, so this interceptor
/// registers it on every connection a <see cref="CryptoStuffDbContext"/>
/// opens. A tool that has not, such as the <c>sqlite3</c> shell, cannot write
/// a name or compare names, since both go through the unique index.
/// </summary>
internal sealed class UnicodeNoCaseCollation : DbConnectionInterceptor
{
    /// <summary>The collation's name, as the schema refers to it.</summary>
    public const string Name = "UNICODE_NOCASE";

    private UnicodeNoCaseCollation()
    {
    }

    /// <summary>The one instance every context adds, so EF sees the same options each time.</summary>
    public static UnicodeNoCaseCollation Registration { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Register(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Register(connection);
        return Task.CompletedTask;
    }

    // Ordinal comparison of the characters' invariant upper-case forms: the
    // comparison Core's own name checks describe, and a consistent total order,
    // which SQLite requires of a collation an index is built on. Letters that
    // differ by more than case, such as a and ã, stay different.
    private static void Register(DbConnection connection) =>
        ((SqliteConnection)connection).CreateCollation(
            Name,
            (first, second) => string.Compare(first, second, StringComparison.OrdinalIgnoreCase));
}
