namespace CryptoStuff.Api;

/// <summary>
/// Migrates the shared database as the host's first start-up step, so no
/// request ever meets a missing or outdated schema. It runs in
/// <see cref="StartingAsync"/>, which the host calls for every lifecycle
/// service before it starts any service, the server included, and with the
/// start-up token, which the host cancels when a stop is requested: Ctrl+C
/// during a slow migration cancels it rather than ending the process mid-way.
/// A failed migration ends start-up with its exception.
/// </summary>
public sealed class StartupDatabaseMigration(Func<CancellationToken, Task> migrate) : IHostedLifecycleService
{
    /// <summary>Runs the migration with the host's start-up token.</summary>
    public Task StartingAsync(CancellationToken cancellationToken) => migrate(cancellationToken);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
