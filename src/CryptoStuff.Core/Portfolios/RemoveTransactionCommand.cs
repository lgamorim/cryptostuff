namespace CryptoStuff.Core.Portfolios;

/// <summary>A request to delete one of a portfolio's transactions.</summary>
public sealed record RemoveTransactionCommand
{
    /// <summary>The id of the portfolio the transaction belongs to.</summary>
    public required int PortfolioId { get; init; }

    /// <summary>The id of the transaction to delete.</summary>
    public required int TransactionId { get; init; }
}
