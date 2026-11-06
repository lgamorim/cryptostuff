namespace CryptoStuff.Core.Alerts;

/// <summary>A request to create an armed price alert.</summary>
public sealed record CreateAlertCommand
{
    /// <summary>The id of the coin to watch, in any case; it is stored lowercased and never changes.</summary>
    public required string CoinId { get; init; }

    /// <summary>The vs_currency to read the price in, in any case; it is stored lowercased and never changes.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>Which way the price has to move past <see cref="Threshold"/> to meet the alert.</summary>
    public required AlertDirection Direction { get; init; }

    /// <summary>The price, in <see cref="VsCurrency"/>, the alert is met at; must be greater than zero.</summary>
    public required decimal Threshold { get; init; }
}
