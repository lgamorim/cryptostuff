namespace CryptoStuff.Core.Alerts;

/// <summary>
/// Whether an alert is still waiting for its price or has been met. Travels as
/// the text token <see cref="AlertStateParser"/> accepts.
/// </summary>
public enum AlertState
{
    /// <summary>Waiting for the price to meet the threshold (<c>armed</c>).</summary>
    Armed,

    /// <summary>Met: the price reached the threshold and the alert recorded it (<c>triggered</c>).</summary>
    Triggered,
}
