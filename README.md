# cryptostuff

[![CI](https://github.com/lgamorim/cryptostuff/actions/workflows/ci.yml/badge.svg)](https://github.com/lgamorim/cryptostuff/actions/workflows/ci.yml)

A cryptocurrency market data application: a core layer that owns the domain
workflow, an isolated CoinGecko API client beneath it, and two presentation
hosts above it — a console app and a minimal REST API. See
[`ROADMAP.md`](ROADMAP.md) for the full plan of record.

## Projects

| Project | Purpose |
|---|---|
| `CryptoStuff.CoinGecko` | API client — sole owner of the wire format; covers `simple/price`, `simple/token_price/{platform}`, `coins/{id}/market_chart`, `coins/{id}`, and `coins/{id}/history` (the latter's `date` query parameter must be `dd-MM-yyyy`) |
| `CryptoStuff.Core` | Queries (`CoinPriceQuery`, `TokenPriceQuery`, `CoinMarketChartQuery`, `CoinQuery`, `CoinDeveloperDataQuery`) and view records (`CoinPriceView`, `TokenPriceView`, `CoinMarketChartView`, `CoinView`, `CoinDeveloperDataView`), results, mapping, caching, validation |
| `CryptoStuff.Composition` | Registration and HTTP pipeline wiring |
| `CryptoStuff.Cli` | Console host |
| `CryptoStuff.Api` | Minimal REST host |
| `CryptoStuff.CoinGecko.UnitTests` | Unit tests for `CryptoStuff.CoinGecko` |
| `CryptoStuff.Core.UnitTests` | Unit tests for `CryptoStuff.Core` |
| `CryptoStuff.Composition.UnitTests` | Unit tests for `CryptoStuff.Composition` |
| `CryptoStuff.Cli.UnitTests` | Unit tests for `CryptoStuff.Cli` |
| `CryptoStuff.Api.UnitTests` | Unit tests for `CryptoStuff.Api` |

`CryptoStuff.Cli` and `CryptoStuff.Api` both reference `CryptoStuff.Composition`,
which references `CryptoStuff.Core`, which references `CryptoStuff.CoinGecko`.
Dependencies point inward; no lower layer references a layer above it.

## Prerequisites

- .NET SDK `10.0.301`, pinned in [`global.json`](global.json).
- A CoinGecko demo API key. Locally, set it as a user secret under
  `CoinGecko:ApiKey`; in deployment, set the `CoinGecko__ApiKey` environment
  variable.

## Configuration

`CryptoStuff.CoinGecko` talks to CoinGecko's REST API at
`https://api.coingecko.com/api/v3`. Every request is authenticated with the
demo API key described in Prerequisites (`CoinGecko:ApiKey` locally,
`CoinGecko__ApiKey` in deployment), sent as the `x-cg-demo-api-key` HTTP
header.

## Error codes

`CryptoStuff.Core` maps a failed CoinGecko response to a `ServiceErrorCode`:

| Code | Meaning | Upstream condition |
|---|---|---|
| `NotFound` | The requested resource does not exist | CoinGecko returned `404` |
| `RateLimited` | The CoinGecko rate limit was exceeded | CoinGecko returned `429` |
| `RequestTimedOut` | The request to CoinGecko did not complete in time | The request timed out |
| `UpstreamUnavailable` | CoinGecko failed for any other reason | Any other non-success response |

## Missing identifiers

`GetPricesAsync` and `GetTokenPricesAsync` check that every requested coin id
(or contract address) is present in CoinGecko's response. CoinGecko's
`simple/price` and `simple/token_price` endpoints silently omit unknown
identifiers instead of returning an error, so `CryptoStuff.Core` treats any
requested identifier absent from the response as a `NotFound` failure for the
whole request, rather than returning a partial result with a silent gap.

The single-resource endpoints (`coins/{id}`, `coins/{id}/market_chart`,
`coins/{id}/history`) don't need this check: CoinGecko itself returns `404`
for an unknown coin id, which the error-code mapping above already turns into
`NotFound`.

## Input validation

`CryptoStuff.Core` provides a small set of shared validation rules for the
CLI and API hosts to apply to user input before a request reaches CoinGecko.
Each rule reports which field failed and what was expected of it.

| Rule | Applies to | What's required |
|---|---|---|
| Non-empty scalar | Coin id, platform, vs_currency code, developer-data date | The value must be present and not just whitespace |
| Non-empty collection | Coin id lists, contract address lists, vs_currency lists | At least one value must be given |
| Positive day count | The number of days of history to fetch | A whole number greater than zero |
| Well-formed date | The developer-data date | A real calendar date written as `dd-MM-yyyy` (e.g. `29-02-2024`) |

## Caching

`CryptoStuff.Core` provides `CachingCryptocurrencyService`, a decorator over
`ICryptocurrencyService` that caches two of its five operations:

| Operation | Cached by |
|---|---|
| `GetCoinAsync` (coin detail) | Coin id |
| `GetDeveloperDataAsync` (developer activity) | Coin id and date |

`GetPricesAsync`, `GetTokenPricesAsync`, and `GetMarketChartAsync` are never
cached — live prices and historical series change too quickly for caching to
be worth it. Only successful results are cached; a failed result is retried
on the very next request for the same key rather than being cached or served
stale.

The decorator's cache duration is a constructor argument; it doesn't read
configuration itself. Once milestone `3.6` wires it into the DI container,
its duration will come from the `CoinGecko:CacheSeconds` setting, defaulting
to `300` (5 minutes) when unset.

## Rate limiting

`CryptoStuff.Composition` provides `RateLimitRetryHandler`, a
`DelegatingHandler` that retries a request automatically when CoinGecko
responds `429 Too Many Requests`, up to a bounded number of attempts:

| Signal | Behaviour |
|---|---|
| `Retry-After` header present, delta-seconds form (e.g. `Retry-After: 5`) | Waits exactly that many seconds before retrying |
| `Retry-After` header present, HTTP-date form | Waits until that date (never negative — a date already in the past retries immediately) |
| `Retry-After` header absent | Waits a constant backoff instead |
| Retry attempts exhausted | Returns the last `429` response as-is |

Retries only ever happen on `429`; any other status code — success or
failure — passes straight through untouched. If every retry still comes
back `429`, the request still fails and the caller still sees a failure — the
handler retries, it doesn't swallow the eventual outcome. Whether that
failure surfaces as `RateLimited` or as `RequestTimedOut` (see
[Error codes](#error-codes)) depends on how the retry delays compare to the
`HttpClient`'s own timeout, which is configured elsewhere.

The handler's attempt count and backoff duration are constructor arguments;
it doesn't read configuration itself and isn't yet wired into the HTTP
pipeline used by the hosts. Milestone `3.6` is where those values, and the
`HttpClient` timeout they need to stay compatible with, actually get chosen.

## Build, test, and format

```bash
dotnet build
dotnet test
dotnet format
```

CI (`.github/workflows/ci.yml`) runs the same three commands — build and test
in the `Release` configuration, plus a format-verification check — on both
`ubuntu-latest` and `windows-latest`, on every push.
