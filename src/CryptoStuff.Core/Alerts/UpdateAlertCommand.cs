namespace CryptoStuff.Core.Alerts;

/// <summary>
/// A request to replace an alert's direction and threshold, which re-arms it.
/// Its coin and vs_currency are fixed when it is created and cannot be changed.
/// </summary>
public sealed record UpdateAlertCommand
{
    /// <summary>The id of the alert to update.</summary>
    public required int AlertId { get; init; }

    /// <summary>The new direction.</summary>
    public required AlertDirection Direction { get; init; }

    /// <summary>The new threshold; must be greater than zero.</summary>
    public required decimal Threshold { get; init; }

    /// <summary>
    /// The version the caller last read, or null to update unconditionally.
    /// When given and the stored alert is at another version, the update is a
    /// <see cref="ServiceErrorCode.PreconditionFailed"/>.
    /// </summary>
    public int? ExpectedVersion { get; init; }
}
