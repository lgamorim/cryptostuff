namespace CryptoStuff.Core.Alerts;

/// <summary>What one evaluation of the armed alerts did.</summary>
public sealed record AlertEvaluationView
{
    /// <summary>The alerts that triggered, ordered by id; empty when none did.</summary>
    public required IReadOnlyList<TriggeredAlertView> Triggered { get; init; }

    /// <summary>
    /// The armed alerts CoinGecko gave no price for, ordered by id; empty when
    /// every alert was priced. They stay armed and are evaluated again next time.
    /// </summary>
    public required IReadOnlyList<UnpricedAlertView> Unpriced { get; init; }
}

/// <summary>An armed alert an evaluation could not price, so could not check.</summary>
public sealed record UnpricedAlertView
{
    /// <summary>The alert's id.</summary>
    public required int Id { get; init; }

    /// <summary>The lowercased id of the coin the alert watches.</summary>
    public required string CoinId { get; init; }

    /// <summary>The lowercased vs_currency CoinGecko gave no price in.</summary>
    public required string VsCurrency { get; init; }
}

/// <summary>An alert an evaluation triggered.</summary>
public sealed record TriggeredAlertView
{
    /// <summary>The alert's id.</summary>
    public required int Id { get; init; }

    /// <summary>The lowercased id of the coin the alert watches.</summary>
    public required string CoinId { get; init; }

    /// <summary>The lowercased vs_currency the alert reads the price in.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>Which way the price had to move past the threshold.</summary>
    public required AlertDirection Direction { get; init; }

    /// <summary>The price, in the alert's vs_currency, the alert was met at.</summary>
    public required decimal Threshold { get; init; }

    /// <summary>The price that met the alert.</summary>
    public required decimal Price { get; init; }

    /// <summary>When the alert triggered, in UTC.</summary>
    public required DateTimeOffset TriggeredAt { get; init; }
}
