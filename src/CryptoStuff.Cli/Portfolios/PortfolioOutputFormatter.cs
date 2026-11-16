using System.Globalization;
using System.Text.Json;
using CryptoStuff.Composition;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli.Portfolios;

/// <summary>Renders portfolio views as either indented JSON or human-readable text.</summary>
public static class PortfolioOutputFormatter
{
    // A valuation's market values, cost bases and profits and losses come out
    // of multiplying and dividing, so they can run to 28 decimal places. Text
    // shows them to at most this many, the places a bitcoin divides into;
    // JSON, and every figure the user entered, keep their full precision.
    private const int ComputedFigurePlaces = 8;

    // Rounds to ComputedFigurePlaces, a midpoint away from zero, and drops
    // trailing zeros; a loss too small to show prints as 0, not -0.
    private static readonly string ComputedFigureFormat = "0." + new string('#', ComputedFigurePlaces);

    // The hosts' JSON shape: enums as the tokens the text output prints, timestamps as UTC ending in Z.
    private static readonly JsonSerializerOptions JsonOptions = CryptoStuffJson.Configure(new JsonSerializerOptions { WriteIndented = true });

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
                $"{view.Id}: {TerminalText.Escape(view.Name)} ({view.VsCurrency}, {EnumTokens.ToToken(view.CostBasisMethod)}, {FormatTransactionCount(view.Transactions.Count)})"));

    private static string FormatText(PortfolioView view)
    {
        var lines = new List<string>
        {
            $"Id: {view.Id}",
            $"Name: {TerminalText.Escape(view.Name)}",
            $"Currency: {view.VsCurrency}",
            $"Method: {EnumTokens.ToToken(view.CostBasisMethod)}",
            $"Version: {view.Version}",
            view.Transactions.Count == 0 ? "Transactions: none" : "Transactions:",
        };

        lines.AddRange(view.Transactions.Select(transaction => FormatTransaction(transaction, view.VsCurrency)));

        return string.Join('\n', lines);
    }

    private static string FormatText(PortfolioValuationView view)
    {
        var lines = new List<string> { $"Portfolio: {view.PortfolioId} ({view.VsCurrency}, {EnumTokens.ToToken(view.CostBasisMethod)})" };

        if (view.Coins.Count == 0)
        {
            lines.Add("(no holdings)");
        }

        lines.AddRange(view.Coins.Select(coin =>
            $"{coin.CoinId}: quantity {Invariant(coin.Quantity)}, "
            + FormatFigures(coin.MarketValue, coin.CostBasis, coin.UnrealizedProfitAndLoss, coin.RealizedProfitAndLoss)));
        lines.Add("Total: " + FormatFigures(
            view.Totals.MarketValue, view.Totals.CostBasis, view.Totals.UnrealizedProfitAndLoss, view.Totals.RealizedProfitAndLoss));
        if (view.UnpricedCoins.Count > 0)
        {
            lines.Add($"Left out of the total, with no price from CoinGecko: {string.Join(", ", view.UnpricedCoins)}");
        }

        return string.Join('\n', lines);
    }

    private static string FormatFigures(decimal? marketValue, decimal costBasis, decimal? unrealized, decimal realized) =>
        $"market value {FormatPriced(marketValue)}, cost basis {Computed(costBasis)}, unrealized {FormatPriced(unrealized)}, realized {Computed(realized)}";

    // A figure that needs the coin's live price, which CoinGecko may not have given.
    private static string FormatPriced(decimal? value) => value is { } figure ? Computed(figure) : "unpriced";

    private static string Computed(decimal value) => value.ToString(ComputedFigureFormat, CultureInfo.InvariantCulture);

    private static string FormatTransaction(PortfolioTransactionView transaction, string vsCurrency) =>
        $"  {transaction.Id}: {UtcTimestamp.Format(transaction.OccurredAt)} {EnumTokens.ToToken(transaction.Kind)} {Invariant(transaction.Quantity)} {transaction.CoinId}"
        + $" @ {Invariant(transaction.UnitPrice)} {vsCurrency}, fee {Invariant(transaction.Fee)} {vsCurrency}";

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormatTransactionCount(int count) => count == 1 ? "1 transaction" : $"{count} transactions";
}
