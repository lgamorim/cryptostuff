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

    // The README's worked example: three units bought at 10,000, 20,000 and
    // 12,000, each with a 0.1% fee, costing 10,010, 20,020 and 12,012, and
    // one sold at 30,000 with a 30 fee, bringing in 29,970. Each method
    // sells a unit of a different cost: the average, 14,014; FIFO the oldest,
    // 10,010; LIFO the newest, 12,012; HIFO the dearest, 20,020.
    [Theory]
    [InlineData(CostBasisMethod.AverageCost, 28028, 15956)]
    [InlineData(CostBasisMethod.Fifo, 32032, 19960)]
    [InlineData(CostBasisMethod.Lifo, 30030, 17958)]
    [InlineData(CostBasisMethod.Hifo, 22022, 9950)]
    public void Should_ReleaseADifferentCostUnderEachMethod_When_UnitsBoughtAtDifferentPricesAreSold(CostBasisMethod method, int costBasis, int realized)
    {
        var transactions = Of(
            Buy(1m, 10000m, day: 1, fee: 10m),
            Buy(1m, 20000m, day: 2, fee: 20m),
            Buy(1m, 12000m, day: 3, fee: 12m),
            Sell(1m, 30000m, day: 4, fee: 30m));

        var position = CostBasisMethods.For(method).CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = costBasis, RealizedProfitAndLoss = realized });
    }

    // With nothing sold, every method holds every unit bought at what it
    // cost. 0.6 + 0.6 + 7e28 + 0.6 needs more digits than a decimal holds, so
    // the total rounds at each addition and depends on the order of the
    // additions: in the order bought it is 7e28 + 2, while starting from the
    // 7e28 lot (HIFO's order) or from the newest (LIFO's) gives 7e28 + 3.
    [Theory]
    [InlineData(CostBasisMethod.AverageCost)]
    [InlineData(CostBasisMethod.Fifo)]
    [InlineData(CostBasisMethod.Lifo)]
    [InlineData(CostBasisMethod.Hifo)]
    public void Should_ReportTheSameCostBasisUnderEveryMethod_When_NothingIsSoldAndTheTotalNeedsMoreDigitsThanADecimalHolds(CostBasisMethod method)
    {
        var transactions = Of(
            Buy(1m, 0.6m, day: 1),
            Buy(1m, 0.6m, day: 2),
            Buy(1m, 7e28m, day: 3),
            Buy(1m, 0.6m, day: 4));

        var position = CostBasisMethods.For(method).CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 4m, CostBasis = 70000000000000000000000000002m, RealizedProfitAndLoss = 0m });
    }

    [Theory]
    [InlineData(CostBasisMethod.AverageCost, typeof(AverageCostMethod))]
    [InlineData(CostBasisMethod.Fifo, typeof(FifoMethod))]
    [InlineData(CostBasisMethod.Lifo, typeof(LifoMethod))]
    [InlineData(CostBasisMethod.Hifo, typeof(HifoMethod))]
    public void Should_ReturnTheMethodsImplementation_When_MethodDefined(CostBasisMethod method, Type implementation)
    {
        var costBasisMethod = CostBasisMethods.For(method);

        costBasisMethod.Should().BeOfType(implementation);
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRangeException_When_MethodNotDefined()
    {
        var act = () => CostBasisMethods.For((CostBasisMethod)42);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("method");
    }
}
