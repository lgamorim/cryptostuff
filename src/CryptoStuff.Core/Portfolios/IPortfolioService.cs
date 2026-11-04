namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// The portfolio workflow both hosts drive. An unknown portfolio or
/// transaction id is <see cref="ServiceErrorCode.NotFound"/>; a write that
/// conflicts with stored state is <see cref="ServiceErrorCode.Conflict"/>; a
/// write that breaks a ledger rule is <see cref="ServiceErrorCode.RuleViolated"/>
/// with a detail explaining it. A write that carries the version its caller
/// last read is a <see cref="ServiceErrorCode.PreconditionFailed"/> when the
/// stored portfolio is at another one, checked before anything else but the
/// portfolio's existence; without one it is unconditional. Names, currencies, tokens, and transaction
/// details are expected to have passed the shared validators already: input
/// that breaks the entities' rules throws <see cref="ArgumentException"/>.
/// </summary>
public interface IPortfolioService
{
    /// <summary>Gets every portfolio, ordered by id.</summary>
    Task<ServiceResult<IReadOnlyList<PortfolioView>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets one portfolio.</summary>
    Task<ServiceResult<PortfolioView>> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an empty portfolio. A name already taken by another portfolio,
    /// compared case-insensitively, is a <see cref="ServiceErrorCode.Conflict"/>.
    /// </summary>
    Task<ServiceResult<PortfolioView>> CreateAsync(CreatePortfolioCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a portfolio's name and cost-basis method. A name already taken
    /// by another portfolio, compared case-insensitively, is a
    /// <see cref="ServiceErrorCode.Conflict"/>, as is losing a race with
    /// another write to the same portfolio.
    /// </summary>
    Task<ServiceResult<PortfolioView>> UpdateAsync(UpdatePortfolioCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a portfolio with all its transactions. When
    /// <paramref name="expectedVersion"/> is given and the stored portfolio is
    /// at another version, it is a <see cref="ServiceErrorCode.PreconditionFailed"/>;
    /// null deletes unconditionally.
    /// </summary>
    Task<ServiceResult> DeleteAsync(int id, int? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Records a transaction in a portfolio. It is a
    /// <see cref="ServiceErrorCode.RuleViolated"/> when the transaction is
    /// dated after the current time, when CoinGecko doesn't know its coin or
    /// has no price for it in the portfolio's vs_currency, or when replaying
    /// the ledger with it would take a holding below zero; any other upstream
    /// failure propagates.
    /// </summary>
    Task<ServiceResult<PortfolioView>> AddTransactionAsync(AddTransactionCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every detail of one of a portfolio's transactions, keeping its
    /// id and sequence, under the same rules as
    /// <see cref="AddTransactionAsync"/>. CoinGecko is asked about the coin
    /// only when the replacement changes it.
    /// </summary>
    Task<ServiceResult<PortfolioView>> ReplaceTransactionAsync(ReplaceTransactionCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of a portfolio's transactions. It is a
    /// <see cref="ServiceErrorCode.RuleViolated"/> when a later sell depends
    /// on the transaction, so that replaying the ledger without it would take
    /// a holding below zero.
    /// </summary>
    Task<ServiceResult<PortfolioView>> RemoveTransactionAsync(RemoveTransactionCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Values a portfolio's holdings at live prices, in the portfolio's
    /// vs_currency, under its cost-basis method. CoinGecko is asked once, for
    /// the coins still held, and not at all when none is. An open coin
    /// CoinGecko returns no price for, in that currency, or does not know at
    /// all, is a <see cref="ServiceErrorCode.UpstreamUnavailable"/>, since the
    /// portfolio itself exists; any other upstream failure propagates with its
    /// code. Nothing is rounded.
    /// </summary>
    Task<ServiceResult<PortfolioValuationView>> GetValuationAsync(int id, CancellationToken cancellationToken);
}
