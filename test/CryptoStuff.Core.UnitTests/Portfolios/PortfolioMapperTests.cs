using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class PortfolioMapperTests
{
    [Fact]
    public void Should_MapIdNameCurrencyMethodAndVersion_When_PortfolioMapped()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        PortfolioStorage.AssignId(portfolio, 7);
        portfolio.Update("Retirement", CostBasisMethod.AverageCost);

        var view = PortfolioMapper.ToView(portfolio);

        view.Id.Should().Be(7);
        view.Name.Should().Be("Retirement");
        view.VsCurrency.Should().Be("usd");
        view.CostBasisMethod.Should().Be(CostBasisMethod.AverageCost);
        view.Version.Should().Be(2);
    }

    [Fact]
    public void Should_MapEveryTransactionFieldInLedgerOrder_When_PortfolioMapped()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        var occurredAt = new DateTimeOffset(2024, 1, 2, 9, 30, 0, TimeSpan.Zero);
        portfolio.AddTransaction(new TransactionDetails
        {
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 42000.25m,
            Fee = 1.5m,
            OccurredAt = occurredAt,
        });
        portfolio.AddTransaction(new TransactionDetails
        {
            Kind = TransactionKind.Buy,
            CoinId = "ethereum",
            Quantity = 2m,
            UnitPrice = 2000m,
            OccurredAt = occurredAt.AddDays(-1),
        });
        PortfolioStorage.AssignId(portfolio.Transactions[1], 11);

        var view = PortfolioMapper.ToView(portfolio);

        view.Transactions.Select(t => t.CoinId).Should().Equal("ethereum", "bitcoin");
        view.Transactions[1].Should().Be(new PortfolioTransactionView
        {
            Id = 11,
            Sequence = 1,
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 42000.25m,
            Fee = 1.5m,
            OccurredAt = occurredAt,
        });
    }

    [Fact]
    public void Should_KeepViewUnchanged_When_PortfolioChangedAfterMapping()
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        var buy = new TransactionDetails { Kind = TransactionKind.Buy, CoinId = "bitcoin", Quantity = 1m, UnitPrice = 100m, OccurredAt = DateTimeOffset.UnixEpoch };
        portfolio.AddTransaction(buy);
        var view = PortfolioMapper.ToView(portfolio);

        portfolio.ReplaceTransaction(portfolio.Transactions[0], buy with { Quantity = 2m });
        portfolio.AddTransaction(buy);

        view.Transactions.Should().ContainSingle().Which.Quantity.Should().Be(1m);
    }
}
