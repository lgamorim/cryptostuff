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
          portfolio list                              List portfolios
          portfolio create <name> <currency> <average-cost|fifo>
                                                      Create an empty portfolio
          portfolio show <id>                         Show a portfolio and its transactions
          portfolio update <id> <name> <average-cost|fifo>
                                                      Replace a portfolio's name and cost-basis method
          portfolio delete <id>                       Delete a portfolio with its transactions
          portfolio value <id>                        Value a portfolio at live prices
          portfolio tx add <id> <buy|sell> <coin> <qty> <price> <when> [--fee <fee>]
                                                      Record a transaction (when: ISO 8601, e.g. 2024-01-31T14:30:00Z)
          portfolio tx edit <id> <txid> <buy|sell> <coin> <qty> <price> <when> [--fee <fee>]
                                                      Replace a transaction
          portfolio tx delete <id> <txid>             Delete a transaction
          alert list                                  List price alerts
          alert create <coin> <currency> <above|below> <threshold>
                                                      Create an armed alert (threshold: a number above zero)
          alert show <id>                             Show an alert
          alert update <id> <above|below> <threshold> Replace an alert's direction and threshold, which re-arms it
          alert delete <id>                           Delete an alert
          alert rearm <id>                            Re-arm a triggered alert
          alert check                                 Evaluate the armed alerts once and print what triggered and what could not be priced

        Options:
          --json         Output as JSON instead of human-readable text
          --fee <fee>    Fee of a portfolio transaction, in the portfolio's currency (default 0)
        """;
}
