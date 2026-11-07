namespace CryptoStuff.Composition;

/// <summary>How often the API host evaluates the armed price alerts in the background.</summary>
public sealed record AlertEvaluationSchedule
{
    /// <summary>The time between two evaluations, and before the first one.</summary>
    public required TimeSpan Interval { get; init; }
}
