namespace CryptoStuff.Core.Alerts;

/// <summary>
/// The price-alert workflow both hosts drive. An unknown alert id is
/// <see cref="ServiceErrorCode.NotFound"/>; a write that conflicts with stored
/// state, or a re-arm of an alert that is already armed, is
/// <see cref="ServiceErrorCode.Conflict"/>; a coin or currency CoinGecko
/// doesn't know is <see cref="ServiceErrorCode.RuleViolated"/> with a detail
/// explaining it. A write that carries the version its caller last read is a
/// <see cref="ServiceErrorCode.PreconditionFailed"/> when the stored alert is
/// at another one, checked before anything else but the alert's existence;
/// without one it is unconditional. Coins, currencies, thresholds, and tokens
/// are expected to have passed the shared validators already: input that
/// breaks the entity's rules throws <see cref="ArgumentException"/>.
/// </summary>
public interface IAlertService
{
    /// <summary>Gets every alert, ordered by id.</summary>
    Task<ServiceResult<IReadOnlyList<AlertView>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets one alert.</summary>
    Task<ServiceResult<AlertView>> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an armed alert. CoinGecko is asked once, for the coin priced in
    /// the currency — the lookup evaluation will make — and it is a
    /// <see cref="ServiceErrorCode.RuleViolated"/> when the coin is unknown or
    /// has no price in that currency; any other upstream failure propagates
    /// with its code.
    /// </summary>
    Task<ServiceResult<AlertView>> CreateAsync(CreateAlertCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an alert's direction and threshold, which re-arms it whether
    /// or not it had triggered. Losing a race with another write to the same
    /// alert is a <see cref="ServiceErrorCode.Conflict"/>. CoinGecko is not
    /// asked, since the coin and currency never change.
    /// </summary>
    Task<ServiceResult<AlertView>> UpdateAsync(UpdateAlertCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an alert. When <paramref name="expectedVersion"/> is given and
    /// the stored alert is at another version, it is a
    /// <see cref="ServiceErrorCode.PreconditionFailed"/>; null deletes
    /// unconditionally.
    /// </summary>
    Task<ServiceResult> DeleteAsync(int id, int? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a triggered alert back to armed. Re-arming an alert that is
    /// already armed is a <see cref="ServiceErrorCode.Conflict"/>, as is
    /// losing a race with another write to the same alert. When
    /// <paramref name="expectedVersion"/> is given and the stored alert is at
    /// another version, it is a <see cref="ServiceErrorCode.PreconditionFailed"/>,
    /// whatever the alert's state.
    /// </summary>
    Task<ServiceResult<AlertView>> RearmAsync(int id, int? expectedVersion, CancellationToken cancellationToken);
}
