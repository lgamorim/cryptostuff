namespace CryptoStuff.Cli;

/// <summary>The parsed shape of the process's raw command-line arguments.</summary>
public sealed record CliArguments
{
    /// <summary>The command name (the first non-flag token), or null when none was given.</summary>
    public string? Command { get; init; }

    /// <summary>The command's positional arguments, in order, with <c>--json</c>, <c>--exit-code</c>, and <c>--fee</c> and its value removed.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Whether <c>--json</c> was present anywhere in the raw arguments.</summary>
    public required bool JsonOutput { get; init; }

    /// <summary>The raw value that followed <c>--fee</c>, or null when the option was absent.</summary>
    public string? Fee { get; init; }

    /// <summary>
    /// Whether <c>--exit-code</c> was present anywhere in the raw arguments,
    /// asking <c>alert check</c> for a distinct exit code when an alert triggered.
    /// </summary>
    public bool ExitCodeOnTrigger { get; init; }
}

/// <summary>
/// Splits raw process arguments into a command, its arguments, the
/// <c>--json</c> and <c>--exit-code</c> flags, and the <c>--fee</c> option.
/// </summary>
public static class CliArgumentParser
{
    private const string JsonFlag = "--json";
    private const string ExitCodeFlag = "--exit-code";
    private const string FeeOption = "--fee";

    /// <summary>
    /// Parses <paramref name="args"/>. <c>--json</c> and <c>--exit-code</c> are
    /// recognized in any position and removed from the returned arguments, as is the first
    /// <c>--fee</c> together with the token after it, which becomes
    /// <see cref="CliArguments.Fee"/>; the first remaining token becomes
    /// <see cref="CliArguments.Command"/> and the rest become
    /// <see cref="CliArguments.Arguments"/>.
    /// </summary>
    public static CliArguments Parse(string[] args)
    {
        var jsonOutput = false;
        var exitCodeOnTrigger = false;
        string? fee = null;
        var positional = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], JsonFlag, StringComparison.Ordinal))
            {
                jsonOutput = true;
            }
            else if (string.Equals(args[i], ExitCodeFlag, StringComparison.Ordinal))
            {
                exitCodeOnTrigger = true;
            }
            else if (fee is null && string.Equals(args[i], FeeOption, StringComparison.Ordinal) && HasValueAfter(args, i))
            {
                fee = args[++i];
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        var command = positional.Count > 0 ? positional[0] : null;
        var arguments = positional.Count > 0 ? positional.GetRange(1, positional.Count - 1) : [];

        return new CliArguments
        {
            Command = command,
            Arguments = arguments,
            JsonOutput = jsonOutput,
            Fee = fee,
            ExitCodeOnTrigger = exitCodeOnTrigger,
        };
    }

    // A value-less --fee, or one followed by a flag, is left positional so the
    // command that receives it reports it as an unexpected argument.
    private static bool HasValueAfter(string[] args, int index) =>
        index + 1 < args.Length
        && !string.Equals(args[index + 1], JsonFlag, StringComparison.Ordinal)
        && !string.Equals(args[index + 1], ExitCodeFlag, StringComparison.Ordinal);
}
