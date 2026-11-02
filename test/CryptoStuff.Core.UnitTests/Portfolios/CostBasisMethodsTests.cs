using CryptoStuff.Core.Portfolios;
using static CryptoStuff.Core.UnitTests.TestSupport.Ledger;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class CostBasisMethodsTests
{
    // Two units bought at 10,000 and 20,000, each with a 0.1% fee, and one
    // sold at 30,000 with a 30 fee. Average cost sells a unit costing the
    // average, 15,015; FIFO sells the first one, costing 10,010.
    [Theory]
    [InlineData(CostBasisMethod.AverageCost, 15015, 14955)]
    [InlineData(CostBasisMethod.Fifo, 20020, 19960)]
    public void Should_ReduceLedgerUnderPortfoliosMethod_When_MethodChosen(CostBasisMethod method, int costBasis, int realized)
    {
        var transactions = Of(
            Buy(1m, 10000m, day: 1, fee: 10m),
            Buy(1m, 20000m, day: 2, fee: 20m),
            Sell(1m, 30000m, day: 3, fee: 30m));

        var position = CostBasisMethods.For(method).CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = costBasis, RealizedProfitAndLoss = realized });
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_MethodNotDefined()
    {
        var act = () => CostBasisMethods.For((CostBasisMethod)42);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("method");
    }
}
