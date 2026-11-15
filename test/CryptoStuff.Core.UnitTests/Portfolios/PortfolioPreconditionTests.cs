using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Portfolios;

// The expected versions a write may carry: the API's If-Match, which may list
// several entity tags. Omitted, a write is unconditional, which every test in
// PortfolioServiceTests covers.
public class PortfolioPreconditionTests
{
    private const int UnknownId = 999;

    private static readonly DateTimeOffset Now = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (FakePortfolioRepository Repository, SpyCryptocurrencyService Upstream, PortfolioService Service) CreateSut()
    {
        var repository = new FakePortfolioRepository();
        var upstream = new SpyCryptocurrencyService();
        return (repository, upstream, new PortfolioService(repository, upstream, new FakeTimeProvider(Now)));
    }

    private static Portfolio SeedPortfolio(FakePortfolioRepository repository) =>
        repository.Seed(Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo));

    private static TransactionDetails Buy(decimal quantity, int day) => Ledger.Buy(quantity, 100m, day);

    // Seeds a portfolio already holding one buy, so it is at version 2.
    private static Portfolio SeedPortfolioWithOneBuy(FakePortfolioRepository repository)
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        portfolio.AddTransaction(Buy(1m, 1));
        return repository.Seed(portfolio);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedNamingVersionsWithoutSaving_When_UpdateAsyncExpectedVersionIsStale()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);
        var command = new UpdatePortfolioCommand
        {
            PortfolioId = portfolio.Id,
            Name = "Retirement",
            CostBasisMethod = CostBasisMethod.AverageCost,
            ExpectedVersions = [2],
        };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 1, not the expected version 2; reload it and try again.");
        portfolio.Name.Should().Be("Long term");
        portfolio.Version.Should().Be(1);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SaveAndReturnNextVersion_When_UpdateAsyncExpectedVersionMatches()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);
        var command = new UpdatePortfolioCommand
        {
            PortfolioId = portfolio.Id,
            Name = "Retirement",
            CostBasisMethod = CostBasisMethod.AverageCost,
            ExpectedVersions = [1],
        };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Version.Should().Be(2);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutRemoving_When_DeleteAsyncExpectedVersionIsStale()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);

        var result = await service.DeleteAsync(portfolio.Id, expectedVersions: [2], CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 1, not the expected version 2; reload it and try again.");
        repository.Saved.Should().Equal(portfolio);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutUpstreamCallOrRecording_When_AddTransactionAsyncExpectedVersionIsStale()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);
        var command = new AddTransactionCommand { PortfolioId = portfolio.Id, Transaction = Buy(1m, 1), ExpectedVersions = [2] };

        var result = await service.AddTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 1, not the expected version 2; reload it and try again.");
        portfolio.Transactions.Should().BeEmpty();
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutUpstreamCallOrReplacing_When_ReplaceTransactionAsyncExpectedVersionIsStale()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWithOneBuy(repository);
        var transaction = portfolio.Transactions.Single();
        var command = new ReplaceTransactionCommand
        {
            PortfolioId = portfolio.Id,
            TransactionId = transaction.Id,
            Transaction = Buy(2m, 1) with { CoinId = "ethereum" },
            ExpectedVersions = [1],
        };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 2, not the expected version 1; reload it and try again.");
        transaction.CoinId.Should().Be("bitcoin");
        transaction.Quantity.Should().Be(1m);
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutRemoving_When_RemoveTransactionAsyncExpectedVersionIsStale()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWithOneBuy(repository);
        var transaction = portfolio.Transactions.Single();
        var command = new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = transaction.Id, ExpectedVersions = [1] };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 2, not the expected version 1; reload it and try again.");
        portfolio.Transactions.Should().Equal(transaction);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_RecordAndReturnNextVersion_When_AddTransactionAsyncExpectedVersionMatches()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 100m }] }],
        });
        var portfolio = SeedPortfolioWithOneBuy(repository);
        var command = new AddTransactionCommand { PortfolioId = portfolio.Id, Transaction = Buy(1m, 2), ExpectedVersions = [2] };

        var result = await service.AddTransactionAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        result.Value!.Version.Should().Be(3);
        result.Value.Transactions.Should().HaveCount(2);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Save_When_UpdateAsyncExpectedVersionsIncludeTheCurrentOneAmongOthers()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);
        var command = new UpdatePortfolioCommand
        {
            PortfolioId = portfolio.Id,
            Name = "Retirement",
            CostBasisMethod = CostBasisMethod.AverageCost,
            ExpectedVersions = [3, 1],
        };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedNamingEveryExpectedVersionInOrder_When_UpdateAsyncExpectedVersionsAreAllStale()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);
        var command = new UpdatePortfolioCommand
        {
            PortfolioId = portfolio.Id,
            Name = "Retirement",
            CostBasisMethod = CostBasisMethod.AverageCost,
            ExpectedVersions = [3, 2, 3],
        };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 1, not any of the expected versions 2, 3; reload it and try again.");
        portfolio.Name.Should().Be("Long term");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_NameTheOneVersion_When_DeleteAsyncExpectedVersionsRepeatOneStaleVersion()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);

        var result = await service.DeleteAsync(portfolio.Id, expectedVersions: [2, 2], CancellationToken.None);

        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 1, not the expected version 2; reload it and try again.");
    }

    // An empty set expects no version at all, so nothing matches it.
    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutRemoving_When_DeleteAsyncExpectedVersionsAreEmpty()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository);

        var result = await service.DeleteAsync(portfolio.Id, expectedVersions: [], CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} is at version 1, not a version the caller expected; reload it and try again.");
        repository.Saved.Should().Equal(portfolio);
    }

    // RFC 9110 has a server ignore a precondition when the request would fail
    // without it, so a missing transaction is a 404 whatever If-Match says.
    [Fact]
    public async Task Should_ReturnNotFoundNamingTheTransaction_When_ReplaceTransactionAsyncTransactionIsUnknownAndExpectedVersionIsStale()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWithOneBuy(repository);
        var command = new ReplaceTransactionCommand
        {
            PortfolioId = portfolio.Id,
            TransactionId = UnknownId,
            Transaction = Buy(2m, 1),
            ExpectedVersions = [1],
        };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} has no transaction with id {UnknownId}.");
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingTheTransaction_When_RemoveTransactionAsyncTransactionIsUnknownAndExpectedVersionIsStale()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWithOneBuy(repository);
        var command = new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = UnknownId, ExpectedVersions = [1] };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Be($"Portfolio {portfolio.Id} has no transaction with id {UnknownId}.");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // A missing portfolio has no version to compare, so the API answers 404, not 412.
    [Fact]
    public async Task Should_ReturnNotFound_When_DeleteAsyncIdIsUnknownAndExpectedVersionGiven()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.DeleteAsync(UnknownId, expectedVersions: [1], CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
