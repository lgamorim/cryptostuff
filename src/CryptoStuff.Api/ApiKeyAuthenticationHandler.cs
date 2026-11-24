using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CryptoStuff.Api;

/// <summary>
/// Authenticates a request whose <c>X-Api-Key</c> header is exactly the
/// configured key without the whitespace around it, and answers every other
/// caller's challenge with the same <c>401</c> ProblemDetails, whatever was
/// wrong, so a caller cannot tell a missing key from a wrong one.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // A missing header reads as empty, which never matches: the configured
        // key is never blank. Comparing SHA-256 digests in constant time gives
        // FixedTimeEquals inputs of equal length, so the response time reveals
        // neither how much of the key matched nor how long it is.
        var supplied = Request.Headers[ApiKeyAuthentication.HeaderName].ToString();
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, Options.KeyHash))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Send a valid API key in the X-Api-Key header.")
            .ExecuteAsync(Context);
}
