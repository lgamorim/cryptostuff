namespace CryptoStuff.Api.Portfolios;

/// <summary>
/// The JSON body of <c>PUT /portfolios/{id}</c>: everything about a portfolio
/// a client can replace. Its vs_currency is fixed at creation, and its
/// transactions change only through the transaction routes.
/// </summary>
public sealed record UpdatePortfolioRequest
{
    /// <summary>The portfolio's new name.</summary>
    public string? Name { get; init; }

    /// <summary>The new cost-basis method token: <c>average-cost</c> or <c>fifo</c>.</summary>
    public string? CostBasisMethod { get; init; }
}
