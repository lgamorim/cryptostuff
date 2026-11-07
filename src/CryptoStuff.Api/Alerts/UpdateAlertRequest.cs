namespace CryptoStuff.Api.Alerts;

/// <summary>
/// The JSON body of <c>PUT /alerts/{id}</c>: everything about an alert a
/// client can replace. Its coin and vs_currency are fixed at creation.
/// Replacing them re-arms the alert, whether or not it had triggered.
/// </summary>
public sealed record UpdateAlertRequest
{
    /// <summary>The new direction token: <c>above</c> or <c>below</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>The new threshold, in the alert's vs_currency; greater than zero.</summary>
    public decimal? Threshold { get; init; }
}
