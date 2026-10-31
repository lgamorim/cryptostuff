using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CryptoStuff.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context from this project alone, so the
/// tooling never starts a host, which would demand a CoinGecko API key.
/// Scaffolding a migration reads only the model and never opens the
/// connection; the data source is a placeholder, not the application's
/// database, which the hosts locate and migrate themselves.
/// </summary>
internal sealed class DesignTimeCryptoStuffDbContextFactory : IDesignTimeDbContextFactory<CryptoStuffDbContext>
{
    public CryptoStuffDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite("Data Source=cryptostuff-design-time.db").Options);
}
