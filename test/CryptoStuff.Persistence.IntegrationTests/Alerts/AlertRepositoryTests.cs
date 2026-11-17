using System.Data.Common;
using System.Globalization;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Persistence.Alerts;
using CryptoStuff.Persistence.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CryptoStuff.Persistence.IntegrationTests.Alerts;

public sealed class AlertRepositoryTests : IAsyncLifetime
{
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
    public async Task Should_AssignIncreasingIds_When_AlertsSaved()
    {
        var first = await SaveNewAsync("bitcoin");
        var second = await SaveNewAsync("ethereum");

        first.Id.Should().BePositive();
        second.Id.Should().BeGreaterThan(first.Id);
    }

    [Fact]
    public async Task Should_KeepEveryField_When_ArmedAlertRoundTrips()
    {
        var saved = await SaveNewAsync(PriceAlert.Create("Bitcoin", "EUR", AlertDirection.Below, 123.45m));

        var found = await FindFreshAsync(saved.Id);

        found.Id.Should().Be(saved.Id);
        found.CoinId.Should().Be("bitcoin");
        found.VsCurrency.Should().Be("eur");
        found.Direction.Should().Be(AlertDirection.Below);
        found.Threshold.Should().Be(123.45m);
        found.State.Should().Be(AlertState.Armed);
        found.TriggeredAt.Should().BeNull();
        found.TriggeredPrice.Should().BeNull();
        found.Version.Should().Be(1);
    }

    [Fact]
    public async Task Should_KeepEveryField_When_TriggeredAlertRoundTrips()
    {
        var triggeredAt = new DateTimeOffset(2026, 1, 5, 12, 30, 15, 123, TimeSpan.FromHours(-3)).AddTicks(4567);
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(150.5m, triggeredAt).IsValid.Should().BeTrue();
        var saved = await SaveNewAsync(alert);

        var found = await FindFreshAsync(saved.Id);

        found.CoinId.Should().Be("bitcoin");
        found.VsCurrency.Should().Be("usd");
        found.Direction.Should().Be(AlertDirection.Above);
        found.Threshold.Should().Be(100m);
        found.State.Should().Be(AlertState.Triggered);
        found.TriggeredAt.Should().Be(triggeredAt);
        // `!` is safe: the assertion above fails the test unless TriggeredAt is the time saved,
        // which is not null.
        found.TriggeredAt!.Value.Offset.Should().Be(TimeSpan.Zero);
        found.TriggeredPrice.Should().Be(150.5m);
        found.Version.Should().Be(2);
    }

    [Theory]
    [InlineData("0.00000001")]
    [InlineData("0.1")]
    [InlineData("12345678901234567890.123456789")]
    [InlineData("79228162514264337593543950335")]
    public async Task Should_RoundTripDecimalsExactly_When_AlertSaved(string literal)
    {
        var amount = decimal.Parse(literal, CultureInfo.InvariantCulture);
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, amount);
        alert.Trigger(amount, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();
        var saved = await SaveNewAsync(alert);

        var found = await FindFreshAsync(saved.Id);

        found.Threshold.Should().Be(amount);
        found.TriggeredPrice.Should().Be(amount);
    }

