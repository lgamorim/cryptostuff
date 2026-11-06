using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Persistence.Alerts;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;

namespace CryptoStuff.Persistence.IntegrationTests.Alerts;

/// <summary>
/// The evaluator over the real repository and SQLite, with a second context
/// standing in for the other host that changes an alert mid-evaluation.
/// </summary>
public sealed class AlertEvaluatorTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);

    // `!` because xUnit runs InitializeAsync, which assigns it, before every test.
    private TestDatabase _database = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Should_SkipTheAlertAnotherContextChanged_When_OthersStillTrigger()
    {
        var stale = await SaveNewAsync("bitcoin");
        var other = await SaveNewAsync("ethereum");
        var upstream = new CompetingPriceService(
            PricesAt(150m, "bitcoin", "ethereum"),
            _ => ChangeFreshAsync(stale.Id, alert => alert.Update(AlertDirection.Below, 50m)));

        var triggered = await EvaluateAsync(upstream);

        triggered.Select(t => t.Id).Should().Equal(other.Id);
        upstream.GetPricesCallCount.Should().Be(1);
        var foundStale = await FindFreshAsync(stale.Id);
        foundStale.State.Should().Be(AlertState.Armed);
        foundStale.Direction.Should().Be(AlertDirection.Below);
        foundStale.Threshold.Should().Be(50m);
        foundStale.Version.Should().Be(2);
        var foundOther = await FindFreshAsync(other.Id);
        foundOther.State.Should().Be(AlertState.Triggered);
        foundOther.TriggeredAt.Should().Be(Now);
        foundOther.TriggeredPrice.Should().Be(150m);
    }

    [Fact]
    public async Task Should_SkipTheAlertAnotherContextDeleted_When_OthersStillTrigger()
    {
        var deleted = await SaveNewAsync("bitcoin");
        var other = await SaveNewAsync("ethereum");
        var upstream = new CompetingPriceService(PricesAt(150m, "bitcoin", "ethereum"), _ => DeleteFreshAsync(deleted.Id));

        var triggered = await EvaluateAsync(upstream);

        triggered.Select(t => t.Id).Should().Equal(other.Id);
        (await FindFreshOrNullAsync(deleted.Id)).Should().BeNull();
        (await FindFreshAsync(other.Id)).State.Should().Be(AlertState.Triggered);
    }

    [Fact]
    public async Task Should_TriggerEveryMetAlertOnce_When_NothingChangesConcurrently()
    {
        var first = await SaveNewAsync("bitcoin");
        var second = await SaveNewAsync("ethereum");
        var upstream = new CompetingPriceService(PricesAt(150m, "bitcoin", "ethereum"), _ => Task.CompletedTask);

        var triggered = await EvaluateAsync(upstream);

        triggered.Select(t => t.Id).Should().Equal(first.Id, second.Id);
        (await FindFreshAsync(first.Id)).State.Should().Be(AlertState.Triggered);
        (await FindFreshAsync(second.Id)).State.Should().Be(AlertState.Triggered);
    }

    [Fact]
    public async Task Should_TriggerNothingAndNotCallUpstream_When_EveryAlertWasTriggeredByAnEarlierEvaluation()
    {
        await SaveNewAsync("bitcoin");
        await SaveNewAsync("ethereum");
        var upstream = new CompetingPriceService(PricesAt(150m, "bitcoin", "ethereum"), _ => Task.CompletedTask);
        await EvaluateAsync(upstream);

        var triggered = await EvaluateAsync(upstream);

        triggered.Should().BeEmpty();
        upstream.GetPricesCallCount.Should().Be(1);
    }

    private static CoinPriceView PricesAt(decimal amount, params string[] coinIds) =>
        new()
        {
            Coins = [.. coinIds.Select(id => new CoinPrice { CoinId = id, Amounts = [new CurrencyAmount { Currency = "usd", Amount = amount }] })],
        };

    // Runs one evaluation in a context of its own, as one host process would,
    // and returns what it triggered.
    private async Task<IReadOnlyList<TriggeredAlertView>> EvaluateAsync(ICryptocurrencyService upstream)
    {
        await using var context = _database.CreateContext();
        var evaluator = new AlertEvaluator(new AlertRepository(context), upstream, new FixedTimeProvider(Now));

        var result = await evaluator.EvaluateAsync(Token);

        result.IsSuccess.Should().BeTrue();
        return result.Value?.Triggered ?? [];
    }

    private async Task<PriceAlert> SaveNewAsync(string coinId)
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        var alert = PriceAlert.Create(coinId, "usd", AlertDirection.Above, 100m);
        repository.Add(alert);
        await repository.SaveChangesAsync(Token);
        return alert;
    }

    private async Task ChangeFreshAsync(int id, Action<PriceAlert> change)
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        change(await FindSavedAsync(repository, id));
        await repository.SaveChangesAsync(Token);
    }

    private async Task DeleteFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        repository.Remove(await FindSavedAsync(repository, id));
        await repository.SaveChangesAsync(Token);
    }

    private async Task<PriceAlert> FindFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await FindSavedAsync(new AlertRepository(context), id);
    }

    private async Task<PriceAlert?> FindFreshOrNullAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await new AlertRepository(context).FindAsync(id, Token);
    }

    private static async Task<PriceAlert> FindSavedAsync(AlertRepository repository, int id) =>
        await repository.FindAsync(id, Token)
            ?? throw new InvalidOperationException($"Alert {id} was expected in the database but is missing.");
}
