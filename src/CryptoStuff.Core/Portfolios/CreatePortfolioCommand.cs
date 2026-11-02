namespace CryptoStuff.Core.Portfolios;

/// <summary>A request to create an empty portfolio.</summary>
public sealed record CreatePortfolioCommand
{
    /// <summary>The new portfolio's name; must satisfy <see cref="NameValidator"/>.</summary>
    public required string Name { get; init; }

    /// <summary>The vs_currency the portfolio is priced in, in any case; it is stored lowercased and never changes.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>How the portfolio attributes cost to the coins it sells.</summary>
    public required CostBasisMethod CostBasisMethod { get; init; }
}
