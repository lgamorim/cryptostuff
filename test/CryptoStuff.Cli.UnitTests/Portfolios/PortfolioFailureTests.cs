using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;

namespace CryptoStuff.Cli.UnitTests.Portfolios;

public class PortfolioFailureTests
{
    private const string OversellDetail = "Selling 2 bitcoin on 2024-02-01 would take the holding below zero.";

    private static readonly string[][] SubcommandArguments =
    [
        ["list"],
        ["create", "main", "usd", "fifo"],
        ["show", "1"],
        ["update", "1", "main", "fifo"],
        ["delete", "1"],
        ["value", "1"],
        ["tx", "add", "1", "sell", "bitcoin", "2", "40000", "2024-02-01T00:00:00Z"],
        ["tx", "edit", "1", "2", "sell", "bitcoin", "2", "40000", "2024-02-01T00:00:00Z"],
        ["tx", "delete", "1", "2"],
    ];

    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyPortfolioService _portfolios = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public PortfolioFailureTests()
    {
        _runner = new CliRunner(_market, _output, _error, portfolios: () => _portfolios);
    }

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

    [Theory]
    [MemberData(nameof(SubcommandsWithErrorMessages))]
    public async Task Should_ReturnOneAndFixedMessage_When_ServiceFailsWithoutDetail(string[] subcommand, ServiceErrorCode code, string expectedMessage)
    {
        // Arrange
        _portfolios.FailAllWith(code);

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"{expectedMessage}\n");
        _output.ToString().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Subcommands))]
    public async Task Should_PreferDetailOverFixedMessage_When_ServiceFailsWithRuleViolated(string[] subcommand)
    {
        // Arrange
        _portfolios.FailAllWith(ServiceErrorCode.RuleViolated, OversellDetail);

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: {OversellDetail}\n");
        _output.ToString().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Subcommands))]
    public async Task Should_Return130AndPassToken_When_RunIsCanceled(string[] subcommand)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        _portfolios.ExceptionToThrow = new OperationCanceledException(cancellationTokenSource.Token);

        // Act
        var exitCode = await _runner.RunAsync(["portfolio", .. subcommand], cancellationTokenSource.Token);

        // Assert
        exitCode.Should().Be(130);
        _portfolios.CapturedCancellationToken.Should().Be(cancellationTokenSource.Token);
        _output.ToString().Should().BeEmpty();
    }
}
