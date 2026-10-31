using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public void Should_GiveEachFactoryItsOwnDatabaseFile_When_Created()
    {
        using var first = new CryptoStuffApiFactory();
        using var second = new CryptoStuffApiFactory();

        second.DatabaseFilePath.Should().NotBe(first.DatabaseFilePath);
    }
}
