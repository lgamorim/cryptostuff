using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class PortfolioValuationTests
{
    private const int UnknownId = 999;

    private static readonly DateTimeOffset Now = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (FakePortfolioRepository Repository, SpyCryptocurrencyService Upstream, PortfolioService Service) CreateSut()
    {
        var repository = new FakePortfolioRepository();
        var upstream = new SpyCryptocurrencyService();
        return (repository, upstream, new PortfolioService(repository, upstream, new FakeTimeProvider(Now)));
    }

    private static Portfolio SeedPortfolio(FakePortfolioRepository repository, CostBasisMethod method, params TransactionDetails[] transactions) =>
        SeedPortfolioIn(repository, "usd", method, transactions);

    private static Portfolio SeedPortfolioIn(FakePortfolioRepository repository, string vsCurrency, CostBasisMethod method, params TransactionDetails[] transactions)
    {
        var portfolio = Portfolio.Create("Long term", vsCurrency, method);
        foreach (var transaction in transactions)
        {
            portfolio.AddTransaction(transaction);
        }

        return repository.Seed(portfolio);
    }

    // Asserts the result succeeded and returns what it carries.
    private static T SucceededValue<T>(ServiceResult<T> result)
    {
        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    private static DateTimeOffset Day(int day) => Ledger.Day(day);

    private static TransactionDetails Buy(decimal quantity, decimal unitPrice, int day, decimal fee = 0m, string coinId = "bitcoin") =>
        Ledger.Buy(quantity, unitPrice, day, fee, coinId);

    private static TransactionDetails Sell(decimal quantity, decimal unitPrice, int day, decimal fee = 0m, string coinId = "bitcoin") =>
        Ledger.Sell(quantity, unitPrice, day, fee, coinId);

    // What GetPricesAsync returns when CoinGecko prices each coin in the given currency.
    private static ServiceResult<CoinPriceView> PricedIn(string currency, params (string CoinId, decimal Price)[] prices) =>
        ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [.. prices.Select(p => new CoinPrice { CoinId = p.CoinId, Amounts = [new CurrencyAmount { Currency = currency, Amount = p.Price }] })],
        });

    private static ServiceResult<CoinPriceView> PricedInUsd(params (string CoinId, decimal Price)[] prices) => PricedIn("usd", prices);

    [Fact]
    public async Task Should_ReturnNotFound_When_PortfolioIdUnknown()
    {
        var (_, upstream, service) = CreateSut();

        var result = await service.GetValuationAsync(UnknownId, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain(UnknownId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnEmptyValuationWithoutCallingUpstream_When_PortfolioHasNoTransactions()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo);

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.Coins.Should().BeEmpty();
        valuation.Totals.Should().Be(new PortfolioValuationTotals { MarketValue = 0m, CostBasis = 0m, UnrealizedProfitAndLoss = 0m, RealizedProfitAndLoss = 0m });
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ValueTheOpenPositionAtTheLivePrice_When_OneCoinIsHeld()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(2m, 100m, 1, fee: 4m));
        upstream.PricesResult = PricedInUsd(("bitcoin", 150m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.Coins.Should().Equal(new CoinValuationView
        {
            CoinId = "bitcoin",
            Quantity = 2m,
            MarketValue = 300m,
            CostBasis = 204m,
            UnrealizedProfitAndLoss = 96m,
            RealizedProfitAndLoss = 0m,
        });
        valuation.Totals.Should().Be(new PortfolioValuationTotals { MarketValue = 300m, CostBasis = 204m, UnrealizedProfitAndLoss = 96m, RealizedProfitAndLoss = 0m });
    }

    // Bitcoin: buys 1 at 10,000 (fee 10) and 1 at 20,000 (fee 20), then sells 1 at 30,000 (fee 30).
    // Ethereum: buys 10 at 2,000 (fee 10). Priced at 25,000 and 1,500, so ethereum is under water.
    private static Portfolio SeedMixedPortfolio(FakePortfolioRepository repository, CostBasisMethod method) =>
        SeedPortfolio(
            repository,
            method,
            Buy(1m, 10_000m, 1, fee: 10m),
            Buy(1m, 20_000m, 2, fee: 20m),
            Sell(1m, 30_000m, 3, fee: 30m),
            Buy(10m, 2_000m, 4, fee: 10m, coinId: "ethereum"));

    [Fact]
    public async Task Should_ValueMixedPortfolioFromAverageCostPositions_When_MethodIsAverageCost()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedMixedPortfolio(repository, CostBasisMethod.AverageCost);
        upstream.PricesResult = PricedInUsd(("bitcoin", 25_000m), ("ethereum", 1_500m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.Coins.Should().Equal(
            new CoinValuationView { CoinId = "bitcoin", Quantity = 1m, MarketValue = 25_000m, CostBasis = 15_015m, UnrealizedProfitAndLoss = 9_985m, RealizedProfitAndLoss = 14_955m },
            new CoinValuationView { CoinId = "ethereum", Quantity = 10m, MarketValue = 15_000m, CostBasis = 20_010m, UnrealizedProfitAndLoss = -5_010m, RealizedProfitAndLoss = 0m });
        valuation.Totals.Should().Be(new PortfolioValuationTotals { MarketValue = 40_000m, CostBasis = 35_025m, UnrealizedProfitAndLoss = 4_975m, RealizedProfitAndLoss = 14_955m });
    }

    [Fact]
    public async Task Should_ValueMixedPortfolioFromFifoPositions_When_MethodIsFifo()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedMixedPortfolio(repository, CostBasisMethod.Fifo);
        upstream.PricesResult = PricedInUsd(("bitcoin", 25_000m), ("ethereum", 1_500m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.Coins.Should().Equal(
            new CoinValuationView { CoinId = "bitcoin", Quantity = 1m, MarketValue = 25_000m, CostBasis = 20_020m, UnrealizedProfitAndLoss = 4_980m, RealizedProfitAndLoss = 19_960m },
            new CoinValuationView { CoinId = "ethereum", Quantity = 10m, MarketValue = 15_000m, CostBasis = 20_010m, UnrealizedProfitAndLoss = -5_010m, RealizedProfitAndLoss = 0m });
        valuation.Totals.Should().Be(new PortfolioValuationTotals { MarketValue = 40_000m, CostBasis = 40_030m, UnrealizedProfitAndLoss = -30m, RealizedProfitAndLoss = 19_960m });
    }

    [Fact]
    public async Task Should_KeepRealizedProfitWithoutCallingUpstream_When_EveryPositionIsClosed()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(1m, 100m, 1), Sell(1m, 150m, 2, fee: 5m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.Coins.Should().Equal(new CoinValuationView
        {
            CoinId = "bitcoin",
            Quantity = 0m,
            MarketValue = 0m,
            CostBasis = 0m,
            UnrealizedProfitAndLoss = 0m,
            RealizedProfitAndLoss = 45m,
        });
        valuation.Totals.Should().Be(new PortfolioValuationTotals { MarketValue = 0m, CostBasis = 0m, UnrealizedProfitAndLoss = 0m, RealizedProfitAndLoss = 45m });
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_RequestOnlyOpenCoinsInThePortfolioCurrencyWithOneCall_When_SomeCoinsAreClosed()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioIn(
            repository,
            "eur",
            CostBasisMethod.Fifo,
            Buy(1m, 100m, 1),
            Buy(5m, 10m, 2, coinId: "ethereum"),
            Sell(5m, 12m, 3, coinId: "ethereum"),
            Buy(300m, 1m, 4, coinId: "dogecoin"));
        upstream.PricesResult = PricedIn("eur", ("bitcoin", 120m), ("dogecoin", 2m));

        await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        upstream.GetPricesCallCount.Should().Be(1);
        upstream.CapturedPricesQuery.Should().NotBeNull();
        // `!` is safe: the assertion above fails the test unless the query was captured.
        upstream.CapturedPricesQuery!.CoinIds.Should().Equal("bitcoin", "dogecoin");
        upstream.CapturedPricesQuery.VsCurrencies.Should().Equal("eur");
    }

    [Fact]
    public async Task Should_ReturnUpstreamUnavailableNamingTheCoin_When_ResponseLacksAnOpenCoin()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(1m, 100m, 1), Buy(1m, 10m, 2, coinId: "ethereum"));
        upstream.PricesResult = PricedInUsd(("bitcoin", 120m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
        result.ErrorDetail.Should().Contain("ethereum").And.Contain("usd");
    }

    [Fact]
    public async Task Should_ReturnUpstreamUnavailableNamingTheCurrency_When_ResponseLacksThePortfolioCurrency()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioIn(repository, "eur", CostBasisMethod.Fifo, Buy(1m, 100m, 1));
        upstream.PricesResult = PricedInUsd(("bitcoin", 120m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("eur");
    }

    [Theory]
    [InlineData(ServiceErrorCode.RateLimited)]
    [InlineData(ServiceErrorCode.RequestTimedOut)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PropagateUpstreamFailureWithItsCodeAndDetail_When_PriceLookupFails(ServiceErrorCode errorCode)
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(1m, 100m, 1));
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(errorCode, "Upstream said no.");

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
        result.ErrorDetail.Should().Be("Upstream said no.");
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepositoryAndUpstream_When_GetValuationAsyncCalled()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(1m, 100m, 1));
        upstream.PricesResult = PricedInUsd(("bitcoin", 120m));
        using var cts = new CancellationTokenSource();

        await service.GetValuationAsync(portfolio.Id, cts.Token);

        repository.CapturedCancellationTokens.Should().ContainSingle().Which.Should().Be(cts.Token);
        upstream.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_UpstreamLookupIsCancelled()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(1m, 100m, 1));
        upstream.ExceptionToThrow = new OperationCanceledException();

        var act = () => service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ReportNegativeUnrealizedProfitAndLoss_When_PriceFellBelowCost()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.AverageCost, Buy(1m, 200m, 1));
        upstream.PricesResult = PricedInUsd(("bitcoin", 150m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.Coins.Should().ContainSingle().Which.UnrealizedProfitAndLoss.Should().Be(-50m);
        valuation.Totals.UnrealizedProfitAndLoss.Should().Be(-50m);
    }

    [Fact]
    public async Task Should_NameThePortfolioItsCurrencyAndItsMethod_When_Valued()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioIn(repository, "eur", CostBasisMethod.AverageCost);

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        valuation.PortfolioId.Should().Be(portfolio.Id);
        valuation.VsCurrency.Should().Be("eur");
        valuation.CostBasisMethod.Should().Be(CostBasisMethod.AverageCost);
    }

    [Fact]
    public async Task Should_KeepEveryFigureExact_When_AmountsHaveManyDecimalPlaces()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(
            repository,
            CostBasisMethod.Fifo,
            Buy(0.12345678m, 43_210.123456m, 1),
            Buy(0.5m, 30_000.5555m, 2),
            Sell(0.12345678m, 45_000.123456789m, 3, fee: 0.000001m));
        upstream.PricesResult = PricedInUsd(("bitcoin", 51_234.56789m));

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        var valuation = SucceededValue(result);
        var expected = new CoinValuationView
        {
            CoinId = "bitcoin",
            Quantity = 0.5m,
            MarketValue = 25_617.283945m,
            CostBasis = 15_000.27775m,
            UnrealizedProfitAndLoss = 10_617.006195m,
            RealizedProfitAndLoss = 220.98763529740739942m,
        };
        valuation.Coins.Should().Equal(expected);
        valuation.Totals.Should().Be(new PortfolioValuationTotals
        {
            MarketValue = expected.MarketValue,
            CostBasis = expected.CostBasis,
            UnrealizedProfitAndLoss = expected.UnrealizedProfitAndLoss,
            RealizedProfitAndLoss = expected.RealizedProfitAndLoss,
        });
    }

    [Fact]
    public async Task Should_ReturnUpstreamUnavailableNamingTheHeldCoins_When_UpstreamDoesNotKnowAHeldCoin()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, CostBasisMethod.Fifo, Buy(1m, 100m, 1), Buy(1m, 10m, 2, coinId: "ethereum"));
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);

        var result = await service.GetValuationAsync(portfolio.Id, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.UpstreamUnavailable);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("ethereum");
    }
}
