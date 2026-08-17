using System.Globalization;
using System.Text.Json;
using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>Renders Core view records as either indented JSON or human-readable text.</summary>
public static class CliOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Formats a live-price result.</summary>
    public static string Format(CoinPriceView view, bool json) => json ? Serialize(view) : FormatText(view);

    /// <summary>Formats a token-price result.</summary>
    public static string Format(TokenPriceView view, bool json) => json ? Serialize(view) : FormatText(view);

    /// <summary>Formats a market-chart result.</summary>
    public static string Format(CoinMarketChartView view, bool json) => json ? Serialize(view) : FormatText(view);

    /// <summary>Formats a coin-detail result.</summary>
    public static string Format(CoinView view, bool json) => json ? Serialize(view) : FormatText(view);

    /// <summary>Formats a developer-data result.</summary>
    public static string Format(CoinDeveloperDataView view, bool json) => json ? Serialize(view) : FormatText(view);

    private static string Serialize<TView>(TView view) => JsonSerializer.Serialize(view, JsonOptions);

    private static string FormatText(CoinPriceView view) =>
        string.Join('\n', view.Coins.Select(coin => $"{coin.CoinId}: {FormatAmounts(coin.Amounts)}"));

    private static string FormatText(TokenPriceView view) =>
        string.Join('\n', view.Tokens.Select(token => $"{token.ContractAddress}: {FormatAmounts(token.Amounts)}"));

    private static string FormatAmounts(IReadOnlyList<CurrencyAmount> amounts) =>
        string.Join(", ", amounts.Select(amount => $"{Invariant(amount.Amount)} {amount.Currency}"));

    private static string FormatText(CoinMarketChartView view)
    {
        var lines = new List<string> { "Prices:" };
        lines.AddRange(view.Prices.Select(FormatPoint));
        lines.Add("Market Caps:");
        lines.AddRange(view.MarketCaps.Select(FormatPoint));
        lines.Add("Total Volumes:");
        lines.AddRange(view.TotalVolumes.Select(FormatPoint));

        return string.Join('\n', lines);
    }

    private static string FormatPoint(CoinMarketChartPoint point) => $"  {point.Date}: {Invariant(point.Value)}";

    private static string FormatText(CoinView view)
    {
        var lines = new List<string>
        {
            $"Id: {view.Id}",
            $"Symbol: {view.Symbol}",
            $"Name: {view.Name}",
            $"Description: {view.Description ?? "(unavailable)"}",
            $"Image: {view.ImageUrl ?? "(unavailable)"}",
        };

        if (view.MarketData is null)
        {
            lines.Add("Market data: (unavailable)");
        }
        else
        {
            lines.Add("Market data:");
            lines.AddRange(view.MarketData.Select(snapshot =>
                $"  {snapshot.Currency}: price={Invariant(snapshot.Price)}, marketCap={Invariant(snapshot.MarketCap)}, volume={Invariant(snapshot.Volume)}"));
        }

        return string.Join('\n', lines);
    }

    private static string FormatText(CoinDeveloperDataView view)
    {
        var lines = new List<string>
        {
            $"Id: {view.Id}",
            $"Symbol: {view.Symbol}",
            $"Name: {view.Name}",
            $"Forks: {FormatNullable(view.Forks)}",
            $"Stars: {FormatNullable(view.Stars)}",
            $"Subscribers: {FormatNullable(view.Subscribers)}",
            $"Total issues: {FormatNullable(view.TotalIssues)}",
            $"Closed issues: {FormatNullable(view.ClosedIssues)}",
            $"Pull requests merged: {FormatNullable(view.PullRequestsMerged)}",
            $"Pull request contributors: {FormatNullable(view.PullRequestContributors)}",
            $"Commits (last 4 weeks): {FormatNullable(view.CommitCount4Weeks)}",
            $"Code additions (last 4 weeks): {FormatNullable(view.CodeAdditionsLast4Weeks)}",
            $"Code deletions (last 4 weeks): {FormatNullable(view.CodeDeletionsLast4Weeks)}",
        };

        return string.Join('\n', lines);
    }

    private static string FormatNullable(int? value) => value is null ? "(unavailable)" : Invariant(value.Value);

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
