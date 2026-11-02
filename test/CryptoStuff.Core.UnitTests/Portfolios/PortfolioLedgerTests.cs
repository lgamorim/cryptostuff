using System.Globalization;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class PortfolioLedgerTests
{
    private static Portfolio NewPortfolio() => Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);

    private static DateTimeOffset Day(int day) => new(2024, 1, day, 0, 0, 0, TimeSpan.Zero);

    private static TransactionDetails Buy(string coinId, decimal quantity, DateTimeOffset occurredAt) =>
        new() { Kind = TransactionKind.Buy, CoinId = coinId, Quantity = quantity, UnitPrice = 100m, OccurredAt = occurredAt };

    private static TransactionDetails Sell(string coinId, decimal quantity, DateTimeOffset occurredAt) =>
        new() { Kind = TransactionKind.Sell, CoinId = coinId, Quantity = quantity, UnitPrice = 100m, OccurredAt = occurredAt };

    // Transactions.

    [Fact]
    public void Should_ListTransactionsByTimeThenSequence_When_TransactionsWereLoadedInAnotherOrder()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(3)));
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(1)));
        PortfolioStorage.ReverseLoadedTransactionOrder(portfolio);

        var transactions = portfolio.Transactions;

        transactions.Select(t => t.Sequence).Should().Equal(2, 3, 1);
    }

    [Fact]
    public void Should_FindTransactionById_When_IdBelongsToOneOfItsTransactions()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("ethereum", 1m, Day(2)));
        PortfolioStorage.AssignId(portfolio.Transactions[0], 41);
        PortfolioStorage.AssignId(portfolio.Transactions[1], 42);

        var found = portfolio.FindTransaction(42);

        found.Should().BeSameAs(portfolio.Transactions[1]);
    }

    [Fact]
    public void Should_ReturnNull_When_NoTransactionHasTheId()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        PortfolioStorage.AssignId(portfolio.Transactions[0], 41);

        var found = portfolio.FindTransaction(999);

        found.Should().BeNull();
    }

    [Fact]
    public void Should_NotExposeMutableBackingList_When_TransactionsRead()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));

        var transactions = portfolio.Transactions;

        transactions.Should().BeAssignableTo<IList<PortfolioTransaction>>().Which.IsReadOnly.Should().BeTrue();
    }

    // AddTransaction.

    [Fact]
    public void Should_RecordTransactionWithItsDetails_When_BuyAdded()
    {
        var portfolio = NewPortfolio();
        var details = new TransactionDetails
        {
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 60000m,
            Fee = 12.5m,
            OccurredAt = Day(1),
        };

        var outcome = portfolio.AddTransaction(details);

        outcome.IsValid.Should().BeTrue();
        portfolio.Transactions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 60000m,
            Fee = 12.5m,
            OccurredAt = Day(1),
        });
    }

    [Fact]
    public void Should_RecordZeroFee_When_FeeOmitted()
    {
        var portfolio = NewPortfolio();

        portfolio.AddTransaction(new TransactionDetails
        {
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 1m,
            UnitPrice = 100m,
            OccurredAt = Day(1),
        });

        portfolio.Transactions.Single().Fee.Should().Be(0m);
    }

    [Fact]
    public void Should_NumberTransactionsInInsertionOrder_When_TransactionsAdded()
    {
        var portfolio = NewPortfolio();

        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(3)));
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(2)));

        portfolio.Transactions.Select(t => (t.OccurredAt, t.Sequence)).Should().BeEquivalentTo(
            [(Day(3), 1), (Day(1), 2), (Day(2), 3)]);
    }

    [Fact]
    public void Should_MoveToNextVersion_When_TransactionAdded()
    {
        var portfolio = NewPortfolio();

        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));

        portfolio.Version.Should().Be(2);
    }

    [Fact]
    public void Should_StoreLowercasedCoinId_When_TransactionAddedWithUppercaseLetters()
    {
        var portfolio = NewPortfolio();

        portfolio.AddTransaction(Buy("BitCoin", 1m, Day(1)));

        portfolio.Transactions.Single().CoinId.Should().Be("bitcoin");
    }

    [Fact]
    public void Should_StoreSameInstantWithZeroOffset_When_TransactionAddedWithNonUtcOffset()
    {
        var portfolio = NewPortfolio();
        var occurredAt = new DateTimeOffset(2024, 1, 1, 9, 30, 0, TimeSpan.FromHours(2));

        portfolio.AddTransaction(Buy("bitcoin", 1m, occurredAt));

        var stored = portfolio.Transactions.Single().OccurredAt;
        stored.Should().Be(new DateTimeOffset(2024, 1, 1, 7, 30, 0, TimeSpan.Zero));
        stored.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00000001")]
    [InlineData("-1")]
    public void Should_ThrowWithoutRecordingOrNewVersion_When_QuantityIsNotPositive(string quantity)
    {
        var portfolio = NewPortfolio();

        var act = () => portfolio.AddTransaction(Buy("bitcoin", decimal.Parse(quantity, CultureInfo.InvariantCulture), Day(1)));

        act.Should().Throw<ArgumentException>();
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
    }

    [Theory]
    [InlineData("-0.00000001")]
    [InlineData("-1")]
    public void Should_ThrowWithoutRecordingOrNewVersion_When_UnitPriceIsNegative(string unitPrice)
    {
        var portfolio = NewPortfolio();

        var act = () => portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)) with { UnitPrice = decimal.Parse(unitPrice, CultureInfo.InvariantCulture) });

        act.Should().Throw<ArgumentException>();
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
    }

    [Fact]
    public void Should_RecordTransaction_When_UnitPriceIsZero()
    {
        var portfolio = NewPortfolio();

        var outcome = portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)) with { UnitPrice = 0m });

        outcome.IsValid.Should().BeTrue();
        portfolio.Transactions.Single().UnitPrice.Should().Be(0m);
    }

    [Theory]
    [InlineData("-0.00000001")]
    [InlineData("-1")]
    public void Should_ThrowWithoutRecordingOrNewVersion_When_FeeIsNegative(string fee)
    {
        var portfolio = NewPortfolio();

        var act = () => portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)) with { Fee = decimal.Parse(fee, CultureInfo.InvariantCulture) });

        act.Should().Throw<ArgumentException>();
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowWithoutRecordingOrNewVersion_When_CoinIdIsBlank(string? coinId)
    {
        var portfolio = NewPortfolio();

        // `!` lets the theory pass null past the non-nullable property to exercise the guard.
        var act = () => portfolio.AddTransaction(Buy(coinId!, 1m, Day(1)));

        act.Should().Throw<ArgumentException>();
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
    }

    [Fact]
    public void Should_ThrowWithoutRecordingOrNewVersion_When_KindIsUndefined()
    {
        var portfolio = NewPortfolio();

        var act = () => portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)) with { Kind = (TransactionKind)99 });

        act.Should().Throw<ArgumentOutOfRangeException>();
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
    }

    // The replay invariant on add.

    [Fact]
    public void Should_RejectNamingCoinAndDateWithoutRecordingOrNewVersion_When_SellHasNothingToSell()
    {
        var portfolio = NewPortfolio();

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 1m, Day(5)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-05");
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
    }

    [Fact]
    public void Should_RecordSell_When_EarlierBuyCoversIt()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 2m, Day(1)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 1.5m, Day(2)));

        outcome.IsValid.Should().BeTrue();
        portfolio.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public void Should_RejectNamingCoinAndDate_When_SellIsDatedBeforeTheOnlyBuy()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(3)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 1m, Day(2)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-02");
        portfolio.Transactions.Should().ContainSingle();
    }

    [Fact]
    public void Should_RecordSell_When_HoldingReachesExactlyZero()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1.5m, Day(1)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 1.5m, Day(2)));

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_RejectSmallestFurtherSell_When_HoldingIsExactlyZero()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1.5m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1.5m, Day(2)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 0.00000001m, Day(3)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-03");
    }

    [Fact]
    public void Should_RecordSell_When_DecimalBuysSumExactlyToIt()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 0.1m, Day(1)));
        portfolio.AddTransaction(Buy("bitcoin", 0.2m, Day(2)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 0.3m, Day(3)));

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_RejectNamingTheSoldCoin_When_OnlyAnotherCoinIsHeld()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 5m, Day(1)));

        var outcome = portfolio.AddTransaction(Sell("ethereum", 1m, Day(2)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("ethereum").And.NotContain("bitcoin");
    }

    [Fact]
    public void Should_KeepEachCoinsHoldingIndependent_When_SeveralCoinsAreTraded()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("ethereum", 10m, Day(2)));
        portfolio.AddTransaction(Sell("ethereum", 10m, Day(3)));

        var bitcoinSell = portfolio.AddTransaction(Sell("bitcoin", 1m, Day(4)));
        var ethereumSell = portfolio.AddTransaction(Sell("ethereum", 0.00000001m, Day(5)));

        bitcoinSell.IsValid.Should().BeTrue();
        ethereumSell.IsValid.Should().BeFalse();
        ethereumSell.ErrorMessage.Should().Contain("ethereum");
    }

    [Fact]
    public void Should_RejectNamingTheLaterSell_When_BackdatedSellLeavesTooLittleForIt()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 2m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 2m, Day(10)));

        // Fine on its own date, with 2 held, but it leaves only 1 for the sell on day 10.
        var outcome = portfolio.AddTransaction(Sell("bitcoin", 1m, Day(5)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-10");
        portfolio.Transactions.Should().HaveCount(2);
    }

    [Fact]
    public void Should_RejectSell_When_HoldingAtItsDateIsShortEvenThoughLaterBuysCoverIt()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(10)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 2m, Day(5)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-05");
    }

    [Fact]
    public void Should_RecordSell_When_BuyAtTheSameInstantWasRecordedFirst()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));

        var outcome = portfolio.AddTransaction(Sell("bitcoin", 1m, Day(1)));

        outcome.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_ReplaySameInstantInSequenceOrder_When_TransactionsWereLoadedInAnotherOrder()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(1)));
        PortfolioStorage.ReverseLoadedTransactionOrder(portfolio);

        var outcome = portfolio.AddTransaction(Buy("ethereum", 1m, Day(2)));

        outcome.IsValid.Should().BeTrue();
    }

    // RemoveTransaction.

    [Fact]
    public void Should_RemoveTransaction_When_SellRemoved()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(2)));
        var sell = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Sell);

        var outcome = portfolio.RemoveTransaction(sell);

        outcome.IsValid.Should().BeTrue();
        portfolio.Transactions.Should().ContainSingle().Which.Kind.Should().Be(TransactionKind.Buy);
    }

    [Fact]
    public void Should_MoveToNextVersion_When_TransactionRemoved()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));

        portfolio.RemoveTransaction(portfolio.Transactions.Single());

        portfolio.Version.Should().Be(3);
    }

    [Fact]
    public void Should_RejectNamingTheSellAndKeepLedgerAndVersion_When_RemovedBuyIsNeededByLaterSell()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(7)));
        var buy = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Buy);

        var outcome = portfolio.RemoveTransaction(buy);

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-07");
        portfolio.Transactions.Should().HaveCount(2);
        portfolio.Version.Should().Be(3);
    }

    [Fact]
    public void Should_ThrowAndKeepLedgerAndVersion_When_RemovedTransactionBelongsToAnotherPortfolio()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        var other = NewPortfolio();
        other.AddTransaction(Buy("bitcoin", 1m, Day(1)));

        var act = () => portfolio.RemoveTransaction(other.Transactions.Single());

        act.Should().Throw<ArgumentException>();
        portfolio.Transactions.Should().ContainSingle();
        portfolio.Version.Should().Be(2);
    }

    [Fact]
    public void Should_NumberNewTransactionAfterEveryRemainingOne_When_EarlierTransactionWasRemoved()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(2)));
        portfolio.RemoveTransaction(portfolio.Transactions.First());

        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(3)));

        portfolio.Transactions.Select(t => t.Sequence).Should().Equal(2, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_RejectRemoval_When_OnlyBuyAtTheSellsInstantWasRecordedAfterTheSell(bool loadedInAnotherOrder)
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(5)));
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(5)));
        var firstBuy = portfolio.Transactions.Single(t => t.Sequence == 1);
        if (loadedInAnotherOrder)
        {
            PortfolioStorage.ReverseLoadedTransactionOrder(portfolio);
        }

        // Without the day-1 buy, the sell comes first at day 5: it was recorded before the day-5 buy.
        var outcome = portfolio.RemoveTransaction(firstBuy);

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-05");
        portfolio.Transactions.Should().HaveCount(3);
    }

    // ReplaceTransaction.

    [Fact]
    public void Should_ReplaceDetailsAndKeepSequence_When_TransactionReplaced()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("bitcoin", 2m, Day(2)));
        var second = portfolio.Transactions.Single(t => t.Sequence == 2);
        var replacement = new TransactionDetails
        {
            Kind = TransactionKind.Sell,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 70000m,
            Fee = 3m,
            OccurredAt = Day(4),
        };

        var outcome = portfolio.ReplaceTransaction(second, replacement);

        outcome.IsValid.Should().BeTrue();
        portfolio.Transactions.Should().HaveCount(2);
        second.Should().BeEquivalentTo(new
        {
            Sequence = 2,
            Kind = TransactionKind.Sell,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 70000m,
            Fee = 3m,
            OccurredAt = Day(4),
        });
    }

    [Fact]
    public void Should_MoveToNextVersion_When_TransactionReplaced()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));

        portfolio.ReplaceTransaction(portfolio.Transactions.Single(), Buy("bitcoin", 2m, Day(1)));

        portfolio.Version.Should().Be(3);
    }

    [Fact]
    public void Should_RejectNamingNewDateAndKeepTransactionAndVersion_When_SellIsMovedBeforeItsBuy()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(3)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(6)));
        var sell = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Sell);

        var outcome = portfolio.ReplaceTransaction(sell, Sell("bitcoin", 1m, Day(2)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-02");
        sell.OccurredAt.Should().Be(Day(6));
        portfolio.Version.Should().Be(3);
    }

    [Fact]
    public void Should_ThrowAndKeepVersion_When_ReplacedTransactionBelongsToAnotherPortfolio()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        var other = NewPortfolio();
        other.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        var foreign = other.Transactions.Single();

        var act = () => portfolio.ReplaceTransaction(foreign, Buy("bitcoin", 5m, Day(1)));

        act.Should().Throw<ArgumentException>();
        foreign.Quantity.Should().Be(1m);
        portfolio.Version.Should().Be(2);
    }

    [Fact]
    public void Should_RejectNamingTheOldCoinsSell_When_BuyItNeedsIsChangedToAnotherCoin()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(8)));
        var buy = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Buy);

        var outcome = portfolio.ReplaceTransaction(buy, Buy("ethereum", 1m, Day(1)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-08");
        buy.CoinId.Should().Be("bitcoin");
    }

    [Fact]
    public void Should_RejectNamingTheNewCoin_When_SellIsChangedToACoinNotHeld()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(8)));
        var sell = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Sell);

        var outcome = portfolio.ReplaceTransaction(sell, Sell("ethereum", 1m, Day(8)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("ethereum").And.Contain("2024-01-08");
        sell.CoinId.Should().Be("bitcoin");
    }

    [Fact]
    public void Should_ReplaceCoin_When_EveryHoldingStaysCovered()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        portfolio.AddTransaction(Buy("ethereum", 1m, Day(2)));
        portfolio.AddTransaction(Sell("bitcoin", 1m, Day(8)));
        var sell = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Sell);

        var outcome = portfolio.ReplaceTransaction(sell, Sell("ethereum", 1m, Day(8)));

        outcome.IsValid.Should().BeTrue();
        sell.CoinId.Should().Be("ethereum");
    }

    [Fact]
    public void Should_RejectNamingTheLaterSell_When_BuyQuantityIsReducedBelowWhatItNeeds()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 2m, Day(1)));
        portfolio.AddTransaction(Sell("bitcoin", 1.5m, Day(8)));
        var buy = portfolio.Transactions.Single(t => t.Kind == TransactionKind.Buy);

        var outcome = portfolio.ReplaceTransaction(buy, Buy("bitcoin", 1m, Day(1)));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("bitcoin").And.Contain("2024-01-08");
        buy.Quantity.Should().Be(2m);
    }

    [Fact]
    public void Should_ThrowAndKeepTransactionAndVersion_When_ReplacementDetailsAreInvalid()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        var buy = portfolio.Transactions.Single();

        var act = () => portfolio.ReplaceTransaction(buy, Buy("bitcoin", 0m, Day(1)));

        act.Should().Throw<ArgumentException>();
        buy.Quantity.Should().Be(1m);
        portfolio.Version.Should().Be(2);
    }

    [Fact]
    public void Should_StoreLowercasedCoinAndUtcInstant_When_ReplacementUsesOtherCaseAndOffset()
    {
        var portfolio = NewPortfolio();
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        var buy = portfolio.Transactions.Single();

        portfolio.ReplaceTransaction(buy, Buy("Ethereum", 1m, new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.FromHours(3))));

        buy.CoinId.Should().Be("ethereum");
        buy.OccurredAt.Should().Be(new DateTimeOffset(2024, 1, 1, 6, 0, 0, TimeSpan.Zero));
        buy.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
