using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CryptoStuff.Api.IntegrationTests;

public class DatabaseMigrationTests
{
    [Fact]
    public async Task Should_ApplyEveryMigration_When_HostStarts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new CryptoStuffApiFactory();

        using var client = factory.CreateClient();

        await using var context = factory.CreateContext();
        (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Should().BeEmpty();
        (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_UseFactoryDatabase_When_HostResolvesContext()
    {
        await using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CryptoStuffDbContext>();

        context.Database.GetConnectionString().Should().Be(factory.ConnectionString);
    }

    [Fact]
    public async Task Should_DeleteDatabaseFile_When_FactoryDisposed()
    {
        var factory = new CryptoStuffApiFactory();
        using (factory.CreateClient())
        {
            File.Exists(factory.DatabaseFilePath).Should().BeTrue();
        }

        await factory.DisposeAsync();

        File.Exists(factory.DatabaseFilePath).Should().BeFalse();
    }

    // The factory starts the host through Program's own RunAsync. A stop
    // requested during start-up, as Ctrl+C during the migration would be,
    // cancels the start; Program must end quietly rather than let that
    // cancellation escape, and the factory then finds the host already
    // disposed instead of seeing the cancellation.
    [Fact]
    public void Should_EndProgramWithoutTheCancellationEscaping_When_AStopIsRequestedDuringStartUp()
    {
        using var factory = new CryptoStuffApiFactory();
        using var stopped = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHostedService(provider => new CancelledStart(provider.GetRequiredService<IHostApplicationLifetime>(), requestStop: true))));

        var act = () => stopped.CreateClient();

        act.Should().Throw<ObjectDisposedException>();
    }

    // Program swallows a cancellation only once a stop has been requested. One
    // that escapes RunAsync with no stop requested is a fault, not a stop, and
    // must still end the process with it.
    [Fact]
    public void Should_LetTheCancellationEscapeProgram_When_StartUpIsCancelledWithoutAStopRequest()
    {
        using var factory = new CryptoStuffApiFactory();
        using var cancelled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHostedService(provider => new CancelledStart(provider.GetRequiredService<IHostApplicationLifetime>(), requestStop: false))));

        var act = () => cancelled.CreateClient();

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Should_GiveEachFactoryItsOwnDatabaseFile_When_Created()
    {
        using var first = new CryptoStuffApiFactory();
        using var second = new CryptoStuffApiFactory();

        second.DatabaseFilePath.Should().NotBe(first.DatabaseFilePath);
    }

    // Fails its own start with a cancellation while the host is starting:
    // either the one a stop it requests first causes, or one of its own with
    // no stop requested.
    private sealed class CancelledStart(IHostApplicationLifetime lifetime, bool requestStop) : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken)
        {
            if (!requestStop)
            {
                return Task.FromCanceled(new CancellationToken(canceled: true));
            }

            lifetime.StopApplication();
            return Task.FromCanceled(cancellationToken);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
