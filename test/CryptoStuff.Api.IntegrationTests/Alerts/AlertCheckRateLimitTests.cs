using System.Net;
using System.Net.Http.Json;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.CoinGecko;
using Microsoft.AspNetCore.Mvc;

namespace CryptoStuff.Api.IntegrationTests.Alerts;

// The limiter's window runs on the real clock, but a test's requests all land
// within a few seconds of the first, well inside one minute's window.
public class AlertCheckRateLimitTests
{
    [Fact]
    public async Task Should_RejectTheCheckOverTheLimitWithoutAskingCoinGecko_When_ChecksPerMinuteIsSpent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Alerts:ChecksPerMinute", "1"));
        using var client = limited.CreateClient();
        await CreateAlertAsync(client, cancellationToken);
        (await client.PostAsync("/alerts/check", content: null, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        var callsBefore = factory.CoinGeckoClient.CallCount;

        var response = await client.PostAsync("/alerts/check", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        factory.CoinGeckoClient.CallCount.Should().Be(callsBefore);
    }

    [Fact]
    public async Task Should_AnswerWithAProblemAndRetryAfter_When_CheckIsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Alerts:ChecksPerMinute", "1"));
        using var client = limited.CreateClient();
        await client.PostAsync("/alerts/check", content: null, cancellationToken);

        var response = await client.PostAsync("/alerts/check", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // `!` is safe: a rejected check always carries Retry-After; were it
        // missing, the NullReferenceException would still fail the test.
        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromMinutes(1));
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await ReadProblemAsync(response, cancellationToken);
        problem.Status.Should().Be(429);
        problem.Title.Should().Be("Too Many Requests");
        problem.Detail.Should().Be("Alert checks are limited to 1 per minute; try again after the wait given in Retry-After.");
    }

    [Fact]
    public async Task Should_AllowSixChecksAMinuteAndRejectTheSeventh_When_TheShippedSettingsAreUsed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 7; i++)
        {
            statuses.Add((await client.PostAsync("/alerts/check", content: null, cancellationToken)).StatusCode);
        }

        statuses.Should().Equal([.. Enumerable.Repeat(HttpStatusCode.OK, 6), HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task Should_UseTheDefaultLimit_When_ChecksPerMinuteIsBlank()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Alerts:ChecksPerMinute", " "));
        using var client = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 7; i++)
        {
            statuses.Add((await client.PostAsync("/alerts/check", content: null, cancellationToken)).StatusCode);
        }

        statuses.Should().Equal([.. Enumerable.Repeat(HttpStatusCode.OK, 6), HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task Should_ServeTheOtherAlertRoutes_When_TheCheckLimitIsSpent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Alerts:ChecksPerMinute", "1"));
        using var client = limited.CreateClient();
        await client.PostAsync("/alerts/check", content: null, cancellationToken);
        (await client.PostAsync("/alerts/check", content: null, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var created = await client.PostAsJsonAsync(
            "/alerts", new { coinId = "bitcoin", vsCurrency = "usd", direction = "above", threshold = 50000m }, cancellationToken);
        var listed = await client.GetAsync("/alerts", cancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("six")]
    [InlineData("2147483648")]
    public void Should_FailToStart_When_ChecksPerMinuteIsNotAPositiveWholeNumber(string checksPerMinute)
    {
        using var factory = new CryptoStuffApiFactory();
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Alerts:ChecksPerMinute", checksPerMinute));

        var act = () => limited.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage(
            $"Alerts:ChecksPerMinute is set to '{checksPerMinute}', which is not a whole number from 1 to 2147483647. "
            + "Set it to how many POST /alerts/check requests a minute to allow, or remove it to allow 6.");
    }

    private static async Task CreateAlertAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/alerts", new { coinId = "bitcoin", vsCurrency = "usd", direction = "above", threshold = 50000m }, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        // `!` is safe: every error response carries a ProblemDetails body.
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken))!;

    // What CoinGecko returns for every price request: bitcoin, priced in usd.
    private static CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>> BitcoinPricedInUsd() =>
        CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new Dictionary<string, Dictionary<string, decimal>> { ["bitcoin"] = new() { ["usd"] = 43000m } },
            HttpStatusCode.OK);
}
