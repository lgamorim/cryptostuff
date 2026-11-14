using System.Globalization;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.UnitTests.TestSupport;
using static CryptoStuff.Core.UnitTests.TestSupport.PriceResults;

namespace CryptoStuff.Core.UnitTests.Alerts;

public class AlertEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (FakeAlertRepository Repository, SpyCryptocurrencyService Upstream, FakeTimeProvider Clock, AlertEvaluator Evaluator) CreateSut()
    {
        var repository = new FakeAlertRepository();
        var upstream = new SpyCryptocurrencyService();
        var clock = new FakeTimeProvider(Now);
        return (repository, upstream, clock, new AlertEvaluator(repository, upstream, clock));
    }

    private static PriceAlert SeedAlert(
        FakeAlertRepository repository,
        string coinId = "bitcoin",
        string vsCurrency = "usd",
        AlertDirection direction = AlertDirection.Above,
        decimal threshold = 100m) =>
        repository.Seed(PriceAlert.Create(coinId, vsCurrency, direction, threshold));

    private static UnpricedAlertView Unpriced(PriceAlert alert) =>
        new() { Id = alert.Id, CoinId = alert.CoinId, VsCurrency = alert.VsCurrency };

    // Asserts the result succeeded and returns what it carries.
    private static T SucceededValue<T>(ServiceResult<T> result)
    {
        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    [Fact]
    public async Task Should_ReturnNothingTriggeredWithoutCallingUpstream_When_NoAlertIsArmed()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var triggered = repository.Seed(PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m));
        triggered.Trigger(150m, Now).IsValid.Should().BeTrue();

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().BeEmpty();
        upstream.GetPricesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_TriggerAlertAndReportIt_When_PriceMeetsThreshold()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedAlert(repository, "bitcoin", "usd", AlertDirection.Above, 100m);
        upstream.PricesResult = Prices(("bitcoin", "usd", 150.5m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        var view = SucceededValue(result);
        view.Unpriced.Should().BeEmpty();
        var triggered = view.Triggered.Should().ContainSingle().Subject;
        triggered.Id.Should().Be(alert.Id);
        triggered.CoinId.Should().Be("bitcoin");
        triggered.VsCurrency.Should().Be("usd");
        triggered.Direction.Should().Be(AlertDirection.Above);
        triggered.Threshold.Should().Be(100m);
        triggered.Price.Should().Be(150.5m);
        triggered.TriggeredAt.Should().Be(Now);
        alert.State.Should().Be(AlertState.Triggered);
        alert.TriggeredAt.Should().Be(Now);
        alert.TriggeredPrice.Should().Be(150.5m);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_MakeOneUpstreamCallForTheUnionOfCoinsAndCurrencies_When_AlertsMixCoinsAndCurrencies()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        SeedAlert(repository, "bitcoin", "usd");
        SeedAlert(repository, "ethereum", "eur");
        SeedAlert(repository, "bitcoin", "eur");
        SeedAlert(repository, "bitcoin", "usd", AlertDirection.Below, 50m);
        upstream.PricesResult = Prices(
            ("bitcoin", "usd", 75m),
            ("bitcoin", "eur", 75m),
            ("ethereum", "eur", 75m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        upstream.GetPricesCallCount.Should().Be(1);
        var query = upstream.CapturedPricesQuery;
        query.Should().NotBeNull();
        query?.CoinIds.Should().Equal("bitcoin", "ethereum");
        query?.VsCurrencies.Should().Equal("usd", "eur");
    }

    [Theory]
    [InlineData(AlertDirection.Above)]
    [InlineData(AlertDirection.Below)]
    public async Task Should_TriggerAlert_When_PriceEqualsThreshold(AlertDirection direction)
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedAlert(repository, direction: direction, threshold: 100m);
        upstream.PricesResult = Prices(("bitcoin", "usd", 100m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().ContainSingle().Which.Id.Should().Be(alert.Id);
        alert.State.Should().Be(AlertState.Triggered);
    }

    [Theory]
    [InlineData(AlertDirection.Above, "99.99999999")]
    [InlineData(AlertDirection.Below, "100.00000001")]
    public async Task Should_LeaveAlertArmedAndUnsaved_When_PriceDoesNotMeetIt(AlertDirection direction, string priceLiteral)
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedAlert(repository, direction: direction, threshold: 100m);
        upstream.PricesResult = Prices(("bitcoin", "usd", decimal.Parse(priceLiteral, CultureInfo.InvariantCulture)));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().BeEmpty();
        alert.State.Should().Be(AlertState.Armed);
        alert.Version.Should().Be(1);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(ServiceErrorCode.RateLimited)]
    [InlineData(ServiceErrorCode.RequestTimedOut)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PropagateErrorAndTriggerNothing_When_UpstreamFails(ServiceErrorCode errorCode)
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var first = SeedAlert(repository, "bitcoin");
        var second = SeedAlert(repository, "ethereum");
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(errorCode, "upstream said no");

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
        result.ErrorDetail.Should().Be("upstream said no");
        first.State.Should().Be(AlertState.Armed);
        second.State.Should().Be(AlertState.Armed);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_TriggerKnownCoinAlertAndReportUnknownCoinAlertUnpriced_When_UpstreamDoesNotKnowACoin()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var known = SeedAlert(repository, "bitcoin");
        var unknown = SeedAlert(repository, "gone-coin");
        upstream.PricesResultFor = KnowsOnly(("bitcoin", "usd", 150m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        var view = SucceededValue(result);
        view.Triggered.Should().ContainSingle().Which.Id.Should().Be(known.Id);
        view.Unpriced.Should().Equal(Unpriced(unknown));
        known.State.Should().Be(AlertState.Triggered);
        unknown.State.Should().Be(AlertState.Armed);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReportEveryAlertUnpricedAndTriggerNothing_When_UpstreamKnowsNoneOfTheCoins()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var first = SeedAlert(repository, "gone-coin", "usd");
        var second = SeedAlert(repository, "lost-coin", "eur");
        upstream.PricesResult = ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound);

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        var view = SucceededValue(result);
        view.Triggered.Should().BeEmpty();
        view.Unpriced.Should().Equal(Unpriced(first), Unpriced(second));
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(ServiceErrorCode.RateLimited)]
    [InlineData(ServiceErrorCode.RequestTimedOut)]
    [InlineData(ServiceErrorCode.UpstreamUnavailable)]
    public async Task Should_PropagateErrorAndTriggerNothing_When_AskingForOneCoinFails(ServiceErrorCode errorCode)
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var priced = SeedAlert(repository, "bitcoin");
        var failing = SeedAlert(repository, "ethereum");
        SeedAlert(repository, "gone-coin");
        upstream.PricesResultFor = query => query.CoinIds switch
        {
            ["bitcoin"] => Prices(("bitcoin", "usd", 150m)),
            ["ethereum"] => ServiceResult<CoinPriceView>.Failure(errorCode, "upstream said no"),
            _ => ServiceResult<CoinPriceView>.Failure(ServiceErrorCode.NotFound),
        };

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
        result.ErrorDetail.Should().Be("upstream said no");
        priced.State.Should().Be(AlertState.Armed);
        failing.State.Should().Be(AlertState.Armed);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("coin")]
    [InlineData("currency")]
    public async Task Should_TriggerPricedAlertAndReportTheOtherUnpricedWithoutAskingAgain_When_PriceIsMissingFromResponse(string missing)
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var met = SeedAlert(repository, "bitcoin", "usd");
        var unpriced = SeedAlert(repository, "ethereum", "eur");
        upstream.PricesResult = missing == "coin"
            ? Prices(("bitcoin", "usd", 150m), ("bitcoin", "eur", 150m))
            : Prices(("bitcoin", "usd", 150m), ("ethereum", "usd", 150m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        var view = SucceededValue(result);
        view.Triggered.Should().ContainSingle().Which.Id.Should().Be(met.Id);
        view.Unpriced.Should().Equal(Unpriced(unpriced));
        unpriced.State.Should().Be(AlertState.Armed);
        upstream.GetPricesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_PassCancellationTokenToEveryAsyncCall_When_AlertsTrigger()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        SeedAlert(repository, "bitcoin");
        SeedAlert(repository, "ethereum");
        upstream.PricesResult = Prices(("bitcoin", "usd", 150m), ("ethereum", "usd", 150m));
        using var source = new CancellationTokenSource();

        var result = await evaluator.EvaluateAsync(source.Token);

        result.IsSuccess.Should().BeTrue();
        upstream.CapturedCancellationToken.Should().Be(source.Token);

        // One load of the armed alerts, then one save per triggered alert.
        repository.CapturedCancellationTokens.Should().HaveCount(3).And.AllSatisfy(token => token.Should().Be(source.Token));
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_SaveIsCancelled()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        SeedAlert(repository);
        upstream.PricesResult = Prices(("bitcoin", "usd", 150m));
        repository.SaveChangesException = new OperationCanceledException();

        var act = () => evaluator.EvaluateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_UpstreamCallIsCancelled()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        SeedAlert(repository);
        upstream.ExceptionToThrow = new OperationCanceledException();

        var act = () => evaluator.EvaluateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SkipTheAlertChangedConcurrentlyAndStillTriggerTheOthers_When_OneSaveConflicts()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var first = SeedAlert(repository, "bitcoin");
        var stale = SeedAlert(repository, "ethereum");
        var last = SeedAlert(repository, "litecoin");
        upstream.PricesResult = Prices(("bitcoin", "usd", 150m), ("ethereum", "usd", 150m), ("litecoin", "usd", 150m));
        repository.ChangeConcurrently(stale.Id);

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Select(t => t.Id).Should().Equal(first.Id, last.Id);
        repository.PersistedStateOf(first.Id).Should().Be(AlertState.Triggered);
        repository.PersistedStateOf(stale.Id).Should().Be(AlertState.Armed);
        repository.PersistedStateOf(last.Id).Should().Be(AlertState.Triggered);
    }

    [Fact]
    public async Task Should_NotReportOrSaveAlert_When_AlertCameBackAlreadyTriggered()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(120m, Now.AddHours(-1)).IsValid.Should().BeTrue();
        repository.ListArmedIncludesTriggeredAlerts = true;
        upstream.PricesResult = Prices(("bitcoin", "usd", 150m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().BeEmpty();
        alert.TriggeredAt.Should().Be(Now.AddHours(-1));
        alert.TriggeredPrice.Should().Be(120m);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_NeitherTriggerNorSave_When_RearmedAlertsPriceStillMeetsIt()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedRearmed(repository);
        upstream.PricesResult = Prices(("bitcoin", "usd", 150m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().BeEmpty();
        alert.State.Should().Be(AlertState.Armed);
        alert.AwaitingReset.Should().BeTrue();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SaveTheResetWithoutTriggering_When_RearmedAlertsPriceHasMovedBack()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedRearmed(repository);
        upstream.PricesResult = Prices(("bitcoin", "usd", 90m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().BeEmpty();
        alert.State.Should().Be(AlertState.Armed);
        alert.AwaitingReset.Should().BeFalse();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_TriggerOnTheNextEvaluation_When_RearmedAlertsPriceMovedBackThenMetItAgain()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var alert = SeedRearmed(repository);
        upstream.PricesResult = Prices(("bitcoin", "usd", 90m));
        await evaluator.EvaluateAsync(CancellationToken.None);
        upstream.PricesResult = Prices(("bitcoin", "usd", 150m));

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().ContainSingle().Which.Id.Should().Be(alert.Id);
        alert.State.Should().Be(AlertState.Triggered);
    }

    [Fact]
    public async Task Should_DropTheResetAndStillTriggerTheOthers_When_TheResetSaveConflicts()
    {
        var (repository, upstream, _, evaluator) = CreateSut();
        var rearmed = SeedRearmed(repository, "bitcoin");
        var other = SeedAlert(repository, "ethereum");
        upstream.PricesResult = Prices(("bitcoin", "usd", 90m), ("ethereum", "usd", 150m));
        repository.ChangeConcurrently(rearmed.Id);

        var result = await evaluator.EvaluateAsync(CancellationToken.None);

        SucceededValue(result).Triggered.Should().ContainSingle().Which.Id.Should().Be(other.Id);
        repository.PersistedStateOf(other.Id).Should().Be(AlertState.Triggered);
    }

    // An Above-100 alert that triggered and was re-armed, so it waits for its
    // price to move back below 100.
    private static PriceAlert SeedRearmed(FakeAlertRepository repository, string coinId = "bitcoin")
    {
        var alert = PriceAlert.Create(coinId, "usd", AlertDirection.Above, 100m);
        alert.Trigger(120m, Now.AddHours(-1)).IsValid.Should().BeTrue();
        alert.Rearm().IsValid.Should().BeTrue();
        return repository.Seed(alert);
    }
}
