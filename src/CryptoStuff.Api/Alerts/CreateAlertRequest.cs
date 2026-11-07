namespace CryptoStuff.Api.Alerts;

/// <summary>The JSON body of <c>POST /alerts</c>.</summary>
public sealed record CreateAlertRequest
{
    /// <summary>The CoinGecko id of the coin to watch, in any case; it never changes.</summary>
    public string? CoinId { get; init; }

    /// <summary>The vs_currency to read the price in, in any case; it never changes.</summary>
    public string? VsCurrency { get; init; }

    /// <summary>The direction token: <c>above</c> or <c>below</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>The price, in the vs_currency, at which the alert is met; greater than zero.</summary>
    public decimal? Threshold { get; init; }
}
