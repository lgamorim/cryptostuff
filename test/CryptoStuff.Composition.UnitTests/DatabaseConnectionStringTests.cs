using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CryptoStuff.Composition.UnitTests;

public class DatabaseConnectionStringTests
{
    private static readonly string LocalAppDataPath = Path.Combine(Path.GetTempPath(), "local-app-data");

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    [Fact]
    public void Should_ReturnConfiguredValue_When_ConnectionStringIsConfigured()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = "Data Source=custom.db" });

        var connectionString = DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        connectionString.Should().Be("Data Source=custom.db");
    }

    [Fact]
    public void Should_ReturnConfiguredValue_When_ConnectionStringIsConfiguredAndAppDataPathIsBlank()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = "Data Source=custom.db" });

        var connectionString = DatabaseConnectionString.Resolve(configuration, "");

        connectionString.Should().Be("Data Source=custom.db");
    }

    [Fact]
    public void Should_FallBackToFileUnderLocalAppData_When_ConnectionStringIsAbsent()
    {
        var configuration = BuildConfiguration();

        var connectionString = DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        new SqliteConnectionStringBuilder(connectionString).DataSource
            .Should().Be(Path.Combine(LocalAppDataPath, "cryptostuff", "cryptostuff.db"));
    }

    [Fact]
    public void Should_FallBackToFileUnderLocalAppData_When_ConnectionStringIsWhitespace()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = "   " });

        var connectionString = DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        new SqliteConnectionStringBuilder(connectionString).DataSource
            .Should().Be(Path.Combine(LocalAppDataPath, "cryptostuff", "cryptostuff.db"));
    }

    [Fact]
    public void Should_KeepDefaultPathIntact_When_AppDataPathContainsSemicolon()
    {
        var configuration = BuildConfiguration();
        var localAppDataPath = Path.Combine(Path.GetTempPath(), "odd;folder");

        var connectionString = DatabaseConnectionString.Resolve(configuration, localAppDataPath);

        new SqliteConnectionStringBuilder(connectionString).DataSource
            .Should().Be(Path.Combine(localAppDataPath, "cryptostuff", "cryptostuff.db"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowActionableInvalidOperationException_When_ConnectionStringIsAbsentAndAppDataPathIsBlank(string? localAppDataPath)
    {
        var configuration = BuildConfiguration();

        var act = () => DatabaseConnectionString.Resolve(configuration, localAppDataPath);

        act.Should().Throw<InvalidOperationException>().WithMessage(
            "No per-user local application-data folder is available for the default database file. "
            + "Set the ConnectionStrings:CryptoStuff setting (the ConnectionStrings__CryptoStuff "
            + "environment variable in deployment) to a SQLite connection string, e.g. "
            + "'Data Source=/path/to/cryptostuff.db'.");
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ConfiguredConnectionStringIsNotValidSqlite()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = "Server=localhost;Password=secret" });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("ConnectionStrings:CryptoStuff is not a valid SQLite connection string: *")
            .Which.Message.Should().NotContain("secret");
    }
}
