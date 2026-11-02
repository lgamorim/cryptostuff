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
}
