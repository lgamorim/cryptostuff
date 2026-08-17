namespace CryptoStuff.Cli;

/// <summary>The parsed shape of the process's raw command-line arguments.</summary>
public sealed record CliArguments
{
    /// <summary>The command name (the first non-flag token), or null when none was given.</summary>
    public string? Command { get; init; }

    /// <summary>The command's positional arguments, in order, with <c>--json</c> removed.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Whether <c>--json</c> was present anywhere in the raw arguments.</summary>
    public required bool JsonOutput { get; init; }
}

/// <summary>Splits raw process arguments into a command, its arguments, and the <c>--json</c> flag.</summary>
public static class CliArgumentParser
{
    private const string JsonFlag = "--json";

    /// <summary>
    /// Parses <paramref name="args"/>. <c>--json</c> is recognized in any
    /// position and removed from the returned arguments; the first remaining
    /// token becomes <see cref="CliArguments.Command"/> and the rest become
    /// <see cref="CliArguments.Arguments"/>.
    /// </summary>
    public static CliArguments Parse(string[] args)
    {
        var jsonOutput = false;
        var positional = new List<string>(args.Length);

        foreach (var arg in args)
        {
            if (string.Equals(arg, JsonFlag, StringComparison.Ordinal))
            {
                jsonOutput = true;
            }
            else
            {
                positional.Add(arg);
            }
        }

        var command = positional.Count > 0 ? positional[0] : null;
        var arguments = positional.Count > 0 ? positional.GetRange(1, positional.Count - 1) : [];

        return new CliArguments
        {
            Command = command,
            Arguments = arguments,
            JsonOutput = jsonOutput,
        };
    }
}
