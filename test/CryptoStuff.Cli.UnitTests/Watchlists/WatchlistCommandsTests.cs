using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli.UnitTests.Watchlists;

public class WatchlistCommandsTests
{
    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyWatchlistService _watchlists = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    private static readonly string[][] SubcommandArguments =
    [
        ["list"],
        ["create", "majors"],
        ["show", "1"],
        ["rename", "1", "majors"],
        ["delete", "1"],
        ["add", "1", "bitcoin"],
        ["remove", "1", "bitcoin"],
        ["prices", "1", "usd"],
    ];

    public static TheoryData<string[]> Subcommands => new(SubcommandArguments);

    public static TheoryData<string[], ServiceErrorCode, string> SubcommandsWithErrorMessages
    {
        get
        {
            var messages = new Dictionary<ServiceErrorCode, string>
            {
                [ServiceErrorCode.NotFound] = "Error: the requested resource was not found.",
                [ServiceErrorCode.RateLimited] = "Error: the CoinGecko rate limit was exceeded. Try again later.",
                [ServiceErrorCode.RequestTimedOut] = "Error: the request to CoinGecko timed out.",
                [ServiceErrorCode.UpstreamUnavailable] = "Error: CoinGecko is currently unavailable.",
                [ServiceErrorCode.Conflict] = "Error: the request conflicts with existing data.",
                [ServiceErrorCode.RuleViolated] = "Error: the request violates a domain rule.",
                [ServiceErrorCode.PreconditionFailed] = "Error: the data has changed since it was last read.",
            };

            var data = new TheoryData<string[], ServiceErrorCode, string>();
            foreach (var args in SubcommandArguments)
            {
                foreach (var (code, message) in messages)
                {
                    data.Add(args, code, message);
                }
            }

            return data;
        }
    }

    public WatchlistCommandsTests()
    {
        _runner = new CliRunner(_market, _output, _error, () => _watchlists);
    }

    [Fact]
    public async Task Should_ReturnZeroAndListText_When_ListSucceeds()
    {
        // Arrange
        _watchlists.ListResult = ServiceResult<IReadOnlyList<WatchlistView>>.Success(
        [
            new WatchlistView { Id = 1, Name = "majors", CoinIds = ["bitcoin", "ethereum"] },
        ]);

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("1: majors (bitcoin, ethereum)\n");
        _error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnZeroAndJson_When_ListSucceedsWithJsonFlag()
    {
        // Arrange
        _watchlists.ListResult = ServiceResult<IReadOnlyList<WatchlistView>>.Success(
        [
            new WatchlistView { Id = 1, Name = "majors", CoinIds = ["bitcoin"] },
        ]);

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "list", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Name\": \"majors\"").And.Contain("\"bitcoin\"");
    }

    [Fact]
    public async Task Should_PrintPlaceholder_When_ListIsEmpty()
    {
        // Arrange
        _watchlists.ListResult = ServiceResult<IReadOnlyList<WatchlistView>>.Success([]);

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "list"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("(no watchlists)\n");
    }

    [Fact]
    public async Task Should_PrintNoCoinsPlaceholder_When_ListedWatchlistIsEmpty()
    {
        // Arrange
        _watchlists.ListResult = ServiceResult<IReadOnlyList<WatchlistView>>.Success(
        [
            new WatchlistView { Id = 1, Name = "majors", CoinIds = [] },
            new WatchlistView { Id = 2, Name = "defi", CoinIds = ["aave"] },
        ]);

        // Act
        await _runner.RunAsync(["watchlist", "list"], CancellationToken.None);

        // Assert
        _output.ToString().Should().Be("1: majors (no coins)\n2: defi (aave)\n");
    }

