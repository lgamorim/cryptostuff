namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// How a portfolio attributes cost to the coins it sells. Travels as the
/// text token <see cref="CostBasisMethodParser"/> accepts.
/// </summary>
public enum CostBasisMethod
{
    /// <summary>Every unit held costs the running average of what was paid (<c>average-cost</c>).</summary>
    AverageCost,

    /// <summary>A sale uses up the oldest units bought first (<c>fifo</c>).</summary>
    Fifo,
}
