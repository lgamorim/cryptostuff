using CryptoStuff.Cli.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Cli.UnitTests.Alerts;

public class AlertFailureTests
{
    private const string UnknownCoinDetail = "CoinGecko does not know the coin 'bitcoinn'.";

    private static readonly string[][] SubcommandArguments =
    [
        ["list"],
        ["create", "bitcoin", "usd", "above", "50000"],
        ["show", "1"],
        ["update", "1", "above", "50000"],
        ["delete", "1"],
        ["rearm", "1"],
        ["check"],
    ];

    private readonly SpyCryptocurrencyService _market = new();
    private readonly SpyAlertService _alerts = new();
    private readonly SpyAlertEvaluator _evaluator = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly CliRunner _runner;

    public AlertFailureTests()
    {
        _runner = new CliRunner(_market, _output, _error, alerts: () => _alerts, alertEvaluator: () => _evaluator);
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
        FailAllWith(code);

        // Act
        var exitCode = await _runner.RunAsync(["alert", .. subcommand], CancellationToken.None);

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
        FailAllWith(ServiceErrorCode.RuleViolated, UnknownCoinDetail);

        // Act
        var exitCode = await _runner.RunAsync(["alert", .. subcommand], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be($"Error: {UnknownCoinDetail}\n");
        _output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnOneAndConflictMessage_When_RearmingAnArmedAlert()
    {
        // Arrange
        _alerts.RearmResult = ServiceResult<AlertView>.Failure(ServiceErrorCode.Conflict, "Alert 1 is already armed.");

        // Act
        var exitCode = await _runner.RunAsync(["alert", "rearm", "1"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Error: Alert 1 is already armed.\n");
        _output.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnOneAndPrintNothingToOutput_When_CheckFailsUpstream()
    {
        // Arrange
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Failure(ServiceErrorCode.RateLimited);

        // Act
        var exitCode = await _runner.RunAsync(["alert", "check"], CancellationToken.None);

        // Assert
        exitCode.Should().Be(1);
        _error.ToString().Should().Be("Error: the CoinGecko rate limit was exceeded. Try again later.\n");
        _output.ToString().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Subcommands))]
    public async Task Should_Return130AndPassToken_When_RunIsCanceled(string[] subcommand)
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellation = new OperationCanceledException(cancellationTokenSource.Token);
        _alerts.ExceptionToThrow = cancellation;
        _evaluator.ExceptionToThrow = cancellation;

        // Act
        var exitCode = await _runner.RunAsync(["alert", .. subcommand], cancellationTokenSource.Token);

        // Assert
        exitCode.Should().Be(130);
        var captured = subcommand[0] == "check" ? _evaluator.CapturedCancellationToken : _alerts.CapturedCancellationToken;
        captured.Should().Be(cancellationTokenSource.Token);
        _output.ToString().Should().BeEmpty();
    }

    private void FailAllWith(ServiceErrorCode errorCode, string? errorDetail = null)
    {
        _alerts.FailAllWith(errorCode, errorDetail);
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Failure(errorCode, errorDetail);
    }
}
