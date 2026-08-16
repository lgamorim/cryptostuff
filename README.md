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
| `CryptoStuff.Core` | Queries, view records, results, mapping, caching, validation |
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

## Build, test, and format

```bash
dotnet build
dotnet test
dotnet format
```

CI (`.github/workflows/ci.yml`) runs the same three commands — build and test
in the `Release` configuration, plus a format-verification check — on both
`ubuntu-latest` and `windows-latest`, on every push.
