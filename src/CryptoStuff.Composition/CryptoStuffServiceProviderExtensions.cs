using CryptoStuff.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition;

/// <summary>Start-up steps a host runs on the provider built from <c>AddCryptoStuff</c>.</summary>
public static class CryptoStuffServiceProviderExtensions
{
    /// <summary>
    /// Brings the configured database up to date through
    /// <see cref="CryptoStuffDatabase.MigrateAsync"/>, creating the file and its
    /// directory when missing. Resolves the context from a scope of its own,
    /// so it never captures a scoped service in the root provider.
    /// </summary>
    public static async Task MigrateCryptoStuffDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CryptoStuffDbContext>();
        await CryptoStuffDatabase.MigrateAsync(context, cancellationToken);
    }
}
