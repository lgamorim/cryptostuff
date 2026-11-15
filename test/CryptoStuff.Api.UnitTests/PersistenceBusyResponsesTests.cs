using System.Text.Json;
using CryptoStuff.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Api.UnitTests;

public class PersistenceBusyResponsesTests
{
    [Fact]
    public async Task Should_ReplaceWhatTheEndpointHadSet_When_TheDatabaseIsBusy()
    {
        var context = CreateContext();

        await PersistenceBusyResponses.InvokeAsync(context, endpointContext =>
        {
            endpointContext.Response.StatusCode = StatusCodes.Status201Created;
            endpointContext.Response.Headers.Location = "/watchlists/1";
            throw new PersistenceBusyException("The database is busy.");
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Headers.RetryAfter.ToString().Should().Be("5");
        context.Response.Headers.Location.Should().BeEmpty("a header set before the failure would describe a write that never happened");
    }

    [Fact]
    public async Task Should_AnswerWithItsOwnDetailNotTheExceptionsMessage_When_TheDatabaseIsBusy()
    {
        var context = CreateContext();

        await PersistenceBusyResponses.InvokeAsync(
            context, _ => throw new PersistenceBusyException(@"database is locked: C:\data\cryptostuff.db"));

        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body, JsonSerializerOptions.Web, TestContext.Current.CancellationToken);
        // `!` is safe: the 503 always carries a ProblemDetails body; were it
        // missing, the NullReferenceException would still fail the test.
        problem!.Detail.Should().Be("The database is busy with another write; try again shortly.");
    }

    [Fact]
    public async Task Should_LetTheExceptionPropagate_When_TheResponseHadAlreadyStarted()
    {
        var context = CreateContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var act = () => PersistenceBusyResponses.InvokeAsync(context, _ => throw new PersistenceBusyException("The database is busy."));

        await act.Should().ThrowAsync<PersistenceBusyException>();
    }

    private static DefaultHttpContext CreateContext() => new()
    {
        RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        Response = { Body = new MemoryStream() },
    };

    // A response whose status line and headers have already gone to the client.
    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
