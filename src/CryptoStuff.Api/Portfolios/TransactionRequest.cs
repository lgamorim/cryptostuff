namespace CryptoStuff.Api.Portfolios;

/// <summary>
/// The JSON body of <c>POST /portfolios/{id}/transactions</c> and
/// <c>PUT /portfolios/{id}/transactions/{txId}</c>, shaped like a transaction
/// in a portfolio response so one can be sent back edited.
/// </summary>
public sealed record TransactionRequest
{
    /// <summary>The kind token: <c>buy</c> or <c>sell</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>The CoinGecko coin id, in any case.</summary>
    public string? CoinId { get; init; }

    /// <summary>How many units were bought or sold; greater than zero.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>The price of one unit, in the portfolio's vs_currency; zero or greater.</summary>
    public decimal? UnitPrice { get; init; }

    /// <summary>The fee paid, in the portfolio's vs_currency; zero or greater, and zero when omitted.</summary>
    public decimal? Fee { get; init; }

    /// <summary>When the transaction happened: an ISO 8601 timestamp with a UTC offset, such as <c>2024-01-31T14:30:00Z</c>.</summary>
    public string? OccurredAt { get; init; }
}
