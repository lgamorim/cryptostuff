using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CryptoStuff.Persistence;

/// <summary>
/// Checks that every enumeration token stored in the database is one this
/// version can read. The value converters throw on an unknown token, so a
/// single row written by a newer version would otherwise fail every query
/// that reads it, an alert evaluation or a whole list among them; checking
/// once at start-up fails loudly instead, before any of that runs.
/// </summary>
internal static class StoredTokens
{
    private static readonly TokenColumn[] Columns =
    [
        new(typeof(PriceAlert), nameof(PriceAlert.Direction), "an alert direction", token => AlertDirectionParser.TryParse(token, out _)),
        new(typeof(PriceAlert), nameof(PriceAlert.State), "an alert state", token => AlertStateParser.TryParse(token, out _)),
        new(typeof(Portfolio), nameof(Portfolio.CostBasisMethod), "a cost-basis method", token => CostBasisMethodParser.TryParse(token, out _)),
        new(typeof(PortfolioTransaction), nameof(PortfolioTransaction.Kind), "a transaction kind", token => TransactionKindParser.TryParse(token, out _)),
    ];

    /// <exception cref="InvalidDataException">
    /// A token column holds a value this version does not know; the message
    /// names the table, the column and the value.
    /// </exception>
    public static async Task EnsureReadableAsync(CryptoStuffDbContext context, CancellationToken cancellationToken)
    {
        foreach (var column in Columns)
        {
            var (table, name) = Locate(context.Model, column);

            // Both names come from the model, never from input.
            var sql = string.Concat("SELECT DISTINCT \"", name, "\" AS \"Value\" FROM \"", table, "\"");
            var tokens = await context.Database.SqlQueryRaw<string>(sql).ToListAsync(cancellationToken);
            if (tokens.FirstOrDefault(token => !column.IsKnown(token)) is { } unknown)
            {
                throw new InvalidDataException(
                    $"{table}.{name} holds '{unknown}', which is not {column.Description} this version knows; the database may have been written by a newer version.");
            }
        }
    }

    private static (string Table, string Column) Locate(IModel model, TokenColumn column)
    {
        // `!` is safe on all three: each entity type and property is mapped in
        // CryptoStuffDbContext, which the columns above are written against.
        var entityType = model.GetEntityTypes().Single(type => type.ClrType == column.EntityType);
        var table = entityType.GetTableName()!;
        var property = entityType.FindProperty(column.Property)!;
        return (table, property.GetColumnName(StoreObjectIdentifier.Table(table))!);
    }

    private sealed record TokenColumn(Type EntityType, string Property, string Description, Func<string, bool> IsKnown);
}
