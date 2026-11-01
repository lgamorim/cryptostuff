namespace CryptoStuff.Cli;

/// <summary>The usage text every command family prints when it is invoked wrongly.</summary>
public static class CliUsage
{
    /// <summary>The full usage message, without a trailing newline.</summary>
    public const string Message =
        """
        Usage: cryptostuff <command> [arguments] [--json]

        Commands:
          price <coins> <currencies>                  Live coin prices (comma-separated ids/currencies)
          token <platform> <addresses> <currencies>   Token prices by contract address
          history <coin> <currency> <days>            Historical market series
          coin <id>                                   Coin detail
          developer <id> <date>                       Developer repository activity (date: dd-MM-yyyy)
          watchlist list                              List watchlists
          watchlist create <name>                     Create an empty watchlist
          watchlist show <id>                         Show a watchlist
          watchlist rename <id> <name>                Rename a watchlist
          watchlist delete <id>                       Delete a watchlist
          watchlist add <id> <coin>                   Add a coin to a watchlist
          watchlist remove <id> <coin>                Remove a coin from a watchlist
          watchlist prices <id> <currency>            Live prices of a watchlist's coins

        Options:
          --json    Output as JSON instead of human-readable text
        """;
}
