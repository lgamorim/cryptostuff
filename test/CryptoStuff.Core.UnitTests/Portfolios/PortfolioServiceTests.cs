using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Portfolios;

public class PortfolioServiceTests
{
    private const int UnknownId = 999;

    private static readonly DateTimeOffset Now = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (FakePortfolioRepository Repository, SpyCryptocurrencyService Upstream, PortfolioService Service) CreateSut()
    {
        var repository = new FakePortfolioRepository();
        var upstream = new SpyCryptocurrencyService();
        return (repository, upstream, new PortfolioService(repository, upstream, new FakeTimeProvider(Now)));
    }

    private static Portfolio SeedPortfolio(FakePortfolioRepository repository, string name, string vsCurrency = "usd") =>
        repository.Seed(Portfolio.Create(name, vsCurrency, CostBasisMethod.Fifo));

    // Seeds a portfolio named "Long term", priced in usd, already holding the given transactions.
    private static Portfolio SeedPortfolioWith(FakePortfolioRepository repository, params TransactionDetails[] transactions)
    {
        var portfolio = Portfolio.Create("Long term", "usd", CostBasisMethod.Fifo);
        foreach (var transaction in transactions)
        {
            portfolio.AddTransaction(transaction);
        }

        return repository.Seed(portfolio);
    }

    private static CreatePortfolioCommand NewPortfolioCommand(string name) =>
        new() { Name = name, VsCurrency = "usd", CostBasisMethod = CostBasisMethod.Fifo };

    private static DateTimeOffset Day(int day) => new(2024, 1, day, 0, 0, 0, TimeSpan.Zero);

    private static TransactionDetails Buy(string coinId, decimal quantity, DateTimeOffset occurredAt) =>
        new() { Kind = TransactionKind.Buy, CoinId = coinId, Quantity = quantity, UnitPrice = 100m, OccurredAt = occurredAt };

    private static TransactionDetails Sell(string coinId, decimal quantity, DateTimeOffset occurredAt) =>
        new() { Kind = TransactionKind.Sell, CoinId = coinId, Quantity = quantity, UnitPrice = 100m, OccurredAt = occurredAt };

    private static AddTransactionCommand AddCommand(Portfolio portfolio, TransactionDetails transaction) =>
        new() { PortfolioId = portfolio.Id, Transaction = transaction };

