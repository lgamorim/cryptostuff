namespace CryptoStuff.Core.Portfolios;

/// <summary>A portfolio as the hosts show it.</summary>
public sealed record PortfolioView
{
    /// <summary>The portfolio's id.</summary>
    public required int Id { get; init; }

    /// <summary>The portfolio's name.</summary>
    public required string Name { get; init; }

    /// <summary>The lowercased vs_currency the portfolio is priced in.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>How the portfolio attributes cost to the coins it sells.</summary>
    public required CostBasisMethod CostBasisMethod { get; init; }

    /// <summary>The portfolio's version, one higher after every change.</summary>
    public required int Version { get; init; }

    /// <summary>The portfolio's transactions in ledger order: by when each happened, then by sequence.</summary>
    public required IReadOnlyList<PortfolioTransactionView> Transactions { get; init; }
}

/// <summary>One transaction in a <see cref="PortfolioView"/>.</summary>
public sealed record PortfolioTransactionView
{
    /// <summary>The transaction's id.</summary>
    public required int Id { get; init; }

    /// <summary>The transaction's position in the order its portfolio recorded it, counting from 1.</summary>
    public required int Sequence { get; init; }

    /// <summary>Whether the transaction buys or sells.</summary>
    public required TransactionKind Kind { get; init; }

    /// <summary>The lowercased coin id bought or sold.</summary>
    public required string CoinId { get; init; }

    /// <summary>How many units were bought or sold.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>The price of one unit, in the portfolio's vs_currency.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>The fee paid, in the portfolio's vs_currency.</summary>
    public required decimal Fee { get; init; }

    /// <summary>When the transaction happened, in UTC.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
