using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CryptoStuff.Api.Alerts;

/// <summary>
/// Throttles <c>POST /alerts/check</c>, the one route that asks CoinGecko for
/// prices every time it is called, so a client checking in a loop cannot spend
/// the API key's rate limit that the background evaluation, the other price
/// routes and the CLI share. One fixed window of a minute holds for every
/// caller together, since the rate limit it protects is the key's, not a
/// caller's. A check over the limit is a <c>429</c> with a ProblemDetails body
/// and a <c>Retry-After</c> header, and asks CoinGecko nothing.
/// </summary>
public static class AlertCheckRateLimit
{
    /// <summary>The rate-limiting policy the check route requires.</summary>
    public const string PolicyName = "alert-check";

    private const int DefaultChecksPerMinute = 6;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Registers the rate limiter with the check route's policy, allowing
    /// <c>Alerts:ChecksPerMinute</c> checks a minute, or 6 when that is absent or blank.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <c>Alerts:ChecksPerMinute</c> is present and non-blank but not a whole
    /// number from 1 to <see cref="int.MaxValue"/>.
    /// </exception>
    internal static IServiceCollection AddAlertCheckRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var checksPerMinute = ReadChecksPerMinute(configuration);
        return services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter(PolicyName, limiter =>
            {
                limiter.PermitLimit = checksPerMinute;
                limiter.Window = Window;
                limiter.QueueLimit = 0;
            });
            options.OnRejected = (context, _) => RejectAsync(context, checksPerMinute);
        });
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, int checksPerMinute)
    {
        // A fixed window answers with its whole length, however much of it is
        // left: a wait that always outlasts it, and whole seconds, since the
        // window is a minute.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too Many Requests",
                detail: $"Alert checks are limited to {checksPerMinute} per minute; try again after the wait given in Retry-After.")
            .ExecuteAsync(context.HttpContext);
    }

    /// <summary>
    /// Reads <c>Alerts:ChecksPerMinute</c>, the checks a minute the route allows:
    /// 6 when it is absent or blank.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// It is present and non-blank but not a whole number from 1 to <see cref="int.MaxValue"/>.
    /// </exception>
    public static int ReadChecksPerMinute(IConfiguration configuration)
    {
        var checksPerMinute = configuration["Alerts:ChecksPerMinute"];
        if (string.IsNullOrWhiteSpace(checksPerMinute))
        {
            return DefaultChecksPerMinute;
        }

        if (!int.TryParse(checksPerMinute, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidOperationException(
                $"Alerts:ChecksPerMinute is set to '{checksPerMinute}', which is not a whole number from 1 to {int.MaxValue}. "
                + $"Set it to how many POST /alerts/check requests a minute to allow, or remove it to allow {DefaultChecksPerMinute}.");
        }

        return parsed;
    }
}
