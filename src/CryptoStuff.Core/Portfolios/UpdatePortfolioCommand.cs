namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// A request to replace a portfolio's name and cost-basis method. Its
/// vs_currency is fixed when it is created and cannot be changed.
/// </summary>
public sealed record UpdatePortfolioCommand
{
    /// <summary>The id of the portfolio to update.</summary>
    public required int PortfolioId { get; init; }

    /// <summary>The new name; must satisfy <see cref="NameValidator"/>.</summary>
    public required string Name { get; init; }

    /// <summary>The new cost-basis method.</summary>
    public required CostBasisMethod CostBasisMethod { get; init; }

    /// <summary>
    /// The version the caller last read, or null to update unconditionally.
    /// When given and the stored portfolio is at another version, the update
    /// is a <see cref="ServiceErrorCode.PreconditionFailed"/>.
    /// </summary>
    public int? ExpectedVersion { get; init; }
}
