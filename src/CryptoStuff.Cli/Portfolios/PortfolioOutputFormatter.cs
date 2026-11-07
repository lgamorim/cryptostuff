using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli.Portfolios;

/// <summary>Renders portfolio views as either indented JSON or human-readable text.</summary>
public static class PortfolioOutputFormatter
{
    // Enums travel as the kebab-case tokens Core parses, in JSON as in text; timestamps as UTC ending in Z.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower), new UtcTimestampJsonConverter() },
    };

    /// <summary>Formats one portfolio and its transactions.</summary>
    public static string Format(PortfolioView view, bool json) =>
        json
            ? JsonSerializer.Serialize(view, JsonOptions)
            : FormatText(view);

    /// <summary>Formats a portfolio's valuation.</summary>
    public static string Format(PortfolioValuationView view, bool json) =>
        json
            ? JsonSerializer.Serialize(view, JsonOptions)
            : FormatText(view);

    /// <summary>Formats the confirmation that portfolio <paramref name="id"/> was deleted.</summary>
    public static string FormatDeleted(int id, bool json) =>
        json
            ? JsonSerializer.Serialize(new { Id = id, Deleted = true }, JsonOptions)
            : $"Deleted portfolio {id}.";

    /// <summary>Formats the list of portfolios.</summary>
    public static string Format(IReadOnlyList<PortfolioView> views, bool json) =>
        json
            ? JsonSerializer.Serialize(views, JsonOptions)
            : FormatListText(views);

    private static string FormatListText(IReadOnlyList<PortfolioView> views) =>
        views.Count == 0
            ? "(no portfolios)"
            : string.Join('\n', views.Select(view =>
                $"{view.Id}: {view.Name} ({view.VsCurrency}, {Token(view.CostBasisMethod)}, {FormatTransactionCount(view.Transactions.Count)})"));

    private static string FormatText(PortfolioView view)
    {
        var lines = new List<string>
        {
            $"Id: {view.Id}",
            $"Name: {view.Name}",
            $"Currency: {view.VsCurrency}",
            $"Method: {Token(view.CostBasisMethod)}",
            $"Version: {view.Version}",
            view.Transactions.Count == 0 ? "Transactions: none" : "Transactions:",
        };

        lines.AddRange(view.Transactions.Select(transaction => FormatTransaction(transaction, view.VsCurrency)));

        return string.Join('\n', lines);
    }

    private static string FormatText(PortfolioValuationView view)
    {
        var lines = new List<string> { $"Portfolio: {view.PortfolioId} ({view.VsCurrency}, {Token(view.CostBasisMethod)})" };

        if (view.Coins.Count == 0)
        {
            lines.Add("(no holdings)");
        }

        lines.AddRange(view.Coins.Select(coin =>
            $"{coin.CoinId}: quantity {Invariant(coin.Quantity)}, "
            + FormatFigures(coin.MarketValue, coin.CostBasis, coin.UnrealizedProfitAndLoss, coin.RealizedProfitAndLoss)));
        lines.Add("Total: " + FormatFigures(
            view.Totals.MarketValue, view.Totals.CostBasis, view.Totals.UnrealizedProfitAndLoss, view.Totals.RealizedProfitAndLoss));

        return string.Join('\n', lines);
    }

    private static string FormatFigures(decimal marketValue, decimal costBasis, decimal unrealized, decimal realized) =>
        $"market value {Invariant(marketValue)}, cost basis {Invariant(costBasis)}, unrealized {Invariant(unrealized)}, realized {Invariant(realized)}";

    private static string FormatTransaction(PortfolioTransactionView transaction, string vsCurrency) =>
        $"  {transaction.Id}: {UtcTimestamp.Format(transaction.OccurredAt)} {Token(transaction.Kind)} {Invariant(transaction.Quantity)} {transaction.CoinId}"
        + $" @ {Invariant(transaction.UnitPrice)} {vsCurrency}, fee {Invariant(transaction.Fee)} {vsCurrency}";

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatTransactionCount(int count) => count == 1 ? "1 transaction" : $"{count} transactions";

    private static string Token<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}
