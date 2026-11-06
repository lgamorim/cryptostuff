namespace CryptoStuff.Core.Alerts;

/// <summary>A price alert as the hosts show it.</summary>
public sealed record AlertView
{
    /// <summary>The alert's id.</summary>
    public required int Id { get; init; }

    /// <summary>The lowercased id of the coin being watched.</summary>
    public required string CoinId { get; init; }

    /// <summary>The lowercased vs_currency the price is read in.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>Which way the price has to move past the threshold to meet the alert.</summary>
    public required AlertDirection Direction { get; init; }

    /// <summary>The price, in the alert's vs_currency, at which the alert is met.</summary>
    public required decimal Threshold { get; init; }

    /// <summary>Whether the alert is waiting for its price or has been met.</summary>
    public required AlertState State { get; init; }

    /// <summary>When the alert last triggered, in UTC; null while it is armed.</summary>
    public required DateTimeOffset? TriggeredAt { get; init; }

    /// <summary>The price the alert last triggered at; null while it is armed.</summary>
    public required decimal? TriggeredPrice { get; init; }

    /// <summary>The alert's version, one higher after every change.</summary>
    public required int Version { get; init; }
}
