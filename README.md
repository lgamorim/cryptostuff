# cryptostuff

[![CI](https://github.com/lgamorim/cryptostuff/actions/workflows/ci.yml/badge.svg)](https://github.com/lgamorim/cryptostuff/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

A cryptocurrency market data application: a core layer that owns the domain
workflow, an isolated CoinGecko API adapter that implements its market-data
contract, and two presentation hosts above it — a console app and a minimal
REST API. See
[`ROADMAP.md`](ROADMAP.md) for the full plan of record. Both hosts also manage
watchlists, a portfolio ledger, and price alerts, stored in a SQLite database
they share, and the API evaluates the price alerts in the background.

## Why this exists

CryptoStuff is a software engineering playground and an experiment in
AI-assisted development using Claude Code.

The cryptocurrency domain provides a small but realistic application for
exploring .NET/C#, architecture, testing, APIs, resilience, and tooling. At
the same time, the project is used to experiment with how AI can participate
in the development process.

The workflow separates implementation and review: one Claude Code agent
implements a change on a feature branch, while a separate agent reviews the
resulting diff with fresh context. The maintainer then evaluates the review
and controls the final merge.

The goal isn't simply to have AI write code. It's to explore whether a
structured, human-controlled workflow can make AI-assisted development more
reliable, repeatable, and useful.

The cryptocurrency part is the subject matter. The engineering is the point.
The AI workflow is the experiment.

## Projects

Seven projects under `src/`, each with its tests under `test/`:

| Project | Role | Tests |
|---|---|---|
| `CryptoStuff.Core` | The domain: the market-data contract, watchlists, portfolios, price alerts, validation, and caching; see [Inside Core](#inside-core) | `CryptoStuff.Core.UnitTests` |
| `CryptoStuff.CoinGecko` | Adapter: the CoinGecko API client, sole owner of the wire format, and the `ICryptocurrencyService` implementation over it | `CryptoStuff.CoinGecko.UnitTests` |
| `CryptoStuff.CoinMarketCap` | Adapter: the CoinMarketCap API client, sole owner of CoinMarketCap's wire format, the translation of the app's ids to CoinMarketCap's, and the `ICryptocurrencyService` implementation over both; no registration uses it yet | `CryptoStuff.CoinMarketCap.UnitTests` |
| `CryptoStuff.Persistence` | Adapter: EF Core over SQLite, implementing Core's repository interfaces, with the checked-in migrations | `CryptoStuff.Persistence.IntegrationTests` |
| `CryptoStuff.Composition` | Wiring: service registration, the HTTP retry pipeline, the database location, and the JSON shape both hosts write | `CryptoStuff.Composition.UnitTests` |
| `CryptoStuff.Cli` | Console host | `CryptoStuff.Cli.UnitTests` |
| `CryptoStuff.Api` | Minimal REST host, which also evaluates the price alerts in the background | `CryptoStuff.Api.UnitTests`, `CryptoStuff.Api.IntegrationTests` |

An arrow reads "references":

```mermaid
graph TD
    Cli[CryptoStuff.Cli] --> Composition[CryptoStuff.Composition]
    Api[CryptoStuff.Api] --> Composition
    Composition --> CoinGecko[CryptoStuff.CoinGecko]
    Composition --> Persistence[CryptoStuff.Persistence]
    Composition --> Core[CryptoStuff.Core]
    CoinGecko --> Core
    CoinMarketCap[CryptoStuff.CoinMarketCap] --> Core
    Persistence --> Core
```

Dependencies point inward. The adapters plug into `CryptoStuff.Core` by
implementing its interfaces: `CryptoStuff.CoinGecko` and
`CryptoStuff.CoinMarketCap` its `ICryptocurrencyService`, and
`CryptoStuff.Persistence` its repository interfaces. Nothing references
`CryptoStuff.CoinMarketCap` yet, so the hosts still call CoinGecko alone.
`CryptoStuff.Core` references no other project, so it never sees either
provider's wire format or EF Core.

### Inside Core

`CryptoStuff.Core` is organized by feature. Each row names the types to start
reading from and the sections that explain the behaviour:

| Area | Folder | Start with | Explained in |
|---|---|---|---|
| Market data | [root](src/CryptoStuff.Core) | `ICryptocurrencyService`, its five `*Query` inputs and `*View` results, `CachingCryptocurrencyService`, `NegativePriceGuard`, `KnownCoinPrices`, `UpstreamMetrics` | [Caching](#caching), [Missing identifiers](#missing-identifiers), [Unpriced coins](#unpriced-coins), [Upstream metrics](#upstream-metrics) |
| Watchlists | [`Watchlists/`](src/CryptoStuff.Core/Watchlists) | `Watchlist`, `IWatchlistService`, `IWatchlistRepository` | [Watchlist routes](#watchlist-routes) |
| Portfolios | [`Portfolios/`](src/CryptoStuff.Core/Portfolios) | `Portfolio`, `IPortfolioService`, `IPortfolioRepository`, `ICostBasisMethod` | [Cost basis](#cost-basis), [Valuation](#valuation) |
| Price alerts | [`Alerts/`](src/CryptoStuff.Core/Alerts) | `PriceAlert`, `IAlertService`, `IAlertEvaluator`, `IAlertRepository` | [Alert evaluation](#alert-evaluation) |
| Shared | [root](src/CryptoStuff.Core) | `ServiceResult`, `ServiceErrorCode`, the validators, `EnumTokens` | [Error codes](#error-codes), [Input validation](#input-validation) |

The three stored features' folders share one shape: the entity, its
repository interface, a service and its interface, `*Command` records for its
writes, `*View` records for its results, and a mapper. Each enumeration a
feature adds (`CostBasisMethod`, `TransactionKind`, `AlertDirection`,
`AlertState`) comes with a parser, and `EnumTokens` is the one way an
enumeration is written as the text token the hosts print, the database stores,
and those parsers read.

### Inside the other projects

| Project | Start with | Explained in |
|---|---|---|
| `CryptoStuff.CoinGecko` | `ICoinGeckoClient` and `CoinGeckoClient` (the wire format), `CryptocurrencyService` (the adapter) | [Configuration](#configuration), [Error codes](#error-codes), [Upstream metrics](#upstream-metrics) |
| `CryptoStuff.CoinMarketCap` | `ICoinMarketCapClient` and `CoinMarketCapClient` (the wire format), `CoinMarketCapClientExtensions` (one call per endpoint), `CoinMarketCapIds` (id translation), `CoinMarketCapCryptocurrencyService` (the adapter) and its `CoinMarketCapServiceOptions` | [Configuration](#configuration), [CoinMarketCap's answers](#coinmarketcaps-answers), [Error codes](#error-codes) |
| `CryptoStuff.Persistence` | `CryptoStuffDbContext`, a `*Configuration` mapping and a `*Repository` for each stored feature, `CryptoStuffDatabase` | [Data storage](#data-storage), [Database migrations](#database-migrations) |
| `CryptoStuff.Composition` | `AddCryptoStuff`, `AddAlertEvaluationSchedule`, `MigrateCryptoStuffDatabaseAsync`, `IsCryptoStuffDatabaseReadyAsync`, `RateLimitRetryHandler`, `DatabaseConnectionString`, `CryptoStuffJson` | [Registration](#registration), [Rate limiting](#rate-limiting), [Upstream metrics](#upstream-metrics), [Data storage](#data-storage) |
| `CryptoStuff.Cli` | `CliRunner`, a `*Commands` class for each stored feature | [Command-line interface](#command-line-interface) |
| `CryptoStuff.Api` | `CryptoStuffEndpoints`, a `*Endpoints` class for each stored feature, `AlertEvaluationBackgroundService`, `StartupDatabaseMigration`, `DatabaseReadinessHealthCheck`, `ApiKeyAuthentication` and its `ApiKeyAuthenticationHandler`, `ApiKeyOpenApiDocumentTransformer` | [REST API](#rest-api), [API key](#api-key), [Background evaluation](#background-evaluation), [Health, OpenAPI, and Scalar](#health-openapi-and-scalar) |

`CryptoStuff.CoinGecko`'s mappers, from its wire format to Core's views and
from its failures to a `ServiceErrorCode`, and `CoinGeckoEndpoint`, which
names the endpoint a request URI calls for the request counter, are internal,
visible only to `CryptoStuff.CoinGecko.UnitTests` through `InternalsVisibleTo`.
`CryptoStuff.CoinMarketCap`'s URL encoder, endpoint paths, response bodies,
id translation, error mapping, and the quote reader, contract resolver and
chart mapper its service is built from are internal in the same way, visible
only to `CryptoStuff.CoinMarketCap.UnitTests`.

## Prerequisites

- .NET SDK `10.0.404`, pinned in [`global.json`](global.json).
- A CoinGecko demo API key. Locally, set it as a user secret under
  `CoinGecko:ApiKey`; in deployment, set the `CoinGecko__ApiKey` environment
  variable.
- For the REST API only, a secret key of your choosing that its clients must
  send (see [API key](#api-key)). Locally, set it as a user secret under
  `Api:Key` from `src/CryptoStuff.Api`; in deployment, set the `Api__Key`
  environment variable. The CLI does not use it.
- For the [container image](#container-image) only, Docker running Linux
  containers; it needs no local .NET SDK.

## Configuration

`CryptoStuff.CoinGecko` talks to CoinGecko's REST API at
`https://api.coingecko.com/api/v3`. Every request is authenticated with the
demo API key described in Prerequisites (`CoinGecko:ApiKey` locally,
`CoinGecko__ApiKey` in deployment), sent as the `x-cg-demo-api-key` HTTP
header. `CryptoStuff.CoinGecko` consumes five endpoints: `simple/price`,
`simple/token_price/{platform}`, `coins/{id}/market_chart`, `coins/{id}`, and
`coins/{id}/history`, whose `date` query parameter must be `dd-MM-yyyy`.

`CryptoStuff.CoinMarketCap`'s client talks to CoinMarketCap's API at
`https://pro-api.coinmarketcap.com`, sending its key as the
`X-CMC_PRO_API_KEY` HTTP header, and consumes three endpoints:
`v3/cryptocurrency/quotes/latest` and `v2/cryptocurrency/info`, which take
coins by slug (and the info endpoint a token by contract address), and
`v3/cryptocurrency/quotes/historical`, which takes CoinMarketCap's numeric id
for a coin, since it documents no slug parameter. Quote requests send
`skip_invalid=true`, so a slug CoinMarketCap does not know is missing from the
answer instead of failing it. A failure keeps CoinMarketCap's own
`status.error_code` and `status.error_message` beside the HTTP status. Only
`CoinMarketCapCryptocurrencyService` calls the client, and nothing registers
that yet, so no CoinMarketCap key is read.

The app's coin and platform ids are CoinGecko's, so `CoinMarketCapIds`
holds the translation `CoinMarketCapCryptocurrencyService` uses. A coin id translates to its own slug unless the override table
lists it: `binancecoin` becomes `bnb`, `ripple` becomes `xrp`, and so on for
the coins among CoinGecko's top 100 whose slug differs. Ids match exactly, so
`Binancecoin` is not `binancecoin`, as with CoinGecko itself. An id that is
another coin's override slug, such as `xrp`, translates to no slug and comes
back unknown, as it does from CoinGecko, which has no such id, rather than
priced as XRP. A slug in an answer resolves back to the ids that were
requested. A token query's platform must be one the platform table
lists (`ethereum`, `binance-smart-chain`, `polygon-pos`, `arbitrum-one`,
`optimistic-ethereum`, `base`, `avalanche`, `solana`, and `tron`), translated
to the slug CoinMarketCap gives that platform's coin; any other platform is
`NotFound`. That slug names a coin, not a platform, and CoinMarketCap gives
some platforms the same coin: `bnb` is the coin of both BNB Smart Chain and
opBNB, so by the slug alone an opBNB address passes for a
`binance-smart-chain` one. A coin missing from the
table whose slug differs comes back as CoinMarketCap not knowing it, or, if
another coin holds that slug, as the wrong coin, which is why the table was
checked by contract address where names were ambiguous (CoinMarketCap's
`bitway` is not CoinGecko's `bitway`).

To add an override, find the coin on coinmarketcap.com: its slug is the last
part of the page's address, `coinmarketcap.com/currencies/<slug>/`. Confirm it
is the same coin by symbol and, for a token, by contract address, then add the
CoinGecko id and that slug to `CoinSlugOverrides` in
`src/CryptoStuff.CoinMarketCap/CoinMarketCapIds.cs`, and the same pair to
`PinnedCoinOverrides` in `CoinMarketCapIdsTests`, which fails until both
lists match. A platform is added the same way, to `PlatformSlugs` and
`PinnedPlatforms`, using the slug of the coin CoinMarketCap names as the
platform's. The tests also reject two ids or two platforms sharing a slug, an
override that changes nothing, and an override slug that is also a listed id.

The API host also reads `Api:Key` (`Api__Key` in deployment), the key every
client must send in the `X-Api-Key` header; see [API key](#api-key). In this
README, "the CoinGecko API key" is always `CoinGecko:ApiKey`, the key the app
sends to CoinGecko; "the market data provider's API key" is the key the app
sends to whichever market data provider it calls, which today is always the
CoinGecko API key; and a bare "API key" is always `Api:Key`, the key clients
send to this API. Only the API host reads `Api:Key`: `AddCryptoStuff` does
not, so the CLI runs without it. Spaces or tabs around its value are ignored,
so clients send the key without them. If it is missing or blank, the API host
fails at start-up, before it serves anything or migrates the database, with:

```
API key is missing. Choose a secret key and set it locally by running 'dotnet user-secrets set Api:Key <your-api-key>' from the src/CryptoStuff.Api project directory, or set the Api__Key environment variable in deployment. Clients send it in the X-Api-Key header.
```

To resolve it locally:

```bash
cd src/CryptoStuff.Api
dotnet user-secrets set Api:Key <your-api-key>
```

The API host reads `PathBase` (in `appsettings.json`, or the `PathBase`
environment variable), empty by default. Behind a reverse proxy that serves
the API under a path and forwards requests with that prefix still on, set it
to the prefix, such as `/api`: the routes then answer beneath it (and still
at the root, for a request without the prefix) and every `Location` header on
a prefixed request names it. It must start with `/`, or the host fails to
start. A proxy that strips the prefix before forwarding needs no setting, but
its clients then get `Location` headers without the prefix.

## CoinMarketCap's answers

`CoinMarketCapCryptocurrencyService` serves `ICryptocurrencyService` within
what CoinMarketCap's free Basic plan allows, so its answers differ from
CoinGecko's in these ways:

- **Prices.** Basic converts to one currency per call, so a price or token
  price query makes one `quotes/latest` call per distinct currency, with every
  coin in each, where CoinGecko answers in one. A currency CoinMarketCap
  rejects leaves every coin without an amount in it, as a currency CoinGecko
  does not quote does. When every currency is rejected, one more call, in
  USD, settles which coins exist, so an unknown coin still fails the query.
  An answer rejecting the `slug` parameter also reads as an unknown coin.
- **Token prices.** CoinMarketCap quotes coins, not addresses, so each
  distinct address is first resolved to its coin with one `info` call, and
  must be deployed on the requested platform. An address CoinMarketCap
  rejects, or holds only on another platform, is `NotFound`.
- **Market chart.** `quotes/historical` takes CoinMarketCap's numeric id, so
  one `quotes/latest` call in USD looks it up first. The history runs back
  the requested days from now with one point a day, where CoinGecko gives
  several a day for ranges up to 90 days. Basic serves 365 days of history,
  so a query for more fails with `UpstreamUnavailable`, with no call made.
- **Coin detail.** A coin query names no currency, and each currency costs a
  call, so the market data holds a snapshot in each of
  `CoinMarketCapServiceOptions.DetailCurrencies` alone, `usd` by default,
  where CoinGecko's covers every currency it quotes. As with CoinGecko, a
  snapshot needs a price, a market cap and a volume, and the market data is
  null when no currency has all three. The image is CoinMarketCap's 64-pixel
  logo, and the symbol is lower-cased, as CoinGecko gives it.
- **Developer data.** CoinMarketCap has none, so the operation fails with
  `Unsupported`, its detail naming CoinMarketCap, with no call made.

## Error codes

`CryptoStuff.Core` reports a failed operation as a `ServiceErrorCode`. A failed
request to the market data provider, whether it got a failure response or no response at all,
maps to one of the first four codes; the next three are for requests on the
application's own stored data, mostly writes; and the last is for an operation
the selected market data provider cannot serve at all:

| Code | Meaning | Condition |
|---|---|---|
| `NotFound` | The requested resource does not exist | CoinGecko returned `404`; CoinMarketCap's answer lacks the coin or token asked for, or it rejected the coins' slugs or the token's address; or no persisted record has that id |
| `RateLimited` | The market data provider's rate limit was exceeded | CoinGecko or CoinMarketCap returned `429` |
| `RequestTimedOut` | The request to the market data provider did not complete in time | The request timed out |
| `UpstreamUnavailable` | The market data provider failed for any other reason | Any other non-success response, or no response at all (a refused connection, a DNS or TLS failure, a dropped connection); also a CoinMarketCap market chart for more days than its Basic plan serves, refused before any request |
| `Conflict` | The write conflicts with stored state | A duplicate name, or a lost concurrency race |
| `RuleViolated` | The request breaks a domain rule | For example a write that would oversell or name an unknown coin, or a valuation with an amount too large to compute |
| `PreconditionFailed` | The stored version no longer matches what the caller expected | A stale `If-Match` |
| `Unsupported` | The selected market data provider cannot serve the operation | Developer data asked of CoinMarketCap, which has none, with a detail naming it. CoinGecko serves every operation |

A failure may also carry a detail message. When it does, the API returns it as
the ProblemDetails `detail` and the CLI prints it, in place of the fixed text
for the code.

## Missing identifiers

`GetPricesAsync` and `GetTokenPricesAsync` check that every requested coin id
(or contract address) is present in CoinGecko's response. CoinGecko's
`simple/price` and `simple/token_price` endpoints silently omit unknown
identifiers instead of returning an error, so `CryptoStuff.CoinGecko` treats any
requested identifier absent from the response as a `NotFound` failure for the
whole request, rather than returning a partial result with a silent gap.
CoinMarketCap's `quotes/latest` omits an unknown slug the same way, since
quote requests send `skip_invalid=true`, and `CryptoStuff.CoinMarketCap`
treats it the same way.

Where the coins are ones the app already tracks (a portfolio's holdings, a
watchlist's coins, the coins armed alerts watch), losing one of them upstream
should not hide the others, so Core reads their prices through
`KnownCoinPrices.GetKnownCoinPricesAsync`. It makes the same single call, and
only when that call is `NotFound` asks for each coin on its own to find the
ones the market data provider still knows. A coin it does not know comes back with no
amounts, exactly as a coin not quoted in a requested currency already does.
An HTTP `404` from CoinGecko's price endpoint itself also reads as
`NotFound`, so it too leaves every coin without amounts. `GET /prices` and `cryptostuff price`
still fail on an unknown coin, since the caller named it.

The single-resource endpoints (`coins/{id}`, `coins/{id}/market_chart`,
`coins/{id}/history`) don't need this check: CoinGecko itself returns `404`
for an unknown coin id, which the error-code mapping above already turns into
`NotFound`. CoinMarketCap's coin detail and market chart look the coin up
through `quotes/latest` first, so an unknown coin is `NotFound` there too.

## Input validation

`CryptoStuff.Core` provides a small set of shared validation rules for the
CLI and API hosts to apply to user input before a request reaches the market data provider.
Each rule reports which field failed and what was expected of it.

| Rule | Applies to | What's required |
|---|---|---|
| Non-empty scalar | Coin id, platform, vs_currency code, developer-data date | The value must be present and not just whitespace |
| Non-empty collection | Coin id lists, contract address lists, vs_currency lists | At least one value must be given |
| Positive day count | The number of days of history to fetch | A whole number greater than zero |
| Well-formed date | The developer-data date | A real calendar date written as `dd-MM-yyyy` (e.g. `29-02-2024`) |
| Resource name | Watchlist and portfolio names | Present and not just whitespace, free of control characters (such as tabs, line breaks, or the escape that starts a terminal colour code), and at most 100 characters once trimmed |

Leading and trailing whitespace is trimmed before a name, or a coin id or
vs_currency kept with a watchlist, portfolio, transaction or alert, is stored,
so `"Main "` and `"Main"` are the same name and `" bitcoin"` is `bitcoin`.
Values stored by an earlier release keep any whitespace they were stored with.

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
configuration itself. `CryptoStuff.Composition`'s `AddCryptoStuff` extension
wires its duration from the `CoinGecko:CacheSeconds` setting, defaulting to
`300` (5 minutes) when unset. Its hits and misses are counted; see
[Upstream metrics](#upstream-metrics).

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
it doesn't read configuration itself. `AddCryptoStuff` wires it into the
CoinGecko `HttpClient` pipeline with 3 retry attempts and a 2-second constant
backoff, and sets that `HttpClient`'s own timeout to 30 seconds —
comfortably longer than three retries' worth of constant backoff plus
request time, so a plain rate-limited sequence with no `Retry-After` header
has room to fully exhaust its retries before the client times out. A
sufficiently long `Retry-After` value can still exceed that timeout; when it
does, the failure surfaces as `RequestTimedOut` rather than `RateLimited`
(see [Error codes](#error-codes)).

Each resend is counted; see [Upstream metrics](#upstream-metrics).

## Upstream metrics

The app counts its calls to the market data provider, the rate-limit retries, and the cache
lookups, so their volume can be measured from a running host. The counts are
`System.Diagnostics.Metrics` instruments only: there is no `/metrics` route,
and nothing is exported unless a tool listens. All three are counters on one
meter, `cryptostuff.upstream`, and each measurement adds 1:

| Instrument | Unit | Recorded by | Tags |
|---|---|---|---|
| `cryptostuff.upstream.requests` | `{request}` | `CoinGeckoClient`, once per call | `provider`, `endpoint`, `outcome` |
| `cryptostuff.upstream.retries` | `{retry}` | `RateLimitRetryHandler`, once per resend after a `429` | `provider` |
| `cryptostuff.cache.lookups` | `{lookup}` | `CachingCryptocurrencyService`, once per lookup of a cached operation | `operation`, `result` |

The tag values are a fixed set:

| Tag | Values |
|---|---|
| `provider` | `coingecko` |
| `endpoint` | `simple/price`, `simple/token_price/{platform}`, `coins/{id}/market_chart`, `coins/{id}`, `coins/{id}/history`, or `other` for a path matching none of them (the app calls none) |
| `operation` | `get_coin`, `get_developer_data` |
| `result` | `hit`, `miss` |

The `endpoint` value is the template, never the URL: no coin id, currency,
date, or query string reaches a tag. `outcome` says how the call ended:

| `outcome` | The call ended with |
|---|---|
| `success` | A success status and a body that was read |
| `not_found` | `404` |
| `rate_limited` | `429`, once the retries ran out |
| `client_error` | Any other `4xx`, such as a `401` or `403` from a rejected CoinGecko API key |
| `server_error` | A `5xx` |
| `other_status` | Any other non-success status, such as a redirect that was not followed |
| `timeout` | The `HttpClient`'s 30-second timeout, retry delays included |
| `no_response` | No response at all, such as a refused connection, a DNS or TLS failure, or a dropped connection |
| `cancelled` | The caller cancelling, such as an API client disconnecting; the call still throws |
| `exception` | A success body that was not valid JSON, or anything else unexpected; the call still throws |

A request is one logical call by the client, however many times it was sent:
a call resent after a `429` is one request, and each resend is one retry. The
number of HTTP sends to the market data provider is therefore requests plus retries, less
any request that ended `cancelled` before its first send. The retry counter
has no `endpoint` tag, so sends can be totalled per provider but not per
endpoint. A resend cancelled during its delay is never sent, so it is not
counted.

A cache lookup is a call to `GetCoinAsync` or `GetDeveloperDataAsync` on the
decorator; the other three operations are never looked up, so they record
nothing. A miss is an absent key, an expired entry, or a key whose last
result was a failure (which is never cached), and each miss is passed on to
the market data provider as one request.

To read the counters from a running API host, attach `dotnet-counters`, a
.NET diagnostic tool installed separately (`dotnet tool install --global
dotnet-counters`):

```bash
dotnet-counters monitor --name CryptoStuff.Api --counters cryptostuff.upstream
```

`--name` matches the host's process name; `--process-id` names one by id
instead. The counts live in the process and start from zero each time it
starts. The CLI records them too, but a single command usually exits before a
tool can attach.

## Registration

`CryptoStuff.Composition` provides `AddCryptoStuff(IServiceCollection,
IConfiguration)`, the single extension method that registers everything
above: the CoinGecko-backed `ICryptocurrencyService`, wrapped in the
[negative-price guard](#unpriced-coins) and then the caching decorator, wired to the CoinGecko `HttpClient` and its rate-limit retry
handler. It also registers the persistence services over the
[shared database](#data-storage): the EF Core context, and the watchlist, portfolio and
alert repositories and services, and the alert evaluator. The clock the portfolio and alert rules read is
`TimeProvider.System`, registered as a singleton unless a `TimeProvider` is
already registered, so a test can substitute its own first. Both hosts
reference `CryptoStuff.Composition` to call it.

`ICryptocurrencyService` is a singleton, so its cache spans every request.
The persistence services are scoped: the API gets a fresh set per request,
and the CLI resolves its services from one scope per invocation.

If `CoinGecko:ApiKey` is missing or blank, `AddCryptoStuff` throws
immediately — before it returns, so before whichever host calls it starts
serving anything — with:

```
CoinGecko API key is missing. Set it locally by running 'dotnet user-secrets set CoinGecko:ApiKey <your-api-key>' from the host project directory, or set the CoinGecko__ApiKey environment variable in deployment.
```

To resolve it locally, from either host project directory:

```bash
dotnet user-secrets set CoinGecko:ApiKey <your-api-key>
```

`AddCryptoStuff` likewise fails immediately when the database connection
string cannot be resolved — see [Data storage](#data-storage).

## Data storage

Watchlists, portfolios, and price alerts are stored in a single SQLite database file that both hosts share:
whatever the CLI writes, the API reads, and the other way round, wherever
either is launched from. By default the file is `cryptostuff/cryptostuff.db`
under the per-user local application-data folder:

| OS | Default database file |
|---|---|
| Windows | `%LOCALAPPDATA%\cryptostuff\cryptostuff.db` (e.g. `C:\Users\<you>\AppData\Local\cryptostuff\cryptostuff.db`) |
| macOS | `~/Library/Application Support/cryptostuff/cryptostuff.db` |
| Linux | `$XDG_DATA_HOME/cryptostuff/cryptostuff.db`, which is `~/.local/share/cryptostuff/cryptostuff.db` when `XDG_DATA_HOME` is unset |

To use another file, set the `ConnectionStrings:CryptoStuff` setting to a
SQLite connection string — as a user secret locally, or as the
`ConnectionStrings__CryptoStuff` environment variable in deployment:

```bash
dotnet user-secrets set ConnectionStrings:CryptoStuff "Data Source=C:\data\cryptostuff.db"
```

```bash
dotnet user-secrets set ConnectionStrings:CryptoStuff "Data Source=/var/lib/cryptostuff/cryptostuff.db"
```

The first is for Windows, the second for macOS and Linux.

User secrets belong to one host project, so set the override for both
`CryptoStuff.Cli` and `CryptoStuff.Api` — or use the environment variable —
if they are to keep sharing a file.

The data source must be an absolute file path, so the two hosts open the same
file wherever they are launched from. On Windows that means one with a drive
(`C:\data\cryptostuff.db`) or a UNC share (`\\server\share\cryptostuff.db`);
a path starting at a single `\` or `/` is relative to the current drive, so it
is refused too. A relative path would resolve against
each host's working directory, and an in-memory or temporary database is gone
with its connection, so these are refused, as are `file:` URIs and
`|DataDirectory|`. A value that is not a valid SQLite connection string, or
that names no absolute file, stops either host before it does anything, with
one of:

```
ConnectionStrings:CryptoStuff is not a valid SQLite connection string. Check that every keyword is one SQLite knows, that each value suits its keyword, and that any value holding ';' or '=' is quoted.
ConnectionStrings:CryptoStuff must name the database file by an absolute path, such as 'Data Source=C:\data\cryptostuff.db' on Windows or 'Data Source=/var/lib/cryptostuff/cryptostuff.db' elsewhere, so the CLI and the API open the same file wherever they are launched from. Relative paths, in-memory and temporary databases, file: URIs and |DataDirectory| are not supported.
```

Neither message repeats any part of the value, which may hold a password.
If the setting is absent and the OS reports no local application-data folder
at all, both hosts stop with:

```
No per-user local application-data folder is available for the default database file. Set the ConnectionStrings:CryptoStuff setting (the ConnectionStrings__CryptoStuff environment variable in deployment) to a SQLite connection string naming an absolute file, such as 'Data Source=C:\data\cryptostuff.db' on Windows or 'Data Source=/var/lib/cryptostuff/cryptostuff.db' elsewhere.
```

A portfolio and its transactions live in two tables, `Portfolios` and
`PortfolioTransactions`, and deleting a portfolio deletes its transactions.
Decimal amounts are stored as exact text and timestamps as UTC, so nothing is
rounded on the way to the file, and ids are never reused after a delete.
Neither are sequences: a portfolio remembers the highest sequence it has
handed out, so removing its newest transaction does not free that number for
the next one, and a unique index on the portfolio and sequence holds every
writer to the same rule. An earlier release writing to an upgraded file does
not keep that record, so a newest transaction it removes can have its number
handed out once more, but the next transaction still always follows every one
the portfolio holds. Upgrading from a release that stored only watchlists
adds the two tables and leaves the watchlists as they were. Upgrading from a
release before sequences were remembered numbers each portfolio's next
transaction after the highest sequence it holds; a newest transaction removed
before the upgrade left no trace, so its number can still be handed out once.

A price alert lives in one table, `PriceAlerts`. Its direction and state are
stored as the same lowercase text tokens the hosts use (`above`, `below`,
`armed`, `triggered`), its threshold and triggered price as exact text, and
when it triggered as UTC, empty while the alert is armed. Its version is a
concurrency token, so a write based on a stale read is rejected as a conflict
rather than overwriting the newer change, and the repository can list just the
armed alerts for evaluation, which an index on the state finds without reading
the triggered ones. Upgrading from a release without alerts adds the table and
leaves the watchlists and portfolios as they were.

There is no setup step. Both hosts create the file and its folder when they
are missing and apply any pending [migrations](#database-migrations) at start
up — the API before it starts listening, the CLI before it runs a command that
uses the database (the `watchlist`, `portfolio` and `alert` commands) — so whichever host runs first
after an upgrade brings the schema up to date. The CLI's market-data-only
commands (`price`, `token`, `history`, `coin`, `developer`) never open the
database, so they skip migration and start as fast as they always did. If the
database cannot be opened or migrated for any reason (an unwritable folder, a
file that is not a SQLite database, or a migration that cannot be applied),
the CLI prints `Error: the database could not be opened or migrated. …` and
exits with code `1`, never a stack trace, and the API fails to start with the
underlying exception. The API migrates as the first step of its own start-up,
so Ctrl+C while it is still migrating cancels the migration, and the API
stops without serving anything; the CLI exits with code `130` the same way.

After migrating, both hosts also check that every stored token (an alert's
direction and state, a portfolio's cost-basis method, a transaction's kind) is
one this version knows. A row written by a newer version could hold one it
does not, and reading that row would fail every list and every alert
evaluation that touched it, so the check refuses to go on instead: the CLI
prints `Error: the database holds data this version cannot read. …`, naming
the table, column and value, and exits with code `1`, and the API fails to
start with the same message.

The CLI, the API and the API's background evaluation can all write to the
file at once. SQLite lets one write through at a time, and a writer waits up to
30 seconds for the lock. If another writer holds it longer than that, nothing
is written: the CLI prints `Error: The database is busy with another write; try
again shortly.` and exits with code `1`, the API answers
[`503`](#rest-api) with a `Retry-After` header, and the background evaluation
logs the failure and tries again on its next tick.

Watchlist and portfolio names are unique ignoring case for every letter, not
just the ASCII ones, so `Ação` and `AÇÃO` are the same name. SQLite's own
`NOCASE` folds only ASCII letters, so the name columns use a collation of the
application's, `UNICODE_NOCASE`, which both hosts register on every connection
they open. Other tools, such as the `sqlite3` shell, do not know it: they can
read the rows with a plain `SELECT`, but a statement that compares or sorts by
name, or that goes through the unique index on it (adding, renaming or
deleting a watchlist or portfolio, even by id, or counting them), fails with
`no such collation sequence: UNICODE_NOCASE`. Upgrading from an earlier
release rebuilds both tables on the new collation and keeps every row. A
database that already holds two names differing only in the case of a
non-ASCII letter cannot be upgraded: the migration checks for them before it
changes anything and stops, and the hosts report the database could not be
migrated, as above. The earlier release still opens that database, so rename
one of the two with it, then upgrade again.

A write also notices when another writer deleted the record it is changing
after reading it: an update, a delete, a coin or transaction change, or a
re-arm of a record that is gone by the time it saves fails as not found, the
same way it would had the record been gone when it was read, rather than as a
conflict.

## Cost basis

A portfolio's cost-basis method decides what the units a sell takes had
cost, and so how much of the sale is profit. Core reduces each coin's
transactions, in ledger order (by when each happened, then by the order they
were recorded), to a position: the quantity still held, the cost basis of
those units, and the profit and loss the sells have realized. A buy's fee adds
to what its units cost, and a sell's fee comes off what the sale brought in.

- **Average cost** (`average-cost`): every unit held costs the average of what
  the units held cost. A sell releases its share of the cost basis (cost basis
  × units sold ÷ units held), so the average of the units left is unchanged.
  Selling everything releases the whole cost basis, so it returns to exactly
  zero with the quantity, and a later buy starts a fresh average.
- **FIFO** (`fifo`): each buy is a lot, and a sell uses up the oldest lots
  first. A lot the sell takes only part of gives up a pro-rata share of its
  cost, fee included, and keeps the rest; a lot used up entirely gives up all
  of the cost it has left.
- **LIFO** (`lifo`): each buy is a lot, and a sell uses up the newest lots
  first: the latest in ledger order among the lots held when the sell is
  replayed, so a buy back-dated before a sell can change which lots it took.
  A lot gives up its cost as under FIFO.
- **HIFO** (`hifo`): each buy is a lot, and a sell uses up the lots that cost
  the most per unit first. A lot's cost per unit is what it cost as bought,
  fee included, over its quantity ((quantity × unit price + fee) ÷ quantity),
  and it keeps that rank however much of it has been sold; lots that cost
  exactly the same per unit go oldest first. A lot gives up its cost as under
  FIFO.

The four diverge as soon as units bought at different prices are sold. Take
one ledger:

| Day | Transaction | Quantity | Unit price | Fee |
|-----|-------------|---------:|-----------:|----:|
| 1   | buy         | 1        | 10,000     | 10  |
| 2   | buy         | 1        | 20,000     | 20  |
| 3   | buy         | 1        | 12,000     | 12  |
| 4   | sell        | 1        | 30,000     | 30  |

With their fees the three units cost 10,010, 20,020 and 12,012, 42,042 in
all, and the sale brings in 30,000 − 30 = 29,970. Each method has the sale
take a different cost, and every one leaves two units held:

| Method       | Cost of the unit sold          | Realized profit and loss | Cost basis held          |
|--------------|--------------------------------|--------------------------|--------------------------|
| Average cost | 42,042 ÷ 3 = 14,014            | 29,970 − 14,014 = 15,956 | 42,042 − 14,014 = 28,028 |
| FIFO         | the oldest, day 1: 10,010      | 29,970 − 10,010 = 19,960 | 20,020 + 12,012 = 32,032 |
| LIFO         | the newest, day 3: 12,012      | 29,970 − 12,012 = 17,958 | 10,010 + 20,020 = 30,030 |
| HIFO         | the dearest, day 2: 20,020     | 29,970 − 20,020 = 9,950  | 10,010 + 12,012 = 22,022 |

All four account for the same 42,042 paid, as cost released by the sale plus
cost basis still held; they differ only in which cost the sale takes. Here
FIFO realizes the most profit now, because the oldest unit was the cheapest,
and HIFO the least, keeping the two cheapest units so that the most profit is
left to come.

Every figure is an exact `decimal`, and Core rounds nothing to currency
places. A figure rounds only where it needs more than the 28 or 29
significant digits a `decimal` holds. One such step is a share that does not
divide evenly, such as a third of an average cost. A share (cost × units sold
÷ units held) is worked out exactly and rounded once, half to even, even when
cost × units sold alone would be too large for a `decimal`; so a share never
exceeds the cost it comes from, and the cost basis left on the units still
held never goes below zero. The others are the plain `decimal` steps: a
quantity × unit price, or a sum (a cost basis, the cost a sell releases from
several lots, a realized profit and loss) that needs more digits rounds there,
as `7e28` + `0.6` gives `7e28` + 1, so the order of the additions can change
the last digit. The lot methods add up the cost of the lots still held in the
order they were bought, whichever lots they sell first, so with nothing sold
every method reports the same cost basis. HIFO's ranking divides nothing: it
compares two lots' costs per unit exactly, so lots whose costs per unit differ
only past a `decimal`'s digits still rank in order, and no cost per unit is
too large to compare.

No figure can go beyond the largest `decimal`, about `7.9e28`. Recording,
replacing or deleting a transaction is refused when it would take any coin's
holding, cost basis, proceeds or profit past it, under any of the four
methods, since a portfolio can switch methods at any time; the `detail` names
the coin. A transaction is refused the same way when it would leave a coin's
holding needing more significant digits than a `decimal` holds, written to as
many decimal places as the finest of its quantities needs (trailing zeros do
not count, so `1.0` needs none): `7e28` plus `0.6`, or `10` plus
`0.0000000000000000000000000006`. Such a holding would round, and a sell of
the rounded figure could take more than the FIFO, LIFO or HIFO lots hold. So
a ledger whose coins all compute keeps doing so. A ledger stored before this
check may already hold a coin past the limit: it cannot be valued, but that
coin's transactions can be edited down or deleted one at a time, even while
others still keep it past the limit, and the other coins can still be traded,
as long as no change takes a coin past the limit that was within it.

## Valuation

Valuing a portfolio joins the [cost-basis](#cost-basis) positions with live
prices. Core makes one price lookup, for the coins the portfolio still holds,
priced in the portfolio's own currency (through
[`KnownCoinPrices`](#missing-identifiers), so it asks coin by coin only when
the market data provider reports one of them unknown). A portfolio that holds nothing,
whether it is empty or every position is closed, makes no upstream call. Each
coin in the valuation reports:

| Figure | Meaning |
|---|---|
| Quantity | The units still held |
| Market value | Quantity × the live price of one unit; null for a coin left [unpriced](#unpriced-coins) |
| Allocation | The coin's share of the total market value, as a percentage from 0 to 100: a coin worth 62.5% of the total reports `62.5`. 0 for a coin worth nothing; null for a coin left unpriced |
| Cost basis | What the units still held cost, fees on their buys included, under the portfolio's method |
| Unrealized profit and loss | Market value − cost basis: the gain or loss that selling every unit held at the live price would realize. Negative while the price is below cost. It is a paper figure and moves with the price; null for a coin left unpriced |
| Realized profit and loss | What the coin's sells have already made: proceeds less sell fees, less the cost the method attributes to the units sold. Fixed once made, and negative for a loss |

The totals add each figure across the priced coins, closed coins included.
Market value − cost basis equals the total unrealized profit and loss, and
total realized profit and loss is every priced coin's realized profit and loss.
Adding the two profit-and-loss totals gives the portfolio's overall result so
far. The totals have no allocation: the priced coins' allocations add up to
100, exactly or within decimal rounding (three coins worth the same each report
33.333…, unrounded, so together they come a hair short of 100). When the total
market value is 0, because nothing is held, every position is closed, or every
coin held is priced at 0, every priced coin's allocation is 0.

A coin whose position is fully closed stays in the list, with a quantity,
market value, allocation, cost basis and unrealized profit and loss of zero and only its
realized profit and loss. It needs no price, so it is not requested, and
keeping it means the priced rows always add up to the totals. Coins are
listed in order of coin id.

Every figure is an exact `decimal`: Core rounds nothing, so a host that shows
currency places rounds when it formats. The API's responses and the CLI's
`--json` output keep every figure exact. `portfolio value`'s text rounds the
market values, cost bases and profits and losses, which come out of
multiplying and dividing and can run to 28 decimal places, to at most 8, the
places a bitcoin divides into: a midpoint rounds away from zero, trailing
zeros are dropped, and a figure that rounds to nothing prints as `0`, so a cost
basis of 2200 / 3 prints as `733.33333333`. It rounds each coin's allocation
the same way to at most 2 places and adds `%`: a third prints as
`allocation 33.33%`, half as `allocation 50%`. Quantities, and the prices and
fees you entered, are always printed exactly. A live price times the quantity held,
or a total across coins, can still go past the largest `decimal`; the
valuation then fails with `RuleViolated` rather than giving a wrong figure.
Working out the allocations never does: a coin's share of the total is taken
before it is scaled to 100.

### Unpriced coins

A coin still held that the market data provider gives no price for, because it no longer
knows the coin or no longer quotes it in the portfolio's currency, does not
fail the valuation. Its row keeps its quantity, cost basis and realized profit
and loss, but its market value, allocation and unrealized profit and loss are
null, and its id is listed in `unpricedCoins`. The totals leave the whole coin
out, its cost basis and realized profit and loss too, so they still add up
across the rows they cover, and the other coins' allocations are shares of
that priced total, so they still add up to 100. `portfolio value` prints
`unpriced` for the missing figures and names the coins left out of the total. Any upstream failure, such as
`RateLimited`, still fails the valuation with its code.

A price below zero counts as no price. Core drops it from the provider's
answer before anything is valued, so the coin is unpriced in that currency,
the same as when the provider has no quote for it. A price of zero is still a
price: the coin is valued at zero and stays in the totals.

## Alert evaluation

An alert only changes state when something evaluates it. One evaluator,
`IAlertEvaluator` in Core, does the work for every trigger, so the CLI and the
API behave identically:

| Trigger | When it runs |
|---|---|
| `alert check` (CLI) | Once, when the command is run. The CLI exits after each command, so it never evaluates in the background |
| `POST /alerts/check` (API) | Once per request, at most `Alerts:ChecksPerMinute` requests a minute (6 by default); see [Check rate limit](#check-rate-limit) |
| The API's background evaluation | Periodically for as long as the API host is running, every `Alerts:EvaluationSeconds` seconds (60 by default), each tick on its own |

Nothing is notified outwardly: a triggered alert is
recorded in its own state (`triggered`, with when and at what price) and
reported by the evaluation that triggered it: printed by `alert check`,
returned by `POST /alerts/check`, and logged by the background evaluation.

One evaluation:

1. Loads the armed alerts. With none, it makes no upstream call and reports
   nothing triggered.
2. Makes one price lookup for every coin and currency the armed alerts need,
   however many alerts there are. Only if the market data provider reports one of those coins
   as unknown does it then look up each coin on its own, in those same
   currencies, to tell the coins the provider still knows from the ones it does
   not (through [`KnownCoinPrices`](#missing-identifiers)).
3. Triggers each alert whose price meets it, inclusively: at or above the
   threshold for `above`, at or below it for `below`. An alert whose price has
   not reached it stays armed and is checked again by the next evaluation.
   A re-armed alert is the exception: it triggers only on a fresh crossing
   (see below). Every alert triggered by one evaluation carries the same
   time, read from the clock once the prices arrived.
4. Reports each alert it triggered: its id, coin, currency, direction,
   threshold, the price that met it, and when. It also reports each alert it
   could not price, by id, coin and currency.

A triggered alert is not evaluated again until it is
re-armed or edited.

**Re-armed alerts wait for a fresh crossing.** Re-arming does not let a price
that never moved back trigger the alert again: a re-armed alert stays
`armed` but is marked `awaitingReset` until an evaluation sees a price that
does not meet it, back below the threshold for `above` or back above it for
`below`; that evaluation saves the reset without triggering, and a later one
can trigger the alert as usual. Editing an alert's condition watches the new
one from scratch, as a new alert would, so an edited alert is never left
awaiting a reset. Alerts re-armed before this rule existed carry no mark and
can trigger on the next evaluation that finds their price past the threshold.

**When something goes wrong.** An upstream failure, such as `RateLimited`,
`RequestTimedOut`, or `UpstreamUnavailable` when the market data provider cannot be reached
at all, fails the evaluation with that code and leaves every alert
armed, so the next evaluation simply tries again; that includes a failure while
looking coins up one at a time. An alert the provider gives no price for, because
it no longer knows the coin or no longer quotes it in the alert's currency,
does not hold up the others: the evaluation checks every alert it could price
and reports the rest as unpriced. A price below zero counts as no price here
too, so it is reported unpriced rather than triggering a `below` alert or
resetting a re-armed `above` one; a price of zero is checked like any other.
An unpriced alert stays armed and is tried
again by every evaluation until the provider prices it or the alert is edited or
deleted. A `404` from CoinGecko's price endpoint itself, such as from a wrong
base address, cannot be told apart from an unknown coin, so it reports every
alert unpriced rather than failing the evaluation.

**Two writers.** Evaluations can overlap another write to the same alert,
such as another evaluation (the CLI beside the API, or a doubled tick), an
edit, a re-arm, or a delete. An evaluation saves all of its triggers, and
every reset of a re-armed alert, together, so it writes all of them or none.
The alert's version turns a lost race into a failure rather than an
overwrite: if another writer changed or deleted any of the evaluated alerts
after the evaluation read it, the evaluation fails with `Conflict`, triggers
nothing, and leaves every alert as it was, so the next evaluation checks them
all again. The same holds when the database stays busy past SQLite's wait or
the evaluation is cancelled while saving: nothing was triggered, so an
evaluation that failed never leaves behind an alert it triggered but did not
report.

### Background evaluation

While the API host runs, `AlertEvaluationBackgroundService` evaluates the armed
alerts every `Alerts:EvaluationSeconds` seconds. The first evaluation runs one
interval after start-up, not at start-up; each one gets a fresh scope, so a
fresh database context, and one ends before the next can start. The
interval is set in `appsettings.json`, or as the `Alerts__EvaluationSeconds`
environment variable:

```json
{ "Alerts": { "EvaluationSeconds": 60 } }
```

It must be a whole number of seconds from `1` to `4294967` (about 49.7 days,
the longest period .NET's `PeriodicTimer` accepts); absent or blank, it is
`60`. Anything else stops the API at start-up, before it serves a request, with
a message naming the value, such as:

```
Alerts:EvaluationSeconds is set to '1.5', which is not a whole number of seconds from 1 to 4294967. Set it to the number of seconds between background alert evaluations, or remove it to evaluate every 60 seconds.
```

Only the API reads the setting (through `AddAlertEvaluationSchedule`); the CLI
never evaluates in the background, so a bad value there does not stop it.

Each alert a background evaluation triggers is logged at `Information`, giving
the alert's id, its coin, the price that met it, and its condition:

```
Alert 3 triggered: bitcoin is at 52000.5 usd, meeting its condition above 50000.
```

The alert itself is then `triggered` in the database, exactly as if
`POST /alerts/check` had triggered it, so `GET /alerts/3` shows when and at
what price. A tick that fails costs only that tick, and the next one tries
again: an evaluation that returns an [error code](#error-codes) is logged at
`Warning` with its code and detail
(`Alert evaluation failed with RateLimited: …`), and one that throws, such as on a database error, is logged at `Error`
with the exception. An evaluation saves its triggers together, so one that
returns an error code triggered nothing and one that throws saved all of its
triggers or none; either way the next one checks every alert still armed.

Alerts an evaluation could not price are logged at `Warning`, but only when
they differ from the ones the last successful evaluation could not price, so a
delisted coin is logged once rather than every tick:

```
Alerts 5 (ghostcoin in usd), 9 (bitcoin in eur) could not be priced, so they stay armed. The market data provider gave no price for them; this is logged again only when the alerts it cannot price change.
```

When none is left unpriced, that is logged once at `Information` as
`Every armed alert was priced again.` A failed tick does not count as a
change.
Stopping the host cancels an evaluation in progress, and that is not logged as
a failure.

### Check rate limit

Each `POST /alerts/check` asks the market data provider for prices, and the
rate limit of the market data provider's API key is shared with the
background evaluation, the other price routes and the CLI. So the API allows at most `Alerts:ChecksPerMinute` checks
a minute, counted for every caller together in one fixed window that opens
anew each minute. Only checks sent with the right [API key](#api-key) count:
one without it is a `401` and uses none of the window. A check over the limit
asks the provider nothing and is answered `429` with a ProblemDetails body and a
`Retry-After: 60` header: the window's whole length, however much of it is
left, so a client that waits that long always finds a new window open:

```json
{ "status": 429, "title": "Too Many Requests", "detail": "Alert checks are limited to 6 per minute; try again after the wait given in Retry-After." }
```

The limit covers that route alone: the background evaluation, which does not go
through it, and every other route are never held back by it. It is set in
`appsettings.json`, or as the `Alerts__ChecksPerMinute` environment variable:

```json
{ "Alerts": { "ChecksPerMinute": 6 } }
```

It must be a whole number from `1` to `2147483647`; absent or blank, it is `6`.
Anything else stops the API at start-up with a message naming the value, such
as:

```
Alerts:ChecksPerMinute is set to '0', which is not a whole number from 1 to 2147483647. Set it to how many POST /alerts/check requests a minute to allow, or remove it to allow 6.
```

The CLI's `alert check` is not limited: it runs once per command.

## Command-line interface

`CryptoStuff.Cli` is a console front end over the same five operations as
`ICryptocurrencyService`, plus the `watchlist`, `portfolio` and `alert` commands
that manage stored watchlists, portfolios and price alerts. Run it with `dotnet run --project src/CryptoStuff.Cli
--`, followed by a command and its arguments:

```bash
dotnet run --project src/CryptoStuff.Cli -- price bitcoin usd
```

| Command | Arguments | Example |
|---|---|---|
| `price` | `<coins> <currencies>` — comma-separated coin ids and vs_currency codes | `cryptostuff price bitcoin,ethereum usd,eur` |
| `token` | `<platform> <addresses> <currencies>` — a platform, comma-separated contract addresses, and comma-separated vs_currency codes | `cryptostuff token ethereum 0xaaa...,0xbbb... usd` |
| `history` | `<coin> <currency> <days>` — a coin id, a vs_currency code, and a positive whole number of days | `cryptostuff history bitcoin usd 30` |
| `coin` | `<id>` — a coin id | `cryptostuff coin bitcoin` |
| `developer` | `<id> <date>` — a coin id and a date in `dd-MM-yyyy` | `cryptostuff developer bitcoin 29-02-2024` |
| `watchlist list` | — lists every watchlist, with its id, name, and coins | `cryptostuff watchlist list` |
| `watchlist create` | `<name>` — a new, empty watchlist; the name follows the [resource name](#input-validation) rule and is unique, ignoring case | `cryptostuff watchlist create majors` |
| `watchlist show` | `<id>` — a watchlist's id, name, and coins | `cryptostuff watchlist show 1` |
| `watchlist rename` | `<id> <name>` — a new name for a watchlist | `cryptostuff watchlist rename 1 blue-chips` |
| `watchlist delete` | `<id>` — deletes a watchlist | `cryptostuff watchlist delete 1` |
| `watchlist add` | `<id> <coin>` — appends a coin id that the market data provider knows; adding a coin already present changes nothing | `cryptostuff watchlist add 1 bitcoin` |
| `watchlist remove` | `<id> <coin>` — removes a coin id; removing one that is absent changes nothing | `cryptostuff watchlist remove 1 bitcoin` |
| `watchlist prices` | `<id> <currency>` — live prices of the watchlist's coins in a vs_currency; a coin the market data provider gives no price for prints `no price` | `cryptostuff watchlist prices 1 usd` |
| `portfolio list` | — lists every portfolio, with its id, name, currency, method, and transaction count | `cryptostuff portfolio list` |
| `portfolio create` | `<name> <currency> <method>` — a new, empty portfolio priced in a vs_currency that never changes; `<method>` is `average-cost`, `fifo`, `lifo` or `hifo`; the name follows the [resource name](#input-validation) rule and is unique, ignoring case | `cryptostuff portfolio create main usd fifo` |
| `portfolio show` | `<id>` — a portfolio's id, name, currency, method, version, and transactions | `cryptostuff portfolio show 1` |
| `portfolio update` | `<id> <name> <method>` — replaces the portfolio's name and cost-basis method | `cryptostuff portfolio update 1 retirement average-cost` |
| `portfolio delete` | `<id>` — deletes a portfolio with all its transactions | `cryptostuff portfolio delete 1` |
| `portfolio value` | `<id>` — the portfolio's holdings at live prices, with each coin's allocation; see [Valuation](#valuation) | `cryptostuff portfolio value 1` |
| `portfolio tx add` | `<id> <kind> <coin> <qty> <price> <when> [--fee <fee>]` — records a buy or sell (`<kind>` is `buy` or `sell`); a buy of a coin new to the portfolio must be one the market data provider knows | `cryptostuff portfolio tx add 1 buy bitcoin 0.5 40000 2024-01-31T14:30:00Z --fee 12.5` |
| `portfolio tx edit` | `<id> <txid> <kind> <coin> <qty> <price> <when> [--fee <fee>]` — replaces every detail of a transaction, so an omitted `--fee` becomes `0` | `cryptostuff portfolio tx edit 1 3 sell bitcoin 0.25 42000 2024-02-15T09:00:00Z` |
| `portfolio tx delete` | `<id> <txid>` — deletes a transaction | `cryptostuff portfolio tx delete 1 3` |
| `alert list` | — lists every alert, with its id, coin, direction, threshold, currency, and state | `cryptostuff alert list` |
| `alert create` | `<coin> <currency> <direction> <threshold>` — a new, armed alert on a coin priced in a vs_currency, both of which the market data provider must know and neither of which changes later; `<direction>` is `above` or `below`; the threshold is greater than zero | `cryptostuff alert create bitcoin usd above 50000` |
| `alert show` | `<id>` — an alert's id, coin, currency, direction, threshold, state, when and at what price it triggered, and version | `cryptostuff alert show 1` |
| `alert update` | `<id> <direction> <threshold>` — replaces the alert's direction and threshold, which re-arms it whether or not it had triggered | `cryptostuff alert update 1 below 42000` |
| `alert delete` | `<id>` — deletes an alert | `cryptostuff alert delete 1` |
| `alert rearm` | `<id>` — moves a triggered alert back to armed, where it waits for its price to move back past the threshold before it can trigger again; re-arming an alert that is already armed leaves it as it is | `cryptostuff alert rearm 1` |
| `alert check` | `[--exit-code]` — evaluates the armed alerts once and prints the ones that triggered, or `Nothing triggered.`, then any it could not price; with `--exit-code` it exits `2` when it triggered an alert; see [Alert evaluation](#alert-evaluation) | `cryptostuff alert check --exit-code` |

Each command applies the applicable [input validation](#input-validation)
rules above to its arguments before calling the market data provider, and reports the first
violation it finds. The non-empty-collection rule doesn't apply here: a
comma-separated list argument can never be truly empty, only contain a blank
entry (e.g. `bitcoin,,ethereum`), which is reported as such.

The `watchlist` commands take a watchlist's id as a positive whole number, which
`create` prints and `list` shows; anything else is reported as a validation
failure. Text output prints any control character in a stored name as its
`\uXXXX` escape (`\u001b` for the escape that starts a terminal colour code),
so a name written before control characters were refused, or by another tool,
cannot recolour or clear the terminal; `--json` output escapes them as JSON
does. They read and write the shared [database](#data-storage), so these
and the `portfolio` and `alert` commands are the only commands that open it (and
migrate it first). A failure the service
reports for them — an unknown id, a duplicate name, a coin the market data provider doesn't
know — is printed as `Error: …` with the service's own message, and exits `1`.
`delete` prints `Deleted watchlist <id>.`; with `--json` it prints
`{"Id": <id>, "Deleted": true}`, indented.

The `portfolio` commands work the same way: ids are positive whole numbers
(`create` prints the portfolio's, and its listing shows each transaction's),
they open the shared [database](#data-storage), and a failure the service
reports is printed as `Error: …` and exits `1`. The ledger rules reach you that
way: selling more of a coin than the portfolio held at that moment, or editing
or deleting a transaction a later sell depends on, is refused with a message
naming the coin and date, and so is a transaction dated more than five
minutes after the current time (the allowance for a clock running a little
ahead of the machine that checks it; the timestamp is stored as given). `show`,
`create`, `update` and the three `tx` commands print the resulting portfolio;
`delete` prints `Deleted portfolio <id>.`, or `{"Id": <id>, "Deleted": true}`
with `--json`.

The `alert` commands follow the same rules. `create`, `show`, `update` and
`rearm` print the resulting alert, text or `--json` alike, with `above` and
`below`, `armed` and `triggered` written as the same lowercase tokens Core
parses. A re-armed alert that has not yet seen its price move back shows as
`armed (waiting for the price to move back)` in `show`, `(armed, waiting for
the price to move back)` in `list`, and with `"AwaitingReset": true` in
`--json`. `delete` prints `Deleted alert <id>.`, or `{"Id": <id>, "Deleted": true}`
with `--json`. A threshold is read like a portfolio quantity (a point for the
decimal mark, an optional exponent, and no thousands separators: `0.5` and
`1.5e3` are accepted, `0,5` is not) and must be greater than zero, so `0` and `-1` are rejected as
`Threshold must be greater than zero.` Core refuses a coin or currency
the market data provider doesn't know, or that has no price in that currency, when the alert is
created, in a message printed as `Error: …` with exit `1`. `rearm` on an alert
that is already armed changes nothing and prints it as it is, waiting for its
price to move back or not, so running it twice does no more than running it
once. Like every stored write, the CLI sends no expected version, so an
`update`, `delete` or `rearm` never fails on a stale one.

`alert check` runs the one [evaluator](#alert-evaluation) once and prints each
alert it triggered, one line apiece (id, coin, direction, threshold, the price
that met it, and when), under a count. Any alert it could not price follows,
one line apiece (id, coin and currency) under `Could not price N alerts, left
armed:`. With `--json` it prints `{"Triggered": [...], "Unpriced": [...]}`,
each an empty list when there is nothing to report. Nothing triggering, and an
alert that could not be priced, are not failures, so `check` exits `0` then,
and by default it exits `0` when alerts triggered too. With `--exit-code` it
exits `2` when it triggered at least one alert, after printing them, so a
script can act on a trigger without reading the output; it still exits `0`
when nothing triggered. Only an evaluation that failed, such as a market data provider
error, exits `1`, with or without the flag, and changes no alert. **Continuous evaluation
happens only in the API host**: the CLI exits after each command, so alerts are
evaluated only when `alert check` is run, while a running API host evaluates them
on its own timer.

Arguments stay positional; the options besides `--json` are `--fee`, which
only `tx add` and `tx edit` take (anywhere on the command line, followed by its
value) and which defaults to `0`, and `--exit-code`, which only `alert check`
takes. Either one given to any other command prints the usage and exits `1`. Numbers are read the same way on every
machine, whatever its regional settings: a decimal with a point for the
decimal mark, an optional exponent as a JSON number allows (`5e-1`, `1.5E+3`),
and no thousands separators. `0.5` and `5e-1` are accepted; `0,5` and `1,000`
are rejected as `Quantity must be a number.` (or `Price`, or `Fee`) rather than
read as something else. A number beyond the largest a decimal holds,
79228162514264337593543950335, is rejected as `Quantity is too large; …` (and
one below its negative as `… is too small; …`). A quantity must be
greater than zero, a price and a fee zero or greater. `<when>` is an ISO 8601
timestamp with a UTC offset, `2024-01-31T14:30:00Z` or
`2024-01-31T16:30:00+02:00`; a date alone, or a timestamp without an offset,
is rejected, and the portfolio shows it back in UTC. The CLI runs one user's
commands, so it sends no `ETag`; a write that loses a race with another is
reported as a conflict.

### JSON output

Add `--json` anywhere on the command line to get indented JSON instead of the
default human-readable text:

```bash
dotnet run --project src/CryptoStuff.Cli -- coin bitcoin --json
```

A valuation's computed figures are rounded to 8 decimal places in the text, its
allocations to 2, and all of them are kept exact in the JSON; see
[Valuation](#valuation).

Timestamps are written the same way in the text and the JSON, and in the API's
responses and error messages: UTC, ending in `Z`, such as `2024-01-31T14:30:00Z`,
with a fraction of a second only when there is one. A timestamp you send with
another offset comes back converted to UTC.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | The command completed successfully. |
| `1` | No command or an unrecognized command, the wrong number of arguments, an input validation failure, a market data provider failure (see [Error codes](#error-codes)), a startup configuration error (a missing or invalid `CoinGecko:ApiKey`/`CoinGecko:CacheSeconds`, see [Registration](#registration), or an unresolvable database connection string, see [Data storage](#data-storage)), or, for a command that uses the database, a database that could not be opened or migrated, that holds data this version cannot read, or that another writer kept locked (see [Data storage](#data-storage)). |
| `2` | `alert check --exit-code` triggered at least one alert. Without `--exit-code`, `alert check` exits `0` whether or not anything triggered. |
| `130` | The command was canceled (Ctrl+C) before it completed. |

Usage and validation errors, market data provider failures, and database
failures are written to standard error; successful results are written to
standard output.

## REST API

`CryptoStuff.Api` is a minimal-API front end over the same five operations as
`ICryptocurrencyService`, plus the watchlist operations of `IWatchlistService`,
the portfolio operations of `IPortfolioService`, and the alert operations of
`IAlertService` and `IAlertEvaluator`. Run it, once `Api:Key` is set (see
[Configuration](#configuration)), with:

```bash
dotnet run --project src/CryptoStuff.Api
```

Every route below requires the [API key](#api-key) in the `X-Api-Key`
header; without it, a request gets `401` and nothing else.

| Route | Query/path parameters | Example |
|---|---|---|
| `GET /prices` | `coins`, `currencies` — comma-separated coin ids and vs_currency codes | `/prices?coins=bitcoin,ethereum&currencies=usd,eur` |
| `GET /token-prices` | `platform`, `addresses` (comma-separated), `currencies` (comma-separated) | `/token-prices?platform=ethereum&addresses=0xaaa...,0xbbb...&currencies=usd` |
| `GET /historical-market-data` | `coin`, `currency`, `days` (a positive whole number) | `/historical-market-data?coin=bitcoin&currency=usd&days=30` |
| `GET /coins/{coin}` | `coin` (path) | `/coins/bitcoin` |
| `GET /coins/{coin}/developer-data` | `coin` (path), `date` in `dd-MM-yyyy` | `/coins/bitcoin/developer-data?date=29-02-2024` |
| `GET /watchlists` | — | `/watchlists` |
| `POST /watchlists` | JSON body `{ "name": "<name>" }` | `/watchlists` |
| `GET /watchlists/{id}` | `id` (path) | `/watchlists/1` |
| `PUT /watchlists/{id}` | `id` (path), JSON body `{ "name": "<name>" }` | `/watchlists/1` |
| `DELETE /watchlists/{id}` | `id` (path) | `/watchlists/1` |
| `PUT /watchlists/{id}/coins/{coin}` | `id`, `coin` (path); no body | `/watchlists/1/coins/bitcoin` |
| `DELETE /watchlists/{id}/coins/{coin}` | `id`, `coin` (path); no body | `/watchlists/1/coins/bitcoin` |
| `GET /watchlists/{id}/prices` | `id` (path), `currency` | `/watchlists/1/prices?currency=usd` |
| `GET /portfolios` | — | `/portfolios` |
| `POST /portfolios` | JSON body `{ "name": "<name>", "vsCurrency": "<currency>", "costBasisMethod": "<method>" }` | `/portfolios` |
| `GET /portfolios/{id}` | `id` (path) | `/portfolios/1` |
| `PUT /portfolios/{id}` | `id` (path), JSON body `{ "name": "<name>", "costBasisMethod": "<method>" }`, optional `If-Match` | `/portfolios/1` |
| `DELETE /portfolios/{id}` | `id` (path), optional `If-Match` | `/portfolios/1` |
| `POST /portfolios/{id}/transactions` | `id` (path), JSON [transaction body](#portfolio-routes), optional `If-Match` | `/portfolios/1/transactions` |
| `GET /portfolios/{id}/transactions/{txId}` | `id`, `txId` (path) | `/portfolios/1/transactions/1` |
| `PUT /portfolios/{id}/transactions/{txId}` | `id`, `txId` (path), JSON [transaction body](#portfolio-routes), optional `If-Match` | `/portfolios/1/transactions/1` |
| `DELETE /portfolios/{id}/transactions/{txId}` | `id`, `txId` (path), optional `If-Match` | `/portfolios/1/transactions/1` |
| `GET /portfolios/{id}/valuation` | `id` (path) | `/portfolios/1/valuation` |
| `GET /alerts` | — | `/alerts` |
| `POST /alerts` | JSON body `{ "coinId": "<coin>", "vsCurrency": "<currency>", "direction": "<direction>", "threshold": <number> }` | `/alerts` |
| `GET /alerts/{id}` | `id` (path) | `/alerts/1` |
| `PUT /alerts/{id}` | `id` (path), JSON body `{ "direction": "<direction>", "threshold": <number> }` | `/alerts/1` |
| `DELETE /alerts/{id}` | `id` (path) | `/alerts/1` |
| `POST /alerts/{id}/rearm` | `id` (path); no body | `/alerts/1/rearm` |
| `POST /alerts/check` | — no body | `/alerts/check` |

Each route applies the applicable [input validation](#input-validation) rules
above to its parameters before calling the market data provider, and reports the first
violation it finds. As with the CLI, the non-empty-collection rule doesn't
apply here: a comma-separated list parameter can never be truly empty, only
contain a blank entry (e.g. `coins=bitcoin,,ethereum`), which is reported as
such.

A successful request returns `200` with the matching view record as JSON
(`201` and `204` for the [watchlist](#watchlist-routes),
[portfolio](#portfolio-routes) and [alert](#alert-routes) routes noted below), serialized with ASP.NET
Core's default (camelCase) naming and enumerations as their lowercase tokens
(`average-cost`, `buy`, `above`) — a validation
failure returns `400` with an
[RFC 7807](https://www.rfc-editor.org/rfc/rfc7807) validation-problem body,
`{ "errors": { "<field>": ["<message>"] }, ... }`. A service
failure (see [Error codes](#error-codes)) returns a `ProblemDetails` body
with one of the statuses below; its `detail` is the failure's detail message
when it has one, and fixed text for the code otherwise (a blank message counts
as none). A `detail` is always text written for the client: no exception's
message reaches a response, since one can carry paths, SQL or other internals.

| Status | `ServiceErrorCode` |
|---|---|
| `404` | `NotFound` |
| `429` | `RateLimited` |
| `502` | `UpstreamUnavailable` |
| `504` | `RequestTimedOut` |
| `409` | `Conflict` |
| `422` | `RuleViolated` |
| `412` | `PreconditionFailed` |
| `501` | `Unsupported` (documented on `GET /coins/{coin}/developer-data`, the one operation it is reserved for) |
| `500` | *(defensive fallback only — every `ServiceErrorCode` value maps above)* |

A write that finds the [database](#data-storage) locked by another writer for
longer than SQLite waits returns `503` with a `ProblemDetails` body, `detail`
`The database is busy with another write; try again shortly.` (always that
text, whatever the database reported), and a
`Retry-After: 5` header. Nothing was written, so the same request can be
retried as it is.

A path whose id is not a whole number (`/watchlists/abc`) matches no route and
returns `404` with a `ProblemDetails` body. A `Location` header keeps the
[`PathBase`](#configuration) the host is configured with, so with `/api` a new
watchlist's is `/api/watchlists/1`.

### API key

Every request must carry the key configured as `Api:Key` in the `X-Api-Key`
header:

```bash
curl -s http://localhost:5200/watchlists -H 'X-Api-Key: <your-api-key>'
```

The key is compared exactly and case-sensitively: a prefix of the key or the
key with more characters after it is wrong. Spaces or tabs around the
header's value are not part of the key: the HTTP server removes them before
the comparison. Spaces or tabs around the configured `Api:Key` are ignored
too. A client's key is read from that header only, never from the query
string. Only these routes answer without it, and also answer a request that
sends a wrong one:

- `GET /health` and `GET /health/ready`
- `GET /openapi/v1.json`
- `GET /scalar/v1`, in the Development environment, where it is mapped

A request without the header, with an empty or blank one, or with a wrong key
gets the same `401` with a `ProblemDetails` body, so a caller cannot tell
which it was:

```
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json

{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2", "title": "Unauthorized", "status": 401, "detail": "Send a valid API key in the X-Api-Key header.", ... }
```

The request is rejected before anything else happens to it: before its input
is validated (an invalid body or query is still a `401`, not a `400`), before
the database or the market data provider is touched, and before the
[check rate limit](#check-rate-limit), so a caller without the key cannot use
up `Alerts:ChecksPerMinute`. A path that matches no route is a `401` too, not
a `404`, until the key is right. Under a configured
[`PathBase`](#configuration) the same routes are open or protected as at the
root. Every route the host maps is protected unless it is one of those
listed above, including any route added later. Neither the configured key
nor a supplied one ever appears in a response or a log entry. The
[background evaluation](#background-evaluation) does not go through HTTP, so
the key does not apply to it.

With the right key, every route behaves as the rest of this section
describes.

### Watchlist routes

The `/watchlists` routes drive the same watchlists as the CLI's
[`watchlist` commands](#command-line-interface), stored in the shared
[database](#data-storage). Ids are the positive whole numbers the database
assigns.

Create and rename take a JSON body with the watchlist's name — following the
[resource name](#input-validation) rule and unique ignoring case, as for the CLI:

```bash
curl -i -X POST http://localhost:5200/watchlists -H 'X-Api-Key: <your-api-key>' -H 'Content-Type: application/json' -d '{ "name": "majors" }'
```

```
HTTP/1.1 201 Created
Location: /watchlists/1

{ "id": 1, "name": "majors", "coinIds": [] }
```

`PUT /watchlists/{id}` replaces the whole resource, so its body is the same
`{ "name": "<name>" }` and the watchlist's coins are left as they are. Every
route that returns a watchlist returns it in that shape: `id`, `name`, and
`coinIds` (lowercased, in the order they were added).

| Route | Success | Notes |
|---|---|---|
| `GET /watchlists` | `200` | An array of watchlists, ordered by id |
| `POST /watchlists` | `201` with a `Location` header | The new, empty watchlist |
| `GET /watchlists/{id}` | `200` | |
| `PUT /watchlists/{id}` | `200` | The renamed watchlist |
| `DELETE /watchlists/{id}` | `204` | No body |
| `PUT /watchlists/{id}/coins/{coin}` | `200` | Idempotent: adding a coin already present changes nothing |
| `DELETE /watchlists/{id}/coins/{coin}` | `200` | Idempotent: removing a coin that is absent changes nothing |
| `GET /watchlists/{id}/prices` | `200` | A `CoinPriceView`, as `GET /prices` returns, in the watchlist's coin order; a coin the market data provider no longer knows comes back with no amounts, and an empty watchlist returns no coins without calling the market data provider |

Failures use the [ProblemDetails](#rest-api) contract above:

| Status | When |
|---|---|
| `400` | A blank or over-long `name`, a missing body or malformed JSON, a blank `coin` or `currency`, or an `id` of zero or below |
| `401` | No valid `X-Api-Key` header (see [API key](#api-key)), whatever else is wrong with the request |
| `404` | No watchlist has that `id`, another writer deleted it while the request was saving, or the `id` is not a whole number (no route matches it) |
| `409` | The name is taken by another watchlist (ignoring case), or a concurrent change won the race |
| `422` | Adding a coin that the market data provider does not know; the `detail` names it |
| `429`, `502`, `504` | The market data provider failed while checking a coin to add or while pricing |

### Portfolio routes

The `/portfolios` routes drive the same portfolios as the CLI's
[`portfolio` commands](#command-line-interface), stored in the shared
[database](#data-storage), and accept exactly what the CLI accepts: each value
goes through the same Core validators and token parsers. Ids are the positive
whole numbers the database assigns.

Create takes the name (following the [resource name](#input-validation) rule,
unique ignoring case), the
vs_currency every amount is in (fixed for good once created), and the
[cost-basis method](#cost-basis), `average-cost`, `fifo`, `lifo` or `hifo`:

```bash
curl -i -X POST http://localhost:5200/portfolios -H 'X-Api-Key: <your-api-key>' -H 'Content-Type: application/json'   -d '{ "name": "main", "vsCurrency": "usd", "costBasisMethod": "fifo" }'
```

```
HTTP/1.1 201 Created
Location: /portfolios/1
ETag: "1"

{ "id": 1, "name": "main", "vsCurrency": "usd", "costBasisMethod": "fifo", "version": 1, "transactions": [] }
```

`PUT /portfolios/{id}` replaces everything a client can change about a
portfolio, `{ "name": "<name>", "costBasisMethod": "<method>" }`; the
vs_currency cannot change, and the transactions change only through their own
routes. Recording or replacing a transaction takes this body:

```json
{ "kind": "buy", "coinId": "bitcoin", "quantity": 0.5, "unitPrice": 42000, "fee": 2.5, "occurredAt": "2024-01-31T14:30:00Z" }
```

- `kind` is `buy` or `sell`; `coinId` is a CoinGecko coin id, stored
  trimmed and lowercased. A buy of a coin new to the portfolio's ledger is
  checked against the market data provider; a coin any of its transactions already holds is
  not, since it passed that check when first recorded, so a coin the provider has
  since delisted can still be sold, and a held coin can be traded while
  the provider is failing. Nor is a sell of a coin new to the ledger, which
  always oversells.
- `quantity` is a JSON number greater than zero; `unitPrice` and `fee` are
  JSON numbers of zero or more, in the portfolio's vs_currency. `fee` is
  optional and zero when omitted. A comma decimal or digit grouping (`"1,5"`)
  is a `400`.
- `occurredAt` is an ISO 8601 timestamp with `Z` or a `±hh:mm` offset, and
  stored as UTC; it cannot be more than five minutes in the future, an
  allowance for a client clock running a little ahead (the timestamp is still
  stored as given). Responses always give it as UTC
  ending in `Z`: `2024-01-31T16:30:00+02:00` is returned as `2024-01-31T14:30:00Z`.

Every route that returns a portfolio returns it whole, as above, with its
transactions in ledger order, each shaped like the body plus its `id` and
`sequence`, so one can be edited and sent back to `PUT`. The `sequence` is the
transaction's place in the order its portfolio recorded it, counting from 1;
it breaks ties between transactions at the same instant, and the portfolio
never gives it to another transaction, even after this one is deleted.

| Route | Success | Notes |
|---|---|---|
| `GET /portfolios` | `200` | An array of portfolios, ordered by id |
| `POST /portfolios` | `201` with `Location` and `ETag` | The new, empty portfolio |
| `GET /portfolios/{id}` | `200` with `ETag` | |
| `PUT /portfolios/{id}` | `200` with `ETag` | The updated portfolio |
| `DELETE /portfolios/{id}` | `204` | No body; its transactions go with it |
| `POST /portfolios/{id}/transactions` | `201` with `Location` and `ETag` | The portfolio; `Location` names the new transaction, `/portfolios/{id}/transactions/{txId}` |
| `GET /portfolios/{id}/transactions/{txId}` | `200` with `ETag` | The one transaction, shaped as in the portfolio's list; the `ETag` is the portfolio's, the one this URI's `PUT` and `DELETE` take |
| `PUT /portfolios/{id}/transactions/{txId}` | `200` with `ETag` | The portfolio; the transaction keeps its id and sequence |
| `DELETE /portfolios/{id}/transactions/{txId}` | `200` with `ETag` | The portfolio, without the transaction |
| `GET /portfolios/{id}/valuation` | `200` | A [valuation](#valuation), each coin with its exact `allocationPercent`: one market data provider call for the coins still held, more only when one of them is unknown; a coin the provider gives no price for is [left unpriced](#unpriced-coins) |

#### `ETag` and `If-Match`

A portfolio's version, which goes up by one with every change to it or its
ledger, is its entity tag. Every response that carries a portfolio carries
its version as a strong `ETag`, such as `ETag: "3"`, and every write
(`PUT`, `DELETE`, and the three transaction writes) honours an optional
`If-Match`:

- Without `If-Match`, or with `If-Match: *`, the write is unconditional.
- With the current tag, or a list of tags that includes it (`"3", "4"`, or
  several `If-Match` header lines), the write goes ahead and the response
  carries the new one.
- With any other tag or tags, the write is refused with `412` and changes
  nothing; the `detail` names the version the portfolio is at and the
  versions the request listed. `If-Match` compares strongly, so a weak tag
  (`W/"3"`) never matches.
- A value that is neither `*` nor a list of entity tags (`3` unquoted, `"3"
  "4"` without a comma, or `*` alongside a tag) is a `400`.
- A missing target is reported before a stale tag: an unknown portfolio, or a
  transaction id the portfolio does not have, is a `404` whatever tags the
  request sends, as RFC 9110 asks. Failures that depend on what the write
  would change, such as a taken name, a future date or an oversell, are
  checked after `If-Match`, so a stale tag reports `412` first.

`If-Match` is checked against the version the write has just read. A
concurrent write that saves between that read and this write's own save is
still caught, by the database's version check, and returns `409`. The CLI
does not use entity tags: one user drives it, and a lost race there is the
same `Conflict` error.

Failures use the [ProblemDetails](#rest-api) contract above:

| Status | When |
|---|---|
| `400` | A missing body or malformed JSON; a blank or over-long `name`; a blank `vsCurrency` or `coinId`; a `costBasisMethod` or `kind` that is not one of its tokens; a missing or out-of-range `quantity`, `unitPrice` or `fee`; an `occurredAt` that is not an ISO 8601 timestamp with an offset; an `id` or `txId` of zero or below; an `If-Match` that is neither `*` nor a list of entity tags |
| `401` | No valid `X-Api-Key` header (see [API key](#api-key)), whatever else is wrong with the request |
| `404` | No portfolio has that `id`, another writer deleted it while the request was saving, the portfolio has no transaction with that `txId`, or either is not a whole number (no route matches it) |
| `409` | The name is taken by another portfolio (ignoring case), or a concurrent write won the race |
| `412` | `If-Match` does not match the portfolio's current `ETag` |
| `422` | The ledger would oversell (the `detail` names the coin and date of the sell), a coin's amounts would be [too large to compute](#cost-basis) (the `detail` names the coin), the transaction is dated more than five minutes in the future, or the market data provider does not know a coin new to the portfolio that the transaction buys, or has no price for it in the portfolio's vs_currency; valuing a portfolio whose valuation has an amount too large to compute |
| `429`, `502`, `504` | The market data provider failed while checking a bought coin new to the portfolio or while valuing |

### Alert routes

The `/alerts` routes drive the same price alerts as the CLI's
[`alert` commands](#command-line-interface), stored in the shared
[database](#data-storage), and accept exactly what the CLI accepts: each value
goes through the same Core validators and token parsers. Ids are the positive
whole numbers the database assigns.

Create takes the CoinGecko coin id and the vs_currency it is priced in (both
checked against the market data provider, stored trimmed and lowercased, and fixed for good once created),
the direction, `above` or `below`, and the threshold, a JSON number greater
than zero in that vs_currency:

```bash
curl -i -X POST http://localhost:5200/alerts -H 'X-Api-Key: <your-api-key>' -H 'Content-Type: application/json'   -d '{ "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000 }'
```

```
HTTP/1.1 201 Created
Location: /alerts/1

{ "id": 1, "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000, "state": "armed", "awaitingReset": false, "triggeredAt": null, "triggeredPrice": null, "version": 1 }
```

`PUT /alerts/{id}` replaces everything a client can change about an alert,
`{ "direction": "<direction>", "threshold": <number> }`, and re-arms it whether
or not it had triggered. A comma decimal or digit grouping (`"1,5"`) in a
threshold is a `400`. Every route that returns an alert returns it in the shape
above.

`POST /alerts/check` runs one [evaluation](#alert-evaluation), as the
[background evaluation](#background-evaluation) does, and returns the alerts it
triggered, an empty `triggered` list when none did, and the alerts it could not
price, an empty `unpriced` list when it priced them all. A triggered alert looks
like this, both in that report and when read back:

```bash
curl -s -X POST http://localhost:5200/alerts/check -H 'X-Api-Key: <your-api-key>'
```

```json
{ "triggered": [ { "id": 1, "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000, "price": 52000.5, "triggeredAt": "2026-11-07T12:30:00Z" } ], "unpriced": [ { "id": 2, "coinId": "ghostcoin", "vsCurrency": "usd" } ] }
```

```bash
curl -s http://localhost:5200/alerts/1 -H 'X-Api-Key: <your-api-key>'
```

```json
{ "id": 1, "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000, "state": "triggered", "awaitingReset": false, "triggeredAt": "2026-11-07T12:30:00Z", "triggeredPrice": 52000.5, "version": 2 }
```

It stays `triggered`, and is not evaluated again, until `POST /alerts/{id}/rearm`
moves it back to `armed` or a `PUT` replaces its condition. A re-armed alert
comes back with `"awaitingReset": true` and triggers only after an evaluation
has seen its price move back past the threshold, which sets `awaitingReset`
to `false` and the version one higher (see
[Re-armed alerts](#alert-evaluation)).

| Route | Success | Notes |
|---|---|---|
| `GET /alerts` | `200` | An array of alerts, ordered by id |
| `POST /alerts` | `201` with a `Location` header | The new, armed alert |
| `GET /alerts/{id}` | `200` | |
| `PUT /alerts/{id}` | `200` | The updated, armed alert |
| `DELETE /alerts/{id}` | `204` | No body |
| `POST /alerts/{id}/rearm` | `200` | The armed alert, awaiting its reset; an alert that was already armed comes back as it was, so a retried re-arm gets the same alert |
| `POST /alerts/check` | `200` | The alerts this evaluation triggered, and the ones it could not price; at most `Alerts:ChecksPerMinute` a minute (see [Check rate limit](#check-rate-limit)) |

Alerts carry a `version`, but no `ETag`: as with the CLI, every alert write is
unconditional, and the entity-tag contract above belongs to the portfolio
routes alone. A concurrent write that wins the race still turns this one into
a `409`.

Failures use the [ProblemDetails](#rest-api) contract above:

| Status | When |
|---|---|
| `400` | A missing body or malformed JSON; a blank `coinId` or `vsCurrency`; a `direction` that is not `above` or `below`; a missing `threshold`, or one of zero or below; an `id` of zero or below |
| `401` | No valid `X-Api-Key` header (see [API key](#api-key)), whatever else is wrong with the request; it does not count toward `Alerts:ChecksPerMinute` |
| `404` | No alert has that `id`, another writer deleted it while the request was saving, or the `id` is not a whole number (no route matches it) |
| `409` | A concurrent write won the race; for `POST /alerts/check`, another write changed one of the evaluated alerts, so nothing was triggered and the check can be run again |
| `422` | Creating an alert on a coin the market data provider does not know, or that has no price in that vs_currency; the `detail` names it |
| `429` | `POST /alerts/check` went over `Alerts:ChecksPerMinute`, with a `Retry-After` header, or the market data provider's rate limit was hit while checking a new alert's coin or while evaluating |
| `502`, `504` | The market data provider failed while checking a new alert's coin or while evaluating |

### Health, OpenAPI, and Scalar

These routes are the only ones open without the [API key](#api-key), and
they answer a wrong one too.

- `GET /health` — a liveness probe, available in every environment. It runs
  no check, so it answers `200` even while the database is unusable.
- `GET /health/ready` — a readiness probe, available in every environment:
  `200` when the shared database opens and has no pending migration, `503`
  otherwise, such as when the file is missing or is not a SQLite database.
  A `503` carries only the plain-text status, `Unhealthy`, with no detail,
  so no file path can leak. It only reads: a missing database file, or its
  missing directory, is reported, not created. It does not call the market
  data provider, so a probe never spends the rate limit of the market data
  provider's API key.
- `GET /openapi/v1.json` — the generated OpenAPI document, available in every
  environment. It declares the `ApiKey` security scheme (an API key in the
  `X-Api-Key` header) and requires it on every operation it lists, each of
  which also lists the `401` response. The open routes are not among its
  operations.
- `GET /scalar/v1` — an interactive Scalar UI over the OpenAPI document,
  mapped in the Development environment only.

## Container image

The [`Dockerfile`](Dockerfile) at the repository root builds an image of the
API host, so the REST API and its
[background evaluation](#background-evaluation) can run unattended. It
publishes `src/CryptoStuff.Api` in `Release` on the
`mcr.microsoft.com/dotnet/sdk:10.0.404` image and runs it on
`mcr.microsoft.com/dotnet/aspnet:10.0`. The SDK tag is the version
[`global.json`](global.json) pins, and the build runs with `global.json` in
place, so an SDK image the pin rejects fails the build: move the tag whenever
`global.json` moves. Build from the repository root:

```bash
docker build --tag cryptostuff-api .
```

Run it with both keys and a named volume for the database:

```bash
docker volume create cryptostuff-data
docker run --detach --name cryptostuff-api \
  --publish 127.0.0.1:8080:8080 \
  --volume cryptostuff-data:/data \
  --env CoinGecko__ApiKey=<your-coingecko-api-key> \
  --env Api__Key=<your-api-key> \
  cryptostuff-api
curl -s http://127.0.0.1:8080/watchlists -H 'X-Api-Key: <your-api-key>'
```

- **Environment.** `CoinGecko__ApiKey` and `Api__Key` must be supplied when
  the container is run; neither is in the image. Every other setting in
  [Configuration](#configuration) and [Background evaluation](#background-evaluation)
  (`Alerts__EvaluationSeconds`, `PathBase`, …) is passed the same way. The
  image runs in the `Production` environment, so `GET /scalar/v1` is not
  mapped; `GET /openapi/v1.json` is.
- **Volume.** The image sets `ConnectionStrings__CryptoStuff` to
  `Data Source=/data/cryptostuff.db`, so mount the volume at `/data`. The
  container runs as the aspnet image's non-root `app` user (uid `1654`),
  which owns `/data`, so on a new, empty named volume the first start-up's
  migration creates `cryptostuff.db` there (with SQLite's `-wal` and `-shm`
  files beside it). Data written through the API survives removing the
  container and starting a new one on the same volume. Without a volume the
  database lives inside the container and goes with it. A bind-mounted host
  directory would have to be writable by uid `1654`; that is not tested.
- **Port.** The API listens on port `8080` over plain HTTP, the aspnet
  image's default (`ASPNETCORE_HTTP_PORTS=8080`), and the image declares no
  `EXPOSE`, so publish the port with `--publish`. There is no HTTPS inside
  the container: put TLS in front of it.
- **Health.** The image declares no Docker `HEALTHCHECK`: the aspnet image
  has neither `curl` nor `wget`, and none is added just for that. An
  orchestrator probes over HTTP on port `8080` instead, without the API key:
  `GET /health` for liveness and `GET /health/ready` for readiness (see
  [Health, OpenAPI, and Scalar](#health-openapi-and-scalar)).
- **Stopping.** `docker stop cryptostuff-api` shuts the host down cleanly
  (exit code `0`).

The image does not include the CLI, an HTTPS certificate, or a health-check
tool, and none is published to a registry: build it yourself. It is built for
the architecture of the machine that builds it (`linux/amd64` on an x64
machine); no multi-architecture image is produced.

### Container smoke check

[`test/container-smoke.sh`](test/container-smoke.sh) builds the image from
the repository and checks it for real, stopping at the first check that
fails and naming it:

1. **readiness** — `GET /health/ready` answers `200`;
2. **API key** — `GET /watchlists` answers `401` without the key and `200`
   with it;
3. **non-root** — the container's process does not run as uid `0`;
4. **persistence** — a watchlist created through the API is still there
   after the container is removed and a new one is started on the same
   volume.

```bash
bash test/container-smoke.sh
```

It needs Docker running Linux containers, `bash` and `curl`, and runs on
Linux and from Git Bash on Windows. It uses placeholder keys, publishes the
port on a free `127.0.0.1` port Docker picks, and only calls routes that do
not reach CoinGecko; the container resolves CoinGecko's host to itself, so
the check never depends on CoinGecko or spends a real key. It removes the
containers, volume and image it created however it exits, and nothing else.

## Build, test, and format

```bash
dotnet build
dotnet test
dotnet format
```

CI (`.github/workflows/ci.yml`) runs the same three commands — build and test
in the `Release` configuration, plus a format-verification check — on both
`ubuntu-latest` and `windows-latest`, on every push. A second job, on
`ubuntu-latest` only since a Windows runner cannot run Linux containers, runs
the [container smoke check](#container-smoke-check).

The tests need neither a real CoinGecko API key nor network access (they
supply placeholder keys of their own, for CoinGecko and for the API), and
never touch the real database. `CryptoStuff.Api.IntegrationTests` hosts the
API for real with only its CoinGecko client faked, each test's host on a
migrated temporary database file of its own.
`CryptoStuff.Persistence.IntegrationTests` works only on throwaway SQLite
databases: temporary files, mostly, and a few in-memory ones.

### Database migrations

The schema lives in EF Core migrations checked in under
`src/CryptoStuff.Persistence/Migrations/`; the database is never edited by
hand. After changing an entity's mapping, restore the pinned `dotnet-ef` local
tool and add a migration from the repository root:

```bash
dotnet tool restore
dotnet ef migrations add <MigrationName> --project src/CryptoStuff.Persistence
```

The persistence project doubles as the startup project through its design-time
context factory, so the tool needs no host and no CoinGecko key. Commit the
generated files as they are: `.editorconfig` marks the folder `generated_code`,
so neither the style analyzers nor `dotnet format` touch them. A migration is
edited only to change what it does, never its style. Forgetting one fails
`dotnet test`, which checks that the model has no changes no migration covers.
There is no `dotnet ef database update` step: both hosts apply pending
migrations at start up (see [Data storage](#data-storage)).
