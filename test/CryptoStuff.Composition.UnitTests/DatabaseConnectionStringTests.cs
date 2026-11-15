using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CryptoStuff.Composition.UnitTests;

public class DatabaseConnectionStringTests
{
    private static readonly string LocalAppDataPath = Path.Combine(Path.GetTempPath(), "local-app-data");

    // Absolute on every OS the tests run on; built so a temp path holding ';'
    // stays one value.
    private static readonly string CustomConnectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(Path.GetTempPath(), "custom.db"),
    }.ToString();

    private const string WindowsExample = @"Data Source=C:\data\cryptostuff.db";

    private const string ElsewhereExample = "Data Source=/var/lib/cryptostuff/cryptostuff.db";

    private const string NotAnAbsoluteFileMessage =
        "ConnectionStrings:CryptoStuff must name the database file by an absolute path, such as "
        + $"'{WindowsExample}' on Windows or '{ElsewhereExample}' elsewhere, so the CLI and the API open "
        + "the same file wherever they are launched from. Relative paths, in-memory and temporary "
        + "databases, file: URIs and |DataDirectory| are not supported.";

    private const string NotValidSqliteMessage =
        "ConnectionStrings:CryptoStuff is not a valid SQLite connection string. Check that every keyword is "
        + "one SQLite knows, that each value suits its keyword, and that any value holding ';' or '=' is quoted.";

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    [Fact]
    public void Should_ReturnConfiguredValue_When_ConnectionStringIsConfigured()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = CustomConnectionString });

        var connectionString = DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        connectionString.Should().Be(CustomConnectionString);
    }

    [Fact]
    public void Should_ReturnConfiguredValue_When_ConnectionStringIsConfiguredAndAppDataPathIsBlank()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = CustomConnectionString });

        var connectionString = DatabaseConnectionString.Resolve(configuration, "");

        connectionString.Should().Be(CustomConnectionString);
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
            + "environment variable in deployment) to a SQLite connection string naming an absolute file, "
            + $"such as '{WindowsExample}' on Windows or '{ElsewhereExample}' elsewhere.");
    }

    // Both messages above suggest these forms; the one for the OS the tests
    // run on must itself be accepted, or a user who copies it is refused again.
    [Fact]
    public void Should_AcceptTheExampleTheMessagesGiveForThisOperatingSystem_When_ItIsConfigured()
    {
        var example = OperatingSystem.IsWindows() ? WindowsExample : ElsewhereExample;
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = example });

        var connectionString = DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        connectionString.Should().Be(example);
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ConfiguredConnectionStringIsNotValidSqlite()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = "Server=localhost;Password=secret" });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage(NotValidSqliteMessage)
            .Which.Message.Should().NotContain("secret");
    }

    [Theory]
    [InlineData("Data Source=x.db;Pooling=maybe")]
    [InlineData("Data Source=x.db;Default Timeout=soon")]
    public void Should_ThrowInvalidOperationException_When_AValueDoesNotSuitItsKeyword(string connectionString)
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = connectionString });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        act.Should().Throw<InvalidOperationException>().WithMessage(NotValidSqliteMessage);
    }

    [Fact]
    public void Should_EchoNoPartOfTheValue_When_AnUnquotedSemicolonSplitsThePassword()
    {
        // The parser would report "zq9", the rest of the password, as an unknown keyword.
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = "Data Source=x.db;Password=ab;zq9=1" });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().NotContain("zq9");
        exception.InnerException.Should().BeNull("an unhandled start-up exception prints its inner exception's message too");
    }

    [Theory]
    [InlineData("Data Source=cryptostuff.db")]
    [InlineData("Data Source=data/cryptostuff.db")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=")]
    [InlineData("Mode=ReadWriteCreate")]
    [InlineData("Data Source=file:cryptostuff.db")]
    [InlineData("Data Source=file:/data/cryptostuff.db?mode=rwc")]
    [InlineData("Data Source=|DataDirectory|cryptostuff.db")]
    public void Should_ThrowInvalidOperationException_When_DataSourceIsNotAnAbsoluteFilePath(string connectionString)
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = connectionString });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        act.Should().Throw<InvalidOperationException>().WithMessage(NotAnAbsoluteFileMessage);
    }

    // Only Windows has paths that are rooted yet not fully qualified: one
    // relative to the current drive, and one relative to a drive's current folder.
    [Theory]
    [InlineData(@"Data Source=\cryptostuff.db")]
    [InlineData(@"Data Source=C:cryptostuff.db")]
    public void Should_ThrowInvalidOperationException_When_DataSourceIsRootedButNotFullyQualifiedOnWindows(string connectionString)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows paths can be rooted without being fully qualified.");
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = connectionString });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        act.Should().Throw<InvalidOperationException>().WithMessage(NotAnAbsoluteFileMessage);
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_AnAbsoluteFileIsOpenedInMemory()
    {
        var configuration = BuildConfiguration(new() { ["ConnectionStrings:CryptoStuff"] = CustomConnectionString + ";Mode=Memory;Cache=Shared" });

        var act = () => DatabaseConnectionString.Resolve(configuration, LocalAppDataPath);

        act.Should().Throw<InvalidOperationException>().WithMessage(NotAnAbsoluteFileMessage);
    }
}
