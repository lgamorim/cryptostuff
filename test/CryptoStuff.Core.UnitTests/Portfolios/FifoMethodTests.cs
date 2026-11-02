using CryptoStuff.Core.Portfolios;
using static CryptoStuff.Core.UnitTests.TestSupport.Ledger;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class FifoMethodTests
{
    private readonly FifoMethod _method = new();

    [Fact]
    public void Should_HoldNothing_When_NoTransactions()
    {
        var transactions = Of();

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 0m, CostBasis = 0m, RealizedProfitAndLoss = 0m });
    }

    [Fact]
    public void Should_HoldQuantityAtPricePaid_When_SingleBuy()
    {
        var transactions = Of(Buy(2.5m, 40000m, day: 1));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2.5m, CostBasis = 100000m, RealizedProfitAndLoss = 0m });
    }

    [Fact]
    public void Should_AddFeeToCostBasis_When_BuyCarriesFee()
    {
        var transactions = Of(Buy(2m, 100m, day: 1, fee: 1.5m), Buy(1m, 400m, day: 2, fee: 0.5m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 3m, CostBasis = 602m, RealizedProfitAndLoss = 0m });
    }

    [Fact]
    public void Should_RealizeProceedsOverOldestLotCost_When_PartOfHoldingSold()
    {
        // The unit sold is the one bought first, at 100; the one bought at 200 is still held.
        var transactions = Of(Buy(1m, 100m, day: 1), Buy(1m, 200m, day: 2), Sell(1m, 300m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 200m, RealizedProfitAndLoss = 200m });
    }

    [Fact]
    public void Should_ConsumeLotsOldestFirst_When_SellsSpanSeveralLots()
    {
        // The first sell takes the 100 lot and one unit of the 200 lot (cost
        // 300, realizing 500); the second takes the other 200 unit and one
        // unit of the 300 lot (cost 500, realizing 300). Two 300 units remain.
        var transactions = Of(
            Buy(1m, 100m, day: 1),
            Buy(2m, 200m, day: 2),
            Buy(3m, 300m, day: 3),
            Sell(2m, 400m, day: 4),
            Sell(2m, 400m, day: 5));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = 600m, RealizedProfitAndLoss = 800m });
    }

    [Fact]
    public void Should_DeductFeeFromProceeds_When_SellCarriesFee()
    {
        var transactions = Of(Buy(2m, 100m, day: 1), Sell(1m, 300m, day: 2, fee: 2.5m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 100m, RealizedProfitAndLoss = 197.5m });
    }

    [Fact]
    public void Should_SplitBuyFeeWithLot_When_LotPartlySold()
    {
        // The lot cost 203 with its fee; half of it is sold, taking 101.5.
        var transactions = Of(Buy(2m, 100m, day: 1, fee: 3m), Sell(1m, 300m, day: 2));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 101.5m, RealizedProfitAndLoss = 198.5m });
    }

    [Fact]
    public void Should_CostOnlyNewLot_When_BoughtAgainAfterFullExit()
    {
        // A third of the 301 lot is 100.33…; the last two units take what
        // is left of it, so the exit realizes exactly 600 - 301 = 299 and
        // the new lot starts from nothing.
        var transactions = Of(
            Buy(3m, 100m, day: 1, fee: 1m),
            Sell(1m, 200m, day: 2),
            Sell(2m, 200m, day: 3),
            Buy(1m, 50m, day: 4, fee: 1m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 51m, RealizedProfitAndLoss = 299m });
    }

    [Fact]
    public void Should_TreatEarlierSequenceAsOlderLot_When_BuysShareTimestamp()
    {
        // Both buys happened at the same instant; the one recorded first,
        // at 100, is the older lot and is sold first.
        var ledger = Of(Buy(1m, 100m, day: 1), Buy(1m, 200m, day: 1), Sell(1m, 300m, day: 2));
        PortfolioTransaction[] loaded = [ledger[1], ledger[0], ledger[2]];

        var position = _method.CalculatePosition(loaded);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 200m, RealizedProfitAndLoss = 200m });
    }

    [Fact]
    public void Should_SellFreeUnitsFirst_When_OldestBuyHasZeroPrice()
    {
        // Three sold at 150: the two free units, then one at 100.
        var transactions = Of(Buy(2m, 0m, day: 1), Buy(2m, 100m, day: 2), Sell(3m, 150m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 100m, RealizedProfitAndLoss = 350m });
    }

    [Fact]
    public void Should_KeepEveryDigit_When_AmountsHaveLongFractionalParts()
    {
        // The two satoshis sold come from the 0.0009 lot of three, taking
        // 0.0006; they sold for 0.0012 less a one-satoshi-sized fee.
        var transactions = Of(
            Buy(0.00000003m, 30000m, day: 1),
            Buy(0.00000001m, 50000m, day: 2),
            Sell(0.00000002m, 60000m, day: 3, fee: 0.00000001m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 0.00000002m, CostBasis = 0.0008m, RealizedProfitAndLoss = 0.00059999m });
    }

    [Fact]
    public void Should_ThrowArgumentException_When_SellExceedsHolding()
    {
        // A portfolio never records an oversell, so the sell comes from another one.
        PortfolioTransaction[] transactions = [Of(Buy(1m, 100m, day: 1))[0], Of(Buy(2m, 100m, day: 1), Sell(2m, 200m, day: 2))[1]];

        var act = () => _method.CalculatePosition(transactions);

        act.Should().Throw<ArgumentException>().WithParameterName("transactions").WithMessage("*below zero*");
    }

    [Fact]
    public void Should_ThrowArgumentException_When_TransactionsSpanSeveralCoins()
    {
        var transactions = Of(Buy(1m, 100m, day: 1), Buy(1m, 10m, day: 1, coinId: "ethereum"));

        var act = () => _method.CalculatePosition(transactions);

        act.Should().Throw<ArgumentException>().WithParameterName("transactions").WithMessage("*one coin*");
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_TransactionsNull()
    {
        // `!` is deliberate: the test passes the null the signature forbids.
        var act = () => _method.CalculatePosition(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("transactions");
    }
}
