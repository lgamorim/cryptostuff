namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// Maps a portfolio's <see cref="CostBasisMethod"/> to the
/// <see cref="ICostBasisMethod"/> that applies it. This is the one place
/// that switches on the method; callers work through the interface.
/// </summary>
public static class CostBasisMethods
{
    /// <summary>Returns the implementation of <paramref name="method"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method"/> is not a defined method.</exception>
    public static ICostBasisMethod For(CostBasisMethod method) =>
        method switch
        {
            CostBasisMethod.AverageCost => new AverageCostMethod(),
            CostBasisMethod.Fifo => new FifoMethod(),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "The cost-basis method is not defined."),
        };
}