    // What GetPricesAsync returns for a coin CoinGecko knows and prices in each of the given currencies.
    private static ServiceResult<CoinPriceView> PricedIn(string coinId, params string[] vsCurrencies) =>
        ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = coinId, Amounts = [.. vsCurrencies.Select(c => new CurrencyAmount { Currency = c, Amount = 100m })] }],
        });

    // Asserts the result succeeded and returns what it carries.
    private static T SucceededValue<T>(ServiceResult<T> result)
    {
        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    // Details a host's validators would have rejected; the service relies on the entity to throw.
    public static TheoryData<TransactionDetails> InvalidTransactions() =>
    [
        Buy("bitcoin", 0m, Day(1)),
        Buy("bitcoin", -1m, Day(1)),
        Buy("bitcoin", 1m, Day(1)) with { UnitPrice = -0.01m },
        Buy("bitcoin", 1m, Day(1)) with { Fee = -0.01m },
        Buy(" ", 1m, Day(1)),
        // `!` lets the case pass null past the non-nullable property to exercise the guard.
        Buy(null!, 1m, Day(1)),
        Buy("bitcoin", 1m, Day(1)) with { Kind = (TransactionKind)99 },
    ];

    // ListAsync.

    [Fact]
    public async Task Should_ReturnEveryPortfolioInIdOrder_When_ListAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        SeedPortfolio(repository, "Long term");
        SeedPortfolio(repository, "Trading", "eur");

        var result = await service.ListAsync(CancellationToken.None);

        var portfolios = SucceededValue(result);
        portfolios.Select(p => p.Name).Should().Equal("Long term", "Trading");
        portfolios[1].VsCurrency.Should().Be("eur");
    }

    // GetAsync.

    [Fact]
    public async Task Should_ReturnPortfolio_When_GetAsyncCalledWithExistingId()
    {
        var (repository, _, service) = CreateSut();
        SeedPortfolio(repository, "Long term");
        var trading = SeedPortfolio(repository, "Trading");

        var result = await service.GetAsync(trading.Id, CancellationToken.None);

        SucceededValue(result).Name.Should().Be("Trading");
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingId_When_GetAsyncCalledWithUnknownId()
    {
        var (_, _, service) = CreateSut();

        var result = await service.GetAsync(UnknownId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
    }

    // CreateAsync.

    [Fact]
    public async Task Should_SaveAndReturnEmptyPortfolioWithAssignedId_When_CreateAsyncNameIsNew()
    {
        var (repository, _, service) = CreateSut();
        var command = new CreatePortfolioCommand { Name = "Long term", VsCurrency = "USD", CostBasisMethod = CostBasisMethod.AverageCost };

        var result = await service.CreateAsync(command, CancellationToken.None);

        var view = SucceededValue(result);
        view.Id.Should().BePositive();
        view.Name.Should().Be("Long term");
        view.VsCurrency.Should().Be("usd");
        view.CostBasisMethod.Should().Be(CostBasisMethod.AverageCost);
        view.Transactions.Should().BeEmpty();
        repository.Saved.Should().ContainSingle().Which.Id.Should().Be(view.Id);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingNameWithoutSaving_When_CreateAsyncNameAlreadyExists()
    {
        var (repository, _, service) = CreateSut();
        SeedPortfolio(repository, "Long term");

        var result = await service.CreateAsync(NewPortfolioCommand("Long term"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("Long term");
        repository.SaveChangesCallCount.Should().Be(0);
        repository.Saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ReturnConflictNamingName_When_CreateAsyncSaveLosesUniqueNameRace()
    {
        var (repository, _, service) = CreateSut();
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.CreateAsync(NewPortfolioCommand("Long term"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("Long term");
        repository.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CreateAsyncNameDiffersFromExistingOnlyInCase()
    {
        var (repository, _, service) = CreateSut();
        SeedPortfolio(repository, "Long term");

        var result = await service.CreateAsync(NewPortfolioCommand("LONG TERM"), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
    }

    [Fact]
    public async Task Should_ThrowArgumentExceptionWithoutSaving_When_CreateAsyncNameIsBlank()
    {
        var (repository, _, service) = CreateSut();

        var act = () => service.CreateAsync(NewPortfolioCommand(" "), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // UpdateAsync.

    [Fact]
    public async Task Should_SaveAndReturnUpdatedPortfolio_When_UpdateAsyncNameIsNew()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        var command = new UpdatePortfolioCommand { PortfolioId = portfolio.Id, Name = "Retirement", CostBasisMethod = CostBasisMethod.AverageCost };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        var view = SucceededValue(result);
        view.Name.Should().Be("Retirement");
        view.CostBasisMethod.Should().Be(CostBasisMethod.AverageCost);
        view.Version.Should().Be(2);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnConflictAndKeepPortfolioWithoutSaving_When_UpdateAsyncNameBelongsToAnotherPortfolio()
    {
        var (repository, _, service) = CreateSut();
        SeedPortfolio(repository, "Trading");
        var portfolio = SeedPortfolio(repository, "Long term");
        var command = new UpdatePortfolioCommand { PortfolioId = portfolio.Id, Name = "trading", CostBasisMethod = CostBasisMethod.AverageCost };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("trading");
        portfolio.Name.Should().Be("Long term");
        portfolio.Version.Should().Be(1);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingPortfolio_When_UpdateAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        repository.SaveChangesException = new PersistenceConflictException();
        var command = new UpdatePortfolioCommand { PortfolioId = portfolio.Id, Name = "Retirement", CostBasisMethod = CostBasisMethod.Fifo };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("Retirement").And.Contain($"Portfolio {portfolio.Id}");
    }

    [Fact]
    public async Task Should_Succeed_When_UpdateAsyncChangesOnlyTheCaseOfItsOwnName()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        var command = new UpdatePortfolioCommand { PortfolioId = portfolio.Id, Name = "Long Term", CostBasisMethod = CostBasisMethod.Fifo };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        SucceededValue(result).Name.Should().Be("Long Term");
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingIdWithoutSaving_When_UpdateAsyncIdIsUnknown()
    {
        var (repository, _, service) = CreateSut();
        var command = new UpdatePortfolioCommand { PortfolioId = UnknownId, Name = "Retirement", CostBasisMethod = CostBasisMethod.Fifo };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // DeleteAsync.

    [Fact]
    public async Task Should_RemovePortfolio_When_DeleteAsyncIdExists()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        var kept = SeedPortfolio(repository, "Trading");

        var result = await service.DeleteAsync(portfolio.Id, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Saved.Should().Equal(kept);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingPortfolio_When_DeleteAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.DeleteAsync(portfolio.Id, expectedVersion: null, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain($"Portfolio {portfolio.Id}");
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingIdWithoutSaving_When_DeleteAsyncIdIsUnknown()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.DeleteAsync(UnknownId, expectedVersion: null, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // AddTransactionAsync.

    [Fact]
    public async Task Should_CheckCoinUpstreamThenRecordAndSave_When_AddTransactionAsyncTransactionIsValid()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 0.5m, Day(1))), CancellationToken.None);

        SucceededValue(result).Transactions.Should().ContainSingle().Which.Quantity.Should().Be(0.5m);
        upstream.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] });
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnTransactionWithIdAssignedBySave_When_AddTransactionAsyncSucceeds()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), CancellationToken.None);

        SucceededValue(result).Transactions.Should().ContainSingle().Which.Id.Should().BePositive();
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinWithoutRecordingOrSaving_When_AddTransactionAsyncCoinIsUnknownToCoinGecko()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("notacoin", 1m, Day(1))), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("notacoin");
        portfolio.Transactions.Should().BeEmpty();
        portfolio.Version.Should().Be(1);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PropagateUpstreamFailureWithoutRecordingOrSaving_When_AddTransactionAsyncCoinCheckIsRateLimited()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.RateLimited, "Slow down.");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RateLimited);
        result.ErrorDetail.Should().Be("Slow down.");
        portfolio.Transactions.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCurrencyWithoutRecordingOrSaving_When_AddTransactionAsyncCoinHasNoPriceInPortfolioCurrency()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term", "xyz");
        upstream.PricesResult = PricedIn("bitcoin");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("xyz").And.Contain("bitcoin");
        portfolio.Transactions.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_CheckAndStoreLowercasedCoin_When_AddTransactionAsyncCoinHasUppercaseLetters()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("BitCoin", 1m, Day(1))), CancellationToken.None);

        upstream.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery { CoinIds = ["bitcoin"], VsCurrencies = ["usd"] });
        SucceededValue(result).Transactions.Should().ContainSingle().Which.CoinId.Should().Be("bitcoin");
    }

    [Theory]
    [MemberData(nameof(InvalidTransactions))]
    public async Task Should_ThrowArgumentExceptionWithoutUpstreamCallOrSave_When_AddTransactionAsyncDetailsBreakTheRules(TransactionDetails transaction)
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");

        var act = () => service.AddTransactionAsync(AddCommand(portfolio, transaction), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedWithoutUpstreamCallOrSave_When_AddTransactionAsyncIsDatedOneTickAfterNow()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Now.AddTicks(1))), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("future");
        portfolio.Transactions.Should().BeEmpty();
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Record_When_AddTransactionAsyncIsDatedExactlyNow()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Now)), CancellationToken.None);

        SucceededValue(result).Transactions.Should().ContainSingle().Which.OccurredAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(2, 0, true)] // 14:00 at +02:00 reads later than the clock's 12:00 UTC, but is the same instant.
    [InlineData(-5, 1, false)] // 07:00 at -05:00 reads earlier than the clock's 12:00 UTC, but is one tick after it.
    public async Task Should_CompareInstantsNotWallClocks_When_AddTransactionAsyncTimestampHasAnotherOffset(int offsetHours, int ticksAfterNow, bool expectedSuccess)
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        var occurredAt = Now.AddTicks(ticksAfterNow).ToOffset(TimeSpan.FromHours(offsetHours));

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, occurredAt)), CancellationToken.None);

        result.IsSuccess.Should().Be(expectedSuccess);
    }

    [Fact]
    public async Task Should_ReadTheClockOnEachCall_When_AddTransactionAsyncIsRetriedAfterItsDateHasPassed()
    {
        var repository = new FakePortfolioRepository();
        var upstream = new SpyCryptocurrencyService { PricesResult = PricedIn("bitcoin", "usd") };
        var clock = new FakeTimeProvider(Now);
        var service = new PortfolioService(repository, upstream, clock);
        var portfolio = SeedPortfolio(repository, "Long term");
        var command = AddCommand(portfolio, Buy("bitcoin", 1m, Now.AddHours(1)));
        await service.AddTransactionAsync(command, CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(1));

        var result = await service.AddTransactionAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinAndDateWithoutSaving_When_AddTransactionAsyncSellWouldOversell()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        portfolio.AddTransaction(Buy("bitcoin", 1m, Day(1)));
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Sell("bitcoin", 1.5m, Day(2))), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("2024-01-02");
        portfolio.Transactions.Should().ContainSingle();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingPortfolio_When_AddTransactionAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain($"Portfolio {portfolio.Id}");
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutUpstreamCall_When_AddTransactionAsyncPortfolioIsUnknown()
    {
        var (repository, upstream, service) = CreateSut();
        var command = new AddTransactionCommand { PortfolioId = UnknownId, Transaction = Buy("bitcoin", 1m, Day(1)) };

        var result = await service.AddTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // ReplaceTransactionAsync.

    [Fact]
    public async Task Should_CheckNewCoinUpstreamThenReplaceKeepingIdAndSequence_When_ReplaceTransactionAsyncChangesTheCoin()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        var transaction = portfolio.Transactions[0];
        upstream.PricesResult = PricedIn("ethereum", "usd");
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = transaction.Id, Transaction = Buy("ethereum", 2m, Day(2)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        var replaced = SucceededValue(result).Transactions.Should().ContainSingle().Subject;
        replaced.Id.Should().Be(transaction.Id);
        replaced.Sequence.Should().Be(1);
        replaced.CoinId.Should().Be("ethereum");
        replaced.Quantity.Should().Be(2m);
        upstream.CapturedPricesQuery.Should().BeEquivalentTo(new CoinPriceQuery { CoinIds = ["ethereum"], VsCurrencies = ["usd"] });
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReplaceWithoutUpstreamCall_When_ReplaceTransactionAsyncKeepsTheCoinInAnyCase()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id, Transaction = Buy("Bitcoin", 3m, Day(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        SucceededValue(result).Transactions.Should().ContainSingle().Which.Quantity.Should().Be(3m);
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutChangingEitherPortfolio_When_ReplaceTransactionAsyncTransactionBelongsToAnotherPortfolio()
    {
        var (repository, upstream, service) = CreateSut();
        var other = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        var portfolio = SeedPortfolio(repository, "Trading");
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = other.Transactions[0].Id, Transaction = Buy("bitcoin", 5m, Day(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain($"Portfolio {portfolio.Id}").And.Contain($"id {other.Transactions[0].Id}");
        other.Transactions[0].Quantity.Should().Be(1m);
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithoutUpstreamCall_When_ReplaceTransactionAsyncPortfolioIsUnknown()
    {
        var (repository, upstream, service) = CreateSut();
        var command = new ReplaceTransactionCommand { PortfolioId = UnknownId, TransactionId = 1, Transaction = Buy("bitcoin", 1m, Day(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinAndDateWithoutSaving_When_ReplaceTransactionAsyncMovesSellBeforeItsBuy()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(2)), Sell("bitcoin", 1m, Day(3)));
        var sell = portfolio.Transactions[1];
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = sell.Id, Transaction = Sell("bitcoin", 1m, Day(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("2024-01-01");
        sell.OccurredAt.Should().Be(Day(3));
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedWithoutUpstreamCallOrSave_When_ReplaceTransactionAsyncIsDatedOneTickAfterNow()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id, Transaction = Buy("ethereum", 1m, Now.AddTicks(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("future");
        portfolio.Transactions[0].CoinId.Should().Be("bitcoin");
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinWithoutSaving_When_ReplaceTransactionAsyncNewCoinIsUnknownToCoinGecko()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id, Transaction = Buy("notacoin", 1m, Day(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("notacoin");
        portfolio.Transactions[0].CoinId.Should().Be("bitcoin");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ThrowArgumentExceptionWithoutUpstreamCallOrSave_When_ReplaceTransactionAsyncDetailsBreakTheRules()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id, Transaction = Buy("ethereum", 0m, Day(1)) };

        var act = () => service.ReplaceTransactionAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    // RemoveTransactionAsync.

    [Fact]
    public async Task Should_RemoveAndSave_When_RemoveTransactionAsyncLeavesEveryHoldingCovered()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)), Buy("bitcoin", 1m, Day(2)), Sell("bitcoin", 1m, Day(3)));
        var command = new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        SucceededValue(result).Transactions.Select(t => t.Sequence).Should().Equal(2, 3);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinAndDateWithoutSaving_When_RemoveTransactionAsyncRemovesBuyALaterSellNeeds()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)), Sell("bitcoin", 1m, Day(5)));
        var command = new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("2024-01-05");
        portfolio.Transactions.Should().HaveCount(2);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingTransactionWithoutSaving_When_RemoveTransactionAsyncTransactionIdIsUnknown()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        var command = new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = UnknownId };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("transaction with id 999");
        portfolio.Transactions.Should().ContainSingle();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingPortfolio_When_RemoveTransactionAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        repository.SaveChangesException = new PersistenceConflictException();
        var command = new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain($"Portfolio {portfolio.Id}");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_RemoveTransactionAsyncPortfolioIsUnknown()
    {
        var (repository, _, service) = CreateSut();
        var command = new RemoveTransactionCommand { PortfolioId = UnknownId, TransactionId = 1 };

        var result = await service.RemoveTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingPortfolio_When_ReplaceTransactionAsyncSaveThrowsPersistenceConflict()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        repository.SaveChangesException = new PersistenceConflictException();
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id, Transaction = Buy("bitcoin", 2m, Day(1)) };

        var result = await service.ReplaceTransactionAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain($"Portfolio {portfolio.Id}");
    }

    // Cancellation.

    [Fact]
    public async Task Should_PropagateCancellationWithoutRecordingOrSaving_When_AddTransactionAsyncUpstreamCheckIsCancelled()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.ExceptionToThrow = new OperationCanceledException();

        var act = () => service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        portfolio.Transactions.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PropagateCancellationRatherThanConflict_When_AddTransactionAsyncSaveIsCancelled()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        repository.SaveChangesException = new OperationCanceledException();

        var act = () => service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepositoryAndUpstream_When_AddTransactionAsyncCalled()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        using var cts = new CancellationTokenSource();

        await service.AddTransactionAsync(AddCommand(portfolio, Buy("bitcoin", 1m, Day(1))), cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
        upstream.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepositoryAndUpstream_When_ReplaceTransactionAsyncChangesTheCoin()
    {
        var (repository, upstream, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        upstream.PricesResult = PricedIn("ethereum", "usd");
        var command = new ReplaceTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id, Transaction = Buy("ethereum", 1m, Day(1)) };
        using var cts = new CancellationTokenSource();

        await service.ReplaceTransactionAsync(command, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
        upstream.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_RemoveTransactionAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolioWith(repository, Buy("bitcoin", 1m, Day(1)));
        using var cts = new CancellationTokenSource();

        await service.RemoveTransactionAsync(new RemoveTransactionCommand { PortfolioId = portfolio.Id, TransactionId = portfolio.Transactions[0].Id }, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_UpdateAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        using var cts = new CancellationTokenSource();

        await service.UpdateAsync(new UpdatePortfolioCommand { PortfolioId = portfolio.Id, Name = "Retirement", CostBasisMethod = CostBasisMethod.Fifo }, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(3).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_CreateAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        using var cts = new CancellationTokenSource();

        await service.CreateAsync(NewPortfolioCommand("Long term"), cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_DeleteAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        using var cts = new CancellationTokenSource();

        await service.DeleteAsync(portfolio.Id, expectedVersion: null, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepository_When_ListAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        using var cts = new CancellationTokenSource();

        await service.ListAsync(cts.Token);

        repository.CapturedCancellationTokens.Should().ContainSingle().Which.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepository_When_GetAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var portfolio = SeedPortfolio(repository, "Long term");
        using var cts = new CancellationTokenSource();

        await service.GetAsync(portfolio.Id, cts.Token);

        repository.CapturedCancellationTokens.Should().ContainSingle().Which.Should().Be(cts.Token);
    }
}
