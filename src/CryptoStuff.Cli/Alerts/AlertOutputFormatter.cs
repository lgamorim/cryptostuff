using System.Globalization;
using System.Text.Json;
using CryptoStuff.Composition;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Cli.Alerts;

/// <summary>Renders alert views as either indented JSON or human-readable text.</summary>
public static class AlertOutputFormatter
{
    // The hosts' JSON shape: enums as the tokens the text output prints, timestamps as UTC ending in Z.
    private static readonly JsonSerializerOptions JsonOptions = CryptoStuffJson.Configure(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Formats one alert.</summary>
    public static string Format(AlertView view, bool json) =>
        json
            ? JsonSerializer.Serialize(view, JsonOptions)
            : FormatText(view);

    /// <summary>Formats the list of alerts.</summary>
    public static string Format(IReadOnlyList<AlertView> views, bool json) =>
        json
            ? JsonSerializer.Serialize(views, JsonOptions)
            : FormatListText(views);

    /// <summary>Formats what one evaluation of the armed alerts triggered.</summary>
    public static string Format(AlertEvaluationView view, bool json) =>
        json
            ? JsonSerializer.Serialize(view, JsonOptions)
            : FormatEvaluationText(view);

    /// <summary>Formats the confirmation that alert <paramref name="id"/> was deleted.</summary>
    public static string FormatDeleted(int id, bool json) =>
        json
            ? JsonSerializer.Serialize(new { Id = id, Deleted = true }, JsonOptions)
            : $"Deleted alert {id}.";

    private static string FormatListText(IReadOnlyList<AlertView> views) =>
        views.Count == 0
            ? "(no alerts)"
            : string.Join('\n', views.Select(view =>
                $"{view.Id}: {view.CoinId} {EnumTokens.ToToken(view.Direction)} {Invariant(view.Threshold)} {view.VsCurrency} ({EnumTokens.ToToken(view.State)}{(view.AwaitingReset ? $", {AwaitingResetNote}" : string.Empty)})"));

    private static string FormatText(AlertView view) =>
        string.Join('\n',
            $"Id: {view.Id}",
            $"Coin: {view.CoinId}",
            $"Currency: {view.VsCurrency}",
            $"Direction: {EnumTokens.ToToken(view.Direction)}",
            $"Threshold: {Invariant(view.Threshold)}",
            $"State: {EnumTokens.ToToken(view.State)}{(view.AwaitingReset ? $" ({AwaitingResetNote})" : string.Empty)}",
            FormatTriggerLine(view),
            $"Version: {view.Version}");

    // Shown beside the state of a re-armed alert that cannot trigger yet.
    private const string AwaitingResetNote = "waiting for the price to move back";

    // Core keeps TriggeredAt and TriggeredPrice null together, while the alert is armed.
    private static string FormatTriggerLine(AlertView view) =>
        view.TriggeredAt is { } triggeredAt && view.TriggeredPrice is { } price
            ? $"Triggered: {UtcTimestamp.Format(triggeredAt)} at {Invariant(price)} {view.VsCurrency}"
            : "Triggered: never";

    private static string FormatEvaluationText(AlertEvaluationView view)
    {
        var triggered = view.Triggered.Count == 0
            ? "Nothing triggered."
            : string.Join('\n', view.Triggered.Select(FormatTriggeredAlert).Prepend(FormatTriggeredHeader(view.Triggered.Count)));
        return view.Unpriced.Count == 0
            ? triggered
            : string.Join('\n', view.Unpriced.Select(FormatUnpricedAlert).Prepend(FormatUnpricedHeader(view.Unpriced.Count)).Prepend(triggered));
    }

    private static string FormatTriggeredHeader(int count) => count == 1 ? "Triggered 1 alert:" : $"Triggered {count} alerts:";

    private static string FormatUnpricedHeader(int count) =>
        count == 1 ? "Could not price 1 alert, left armed:" : $"Could not price {count} alerts, left armed:";

    private static string FormatUnpricedAlert(UnpricedAlertView alert) => $"  {alert.Id}: {alert.CoinId} in {alert.VsCurrency}";

    private static string FormatTriggeredAlert(TriggeredAlertView alert) =>
        $"  {alert.Id}: {alert.CoinId} {EnumTokens.ToToken(alert.Direction)} {Invariant(alert.Threshold)} {alert.VsCurrency}"
        + $", price {Invariant(alert.Price)} {alert.VsCurrency}, at {UtcTimestamp.Format(alert.TriggeredAt)}";

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