    [Fact]
    public async Task Should_PrintWatchlistText_When_ShowSucceeds()
    {
        // Arrange
        _watchlists.GetResult = ServiceResult<WatchlistView>.Success(
            new WatchlistView { Id = 7, Name = "majors", CoinIds = ["bitcoin", "ethereum"] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "show", "7"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Id: 7\nName: majors\nCoins: bitcoin, ethereum\n");
        _watchlists.CapturedId.Should().Be(7);
    }

    [Fact]
    public async Task Should_PrintWatchlistJson_When_ShowSucceedsWithJsonFlag()
    {
        // Arrange
        _watchlists.GetResult = ServiceResult<WatchlistView>.Success(
            new WatchlistView { Id = 7, Name = "majors", CoinIds = ["bitcoin"] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "show", "7", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Id\": 7").And.Contain("\"Name\": \"majors\"").And.Contain("\"bitcoin\"");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData("99999999999")]
    public async Task Should_ReturnOneAndValidationError_When_ShowIdIsNotAWholeNumber(string rawId)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "show", rawId], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id must be a whole number.\n");
        _output.ToString().Should().BeEmpty();
        _watchlists.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Should_ReturnOneAndValidationError_When_ShowIdIsNotPositive(string rawId)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "show", rawId], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id must be a positive whole number.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_CreateAndPrintWatchlist_When_CreateSucceeds()
    {
        // Arrange
        _watchlists.CreateResult = ServiceResult<WatchlistView>.Success(
            new WatchlistView { Id = 3, Name = "my picks", CoinIds = [] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "create", "my picks"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Id: 3\nName: my picks\nCoins: no coins\n");
        _watchlists.CapturedCreateCommand.Should().BeEquivalentTo(new CreateWatchlistCommand { Name = "my picks" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnOneAndValidationError_When_CreateNameIsBlank(string name)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "create", name], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Name is required.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_CreateNameIsTooLong()
    {
        // Arrange
        var name = new string('a', NameValidator.MaxLength + 1);

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "create", name], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Name must be at most {NameValidator.MaxLength} characters.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_RenameAndPrintWatchlist_When_RenameSucceeds()
    {
        // Arrange
        _watchlists.RenameResult = ServiceResult<WatchlistView>.Success(
            new WatchlistView { Id = 4, Name = "new name", CoinIds = ["bitcoin"] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "rename", "4", "new name"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Id: 4\nName: new name\nCoins: bitcoin\n");
        _watchlists.CapturedRenameCommand.Should().BeEquivalentTo(new RenameWatchlistCommand { WatchlistId = 4, Name = "new name" });
    }

    [Fact]
    public async Task Should_ReturnOneAndValidationError_When_RenameNameIsBlank()
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "rename", "4", " "], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Name is required.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_DeleteAndConfirm_When_DeleteSucceeds()
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "delete", "5"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Deleted watchlist 5.\n");
        _watchlists.CapturedId.Should().Be(5);
    }

    [Fact]
    public async Task Should_PrintDeletedJson_When_DeleteSucceedsWithJsonFlag()
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "delete", "5", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Id\": 5").And.Contain("\"Deleted\": true");
    }

    [Fact]
    public async Task Should_AddCoinAndPrintWatchlist_When_AddSucceeds()
    {
        // Arrange
        _watchlists.AddCoinResult = ServiceResult<WatchlistView>.Success(
            new WatchlistView { Id = 2, Name = "majors", CoinIds = ["bitcoin", "ethereum"] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "add", "2", "Ethereum"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Id: 2\nName: majors\nCoins: bitcoin, ethereum\n");
        _watchlists.CapturedAddCoinCommand.Should().BeEquivalentTo(new WatchlistCoinCommand { WatchlistId = 2, CoinId = "Ethereum" });
        _watchlists.CapturedRemoveCoinCommand.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_ReturnOneAndValidationError_When_AddCoinIsBlank(string coin)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "add", "2", coin], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Coin is required.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_RemoveCoinAndPrintWatchlist_When_RemoveSucceeds()
    {
        // Arrange
        _watchlists.RemoveCoinResult = ServiceResult<WatchlistView>.Success(
            new WatchlistView { Id = 2, Name = "majors", CoinIds = ["bitcoin"] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "remove", "2", "ethereum"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("Id: 2\nName: majors\nCoins: bitcoin\n");
        _watchlists.CapturedRemoveCoinCommand.Should().BeEquivalentTo(new WatchlistCoinCommand { WatchlistId = 2, CoinId = "ethereum" });
        _watchlists.CapturedAddCoinCommand.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_ReturnOneAndValidationError_When_RemoveCoinIsBlank(string coin)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "remove", "2", coin], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Coin is required.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PrintPricesAndForwardQuery_When_PricesSucceeds()
    {
        // Arrange
        _watchlists.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "prices", "2", "usd"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("bitcoin: 43000 usd\n");
        _watchlists.CapturedPriceQuery.Should().BeEquivalentTo(new WatchlistPriceQuery { WatchlistId = 2, VsCurrency = "usd" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_ReturnOneAndValidationError_When_PricesCurrencyIsBlank(string currency)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "prices", "2", currency], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Currency is required.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PrintNoCoinsPlaceholder_When_PricesOfEmptyWatchlist()
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "prices", "2", "usd"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Be("(no coins)\n");
    }

    [Fact]
    public async Task Should_PrintPricesJson_When_PricesSucceedsWithJsonFlag()
    {
        // Arrange
        _watchlists.PricesResult = ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = [] });

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", "prices", "2", "usd", "--json"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(0);
        _output.ToString().Should().Contain("\"Coins\": []");
    }

    [Theory]
    [MemberData(nameof(SubcommandsWithErrorMessages))]
    public async Task Should_ReturnOneAndErrorLine_When_ServiceFailsWithoutDetail(string[] subcommand, ServiceErrorCode errorCode, string expectedMessage)
    {
        // Arrange
        _watchlists.FailAllWith(errorCode);

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedMessage}\n");
        _output.ToString().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Subcommands))]
    public async Task Should_PreferErrorDetail_When_ServiceFailsWithDetail(string[] subcommand)
    {
        // Arrange
        _watchlists.FailAllWith(ServiceErrorCode.Conflict, "A watchlist named 'majors' already exists.");

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Error: A watchlist named 'majors' already exists.\n");
        _output.ToString().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Subcommands))]
    public async Task Should_Return130AndPassToken_When_SubcommandIsCanceled(string[] subcommand)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        _watchlists.ExceptionToThrow = new OperationCanceledException();

        // Act
        var exitCode = await _runner.RunAsync(["watchlist", .. subcommand], cancellationTokenSource.Token);

        // Assert
        exitCode.Should().Be(130);
        _output.ToString().Should().BeEmpty();
        _error.ToString().Should().BeEmpty();
        _watchlists.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
    }

    [Theory]
    [InlineData("rename", "abc", "majors")]
    [InlineData("delete", "abc")]
    [InlineData("add", "abc", "bitcoin")]
    [InlineData("remove", "abc", "bitcoin")]
    [InlineData("prices", "abc", "usd")]
    public async Task Should_ReturnOneAndValidationError_When_IdIsNotAWholeNumber(params string[] subcommand)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id must be a whole number.\n");
        _watchlists.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("rename", "0", "majors")]
    [InlineData("delete", "-3")]
    [InlineData("add", "0", "bitcoin")]
    [InlineData("remove", "-1", "bitcoin")]
    [InlineData("prices", "0", "usd")]
    public async Task Should_ReturnOneAndValidationError_When_IdIsNotPositive(params string[] subcommand)
    {
        // Act
        var exitCode = await _runner.RunAsync(["watchlist", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Id must be a positive whole number.\n");
        _watchlists.CallCount.Should().Be(0);
    }
}
