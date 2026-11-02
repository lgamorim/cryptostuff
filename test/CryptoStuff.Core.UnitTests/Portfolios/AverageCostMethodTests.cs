using CryptoStuff.Core.Portfolios;
using static CryptoStuff.Core.UnitTests.TestSupport.Ledger;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class AverageCostMethodTests
{
    private readonly AverageCostMethod _method = new();

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
    public void Should_RealizeProceedsOverAverageCost_When_PartOfHoldingSold()
    {
        // Two units cost 300, an average of 150: selling one at 300 realizes
        // 150 and leaves one unit costing 150.
        var transactions = Of(Buy(1m, 100m, day: 1), Buy(1m, 200m, day: 2), Sell(1m, 300m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 150m, RealizedProfitAndLoss = 150m });
    }

    [Fact]
    public void Should_DeductFeeFromProceeds_When_SellCarriesFee()
    {
        var transactions = Of(Buy(2m, 100m, day: 1), Sell(1m, 300m, day: 2, fee: 2.5m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 100m, RealizedProfitAndLoss = 197.5m });
    }

    [Fact]
    public void Should_ReturnCostBasisToExactlyZero_When_WholeHoldingSoldInParts()
    {
        // Six units cost 1100. Selling two leaves four costing 733.33…,
        // which decimal can only hold to 28 digits, so taking a share of it
        // for the last four would leave a sliver of cost behind. The six sold
        // for 1800 in all, a realized 700.
        var transactions = Of(
            Buy(1m, 100m, day: 1),
            Buy(5m, 200m, day: 2),
            Sell(2m, 300m, day: 3),
            Sell(4m, 300m, day: 4));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 0m, CostBasis = 0m, RealizedProfitAndLoss = 700m });
    }

    [Fact]
    public void Should_AverageOnlyNewBuys_When_BoughtAgainAfterFullExit()
    {
        var transactions = Of(
            Buy(1m, 100m, day: 1),
            Buy(5m, 200m, day: 2),
            Sell(2m, 300m, day: 3),
            Sell(4m, 300m, day: 4),
            Buy(2m, 50m, day: 5, fee: 1m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = 101m, RealizedProfitAndLoss = 700m });
    }

    [Fact]
    public void Should_ApplyTransactionsBySequence_When_TheyShareTimestamp()
    {
        // The day-2 buy was recorded before the day-2 sell, so the sell
        // realizes against the average of all four units, 200.
        var ledger = Of(Buy(2m, 100m, day: 1), Buy(2m, 300m, day: 2), Sell(1m, 400m, day: 2));
        PortfolioTransaction[] loaded = [ledger[0], ledger[2], ledger[1]];

        var position = _method.CalculatePosition(loaded);

        position.Should().Be(new CoinPosition { Quantity = 3m, CostBasis = 600m, RealizedProfitAndLoss = 200m });
    }

    [Fact]
    public void Should_AverageInZeroCostUnits_When_BuyHasZeroPrice()
    {
        // Two free units and two at 100 average 50 a unit.
        var transactions = Of(Buy(2m, 0m, day: 1), Buy(2m, 100m, day: 2), Sell(3m, 150m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 50m, RealizedProfitAndLoss = 300m });
    }

    [Fact]
    public void Should_KeepEveryDigit_When_AmountsHaveLongFractionalParts()
    {
        // Four satoshis cost 0.0014, half of it for the two sold for 0.0012
        // less a one-satoshi-sized fee.
        var transactions = Of(
            Buy(0.00000003m, 30000m, day: 1),
            Buy(0.00000001m, 50000m, day: 2),
            Sell(0.00000002m, 60000m, day: 3, fee: 0.00000001m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 0.00000002m, CostBasis = 0.0007m, RealizedProfitAndLoss = 0.00049999m });
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
