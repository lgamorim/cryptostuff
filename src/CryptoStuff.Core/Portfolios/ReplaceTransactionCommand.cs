namespace CryptoStuff.Core.Portfolios;

/// <summary>A request to replace every detail of one of a portfolio's transactions.</summary>
public sealed record ReplaceTransactionCommand
{
    /// <summary>The id of the portfolio the transaction belongs to.</summary>
    public required int PortfolioId { get; init; }

    /// <summary>The id of the transaction to replace.</summary>
    public required int TransactionId { get; init; }

    /// <summary>The transaction's new details.</summary>
    public required TransactionDetails Transaction { get; init; }

    /// <summary>
    /// The version the caller last read, or null to write unconditionally.
    /// When given and the stored portfolio is at another version, the write
    /// is a <see cref="ServiceErrorCode.PreconditionFailed"/>.
    /// </summary>
    public int? ExpectedVersion { get; init; }
}
