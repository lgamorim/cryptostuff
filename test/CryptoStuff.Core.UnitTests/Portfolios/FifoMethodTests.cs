using System.Globalization;
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

    [Fact]
    public void Should_ReleaseTheShareExactly_When_CostTimesTheSoldQuantityWouldOverflow()
    {
        // 1e28 of cost on 1e14 units fits a decimal, as does the 5e27 half
        // sold, but 1e28 times the 5e13 units sold does not.
        var transactions = Of(Buy(1e14m, 1e14m, day: 1), Sell(5e13m, 1e14m, day: 2));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 5e13m, CostBasis = 5e27m, RealizedProfitAndLoss = 0m });
    }

    [Theory]
    [InlineData("3e14", "1e28", "1e14", "6666666666666666666666666666.7")]
    [InlineData("3e24", "50000", "2e24", "16666.666666666666666666666667")]
    [InlineData("7e28", "1e10", "6e28", "1428571428.571428571428571429")]
    [InlineData("5.4414245390060482000000000612", "79228162514264337593543950013", "5.4414245390060482000000000611", "1")]
    [InlineData("2", "0.0000000000000000000000000003", "1", "0.0000000000000000000000000001")]
    [InlineData("2", "0.0000000000000000000000000005", "1", "0.0000000000000000000000000003")]
    public void Should_ReleaseTheExactShareRoundedOnceHalfToEven_When_ASellTakesPartOfTheHolding(
        string bought, string cost, string sold, string costBasisKept)
    {
        // In the first four rows cost times the units sold does not fit a
        // decimal, though the share does: it is the exact share, rounded once
        // to the most digits a decimal holds, so what is kept never goes
        // below zero and what is released and kept add up to exactly what
        // was paid. The fourth row keeps 1.456… of cost on its last 1e-28 of
        // a unit. The last two rows' shares, 1.5e-28 and 2.5e-28, are ties
        // and round to the even 2e-28.
        var transactions = Of(Buy(Amount(bought), 0m, day: 1, fee: Amount(cost)), Sell(Amount(sold), 0m, day: 2));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition
        {
            Quantity = Amount(bought) - Amount(sold),
            CostBasis = Amount(costBasisKept),
            RealizedProfitAndLoss = Amount(costBasisKept) - Amount(cost),
        });
    }

    [Theory]
    [InlineData("7e28", "0.6")]
    [InlineData("10", "0.0000000000000000000000000006")]
    [InlineData("0.5", "7922816251426433759354395033.5")]
    public void Should_ThrowOverflowException_When_ABuyWouldLeaveAHoldingNeedingMoreDigitsThanADecimalHolds(string held, string bought)
    {
        // Each holding, written to the finer of the two quantities' decimal
        // places, needs more than a decimal's 96 bits of digits, so the sum
        // would round; the last row's sum fits only once its trailing zero is
        // dropped. No portfolio records such a buy, so each buy comes from a
        // ledger of its own.
        var transactions = Of(Buy(Amount(held), 0m, day: 1)).Concat(Of(Buy(Amount(bought), 0m, day: 2)));

        var act = () => _method.CalculatePosition(transactions);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData("0.5,0.5,7922816251426433759354395033")]
    [InlineData("0.00000004,0.00000004,0.99999992,1e21")]
    public void Should_ThrowOverflowException_When_ABuyNeedsThePlacesOfQuantitiesTheHoldingNoLongerShows(string buys)
    {
        // The holding before the last buy, 1 in both rows, shows no decimal
        // places, but its lots still need them: written to those places, the
        // holding after the last buy needs more digits than a decimal holds,
        // and FIFO's lots would round. No portfolio records such a buy, so
        // each buy comes from a ledger of its own.
        var transactions = buys.Split(',').SelectMany((quantity, index) => Of(Buy(Amount(quantity), 0m, day: index + 1)));

        var act = () => _method.CalculatePosition(transactions);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData("7e28", "1.0", "70000000000000000000000000001")]
    [InlineData("7922816251426433759354395033", "1.00", "7922816251426433759354395034")]
    public void Should_CountOnlyThePlacesAQuantityNeeds_When_ItIsWrittenWithTrailingZeros(string held, string bought, string expected)
    {
        // Storage reads a whole number back as 1.0, so a quantity's trailing
        // zeros must not change whether the holding fits a decimal.
        var transactions = Of(Buy(Amount(held), 0m, day: 1)).Concat(Of(Buy(Amount(bought), 0m, day: 2)));

        var position = _method.CalculatePosition(transactions);

        position.Quantity.Should().Be(Amount(expected));
    }

    [Fact]
    public void Should_ThrowOverflowException_When_ASellWouldLeaveAHoldingNeedingMoreDigitsThanADecimalHolds()
    {
        // 7e28 - 0.6 needs 30 significant digits. No portfolio records that
        // sell, so it is taken from a second ledger.
        var sell = Of(Buy(1m, 0m, day: 1), Sell(0.6m, 0m, day: 2))[1];
        var transactions = Of(Buy(7e28m, 0m, day: 1)).Append(sell);

        var act = () => _method.CalculatePosition(transactions);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData("2", "200.00", "1", "-100.00")]
    [InlineData("23", "98238", "20.5", "-87559.9565217391304347826087")]
    public void Should_ShowTheShareWithTheDigitsDecimalDivisionGives_When_TheShareIsWorkedOutExactly(
        string bought, string cost, string sold, string realizedShown)
    {
        // Figures reach callers as text, so the exact share keeps the digits
        // cost × units sold ÷ units held shows in decimal arithmetic: no
        // trailing zeros past what the operands carry, and none at all on a
        // rounded share.
        var transactions = Of(Buy(Amount(bought), 0m, day: 1, fee: Amount(cost)), Sell(Amount(sold), 0m, day: 2));

        var position = _method.CalculatePosition(transactions);

        position.RealizedProfitAndLoss.ToString(CultureInfo.InvariantCulture).Should().Be(realizedShown);
    }

    private static decimal Amount(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
