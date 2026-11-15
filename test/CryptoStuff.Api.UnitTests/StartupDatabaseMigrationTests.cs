namespace CryptoStuff.Api.UnitTests;

public class StartupDatabaseMigrationTests
{
    private readonly List<CancellationToken> _migrations = [];
    private readonly StartupDatabaseMigration _migration;

    public StartupDatabaseMigrationTests()
    {
        _migration = new StartupDatabaseMigration(cancellationToken =>
        {
            _migrations.Add(cancellationToken);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Should_MigrateOnceWithTheStartUpToken_When_TheHostIsStarting()
    {
        using var startUp = new CancellationTokenSource();

        await _migration.StartingAsync(startUp.Token);

        _migrations.Should().Equal(startUp.Token);
    }

    [Fact]
    public async Task Should_LetTheMigrationsFailureEndStartUp_When_TheMigrationFails()
    {
        var failing = new StartupDatabaseMigration(_ => Task.FromException(new IOException("The folder cannot be created.")));

        var act = () => failing.StartingAsync(CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task Should_NotMigrate_When_AnyOtherLifecycleStepRuns()
    {
        await _migration.StartAsync(CancellationToken.None);
        await _migration.StartedAsync(CancellationToken.None);
        await _migration.StoppingAsync(CancellationToken.None);
        await _migration.StopAsync(CancellationToken.None);
        await _migration.StoppedAsync(CancellationToken.None);

        _migrations.Should().BeEmpty();
    }
}