    [Fact]
    public async Task Should_StoreDecimalsAsText_When_TriggeredAlertSaved()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 0.00000001m);
        alert.Trigger(0.00000002m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();
        await SaveNewAsync(alert);

        var stored = await ReadColumnAsync("SELECT typeof(Threshold) || typeof(TriggeredPrice) AS Value FROM PriceAlerts");

        stored.Should().Equal("texttext");
    }

    [Fact]
    public async Task Should_StoreNullTriggerColumns_When_AlertArmed()
    {
        await SaveNewAsync("bitcoin");

        var stored = await ReadColumnAsync("SELECT typeof(TriggeredAt) || typeof(TriggeredPrice) AS Value FROM PriceAlerts");

        stored.Should().Equal("nullnull");
    }

    [Theory]
    [InlineData(AlertDirection.Above, "above")]
    [InlineData(AlertDirection.Below, "below")]
    public async Task Should_StoreDirectionAsToken_When_AlertSaved(AlertDirection direction, string token)
    {
        await SaveNewAsync(PriceAlert.Create("bitcoin", "usd", direction, 100m));

        var stored = await ReadColumnAsync("SELECT CAST(Direction AS TEXT) AS Value FROM PriceAlerts");

        stored.Should().Equal(token);
    }

    [Theory]
    [InlineData(AlertState.Armed, "armed")]
    [InlineData(AlertState.Triggered, "triggered")]
    public async Task Should_StoreStateAsToken_When_AlertSaved(AlertState state, string token)
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        if (state == AlertState.Triggered)
        {
            alert.Trigger(150m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();
        }

        await SaveNewAsync(alert);

        var stored = await ReadColumnAsync("SELECT CAST(State AS TEXT) AS Value FROM PriceAlerts");

        stored.Should().Equal(token);
    }

    [Fact]
    public async Task Should_StoreTriggeredAtAsUtc_When_AlertTriggeredAtAnotherOffset()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(150m, new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(2))).IsValid.Should().BeTrue();
        await SaveNewAsync(alert);

        var stored = await ReadColumnAsync("SELECT TriggeredAt AS Value FROM PriceAlerts");

        stored.Should().Equal("2026-02-01 07:00:00");
    }

    [Fact]
    public async Task Should_ListEveryAlertOrderedById_When_Listing()
    {
        var first = await SaveNewAsync("zcash");
        var second = await SaveNewAsync("bitcoin");
        var third = await SaveNewAsync(Triggered("ethereum"));
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var listed = await repository.ListAsync(Token);

        listed.Select(a => a.Id).Should().Equal(first.Id, second.Id, third.Id);
        listed.Select(a => a.CoinId).Should().Equal("zcash", "bitcoin", "ethereum");
        listed.Select(a => a.State).Should().Equal(AlertState.Armed, AlertState.Armed, AlertState.Triggered);
    }

    [Fact]
    public async Task Should_DeleteOnlyThatAlert_When_AlertRemoved()
    {
        var deleted = await SaveNewAsync("bitcoin");
        var kept = await SaveNewAsync("ethereum");

        await DeleteFreshAsync(deleted.Id);

        await using var context = _database.CreateContext();
        var remaining = await new AlertRepository(context).ListAsync(Token);
        remaining.Select(a => a.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Should_NotReuseAlertId_When_HighestIdDeleted()
    {
        await SaveNewAsync("bitcoin");
        var deleted = await SaveNewAsync("ethereum");
        await DeleteFreshAsync(deleted.Id);

        var next = await SaveNewAsync("solana");

        next.Id.Should().BeGreaterThan(deleted.Id);
    }

    [Fact]
    public async Task Should_ReturnNull_When_FindingUnknownId()
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var found = await repository.FindAsync(42, Token);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_AlertChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("bitcoin");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await UpdateFreshAsync(saved.Id, AlertDirection.Below, 50m);
        stale.Update(AlertDirection.Above, 500m);

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        var found = await FindFreshAsync(saved.Id);
        found.Direction.Should().Be(AlertDirection.Below);
        found.Threshold.Should().Be(50m);
    }

    [Fact]
    public async Task Should_SaveLaterChanges_When_ConflictingAlertIsDiscarded()
    {
        var stale = await SaveNewAsync("bitcoin");
        var other = await SaveNewAsync("ethereum");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        var staleCopy = await FindSavedAsync(repository, stale.Id);
        var otherCopy = await FindSavedAsync(repository, other.Id);
        await UpdateFreshAsync(stale.Id, AlertDirection.Below, 50m);
        staleCopy.Trigger(150m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();
        await SaveExpectingConflictAsync(repository);
        repository.Discard(staleCopy);
        otherCopy.Trigger(150m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();

        await repository.SaveChangesAsync(Token);

        var foundStale = await FindFreshAsync(stale.Id);
        foundStale.State.Should().Be(AlertState.Armed);
        foundStale.Direction.Should().Be(AlertDirection.Below);
        foundStale.Threshold.Should().Be(50m);
        (await FindFreshAsync(other.Id)).State.Should().Be(AlertState.Triggered);
    }

    // The alert evaluation saves a whole run's triggers in one save and relies
    // on it writing all of them or none. The fresh alert's id is the lower, so
    // its update is written before the stale one fails.
    [Fact]
    public async Task Should_WriteNoneOfTheSavesChanges_When_OneAlertInItConflicts()
    {
        var fresh = await SaveNewAsync("bitcoin");
        var stale = await SaveNewAsync("ethereum");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        var freshCopy = await FindSavedAsync(repository, fresh.Id);
        var staleCopy = await FindSavedAsync(repository, stale.Id);
        await UpdateFreshAsync(stale.Id, AlertDirection.Below, 50m);
        var triggeredAt = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
        freshCopy.Trigger(150m, triggeredAt).IsValid.Should().BeTrue();
        staleCopy.Trigger(150m, triggeredAt).IsValid.Should().BeTrue();

        await SaveExpectingConflictAsync(repository);

        (await FindFreshAsync(fresh.Id)).State.Should().Be(AlertState.Armed);
        (await FindFreshAsync(stale.Id)).State.Should().Be(AlertState.Armed);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_RemovingAlertChangedSinceItWasRead()
    {
        var saved = await SaveNewAsync("bitcoin");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        var stale = await FindSavedAsync(repository, saved.Id);
        await UpdateFreshAsync(saved.Id, AlertDirection.Below, 50m);
        repository.Remove(stale);

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
        (await FindFreshAsync(saved.Id)).Threshold.Should().Be(50m);
    }

    [Fact]
    public async Task Should_ThrowPersistenceConflictException_When_ChangingAlertDeletedConcurrently()
    {
        var saved = await SaveNewAsync("bitcoin");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        var alert = await FindSavedAsync(repository, saved.Id);
        await DeleteFreshAsync(saved.Id);
        alert.Trigger(150m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();

        var act = () => repository.SaveChangesAsync(Token);

        (await act.Should().ThrowAsync<PersistenceConflictException>())
            .WithInnerException<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Should_ClearTriggerColumnsAndBumpVersion_When_TriggeredAlertRearmedAndSaved()
    {
        var saved = await SaveNewAsync(Triggered("bitcoin"));
        await using (var context = _database.CreateContext())
        {
            var repository = new AlertRepository(context);
            (await FindSavedAsync(repository, saved.Id)).Rearm().Should().BeTrue();
            await repository.SaveChangesAsync(Token);
        }

        var found = await FindFreshAsync(saved.Id);

        found.State.Should().Be(AlertState.Armed);
        found.TriggeredAt.Should().BeNull();
        found.TriggeredPrice.Should().BeNull();
        found.AwaitingReset.Should().BeTrue();
        found.Version.Should().Be(3);
    }

    [Fact]
    public async Task Should_ListOnlyArmedAlertsOrderedById_When_ListingArmed()
    {
        var first = await SaveNewAsync("zcash");
        await SaveNewAsync(Triggered("ethereum"));
        var third = await SaveNewAsync("bitcoin");
        await SaveNewAsync(Triggered("solana"));
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var armed = await repository.ListArmedAsync(Token);

        armed.Select(a => a.Id).Should().Equal(first.Id, third.Id);
        armed.Should().OnlyContain(a => a.State == AlertState.Armed);
    }

    // The background evaluation lists the armed alerts every tick, so that
    // lookup must reach them through an index, already in id order, rather
    // than read and sort every alert ever created.
    [Fact]
    public async Task Should_FindArmedAlertsThroughTheStateIndexWithoutSorting_When_ListingArmed()
    {
        var capture = new QueryCapture();
        await using (var context = new CryptoStuffDbContext(
            new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite(_database.ConnectionString).AddInterceptors(capture).Options))
        {
            await new AlertRepository(context).ListArmedAsync(Token);
        }

        var plan = await ExplainAsync(capture);

        plan.Should().Contain(step => step.Contains("USING INDEX IX_PriceAlerts_State", StringComparison.Ordinal))
            .And.NotContain(step => step.Contains("TEMP B-TREE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_ListNothing_When_NoAlertIsArmed()
    {
        await SaveNewAsync(Triggered("bitcoin"));
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var armed = await repository.ListArmedAsync(Token);

        armed.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_SaveCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        repository.Add(PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m));

        var act = () => repository.SaveChangesAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_ExistenceCheckCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var act = () => repository.ExistsAsync(1, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_ListCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var act = () => repository.ListAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_ListArmedCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var act = () => repository.ListArmedAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_FindCanceled()
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var act = () => repository.FindAsync(1, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ThrowInvalidDataException_When_StoredDirectionIsNotAToken()
    {
        var saved = await SaveNewAsync("bitcoin");
        await ExecuteAsync("UPDATE PriceAlerts SET Direction = 'sideways'");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var act = () => repository.FindAsync(saved.Id, Token);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage("*'sideways'*alert direction*");
    }

    [Fact]
    public async Task Should_ThrowInvalidDataException_When_StoredStateIsNotAToken()
    {
        var saved = await SaveNewAsync("bitcoin");
        await ExecuteAsync("UPDATE PriceAlerts SET State = 'snoozed'");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);

        var act = () => repository.FindAsync(saved.Id, Token);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage("*'snoozed'*alert state*");
    }

    // Existence, read past the change tracker.

    [Fact]
    public async Task Should_ReportAbsent_When_NoAlertHasTheId()
    {
        await using var context = _database.CreateContext();

        var exists = await new AlertRepository(context).ExistsAsync(42, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportAbsent_When_AlertDeletedByAnotherContextAfterItWasRead()
    {
        var saved = await SaveNewAsync("bitcoin");
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        await FindSavedAsync(repository, saved.Id);
        await DeleteFreshAsync(saved.Id);

        var exists = await repository.ExistsAsync(saved.Id, Token);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReportExists_When_AlertSaved()
    {
        var saved = await SaveNewAsync("bitcoin");
        await using var context = _database.CreateContext();

        var exists = await new AlertRepository(context).ExistsAsync(saved.Id, Token);

        exists.Should().BeTrue();
    }

    // The database busy with another writer.

    [Fact]
    public async Task Should_ThrowPersistenceBusyException_When_AnotherConnectionHoldsTheWriteLock()
    {
        var saved = await SaveNewAsync("bitcoin");
        await using var context = _database.CreateImpatientContext();
        var repository = new AlertRepository(context);
        (await FindSavedAsync(repository, saved.Id)).Update(AlertDirection.Below, 90m);
        await using var writeLock = await WriteLock.HoldAsync(_database.ConnectionString, Token);

        var act = () => repository.SaveChangesAsync(Token);

        // SQLITE_BUSY: https://www.sqlite.org/rescode.html#busy
        (await act.Should().ThrowAsync<PersistenceBusyException>())
            .Which.GetBaseException().Should().BeOfType<SqliteException>()
            .Which.SqliteErrorCode.Should().Be(5);
    }

    // Helpers: each runs in its own context, so the arrange and verify steps
    // never share a change tracker with the step under test.

    private Task<PriceAlert> SaveNewAsync(string coinId) =>
        SaveNewAsync(PriceAlert.Create(coinId, "usd", AlertDirection.Above, 100m));

    private async Task<PriceAlert> SaveNewAsync(PriceAlert alert)
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        repository.Add(alert);
        await repository.SaveChangesAsync(Token);
        return alert;
    }

    private static PriceAlert Triggered(string coinId)
    {
        var alert = PriceAlert.Create(coinId, "usd", AlertDirection.Above, 100m);
        alert.Trigger(150m, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero)).IsValid.Should().BeTrue();
        return alert;
    }

    // Leaves the conflicting change pending in the repository's context, as a
    // failed save does; the conflict itself is the starting point of the test
    // using it, so it is absorbed here rather than asserted.
    private static async Task SaveExpectingConflictAsync(AlertRepository repository)
    {
        try
        {
            await repository.SaveChangesAsync(Token);
        }
        catch (PersistenceConflictException)
        {
            // Expected: the starting state the caller wants.
        }
    }

    // SQLite's plan for the captured query, one step's detail per element.
    private async Task<List<string>> ExplainAsync(QueryCapture capture)
    {
        await using var connection = new SqliteConnection(_database.ConnectionString);
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + capture.CommandText;
        foreach (var (name, value) in capture.Parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var steps = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Token);
        while (await reader.ReadAsync(Token))
        {
            steps.Add(reader.GetString(reader.GetOrdinal("detail")));
        }

        return steps;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var context = _database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql, Token);
    }

    // Reads a column's stored text straight from the file, bypassing the mapping.
    private async Task<List<string>> ReadColumnAsync(string sql)
    {
        await using var context = _database.CreateContext();
        return await context.Database.SqlQueryRaw<string>(sql).ToListAsync(Token);
    }

    private async Task DeleteFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        repository.Remove(await FindSavedAsync(repository, id));
        await repository.SaveChangesAsync(Token);
    }

    private async Task UpdateFreshAsync(int id, AlertDirection direction, decimal threshold)
    {
        await using var context = _database.CreateContext();
        var repository = new AlertRepository(context);
        (await FindSavedAsync(repository, id)).Update(direction, threshold);
        await repository.SaveChangesAsync(Token);
    }

    private async Task<PriceAlert> FindFreshAsync(int id)
    {
        await using var context = _database.CreateContext();
        return await FindSavedAsync(new AlertRepository(context), id);
    }

    private static async Task<PriceAlert> FindSavedAsync(AlertRepository repository, int id) =>
        await repository.FindAsync(id, Token)
            ?? throw new InvalidOperationException($"Alert {id} was expected in the database but is missing.");

    // Keeps the text and parameters of the last query a context ran.
    private sealed class QueryCapture : DbCommandInterceptor
    {
        public string CommandText { get; private set; } = "";

        public List<(string Name, object Value)> Parameters { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            CommandText = command.CommandText;
            Parameters.Clear();
            Parameters.AddRange(command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value ?? DBNull.Value)));
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
