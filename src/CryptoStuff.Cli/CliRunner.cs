using System.Globalization;
using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>
/// Parses, validates, and executes a single CLI invocation against
/// <see cref="ICryptocurrencyService"/>, writing results and errors to the
/// given writers instead of the console directly, so tests can assert on
/// strings rather than the console.
/// </summary>
public sealed class CliRunner
{
    private const string UsageMessage =
        """
        Usage: cryptostuff <command> [arguments] [--json]

        Commands:
          price <coins> <currencies>                  Live coin prices (comma-separated ids/currencies)
          token <platform> <addresses> <currencies>   Token prices by contract address
          history <coin> <currency> <days>            Historical market series
          coin <id>                                   Coin detail
          developer <id> <date>                       Developer repository activity (date: dd-MM-yyyy)

        Options:
          --json    Output as JSON instead of human-readable text
        """;

    private readonly ICryptocurrencyService _service;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    /// <summary>Creates a runner that calls <paramref name="service"/> and writes to the given writers.</summary>
    public CliRunner(ICryptocurrencyService service, TextWriter output, TextWriter error)
    {
        _service = service;
        _output = output;
        _error = error;
    }

    /// <summary>
    /// Parses and executes <paramref name="args"/>, returning the process
    /// exit code: <c>0</c> on success, <c>1</c> on a usage, validation, or
    /// service failure, and <c>130</c> if <paramref name="cancellationToken"/>
    /// is canceled mid-request.
    /// </summary>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var parsed = CliArgumentParser.Parse(args);

            return parsed.Command switch
            {
                "price" => await ExecutePriceAsync(parsed, cancellationToken),
                "token" => await ExecuteTokenAsync(parsed, cancellationToken),
                "history" => await ExecuteHistoryAsync(parsed, cancellationToken),
                "coin" => await ExecuteCoinAsync(parsed, cancellationToken),
                "developer" => await ExecuteDeveloperAsync(parsed, cancellationToken),
                _ => WriteUsage(),
            };
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
    }

    private async Task<int> ExecutePriceAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 2)
        {
            return WriteUsage();
        }

        if (!TryParseIdentifierList(parsed.Arguments[0], "Coins", out var coinIds, out var error))
        {
            return WriteError(error);
        }

        if (!TryParseIdentifierList(parsed.Arguments[1], "Currencies", out var vsCurrencies, out error))
        {
            return WriteError(error);
        }

        var query = new CoinPriceQuery { CoinIds = coinIds, VsCurrencies = vsCurrencies };
        var result = await _service.GetPricesAsync(query, cancellationToken);

        return WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteTokenAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 3)
        {
            return WriteUsage();
        }

        if (!TryValidateScalar(parsed.Arguments[0], "Platform", out var error))
        {
            return WriteError(error);
        }

        if (!TryParseIdentifierList(parsed.Arguments[1], "Addresses", out var addresses, out error))
        {
            return WriteError(error);
        }

        if (!TryParseIdentifierList(parsed.Arguments[2], "Currencies", out var vsCurrencies, out error))
        {
            return WriteError(error);
        }

        var query = new TokenPriceQuery
        {
            Platform = parsed.Arguments[0],
            ContractAddresses = addresses,
            VsCurrencies = vsCurrencies,
        };

        var result = await _service.GetTokenPricesAsync(query, cancellationToken);

        return WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteHistoryAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 3)
        {
            return WriteUsage();
        }

        if (!TryValidateScalar(parsed.Arguments[0], "Coin", out var error))
        {
            return WriteError(error);
        }

        if (!TryValidateScalar(parsed.Arguments[1], "Currency", out error))
        {
            return WriteError(error);
        }

        if (!TryValidateDays(parsed.Arguments[2], out var days, out error))
        {
            return WriteError(error);
        }

        var query = new CoinMarketChartQuery { CoinId = parsed.Arguments[0], VsCurrency = parsed.Arguments[1], Days = days };
        var result = await _service.GetMarketChartAsync(query, cancellationToken);

        return WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteCoinAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 1)
        {
            return WriteUsage();
        }

        if (!TryValidateScalar(parsed.Arguments[0], "Id", out var error))
        {
            return WriteError(error);
        }

        var query = new CoinQuery { CoinId = parsed.Arguments[0] };
        var result = await _service.GetCoinAsync(query, cancellationToken);

        return WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteDeveloperAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 2)
        {
            return WriteUsage();
        }

        if (!TryValidateScalar(parsed.Arguments[0], "Id", out var error))
        {
            return WriteError(error);
        }

        if (!TryValidateDate(parsed.Arguments[1], "Date", out error))
        {
            return WriteError(error);
        }

        var query = new CoinDeveloperDataQuery { CoinId = parsed.Arguments[0], Date = parsed.Arguments[1] };
        var result = await _service.GetDeveloperDataAsync(query, cancellationToken);

        return WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    // A CLI positional argument is never null, and string.Split always returns
    // at least one element (an empty string splits to [""]), so a genuinely
    // empty list can never reach this method - only per-element blank entries
    // can. NonEmptyCollectionValidator is therefore not invoked here; the
    // per-element NonEmptyScalarValidator check below already rejects a blank
    // or all-blank argument.
    private static bool TryParseIdentifierList(string raw, string fieldName, out IReadOnlyList<string> values, out string errorMessage)
    {
        var split = raw.Split(',').Select(value => value.Trim()).ToArray();

        foreach (var value in split)
        {
            if (!NonEmptyScalarValidator.Validate(value, fieldName).IsValid)
            {
                values = [];
                errorMessage = $"{fieldName} contains a blank entry.";
                return false;
            }
        }

        values = split;
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateScalar(string value, string fieldName, out string errorMessage)
    {
        var outcome = NonEmptyScalarValidator.Validate(value, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    private static bool TryValidateDate(string value, string fieldName, out string errorMessage)
    {
        var scalarOutcome = NonEmptyScalarValidator.Validate(value, fieldName);
        if (!scalarOutcome.IsValid)
        {
            // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
            errorMessage = scalarOutcome.ErrorMessage!;
            return false;
        }

        var dateOutcome = DateFormatValidator.Validate(value, fieldName);
        errorMessage = dateOutcome.ErrorMessage ?? string.Empty;
        return dateOutcome.IsValid;
    }

    private static bool TryValidateDays(string raw, out int days, out string errorMessage)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
        {
            errorMessage = "Days must be a whole number.";
            return false;
        }

        var outcome = PositiveDayCountValidator.Validate(days, "Days");
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    private int WriteServiceResult<TView>(ServiceResult<TView> result, bool json, Func<TView, bool, string> format) =>
        // ServiceResult<T> guarantees Value is non-null when IsSuccess is true, and ErrorCode is set when it's false.
        result.IsSuccess
            ? WriteResult(format(result.Value!, json))
            : WriteError(ServiceErrorMessages.For(result.ErrorCode!.Value));

    private int WriteUsage()
    {
        _error.Write(UsageMessage);
        _error.Write('\n');
        return 1;
    }

    private int WriteError(string message)
    {
        _error.Write(message);
        _error.Write('\n');
        return 1;
    }

    private int WriteResult(string formatted)
    {
        _output.Write(formatted);
        _output.Write('\n');
        return 0;
    }
}
