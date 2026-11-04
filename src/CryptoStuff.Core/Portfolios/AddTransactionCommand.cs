namespace CryptoStuff.Core.Portfolios;

/// <summary>A request to record a transaction in a portfolio.</summary>
public sealed record AddTransactionCommand
{
    /// <summary>The id of the portfolio to record the transaction in.</summary>
    public required int PortfolioId { get; init; }

    /// <summary>The transaction to record.</summary>
    public required TransactionDetails Transaction { get; init; }

    /// <summary>
    /// The version the caller last read, or null to write unconditionally.
    /// When given and the stored portfolio is at another version, the write
    /// is a <see cref="ServiceErrorCode.PreconditionFailed"/>.
    /// </summary>
    public int? ExpectedVersion { get; init; }
}
