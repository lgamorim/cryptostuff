using System.Globalization;
using CryptoStuff.Core;

namespace CryptoStuff.Api;

/// <summary>
/// Turns a <see cref="PersistenceBusyException"/> thrown by any endpoint into
/// a <c>503</c> with a ProblemDetails body and a <c>Retry-After</c> header. The
/// write failed only because another writer (the CLI, the background
/// evaluation, another request) held the database's lock past the provider's
/// wait, and nothing was written, so the same request can succeed shortly.
/// Every other exception passes through untouched.
/// </summary>
public static class PersistenceBusyResponses
{
    private const int RetryAfterSeconds = 5;

    internal static IApplicationBuilder UsePersistenceBusyResponses(this IApplicationBuilder app) =>
        app.Use(InvokeAsync);

    /// <summary>
    /// Runs <paramref name="next"/>, replacing whatever it had begun to set on
    /// the response with the <c>503</c> when it throws a
    /// <see cref="PersistenceBusyException"/>. Once the response has started,
    /// its status can no longer change, so the exception propagates instead.
    /// </summary>
    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (PersistenceBusyException exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            await TypedResults.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Service Unavailable",
                    detail: exception.Message)
                .ExecuteAsync(context);
        }
    }
}
