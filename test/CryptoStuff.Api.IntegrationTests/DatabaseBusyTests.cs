using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CryptoStuff.Api.IntegrationTests.TestSupport;

namespace CryptoStuff.Api.IntegrationTests;

public class DatabaseBusyTests
{
    [Fact]
    public async Task Should_Return503WithRetryAfter_When_AnotherWriterHoldsTheDatabaseLock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new CryptoStuffApiFactory(busyTimeoutSeconds: 1);
        using var client = factory.CreateClient();
        await using var writeLock = await WriteLock.HoldAsync(factory.ConnectionString, cancellationToken);

        var response = await client.PostAsJsonAsync("/watchlists", new { name = "Majors" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (response.Headers.RetryAfter?.Delta).Should().Be(TimeSpan.FromSeconds(5));
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(503);
        problem.RootElement.GetProperty("title").GetString().Should().Be("Service Unavailable");
        problem.RootElement.GetProperty("detail").GetString().Should().Be("The database is busy with another write; try again shortly.");
    }

    [Fact]
    public async Task Should_SaveNormally_When_TheOtherWriterReleasesTheLockInTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var insert = new InsertSignal();
        await using var factory = new CryptoStuffApiFactory(interceptor: insert);
        using var client = factory.CreateClient();
        await using var writeLock = await WriteLock.HoldAsync(factory.ConnectionString, cancellationToken);

        var request = client.PostAsJsonAsync("/watchlists", new { name = "Majors" }, cancellationToken);

        // The insert goes out while the lock is still held, so it can succeed
        // only by waiting for the release below.
        var first = await Task.WhenAny(insert.Issued, request);
        first.Should().BeSameAs(insert.Issued, "the save must reach the locked database before the lock is released");
        await writeLock.DisposeAsync();
        var response = await request;

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
