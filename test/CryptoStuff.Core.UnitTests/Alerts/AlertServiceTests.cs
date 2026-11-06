using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Alerts;

public class AlertServiceTests
{
    private const int UnknownId = 999;

    private static readonly DateTimeOffset Now = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (FakeAlertRepository Repository, SpyCryptocurrencyService Upstream, AlertService Service) CreateSut()
    {
        var repository = new FakeAlertRepository();
        var upstream = new SpyCryptocurrencyService();
        return (repository, upstream, new AlertService(repository, upstream));
    }

    private static PriceAlert SeedAlert(FakeAlertRepository repository, string coinId = "bitcoin", string vsCurrency = "usd") =>
        repository.Seed(PriceAlert.Create(coinId, vsCurrency, AlertDirection.Above, 100m));

    private static CreateAlertCommand NewAlertCommand(string coinId = "bitcoin", string vsCurrency = "usd", decimal threshold = 100m) =>
        new() { CoinId = coinId, VsCurrency = vsCurrency, Direction = AlertDirection.Above, Threshold = threshold };

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

    // ListAsync.

    [Fact]
    public async Task Should_ReturnEveryAlertInIdOrder_When_ListAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        SeedAlert(repository, "bitcoin");
        SeedAlert(repository, "ethereum", "eur");

        var result = await service.ListAsync(CancellationToken.None);

