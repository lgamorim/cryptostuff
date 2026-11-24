using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace CryptoStuff.Api;

/// <summary>
/// Protects the API with the single key configured as <c>Api:Key</c>, which
/// clients send in the <c>X-Api-Key</c> header. Every route requires it unless
/// it opts out with <c>AllowAnonymous</c>, so a route added later is protected
/// without its author doing anything. A request without the right key is a
/// <c>401</c> with a ProblemDetails body, before anything else happens to it.
/// </summary>
public static class ApiKeyAuthentication
{
    /// <summary>The authentication scheme, and the OpenAPI security scheme, the key is checked under.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>The request header the key is read from; it is never read from anywhere else.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>
    /// Registers the API key scheme as the default authentication scheme and an
    /// authorization fallback policy that requires it for every endpoint
    /// declaring no authorization of its own.
    /// </summary>
    /// <exception cref="InvalidOperationException"><c>Api:Key</c> is missing or blank.</exception>
    internal static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(ReadApiKey(configuration)));
        services.AddAuthentication(SchemeName)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(SchemeName, options => options.KeyHash = keyHash);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    /// <summary>
    /// Reads <c>Api:Key</c>, the key every protected request must carry,
    /// without any whitespace around it: an HTTP server strips that whitespace
    /// from the header, so a client could never send it.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// It is missing or blank. The message says how to set it, and never
    /// contains a key.
    /// </exception>
    public static string ReadApiKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = configuration["Api:Key"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "API key is missing. Choose a secret key and set it locally by running "
                + "'dotnet user-secrets set Api:Key <your-api-key>' from the src/CryptoStuff.Api "
                + "project directory, or set the Api__Key environment variable in deployment. "
                + "Clients send it in the X-Api-Key header.");
        }

        return apiKey.Trim();
    }
}
