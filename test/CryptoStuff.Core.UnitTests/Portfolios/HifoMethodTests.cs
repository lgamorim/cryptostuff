using System.Globalization;
using CryptoStuff.Core.Portfolios;
using static CryptoStuff.Core.UnitTests.TestSupport.Ledger;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class HifoMethodTests
{
    private readonly HifoMethod _method = new();

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
    public void Should_SellTheDearestLot_When_ItIsNeitherTheOldestNorTheNewest()
    {
        // The unit sold is the one that cost 300, bought between the 100 and
        // the 200 units, which are both still held.
        var transactions = Of(Buy(1m, 100m, day: 1), Buy(1m, 300m, day: 2), Buy(1m, 200m, day: 3), Sell(1m, 400m, day: 4));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = 300m, RealizedProfitAndLoss = 100m });
    }

    [Fact]
    public void Should_RankLotsByCostPerUnitWithTheirFee_When_AFeeMakesTheCheaperPriceDearer()
    {
        // The unit priced at 100 cost 130 with its fee, more than the unit
        // priced at 120 with none, so it is the one sold.
        var transactions = Of(
            Buy(1m, 50m, day: 1),
            Buy(1m, 100m, day: 2, fee: 30m),
            Buy(1m, 120m, day: 3),
            Sell(1m, 200m, day: 4));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = 170m, RealizedProfitAndLoss = 70m });
    }

    [Fact]
    public void Should_ConsumeLotsDearestFirst_When_SellsSpanSeveralLots()
    {
        // The first sell takes both 300 units (cost 600, realizing 200); the
        // second takes two of the three 200 units (cost 400, realizing 400).
        // One 200 unit and the 100 unit remain.
        var transactions = Of(
            Buy(1m, 100m, day: 1),
            Buy(2m, 300m, day: 2),
            Buy(3m, 200m, day: 3),
            Sell(2m, 400m, day: 4),
            Sell(2m, 400m, day: 5));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = 300m, RealizedProfitAndLoss = 600m });
    }

    [Fact]
    public void Should_TakeAProRataShareOfTheDearestLot_When_SellTakesOnlyPartOfIt()
    {
        // The dearest lot cost 402 with its fee, 201 a unit; one of its two
        // units is sold, taking 201. The other lots are untouched.
        var transactions = Of(
            Buy(1m, 50m, day: 1),
            Buy(2m, 200m, day: 2, fee: 2m),
            Buy(2m, 100m, day: 3),
            Sell(1m, 300m, day: 4));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 4m, CostBasis = 451m, RealizedProfitAndLoss = 99m });
    }

    [Fact]
    public void Should_RankAPartlySoldLotByWhatItCostWhenBought_When_ItsRemainderCostsMorePerUnit()
    {
        // Three units for 1 cost a third each. Selling one takes 0.33…33 and
        // leaves 0.66…67 on two units, a hair over a third each. The nine
        // units bought next for 3 and 1e-28 cost a third and about 1.1e-29
        // each: more than the first lot as bought, less than its remainder.
        // The lot keeps its rank as bought, so the second sell takes from the
        // nine, and its share, a ninth of their cost, rounds to 0.33…33.
        var transactions = Of(
            Buy(3m, 0m, day: 1, fee: 1m),
            Sell(1m, 0m, day: 2),
            Buy(9m, 0m, day: 3, fee: 3.0000000000000000000000000001m),
            Sell(1m, 0m, day: 4));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition
        {
            Quantity = 10m,
            CostBasis = 3.3333333333333333333333333335m,
            RealizedProfitAndLoss = -0.6666666666666666666666666666m,
        });
    }

    [Fact]
    public void Should_SellTheOlderLotFirst_When_LotsCostExactlyTheSamePerUnit()
    {
        // Three units for 1 and six for 2 both cost a third per unit. After
        // the dearer unit in between goes, the two units sold both come from
        // the older lot of three: the first takes a third, 0.33…33, and the
        // second half of the 0.66…67 left, which rounds half to even to
        // 0.33…34. Had they come from the lot of six, the second would have
        // taken a fifth of 1.66…67, rounding to 0.33…33, so the figures show
        // which lot each sell took.
        var transactions = Of(
            Buy(3m, 0m, day: 1, fee: 1m),
            Buy(1m, 1m, day: 2),
            Buy(6m, 0m, day: 3, fee: 2m),
            Sell(1m, 0m, day: 4),
            Sell(1m, 0m, day: 5),
            Sell(1m, 0m, day: 6));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition
        {
            Quantity = 7m,
            CostBasis = 2.3333333333333333333333333333m,
            RealizedProfitAndLoss = -1.6666666666666666666666666667m,
        });
    }

    [Fact]
    public void Should_SellTheDearerLot_When_CostsPerUnitDifferOnlyBeyondADecimalsDigits()
    {
        // Nine units for 3 cost a third each; nine for 3 and 1e-28 cost a
        // third and about 1.1e-29, a difference no decimal shows: both
        // quotients round to 0.33…33. The newer lot still costs more, so the
        // sell takes all of it.
        var transactions = Of(
            Buy(9m, 0m, day: 1, fee: 3m),
            Buy(9m, 0m, day: 2, fee: 3.0000000000000000000000000001m),
            Sell(9m, 0m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 9m, CostBasis = 3m, RealizedProfitAndLoss = -3.0000000000000000000000000001m });
    }

    [Fact]
    public void Should_RankWithoutOverflowing_When_CostsPerUnitGoPastADecimalsRange()
    {
        // Half a unit for 7e28 costs 1.4e29 per unit, past decimal's range,
        // and 7e28 times the other lot's two units is too; the ledger holds
        // both lots, so ranking them must not overflow. The half unit, the
        // dearer, is sold.
        var transactions = Of(Buy(2m, 0m, day: 1, fee: 1m), Buy(0.5m, 0m, day: 2, fee: 7e28m), Sell(0.5m, 0m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 2m, CostBasis = 1m, RealizedProfitAndLoss = -7e28m });
    }

    [Fact]
    public void Should_SellTheDearerLot_When_LotCostsAreWrittenToDifferentDecimalPlaces()
    {
        // The unit that cost 100.50 is written with two places and the one
        // that cost 200 with none. Their digits alone, 10050 and 200, would
        // rank the cheaper first; the 200 unit is the dearer and is sold.
        var transactions = Of(Buy(1m, 100.50m, day: 1), Buy(1m, 200m, day: 2), Sell(1m, 300m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 100.50m, RealizedProfitAndLoss = 100m });
    }

    [Fact]
    public void Should_SellTheDearerLot_When_LotQuantitiesAreWrittenToDifferentDecimalPlaces()
    {
        // Half a unit for 50.0 costs 100 per unit, one unit for 80 costs 80.
        // Ignoring the half's decimal place would read it as five units, 10
        // each, and rank it the cheaper; it is the dearer and is sold.
        var transactions = Of(Buy(1m, 80m, day: 1), Buy(0.5m, 100m, day: 2), Sell(0.5m, 120m, day: 3));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 1m, CostBasis = 80m, RealizedProfitAndLoss = 10m });
    }

    [Fact]
    public void Should_AddUpTheLotsHeldInTheOrderTheyWereBought_When_TheirTotalNeedsMoreDigitsThanADecimalHolds()
    {
        // 0.6 + 0.6 + 7e28 + 0.6 + 0.6 needs more digits than a decimal holds,
        // so each addition rounds. In the order bought: 1.2, then 7e28 + 1.2
        // rounds to 7e28 + 1, then 7e28 + 2, then 7e28 + 3. Taking the 7e28
        // lot first, as HIFO sells, would round up four times, to 7e28 + 4.
        var transactions = Of(
            Buy(1m, 0.6m, day: 1),
            Buy(1m, 0.6m, day: 2),
            Buy(1m, 7e28m, day: 3),
            Buy(1m, 0.6m, day: 4),
            Buy(1m, 0.6m, day: 5));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 5m, CostBasis = 70000000000000000000000000003m, RealizedProfitAndLoss = 0m });
    }

    [Fact]
    public void Should_TreatEarlierSequenceAsOlderLot_When_EqualCostLotsShareTimestamp()
    {
        // The ledger of the equal-cost case above, every buy at one instant
        // and loaded out of order: the lot of three, recorded first, is still
        // the older one and is sold first.
        var ledger = Of(
            Buy(3m, 0m, day: 1, fee: 1m),
            Buy(1m, 1m, day: 1),
            Buy(6m, 0m, day: 1, fee: 2m),
            Sell(1m, 0m, day: 2),
            Sell(1m, 0m, day: 3),
            Sell(1m, 0m, day: 4));
        PortfolioTransaction[] loaded = [ledger[2], ledger[1], ledger[0], ledger[3], ledger[4], ledger[5]];

        var position = _method.CalculatePosition(loaded);

        position.Should().Be(new CoinPosition
        {
            Quantity = 7m,
            CostBasis = 2.3333333333333333333333333333m,
            RealizedProfitAndLoss = -1.6666666666666666666666666667m,
        });
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
    public void Should_SellFreeUnitsLast_When_SomeBuysHaveZeroPrice()
    {
        // Three sold at 150: the two units at 100, then one of the free ones.
        var transactions = Of(Buy(2m, 0m, day: 1), Buy(2m, 100m, day: 2), Buy(2m, 0m, day: 3), Sell(3m, 150m, day: 4));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 3m, CostBasis = 0m, RealizedProfitAndLoss = 250m });
    }

    [Fact]
    public void Should_KeepEveryDigit_When_AmountsHaveLongFractionalParts()
    {
        // The two satoshis sold are the 0.0005 lot of one, bought at 50000,
        // and half of the 0.0008 lot bought at 40000, 0.0004; they sold for
        // 0.0012 less a one-satoshi-sized fee.
        var transactions = Of(
            Buy(0.00000003m, 30000m, day: 1),
            Buy(0.00000001m, 50000m, day: 2),
            Buy(0.00000002m, 40000m, day: 3),
            Sell(0.00000002m, 60000m, day: 4, fee: 0.00000001m));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 0.00000004m, CostBasis = 0.0013m, RealizedProfitAndLoss = 0.00029999m });
    }

    [Fact]
    public void Should_ThrowArgumentException_When_SellExceedsHolding()
    {
        // A portfolio never records an oversell, so the sell comes from another one.
        PortfolioTransaction[] transactions = [Of(Buy(1m, 100m, day: 1))[0], Of(Buy(2m, 100m, day: 1), Sell(2m, 200m, day: 2))[1]];

        var act = () => _method.CalculatePosition(transactions);

        // Named as a ledger names an oversell it refuses: by the sell's date too.
        act.Should().Throw<ArgumentException>().WithParameterName("transactions").WithMessage("*on 2024-01-02T00:00:00Z would take its holding below zero*");
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

    [Fact]
    public void Should_GiveBackExactlyWhatEachLotCost_When_PartlySoldLotsAreUsedUp()
    {
        // Each lot of three cost 1, which no decimal splits into thirds
        // exactly; they cost the same per unit, so the older goes first. It
        // sells one unit, then the two left, then the newer lot does the
        // same; each lot's last sell gives up all that is left of it, so
        // exactly the 2 paid is released.
        var transactions = Of(
            Buy(3m, 0m, day: 1, fee: 1m),
            Buy(3m, 0m, day: 2, fee: 1m),
            Sell(1m, 0m, day: 3),
            Sell(2m, 0m, day: 4),
            Sell(1m, 0m, day: 5),
            Sell(2m, 0m, day: 6));

        var position = _method.CalculatePosition(transactions);

        position.Should().Be(new CoinPosition { Quantity = 0m, CostBasis = 0m, RealizedProfitAndLoss = -2m });
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
        // The same cases as FIFO's: with one lot, it is the dearest, and the
        // share it gives up is worked out the same way.
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
        // No portfolio records such a buy, so each buy comes from a ledger of its own.
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
        // places, but its lots still need them. No portfolio records such a
        // buy, so each buy comes from a ledger of its own.
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
        var transactions = Of(Buy(Amount(bought), 0m, day: 1, fee: Amount(cost)), Sell(Amount(sold), 0m, day: 2));

        var position = _method.CalculatePosition(transactions);

        position.RealizedProfitAndLoss.ToString(CultureInfo.InvariantCulture).Should().Be(realizedShown);
    }

    private static decimal Amount(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
