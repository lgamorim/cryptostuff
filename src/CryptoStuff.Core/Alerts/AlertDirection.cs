namespace CryptoStuff.Core.Alerts;

/// <summary>
/// Which way the price has to move past an alert's threshold for the alert to
/// be met. Travels as the text token <see cref="AlertDirectionParser"/> accepts.
/// </summary>
public enum AlertDirection
{
    /// <summary>Met when the price is at or above the threshold (<c>above</c>).</summary>
    Above,

    /// <summary>Met when the price is at or below the threshold (<c>below</c>).</summary>
    Below,
}
