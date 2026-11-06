namespace CryptoStuff.Core.Alerts;

/// <summary>
/// A standing instruction to notice when one coin's price crosses a threshold
/// in one vs_currency. It starts <see cref="AlertState.Armed"/>; when the
/// price meets it, <see cref="Trigger"/> records when and at what price and
/// moves it to <see cref="AlertState.Triggered"/>, where it stays until
/// <see cref="Rearm"/> or <see cref="Update"/> arms it again. The coin and
/// the vs_currency are fixed at creation, so a stored alert can never break
/// the price lookup the coin was validated for; to watch another coin, delete
/// the alert and create a new one. The id is zero until the repository first
/// saves the alert and assigns one.
/// </summary>
public sealed class PriceAlert
{
    // Takes only the scalar properties so the persistence layer can construct
    // the entity through this constructor, binding each parameter to its
    // property, without a public parameterless constructor that would bypass
    // the rules in Create.
    private PriceAlert(string coinId, string vsCurrency, AlertDirection direction, decimal threshold)
    {
        CoinId = coinId;
        VsCurrency = vsCurrency;
        Direction = direction;
        Threshold = threshold;
    }

    /// <summary>The alert's id; zero until first saved.</summary>
    public int Id { get; private set; }

    /// <summary>The lowercased id of the coin being watched; it never changes.</summary>
    public string CoinId { get; }

    /// <summary>The lowercased vs_currency the price is read in; it never changes.</summary>
    public string VsCurrency { get; }

    /// <summary>Which way the price has to move past <see cref="Threshold"/> to meet the alert.</summary>
    public AlertDirection Direction { get; private set; }

    /// <summary>The price, in <see cref="VsCurrency"/>, the alert is met at; always greater than zero.</summary>
    public decimal Threshold { get; private set; }

    /// <summary>Whether the alert is waiting for its price or has been met.</summary>
    public AlertState State { get; private set; }

    /// <summary>When the alert was last triggered, in UTC; null while it is armed.</summary>
    public DateTimeOffset? TriggeredAt { get; private set; }

    /// <summary>The price the alert was last triggered at; null while it is armed.</summary>
    public decimal? TriggeredPrice { get; private set; }

    /// <summary>
    /// The alert's version: 1 when created, and one higher after every change
    /// to it, so a save can detect that the stored alert changed since it was read.
    /// </summary>
    public int Version { get; private set; } = 1;

    /// <summary>Creates an armed alert; the coin id and vs_currency are stored lowercased.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="coinId"/> or <paramref name="vsCurrency"/> is null,
    /// empty, or whitespace-only.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="threshold"/> is not greater than zero, or
    /// <paramref name="direction"/> is not a defined direction.
    /// </exception>
    public static PriceAlert Create(string coinId, string vsCurrency, AlertDirection direction, decimal threshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coinId);
        ArgumentException.ThrowIfNullOrWhiteSpace(vsCurrency);
        EnsurePositive(threshold);
        EnsureDefined(direction);
        return new(coinId.ToLowerInvariant(), vsCurrency.ToLowerInvariant(), direction, threshold);
    }

    /// <summary>
    /// Whether <paramref name="price"/> meets the alert, inclusively: at or
    /// above the threshold for <see cref="AlertDirection.Above"/>, at or below
    /// it for <see cref="AlertDirection.Below"/>. It looks only at the price,
    /// not at <see cref="State"/>; deciding whether to trigger is the caller's.
    /// </summary>
    public bool IsMetBy(decimal price) =>
        Direction == AlertDirection.Above ? price >= Threshold : price <= Threshold;

    /// <summary>
    /// Moves an armed alert to triggered, recording when it happened, stored
    /// in UTC, and at what price. A failed outcome, for an alert that has
    /// already triggered, leaves the alert unchanged.
    /// </summary>
    public ValidationOutcome Trigger(decimal price, DateTimeOffset at)
    {
        if (State == AlertState.Triggered)
        {
            return ValidationOutcome.Failure($"Alert {Id} has already triggered; re-arm it before it can trigger again.");
        }

        State = AlertState.Triggered;
        TriggeredAt = at.ToUniversalTime();
        TriggeredPrice = price;
        Version++;
        return ValidationOutcome.Success();
    }

    /// <summary>
    /// Moves a triggered alert back to armed and forgets when and at what
    /// price it triggered. A failed outcome, for an alert that is already
    /// armed, leaves the alert unchanged.
    /// </summary>
    public ValidationOutcome Rearm()
    {
        if (State == AlertState.Armed)
        {
            return ValidationOutcome.Failure($"Alert {Id} is already armed.");
        }

        ResetToArmed();
        return ValidationOutcome.Success();
    }

    /// <summary>
    /// Replaces the direction and threshold, the only editable fields, and arms
    /// the alert, since a triggered record means nothing against a new
    /// condition. An alert that is already armed stays armed. The alert is left
    /// unchanged when an argument is rejected.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="threshold"/> is not greater than zero, or
    /// <paramref name="direction"/> is not a defined direction.
    /// </exception>
    public void Update(AlertDirection direction, decimal threshold)
    {
        EnsurePositive(threshold);
        EnsureDefined(direction);
        Direction = direction;
        Threshold = threshold;
        ResetToArmed();
    }

    private void ResetToArmed()
    {
        State = AlertState.Armed;
        TriggeredAt = null;
        TriggeredPrice = null;
        Version++;
    }

    private static void EnsureDefined(AlertDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "The alert direction is not defined.");
        }
    }

    private static void EnsurePositive(decimal threshold)
    {
        var outcome = PositiveDecimalValidator.Validate(threshold, nameof(threshold));
        if (!outcome.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, outcome.ErrorMessage);
        }
    }
}
