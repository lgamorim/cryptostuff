namespace CryptoStuff.Api.Portfolios;

/// <summary>The JSON body of <c>POST /portfolios</c>.</summary>
public sealed record CreatePortfolioRequest
{
    /// <summary>The portfolio's name.</summary>
    public string? Name { get; init; }

    /// <summary>The vs_currency the portfolio is priced in; it never changes.</summary>
    public string? VsCurrency { get; init; }

    /// <summary>The cost-basis method token: <c>average-cost</c> or <c>fifo</c>.</summary>
    public string? CostBasisMethod { get; init; }
}
