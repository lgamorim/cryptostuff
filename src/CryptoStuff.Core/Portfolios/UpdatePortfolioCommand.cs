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
    /// The versions the caller accepts, such as the one it last read, or null
    /// to update unconditionally. When given and the stored portfolio is at
    /// none of them, the update is a <see cref="ServiceErrorCode.PreconditionFailed"/>.
    /// </summary>
    public IReadOnlyCollection<int>? ExpectedVersions { get; init; }
}
