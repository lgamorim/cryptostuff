using Microsoft.AspNetCore.Authentication;

namespace CryptoStuff.Api;

/// <summary>The options of <see cref="ApiKeyAuthenticationHandler"/>.</summary>
internal sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The SHA-256 digest of the configured <c>Api:Key</c>, without the
    /// whitespace around it, the only form of the key the handler keeps.
    /// </summary>
    public byte[] KeyHash { get; set; } = [];
}
