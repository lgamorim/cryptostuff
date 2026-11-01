using System.Text.Json;
using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli.Watchlists;

/// <summary>Renders watchlist views as either indented JSON or human-readable text.</summary>
public static class WatchlistOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Formats one watchlist.</summary>
    public static string Format(WatchlistView view, bool json) =>
        json
            ? JsonSerializer.Serialize(view, JsonOptions)
            : string.Join('\n', $"Id: {view.Id}", $"Name: {view.Name}", $"Coins: {FormatCoins(view.CoinIds)}");

    /// <summary>Formats a watchlist's prices, saying so in text when it holds no coins.</summary>
    public static string FormatPrices(CoinPriceView view, bool json) =>
        !json && view.Coins.Count == 0 ? "(no coins)" : CliOutputFormatter.Format(view, json);

    /// <summary>Formats the confirmation that watchlist <paramref name="id"/> was deleted.</summary>
    public static string FormatDeleted(int id, bool json) =>
        json
            ? JsonSerializer.Serialize(new { Id = id, Deleted = true }, JsonOptions)
            : $"Deleted watchlist {id}.";

    /// <summary>Formats the list of watchlists.</summary>
    public static string Format(IReadOnlyList<WatchlistView> views, bool json) =>
        json
            ? JsonSerializer.Serialize(views, JsonOptions)
            : FormatListText(views);

    private static string FormatListText(IReadOnlyList<WatchlistView> views) =>
        views.Count == 0
            ? "(no watchlists)"
            : string.Join('\n', views.Select(view => $"{view.Id}: {view.Name} ({FormatCoins(view.CoinIds)})"));

    private static string FormatCoins(IReadOnlyList<string> coinIds) =>
        coinIds.Count == 0 ? "no coins" : string.Join(", ", coinIds);
}