        var alerts = SucceededValue(result);
        alerts.Select(a => a.CoinId).Should().Equal("bitcoin", "ethereum");
        alerts[1].VsCurrency.Should().Be("eur");
    }

    // GetAsync.

    [Fact]
    public async Task Should_ReturnAlert_When_GetAsyncCalledWithExistingId()
    {
        var (repository, _, service) = CreateSut();
        SeedAlert(repository, "bitcoin");
        var ethereum = SeedAlert(repository, "ethereum");

        var result = await service.GetAsync(ethereum.Id, CancellationToken.None);

        SucceededValue(result).CoinId.Should().Be("ethereum");
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
    public async Task Should_SaveAndReturnArmedAlertWithAssignedId_When_CreateAsyncCoinAndCurrencyAreKnown()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var result = await service.CreateAsync(NewAlertCommand(), CancellationToken.None);

        var view = SucceededValue(result);
        view.Id.Should().BePositive();
        view.CoinId.Should().Be("bitcoin");
        view.VsCurrency.Should().Be("usd");
        view.Direction.Should().Be(AlertDirection.Above);
        view.Threshold.Should().Be(100m);
        view.State.Should().Be(AlertState.Armed);
        view.Version.Should().Be(1);
        repository.Saved.Should().ContainSingle().Which.Id.Should().Be(view.Id);
    }

    [Fact]
    public async Task Should_AskCoinGeckoOnceForTheLowercasedCoinInTheLowercasedCurrency_When_CreateAsyncGivenMixedCase()
    {
        var (_, upstream, service) = CreateSut();
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        await service.CreateAsync(NewAlertCommand("BitCoin", "USD"), CancellationToken.None);

        upstream.GetPricesCallCount.Should().Be(1);
        upstream.CapturedPricesQuery.Should().NotBeNull();

        // `!` is safe: the assertion above fails the test unless a query was captured.
        upstream.CapturedPricesQuery!.CoinIds.Should().Equal("bitcoin");
        upstream.CapturedPricesQuery.VsCurrencies.Should().Equal("usd");
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinWithoutSaving_When_CreateAsyncCoinIsUnknownToCoinGecko()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);

        var result = await service.CreateAsync(NewAlertCommand("notacoin"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("notacoin");
        repository.SaveChangesCallCount.Should().Be(0);
        repository.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnRuleViolatedNamingCoinAndCurrencyWithoutSaving_When_CreateAsyncCurrencyHasNoPriceForTheCoin()
    {
        var (repository, upstream, service) = CreateSut();
        // CoinGecko knows the coin but prices it only in eur: an unknown currency is not an upstream error.
        upstream.PricesResult = PricedIn("bitcoin", "eur");

        var result = await service.CreateAsync(NewAlertCommand("bitcoin", "notacurrency"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.RuleViolated);
        result.ErrorDetail.Should().Contain("bitcoin").And.Contain("notacurrency");
        repository.SaveChangesCallCount.Should().Be(0);
        repository.Saved.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ServiceErrorCode.RateLimited)]
    [InlineData(ServiceErrorCode.RequestTimedOut)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PassOnTheUpstreamErrorWithoutSaving_When_CreateAsyncCoinCheckFailsUpstream(ServiceErrorCode errorCode)
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(errorCode, "upstream detail");

        var result = await service.CreateAsync(NewAlertCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
        result.ErrorDetail.Should().Be("upstream detail");
        repository.SaveChangesCallCount.Should().Be(0);
        repository.Saved.Should().BeEmpty();
    }

    // Details a host's validators would have rejected; the service relies on the entity to throw.
    public static TheoryData<CreateAlertCommand> InvalidNewAlerts() =>
    [
        NewAlertCommand(threshold: 0m),
        NewAlertCommand(threshold: -1m),
        NewAlertCommand(threshold: -0.00000001m),
        NewAlertCommand(coinId: " "),
        NewAlertCommand(vsCurrency: " "),
        NewAlertCommand() with { Direction = (AlertDirection)99 },
        // `!` lets the case pass null past the non-nullable property to exercise the guard.
        NewAlertCommand(coinId: null!),
    ];

    [Theory]
    [MemberData(nameof(InvalidNewAlerts))]
    public async Task Should_ThrowBeforeAskingCoinGeckoOrSaving_When_CreateAsyncGivenDetailsThatBreakTheEntityRules(CreateAlertCommand command)
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = PricedIn("bitcoin", "usd");

        var act = () => service.CreateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        upstream.GetPricesCallCount.Should().Be(0);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CreateAsyncSaveLosesARace()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.CreateAsync(NewAlertCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().NotBeNullOrWhiteSpace();
        repository.Saved.Should().BeEmpty();
    }

    // UpdateAsync.

    [Fact]
    public async Task Should_SaveAndReturnReplacedDirectionAndThreshold_When_UpdateAsyncAlertExists()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Below, Threshold = 50m };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        var view = SucceededValue(result);
        view.Direction.Should().Be(AlertDirection.Below);
        view.Threshold.Should().Be(50m);
        view.State.Should().Be(AlertState.Armed);
        view.Version.Should().Be(2);
        alert.Direction.Should().Be(AlertDirection.Below);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingIdWithoutSaving_When_UpdateAsyncAlertDoesNotExist()
    {
        var (repository, _, service) = CreateSut();
        var command = new UpdateAlertCommand { AlertId = UnknownId, Direction = AlertDirection.Below, Threshold = 50m };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReArmAndClearTheTriggerRecord_When_UpdateAsyncAlertHasTriggered()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(105m, Now);
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Above, Threshold = 200m };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        var view = SucceededValue(result);
        view.State.Should().Be(AlertState.Armed);
        view.TriggeredAt.Should().BeNull();
        view.TriggeredPrice.Should().BeNull();
        alert.State.Should().Be(AlertState.Armed);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_UpdateAsyncSaveLosesARace()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        repository.SaveChangesException = new PersistenceConflictException();
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Below, Threshold = 50m };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain(alert.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Should_NotAskCoinGecko_When_UpdateAsyncCalled()
    {
        var (repository, upstream, service) = CreateSut();
        var alert = SeedAlert(repository);
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Below, Threshold = 50m };

        await service.UpdateAsync(command, CancellationToken.None);

        upstream.GetPricesCallCount.Should().Be(0);
    }

    // Details a host's validators would have rejected; the service relies on the entity to throw.
    public static TheoryData<UpdateAlertCommand> InvalidAlertUpdates() =>
    [
        new UpdateAlertCommand { AlertId = 1, Direction = AlertDirection.Below, Threshold = 0m },
        new UpdateAlertCommand { AlertId = 1, Direction = AlertDirection.Below, Threshold = -1m },
        new UpdateAlertCommand { AlertId = 1, Direction = (AlertDirection)99, Threshold = 50m },
    ];

    [Theory]
    [MemberData(nameof(InvalidAlertUpdates))]
    public async Task Should_ThrowWithoutSavingAndLeaveAlertUnchanged_When_UpdateAsyncGivenDetailsThatBreakTheEntityRules(UpdateAlertCommand command)
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);

        var act = () => service.UpdateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        repository.SaveChangesCallCount.Should().Be(0);
        alert.Threshold.Should().Be(100m);
        alert.Direction.Should().Be(AlertDirection.Above);
        alert.Version.Should().Be(1);
    }

    // DeleteAsync.

    [Fact]
    public async Task Should_RemoveAndSave_When_DeleteAsyncAlertExists()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);

        var result = await service.DeleteAsync(alert.Id, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingIdWithoutSaving_When_DeleteAsyncAlertDoesNotExist()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.DeleteAsync(UnknownId, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_DeleteAsyncSaveLosesARace()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.DeleteAsync(alert.Id, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain(alert.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // RearmAsync.

    [Fact]
    public async Task Should_SaveAndReturnArmedAlertWithTheTriggerRecordCleared_When_RearmAsyncAlertHasTriggered()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(105m, Now);

        var result = await service.RearmAsync(alert.Id, expectedVersion: null, CancellationToken.None);

        var view = SucceededValue(result);
        view.State.Should().Be(AlertState.Armed);
        view.TriggeredAt.Should().BeNull();
        view.TriggeredPrice.Should().BeNull();
        view.Version.Should().Be(3);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnConflictNamingIdWithoutSaving_When_RearmAsyncAlertIsAlreadyArmed()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);

        var result = await service.RearmAsync(alert.Id, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain("armed");
        repository.SaveChangesCallCount.Should().Be(0);
        alert.Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnNotFoundNamingIdWithoutSaving_When_RearmAsyncAlertDoesNotExist()
    {
        var (repository, _, service) = CreateSut();

        var result = await service.RearmAsync(UnknownId, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        result.ErrorDetail.Should().Contain("999");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_RearmAsyncSaveLosesARace()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(105m, Now);
        repository.SaveChangesException = new PersistenceConflictException();

        var result = await service.RearmAsync(alert.Id, expectedVersion: null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ServiceErrorCode.Conflict);
        result.ErrorDetail.Should().Contain(alert.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // Cancellation.

    [Fact]
    public async Task Should_PropagateCancellationRatherThanConflict_When_CreateAsyncSaveIsCancelled()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        repository.SaveChangesException = new OperationCanceledException();

        var act = () => service.CreateAsync(NewAlertCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_PropagateCancellationRatherThanConflict_When_UpdateDeleteOrRearmSaveIsCancelled()
    {
        var (repository, _, service) = CreateSut();
        // One alert per operation: a failed save still leaves the in-memory change on its alert.
        var toUpdate = SeedAlert(repository);
        var toDelete = SeedAlert(repository);
        var toRearm = SeedAlert(repository);
        toRearm.Trigger(105m, Now);
        repository.SaveChangesException = new OperationCanceledException();
        var update = new UpdateAlertCommand { AlertId = toUpdate.Id, Direction = AlertDirection.Below, Threshold = 50m };

        var updating = () => service.UpdateAsync(update, CancellationToken.None);
        var deleting = () => service.DeleteAsync(toDelete.Id, expectedVersion: null, CancellationToken.None);
        var rearming = () => service.RearmAsync(toRearm.Id, expectedVersion: null, CancellationToken.None);

        await updating.Should().ThrowAsync<OperationCanceledException>();
        await deleting.Should().ThrowAsync<OperationCanceledException>();
        await rearming.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_PropagateCancellationWithoutSaving_When_CreateAsyncUpstreamCheckIsCancelled()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.ExceptionToThrow = new OperationCanceledException();

        var act = () => service.CreateAsync(NewAlertCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToRepositoryAndUpstream_When_CreateAsyncCalled()
    {
        var (repository, upstream, service) = CreateSut();
        upstream.PricesResult = PricedIn("bitcoin", "usd");
        using var cts = new CancellationTokenSource();

        await service.CreateAsync(NewAlertCommand(), cts.Token);

        repository.CapturedCancellationTokens.Should().ContainSingle().Which.Should().Be(cts.Token);
        upstream.CapturedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_UpdateAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        using var cts = new CancellationTokenSource();
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Below, Threshold = 50m };

        await service.UpdateAsync(command, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_DeleteAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        using var cts = new CancellationTokenSource();

        await service.DeleteAsync(alert.Id, expectedVersion: null, cts.Token);

        repository.CapturedCancellationTokens.Should().HaveCount(2).And.OnlyContain(token => token == cts.Token);
    }

    [Fact]
    public async Task Should_ForwardCancellationTokenToEveryRepositoryCall_When_RearmAsyncCalled()
    {
        var (repository, _, service) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(105m, Now);
        using var cts = new CancellationTokenSource();

        await service.RearmAsync(alert.Id, expectedVersion: null, cts.Token);

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
        using var cts = new CancellationTokenSource();

        await service.GetAsync(UnknownId, cts.Token);

        repository.CapturedCancellationTokens.Should().ContainSingle().Which.Should().Be(cts.Token);
    }
}
