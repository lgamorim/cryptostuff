namespace CryptoStuff.Core.Portfolios;

/// <summary>Maps a <see cref="Portfolio"/> to a <see cref="PortfolioView"/>.</summary>
public static class PortfolioMapper
{
    /// <summary>Maps <paramref name="portfolio"/> to a view.</summary>
    public static PortfolioView ToView(Portfolio portfolio) =>
        new()
        {
            Id = portfolio.Id,
            Name = portfolio.Name,
            VsCurrency = portfolio.VsCurrency,
            CostBasisMethod = portfolio.CostBasisMethod,
            Version = portfolio.Version,
            Transactions = [.. portfolio.Transactions.Select(ToView)],
        };

    private static PortfolioTransactionView ToView(PortfolioTransaction transaction) =>
        new()
        {
            Id = transaction.Id,
            Sequence = transaction.Sequence,
            Kind = transaction.Kind,
            CoinId = transaction.CoinId,
            Quantity = transaction.Quantity,
            UnitPrice = transaction.UnitPrice,
            Fee = transaction.Fee,
            OccurredAt = transaction.OccurredAt,
        };
}
