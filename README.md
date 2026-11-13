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

| Project | Purpose |
|---|---|
| `CryptoStuff.CoinGecko` | API client and adapter — sole owner of the wire format; covers `simple/price`, `simple/token_price/{platform}`, `coins/{id}/market_chart`, `coins/{id}`, and `coins/{id}/history` (the latter's `date` query parameter must be `dd-MM-yyyy`); `CryptocurrencyService` implements Core's `ICryptocurrencyService` over the client, mapping its responses to Core's views (`CoinPriceMapper`, `TokenPriceMapper`, `CoinMarketChartMapper`, `CoinMapper`, `CoinDeveloperDataMapper`) and its failures to a `ServiceErrorCode` (`ServiceErrorCodeMapper`) |
| `CryptoStuff.Core` | Queries (`CoinPriceQuery`, `TokenPriceQuery`, `CoinMarketChartQuery`, `CoinQuery`, `CoinDeveloperDataQuery`) and view records (`CoinPriceView`, `TokenPriceView`, `CoinMarketChartView`, `CoinView`, `CoinDeveloperDataView`), the `ICryptocurrencyService` market-data contract, results, caching, validation; watchlists (`Watchlists/`): the `Watchlist` entity, `IWatchlistRepository`, `IWatchlistService` and `WatchlistService`, the `CreateWatchlistCommand`, `RenameWatchlistCommand` and `WatchlistCoinCommand` commands, the `WatchlistPriceQuery` query, the `WatchlistView` view, and `WatchlistMapper`; portfolios (`Portfolios/`): the `Portfolio` entity, whose ledger of `PortfolioTransaction` buys and sells never takes a holding below zero, `TransactionDetails`, the `CostBasisMethod` and `TransactionKind` enumerations with their token parsers (`CostBasisMethodParser`, `TransactionKindParser`), `IPortfolioRepository`, `IPortfolioService` and `PortfolioService`, the `CreatePortfolioCommand`, `UpdatePortfolioCommand`, `AddTransactionCommand`, `ReplaceTransactionCommand` and `RemoveTransactionCommand` commands, the `PortfolioView` and `PortfolioTransactionView` views, `PortfolioMapper`, and the [cost-basis](#cost-basis) strategy (`ICostBasisMethod`, implemented by `AverageCostMethod` and `FifoMethod`, chosen through `CostBasisMethods`, each reducing a coin's transactions to a `CoinPosition`), and [valuation](#valuation) (`IPortfolioService.GetValuationAsync`, returning a `PortfolioValuationView` of `CoinValuationView` rows and `PortfolioValuationTotals`); price alerts (`Alerts/`): the `PriceAlert` entity, an armed or triggered alert met inclusively by a price at or past its threshold, whose coin and vs_currency are fixed at creation, the `AlertDirection` and `AlertState` enumerations with their token parsers (`AlertDirectionParser`, `AlertStateParser`), `IAlertRepository`, `IAlertService` and `AlertService`, the `CreateAlertCommand` and `UpdateAlertCommand` commands, the `AlertView` view, `AlertMapper`, and [evaluation](#alert-evaluation) (`IAlertEvaluator` and `AlertEvaluator`, returning an `AlertEvaluationView` of `TriggeredAlertView` rows); and, shared by the stored features, `PersistenceConflictException` and the `NameValidator`, `PositiveIdValidator`, `PositiveDecimalValidator`, `NonNegativeDecimalValidator` and `TimestampFormatValidator` rules |
| `CryptoStuff.Persistence` | EF Core over SQLite: `CryptoStuffDbContext`, the watchlist mapping (`WatchlistConfiguration`) and `WatchlistRepository`, the portfolio mapping (`PortfolioConfiguration`, which owns the portfolio's transactions) and `PortfolioRepository`, the alert mapping (`AlertConfiguration`) and `AlertRepository`, which also lists the armed alerts and can discard an alert whose save conflicted, translation of unique-constraint and concurrency failures (and, for portfolios, a portfolio deleted under a pending save) into `PersistenceConflictException`, `CryptoStuffDatabase.MigrateAsync` (creates a file database's directory, then migrates), the design-time context factory, and the checked-in migrations (`Migrations/`) |
| `CryptoStuff.Composition` | Registration and HTTP pipeline wiring: `AddCryptoStuff`, `AddAlertEvaluationSchedule` (the API's [background evaluation](#background-evaluation) interval, as an `AlertEvaluationSchedule`), `DatabaseConnectionString` (locates the shared database), and `MigrateCryptoStuffDatabaseAsync` (the start-up migration both hosts run) |
| `CryptoStuff.Cli` | Console host |
| `CryptoStuff.Api` | Minimal REST host, including `AlertEvaluationBackgroundService`, the [background alert evaluation](#background-evaluation) |
| `CryptoStuff.CoinGecko.UnitTests` | Unit tests for `CryptoStuff.CoinGecko` |
| `CryptoStuff.Core.UnitTests` | Unit tests for `CryptoStuff.Core` |
| `CryptoStuff.Composition.UnitTests` | Unit tests for `CryptoStuff.Composition` |
| `CryptoStuff.Cli.UnitTests` | Unit tests for `CryptoStuff.Cli` |
| `CryptoStuff.Api.UnitTests` | Unit tests for `CryptoStuff.Api` |
| `CryptoStuff.Api.IntegrationTests` | Integration tests over `CryptoStuff.Api`, hosted for real with only its CoinGecko client faked and each test's host on a migrated temporary database file of its own — needs neither an API key nor network access, and never touches the real database |

`CryptoStuff.Cli` and `CryptoStuff.Api` both reference `CryptoStuff.Composition`,
which references `CryptoStuff.Core` and the two adapters that plug into it:
`CryptoStuff.CoinGecko`, which references `CryptoStuff.Core`, implementing its
`ICryptocurrencyService`, and `CryptoStuff.Persistence`, which references
`CryptoStuff.Core`, implementing its repository interfaces. Dependencies point
inward: `CryptoStuff.Core` references no other project, so it never sees
CoinGecko's wire format or EF Core.

## Prerequisites

- .NET SDK `10.0.404`, pinned in [`global.json`](global.json).
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

`CryptoStuff.Core` reports a failed operation as a `ServiceErrorCode`. A failed
CoinGecko response maps to one of the first four codes; the last three are for
writes to the application's own stored data:

| Code | Meaning | Condition |
|---|---|---|
| `NotFound` | The requested resource does not exist | CoinGecko returned `404`, or no persisted record has that id |
| `RateLimited` | The CoinGecko rate limit was exceeded | CoinGecko returned `429` |
| `RequestTimedOut` | The request to CoinGecko did not complete in time | The request timed out |
| `UpstreamUnavailable` | CoinGecko failed for any other reason | Any other non-success response |
| `Conflict` | The write conflicts with stored state | A duplicate name, or a lost concurrency race |
| `RuleViolated` | The write breaks a domain rule | For example an oversell or an unknown coin |
| `PreconditionFailed` | The stored version no longer matches what the caller expected | A stale `If-Match` |

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
configuration itself. `CryptoStuff.Composition`'s `AddCryptoStuff` extension
wires its duration from the `CoinGecko:CacheSeconds` setting, defaulting to
`300` (5 minutes) when unset.

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

## Registration

`CryptoStuff.Composition` provides `AddCryptoStuff(IServiceCollection,
IConfiguration)`, the single extension method that registers everything
above: the CoinGecko-backed `ICryptocurrencyService`, wrapped in the caching
decorator, wired to the CoinGecko `HttpClient` and its rate-limit retry
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
dotnet user-secrets set ConnectionStrings:CryptoStuff "Data Source=/path/to/cryptostuff.db"
```

User secrets belong to one host project, so set the override for both
`CryptoStuff.Cli` and `CryptoStuff.Api` — or use the environment variable —
if they are to keep sharing a file. A value that is not a valid SQLite
connection string stops either host before it does anything, with
`ConnectionStrings:CryptoStuff is not a valid SQLite connection string: …`.
If the setting is absent and the OS reports no local application-data folder
at all, both hosts stop with:

```
No per-user local application-data folder is available for the default database file. Set the ConnectionStrings:CryptoStuff setting (the ConnectionStrings__CryptoStuff environment variable in deployment) to a SQLite connection string, e.g. 'Data Source=/path/to/cryptostuff.db'.
```

A portfolio and its transactions live in two tables, `Portfolios` and
`PortfolioTransactions`, and deleting a portfolio deletes its transactions.
Decimal amounts are stored as exact text and timestamps as UTC, so nothing is
rounded on the way to the file, and ids are never reused after a delete.
Upgrading from a release that stored only watchlists adds the two tables and
leaves the watchlists as they were.

A price alert lives in one table, `PriceAlerts`. Its direction and state are
stored as the same lowercase text tokens the hosts use (`above`, `below`,
`armed`, `triggered`), its threshold and triggered price as exact text, and
when it triggered as UTC, empty while the alert is armed. Its version is a
concurrency token, so a write based on a stale read is rejected as a conflict
rather than overwriting the newer change, and the repository can list just the
armed alerts for evaluation. Upgrading from a release without alerts adds the
table and leaves the watchlists and portfolios as they were.

There is no setup step. Both hosts create the file and its folder when they
are missing and apply any pending [migrations](#database-migrations) at start
up — the API before it starts listening, the CLI before it runs a command that
uses the database (the `watchlist`, `portfolio` and `alert` commands) — so whichever host runs first
after an upgrade brings the schema up to date. The CLI's CoinGecko-only
commands (`price`, `token`, `history`, `coin`, `developer`) never open the
database, so they skip migration and start as fast as they always did. If the
database cannot be opened or migrated (an unwritable folder, or a file that is
not a SQLite database), the CLI prints
`Error: the database could not be opened or migrated. …` and exits with code
`1`, and the API fails to start with the underlying exception.

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

The two diverge as soon as units bought at different prices are sold. Take
one ledger:

| Day | Transaction | Quantity | Unit price | Fee |
|-----|-------------|---------:|-----------:|----:|
| 1   | buy         | 1        | 10,000     | 10  |
| 2   | buy         | 1        | 20,000     | 20  |
| 3   | sell        | 1        | 30,000     | 30  |

With their fees the two units cost 10,010 and 20,020, 30,030 in all, and the
sale brings in 30,000 − 30 = 29,970.

|                          | Average cost             | FIFO                     |
|--------------------------|--------------------------|--------------------------|
| Cost of the unit sold    | 30,030 ÷ 2 = 15,015      | the day-1 unit, 10,010   |
| Realized profit and loss | 29,970 − 15,015 = 14,955 | 29,970 − 10,010 = 19,960 |
| Quantity held            | 1                        | 1                        |
| Cost basis held          | 15,015                   | 20,020                   |

Both account for the same 30,030 paid, as cost released by the sale plus cost
basis still held; they differ only in which cost the sale takes. Here, with
prices rising, FIFO realizes more profit now and keeps the dearer unit, so
less profit is left to come.

Every figure is an exact `decimal`, and Core rounds nothing to currency
places. The one inexact step is a share that does not divide evenly, such as
a third of an average cost, which is kept to the 28 significant digits a
`decimal` holds.

## Valuation

Valuing a portfolio joins the [cost-basis](#cost-basis) positions with live
prices. Core makes one `GetPricesAsync` call, for the coins the portfolio still
holds, priced in the portfolio's own currency. A portfolio that holds nothing,
whether it is empty or every position is closed, makes no upstream call. Each
coin in the valuation reports:

| Figure | Meaning |
|---|---|
| Quantity | The units still held |
| Market value | Quantity × the live price of one unit |
| Cost basis | What the units still held cost, fees on their buys included, under the portfolio's method |
| Unrealized profit and loss | Market value − cost basis: the gain or loss that selling every unit held at the live price would realize. Negative while the price is below cost. It is a paper figure and moves with the price |
| Realized profit and loss | What the coin's sells have already made: proceeds less sell fees, less the cost the method attributes to the units sold. Fixed once made, and negative for a loss |

The totals add each figure across the coins. Market value − cost basis equals
the total unrealized profit and loss, and total realized profit and loss is
every coin's realized profit and loss, closed coins included. Adding the two
profit-and-loss totals gives the portfolio's overall result so far.

A coin whose position is fully closed stays in the list, with a quantity,
market value, cost basis and unrealized profit and loss of zero and only its
realized profit and loss. It needs no price, so it is not requested, and
keeping it means the rows always add up to the totals. Coins are listed in
order of coin id.

If CoinGecko's response lacks a price for a coin still held, or any price in the
portfolio's currency, or CoinGecko reports one of those coins as unknown
(`NotFound` for the price lookup), the valuation fails with
`UpstreamUnavailable` and a detail naming the coin or coins, rather than
showing a partial total: the portfolio exists, so `NotFound` is kept for an
unknown portfolio id alone. Any other upstream failure, such as `RateLimited`,
passes through with its code.

Every figure is an exact `decimal`: Core rounds nothing, so a host that shows
currency places rounds when it formats.

## Alert evaluation

An alert only changes state when something evaluates it. One evaluator,
`IAlertEvaluator` in Core, does the work for every trigger, so the CLI and the
API behave identically:

| Trigger | When it runs |
|---|---|
| `alert check` (CLI) | Once, when the command is run. The CLI exits after each command, so it never evaluates in the background |
| `POST /alerts/check` (API) | Once per request |
| The API's background evaluation | Periodically for as long as the API host is running, every `Alerts:EvaluationSeconds` seconds (60 by default), each tick on its own |

Nothing is notified outwardly: a triggered alert is
recorded in its own state (`triggered`, with when and at what price) and
reported by the evaluation that triggered it: printed by `alert check`,
returned by `POST /alerts/check`, and logged by the background evaluation.

One evaluation:

1. Loads the armed alerts. With none, it makes no upstream call and reports
   nothing triggered.
2. Makes one price lookup (`GetPricesAsync`) for every coin and currency the
   armed alerts need, however many alerts there are.
3. Triggers each alert whose price meets it, inclusively: at or above the
   threshold for `above`, at or below it for `below`. An alert whose price has
   not reached it stays armed and is checked again by the next evaluation.
   Every alert triggered by one evaluation carries the same time, read from
   the clock once the prices arrived.
4. Reports each alert it triggered: its id, coin, currency, direction,
   threshold, the price that met it, and when.

A triggered alert is not evaluated again until it is
re-armed or edited.

**When something goes wrong.** An upstream failure, such as `RateLimited` or
`RequestTimedOut`, fails the evaluation with that code and leaves every alert
armed, so the next evaluation simply tries again. If CoinGecko reports one of
the coins as unknown, or its response lacks a price for a coin in an alert's
currency, the evaluation fails with `UpstreamUnavailable` and a detail naming
the coin, as a [valuation](#valuation) does, and triggers nothing: one alert
that cannot be priced holds up the others until it is edited or deleted.

**Two writers.** Evaluations can overlap another write to the same alert,
such as another evaluation (the CLI beside the API, or a doubled tick), an
edit, a re-arm, or a delete. Each trigger is saved on its own, and the alert's
version turns a lost race into a skip rather than an overwrite: that alert is
left as the other writer made it and is checked again by the next evaluation,
while the rest of the evaluation's triggers are still saved. The skipped alert
does not appear in the report.

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
with the exception. Either way every alert it had not triggered stays armed.
Stopping the host cancels an evaluation in progress, and that is not logged as
a failure.

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
| `watchlist create` | `<name>` — a new, empty watchlist; the name is at most 100 characters and unique, ignoring case | `cryptostuff watchlist create majors` |
| `watchlist show` | `<id>` — a watchlist's id, name, and coins | `cryptostuff watchlist show 1` |
| `watchlist rename` | `<id> <name>` — a new name for a watchlist | `cryptostuff watchlist rename 1 blue-chips` |
| `watchlist delete` | `<id>` — deletes a watchlist | `cryptostuff watchlist delete 1` |
| `watchlist add` | `<id> <coin>` — appends a coin id that CoinGecko knows; adding a coin already present changes nothing | `cryptostuff watchlist add 1 bitcoin` |
| `watchlist remove` | `<id> <coin>` — removes a coin id; removing one that is absent changes nothing | `cryptostuff watchlist remove 1 bitcoin` |
| `watchlist prices` | `<id> <currency>` — live prices of the watchlist's coins in a vs_currency | `cryptostuff watchlist prices 1 usd` |
| `portfolio list` | — lists every portfolio, with its id, name, currency, method, and transaction count | `cryptostuff portfolio list` |
| `portfolio create` | `<name> <currency> <method>` — a new, empty portfolio priced in a vs_currency that never changes; `<method>` is `average-cost` or `fifo`; the name is at most 100 characters and unique, ignoring case | `cryptostuff portfolio create main usd fifo` |
| `portfolio show` | `<id>` — a portfolio's id, name, currency, method, version, and transactions | `cryptostuff portfolio show 1` |
| `portfolio update` | `<id> <name> <method>` — replaces the portfolio's name and cost-basis method | `cryptostuff portfolio update 1 retirement average-cost` |
| `portfolio delete` | `<id>` — deletes a portfolio with all its transactions | `cryptostuff portfolio delete 1` |
| `portfolio value` | `<id>` — the portfolio's holdings at live prices; see [Valuation](#valuation) | `cryptostuff portfolio value 1` |
| `portfolio tx add` | `<id> <kind> <coin> <qty> <price> <when> [--fee <fee>]` — records a buy or sell (`<kind>` is `buy` or `sell`); the coin must be one CoinGecko knows | `cryptostuff portfolio tx add 1 buy bitcoin 0.5 40000 2024-01-31T14:30:00Z --fee 12.5` |
| `portfolio tx edit` | `<id> <txid> <kind> <coin> <qty> <price> <when> [--fee <fee>]` — replaces every detail of a transaction, so an omitted `--fee` becomes `0` | `cryptostuff portfolio tx edit 1 3 sell bitcoin 0.25 42000 2024-02-15T09:00:00Z` |
| `portfolio tx delete` | `<id> <txid>` — deletes a transaction | `cryptostuff portfolio tx delete 1 3` |
| `alert list` | — lists every alert, with its id, coin, direction, threshold, currency, and state | `cryptostuff alert list` |
| `alert create` | `<coin> <currency> <direction> <threshold>` — a new, armed alert on a coin priced in a vs_currency, both of which CoinGecko must know and neither of which changes later; `<direction>` is `above` or `below`; the threshold is greater than zero | `cryptostuff alert create bitcoin usd above 50000` |
| `alert show` | `<id>` — an alert's id, coin, currency, direction, threshold, state, when and at what price it triggered, and version | `cryptostuff alert show 1` |
| `alert update` | `<id> <direction> <threshold>` — replaces the alert's direction and threshold, which re-arms it whether or not it had triggered | `cryptostuff alert update 1 below 42000` |
| `alert delete` | `<id>` — deletes an alert | `cryptostuff alert delete 1` |
| `alert rearm` | `<id>` — moves a triggered alert back to armed; re-arming an armed alert is refused | `cryptostuff alert rearm 1` |
| `alert check` | — evaluates the armed alerts once and prints the ones that triggered, or `Nothing triggered.`; see [Alert evaluation](#alert-evaluation) | `cryptostuff alert check` |

Each command applies the applicable [input validation](#input-validation)
rules above to its arguments before calling CoinGecko, and reports the first
violation it finds. The non-empty-collection rule doesn't apply here: a
comma-separated list argument can never be truly empty, only contain a blank
entry (e.g. `bitcoin,,ethereum`), which is reported as such.

The `watchlist` commands take a watchlist's id as a positive whole number, which
`create` prints and `list` shows; anything else is reported as a validation
failure. They read and write the shared [database](#data-storage), so these
and the `portfolio` and `alert` commands are the only commands that open it (and
migrate it first). A failure the service
reports for them — an unknown id, a duplicate name, a coin CoinGecko doesn't
know — is printed as `Error: …` with the service's own message, and exits `1`.
`delete` prints `Deleted watchlist <id>.`; with `--json` it prints
`{"Id": <id>, "Deleted": true}`, indented.

The `portfolio` commands work the same way: ids are positive whole numbers
(`create` prints the portfolio's, and its listing shows each transaction's),
they open the shared [database](#data-storage), and a failure the service
reports is printed as `Error: …` and exits `1`. The ledger rules reach you that
way: selling more of a coin than the portfolio held at that moment, or editing
or deleting a transaction a later sell depends on, is refused with a message
naming the coin and date, and so is a transaction dated in the future. `show`,
`create`, `update` and the three `tx` commands print the resulting portfolio;
`delete` prints `Deleted portfolio <id>.`, or `{"Id": <id>, "Deleted": true}`
with `--json`.

The `alert` commands follow the same rules. `create`, `show`, `update` and
`rearm` print the resulting alert, text or `--json` alike, with `above` and
`below`, `armed` and `triggered` written as the same lowercase tokens Core
parses; `delete` prints `Deleted alert <id>.`, or `{"Id": <id>, "Deleted": true}`
with `--json`. A threshold is read like a portfolio quantity (a point for the
decimal mark, no thousands separators or exponent: `0.5` is accepted, `0,5` and
`1e3` are not) and must be greater than zero, so `0` and `-1` are rejected as
`Threshold must be greater than zero.` Core refuses a coin or currency
CoinGecko doesn't know, or that has no price in that currency, when the alert is
created, and refuses to re-arm an alert that is already armed, in a message
printed as `Error: …` with exit `1`. Like every stored write, the CLI sends no
expected version, so an `update`, `delete` or `rearm` never fails on a stale one.

`alert check` runs the one [evaluator](#alert-evaluation) once and prints each
alert it triggered, one line apiece (id, coin, direction, threshold, the price
that met it, and when), under a count; with `--json` it prints
`{"Triggered": [...]}`, an empty list when nothing triggered. Nothing triggering
is not a failure, so `check` exits `0` then; only an evaluation that failed, such
as a CoinGecko error, exits `1` and changes no alert. **Continuous evaluation
happens only in the API host**: the CLI exits after each command, so alerts are
evaluated only when `alert check` is run, while a running API host evaluates them
on its own timer.

Arguments stay positional; the one option besides `--json` is `--fee`, which
only `tx add` and `tx edit` take (anywhere on the command line, followed by its
value) and which defaults to `0`. Numbers are read the same way on every
machine, whatever its regional settings: a plain decimal with a point for the
decimal mark, and no thousands separators or exponent. `0.5` is accepted;
`0,5`, `1,000` and `1e3` are rejected as `Quantity must be a number.` (or
`Price`, or `Fee`) rather than read as something else. A quantity must be
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

Timestamps are written the same way in the text and the JSON, and in the API's
responses and error messages: UTC, ending in `Z`, such as `2024-01-31T14:30:00Z`,
with a fraction of a second only when there is one. A timestamp you send with
another offset comes back converted to UTC.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | The command completed successfully. |
| `1` | No command or an unrecognized command, the wrong number of arguments, an input validation failure, a CoinGecko service failure (see [Error codes](#error-codes)), a startup configuration error (a missing or invalid `CoinGecko:ApiKey`/`CoinGecko:CacheSeconds`, see [Registration](#registration), or an unresolvable database connection string, see [Data storage](#data-storage)), or, for a command that uses the database, a database that could not be opened or migrated (see [Data storage](#data-storage)). |
| `130` | The command was canceled (Ctrl+C) before it completed. |

Usage and validation errors, CoinGecko service failures, and database
failures are written to standard error; successful results are written to
standard output.

## REST API

`CryptoStuff.Api` is a minimal-API front end over the same five operations as
`ICryptocurrencyService`, plus the watchlist operations of `IWatchlistService`,
the portfolio operations of `IPortfolioService`, and the alert operations of
`IAlertService` and `IAlertEvaluator`. Run it with:

```bash
dotnet run --project src/CryptoStuff.Api
```

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
above to its parameters before calling CoinGecko, and reports the first
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
when it has one, and fixed text for the code otherwise:

| Status | `ServiceErrorCode` |
|---|---|
| `404` | `NotFound` |
| `429` | `RateLimited` |
| `502` | `UpstreamUnavailable` |
| `504` | `RequestTimedOut` |
| `409` | `Conflict` |
| `422` | `RuleViolated` |
| `412` | `PreconditionFailed` |
| `500` | *(defensive fallback only — every `ServiceErrorCode` value maps above)* |

### Watchlist routes

The `/watchlists` routes drive the same watchlists as the CLI's
[`watchlist` commands](#command-line-interface), stored in the shared
[database](#data-storage). Ids are the positive whole numbers the database
assigns.

Create and rename take a JSON body with the watchlist's name — at most 100
characters, unique ignoring case, as for the CLI:

```bash
curl -i -X POST http://localhost:5200/watchlists -H 'Content-Type: application/json' -d '{ "name": "majors" }'
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
| `GET /watchlists/{id}/prices` | `200` | A `CoinPriceView`, as `GET /prices` returns, in the watchlist's coin order; an empty watchlist returns no coins without calling CoinGecko |

Failures use the [ProblemDetails](#rest-api) contract above:

| Status | When |
|---|---|
| `400` | A blank or over-long `name`, a missing body or malformed JSON, a blank `coin` or `currency`, or an `id` of zero or below |
| `404` | No watchlist has that `id`, or the `id` is not a whole number (no route matches it) |
| `409` | The name is taken by another watchlist (ignoring case), or a concurrent change won the race |
| `422` | Adding a coin that CoinGecko does not know; the `detail` names it |
| `429`, `502`, `504` | CoinGecko failed while checking a coin to add or while pricing; pricing a watchlist that holds a coin CoinGecko no longer knows is a `502`, because the watchlist itself exists |

### Portfolio routes

The `/portfolios` routes drive the same portfolios as the CLI's
[`portfolio` commands](#command-line-interface), stored in the shared
[database](#data-storage), and accept exactly what the CLI accepts: each value
goes through the same Core validators and token parsers. Ids are the positive
whole numbers the database assigns.

Create takes the name (at most 100 characters, unique ignoring case), the
vs_currency every amount is in (fixed for good once created), and the
[cost-basis method](#cost-basis), `average-cost` or `fifo`:

```bash
curl -i -X POST http://localhost:5200/portfolios -H 'Content-Type: application/json'   -d '{ "name": "main", "vsCurrency": "usd", "costBasisMethod": "fifo" }'
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

- `kind` is `buy` or `sell`; `coinId` is a CoinGecko coin id, checked against
  CoinGecko and stored lowercased.
- `quantity` is a JSON number greater than zero; `unitPrice` and `fee` are
  JSON numbers of zero or more, in the portfolio's vs_currency. `fee` is
  optional and zero when omitted. A comma decimal or digit grouping (`"1,5"`)
  is a `400`.
- `occurredAt` is an ISO 8601 timestamp with `Z` or a `±hh:mm` offset, and
  stored as UTC; it cannot be in the future. Responses always give it as UTC
  ending in `Z`: `2024-01-31T16:30:00+02:00` is returned as `2024-01-31T14:30:00Z`.

Every route that returns a portfolio returns it whole, as above, with its
transactions in ledger order, each shaped like the body plus its `id` and
`sequence`, so one can be edited and sent back to `PUT`.

| Route | Success | Notes |
|---|---|---|
| `GET /portfolios` | `200` | An array of portfolios, ordered by id |
| `POST /portfolios` | `201` with `Location` and `ETag` | The new, empty portfolio |
| `GET /portfolios/{id}` | `200` with `ETag` | |
| `PUT /portfolios/{id}` | `200` with `ETag` | The updated portfolio |
| `DELETE /portfolios/{id}` | `204` | No body; its transactions go with it |
| `POST /portfolios/{id}/transactions` | `201` with `Location` and `ETag` | The portfolio; `Location` names the new transaction, `/portfolios/{id}/transactions/{txId}` |
| `PUT /portfolios/{id}/transactions/{txId}` | `200` with `ETag` | The portfolio; the transaction keeps its id and sequence |
| `DELETE /portfolios/{id}/transactions/{txId}` | `200` with `ETag` | The portfolio, without the transaction |
| `GET /portfolios/{id}/valuation` | `200` | A [valuation](#valuation): one CoinGecko call for the coins still held |

#### `ETag` and `If-Match`

A portfolio's version, which goes up by one with every change to it or its
ledger, is its entity tag. Every response that carries a portfolio carries
its version as a strong `ETag`, such as `ETag: "3"`, and every write
(`PUT`, `DELETE`, and the three transaction routes) honours an optional
`If-Match`:

- Without `If-Match`, or with `If-Match: *`, the write is unconditional.
- With the current tag, the write goes ahead and the response carries the new
  one.
- With any other tag, the write is refused with `412` and changes nothing; the
  `detail` names the version the portfolio is at. `If-Match` compares
  strongly, so a weak tag (`W/"3"`) never matches.
- A value that is not one entity tag (`3` unquoted, or a list such as
  `"3", "4"`) is a `400`.

`If-Match` is checked against the version the write has just read. A
concurrent write that saves between that read and this write's own save is
still caught, by the database's version check, and returns `409`. The CLI
does not use entity tags: one user drives it, and a lost race there is the
same `Conflict` error.

Failures use the [ProblemDetails](#rest-api) contract above:

| Status | When |
|---|---|
| `400` | A missing body or malformed JSON; a blank or over-long `name`; a blank `vsCurrency` or `coinId`; a `costBasisMethod` or `kind` that is not one of its tokens; a missing or out-of-range `quantity`, `unitPrice` or `fee`; an `occurredAt` that is not an ISO 8601 timestamp with an offset; an `id` or `txId` of zero or below; an `If-Match` that is not one entity tag |
| `404` | No portfolio has that `id`, the portfolio has no transaction with that `txId`, or either is not a whole number (no route matches it) |
| `409` | The name is taken by another portfolio (ignoring case), or a concurrent write won the race |
| `412` | `If-Match` does not match the portfolio's current `ETag` |
| `422` | The ledger would oversell (the `detail` names the coin and date of the sell), the transaction is dated in the future, or CoinGecko does not know the coin or has no price for it in the portfolio's vs_currency |
| `429`, `502`, `504` | CoinGecko failed while checking a transaction's coin or while valuing; valuing a portfolio that holds a coin CoinGecko no longer knows is a `502`, because the portfolio itself exists |

### Alert routes

The `/alerts` routes drive the same price alerts as the CLI's
[`alert` commands](#command-line-interface), stored in the shared
[database](#data-storage), and accept exactly what the CLI accepts: each value
goes through the same Core validators and token parsers. Ids are the positive
whole numbers the database assigns.

Create takes the CoinGecko coin id and the vs_currency it is priced in (both
checked against CoinGecko, stored lowercased, and fixed for good once created),
the direction, `above` or `below`, and the threshold, a JSON number greater
than zero in that vs_currency:

```bash
curl -i -X POST http://localhost:5200/alerts -H 'Content-Type: application/json'   -d '{ "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000 }'
```

```
HTTP/1.1 201 Created
Location: /alerts/1

{ "id": 1, "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000, "state": "armed", "triggeredAt": null, "triggeredPrice": null, "version": 1 }
```

`PUT /alerts/{id}` replaces everything a client can change about an alert,
`{ "direction": "<direction>", "threshold": <number> }`, and re-arms it whether
or not it had triggered. A comma decimal or digit grouping (`"1,5"`) in a
threshold is a `400`. Every route that returns an alert returns it in the shape
above.

`POST /alerts/check` runs one [evaluation](#alert-evaluation), as the
[background evaluation](#background-evaluation) does, and returns the alerts it
triggered, an empty `triggered` list when none did. A triggered alert looks
like this, both in that report and when read back:

```bash
curl -s -X POST http://localhost:5200/alerts/check
```

```json
{ "triggered": [ { "id": 1, "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000, "price": 52000.5, "triggeredAt": "2026-11-07T12:30:00Z" } ] }
```

```bash
curl -s http://localhost:5200/alerts/1
```

```json
{ "id": 1, "coinId": "bitcoin", "vsCurrency": "usd", "direction": "above", "threshold": 50000, "state": "triggered", "triggeredAt": "2026-11-07T12:30:00Z", "triggeredPrice": 52000.5, "version": 2 }
```

It stays `triggered`, and is not evaluated again, until `POST /alerts/{id}/rearm`
moves it back to `armed` or a `PUT` replaces its condition.

| Route | Success | Notes |
|---|---|---|
| `GET /alerts` | `200` | An array of alerts, ordered by id |
| `POST /alerts` | `201` with a `Location` header | The new, armed alert |
| `GET /alerts/{id}` | `200` | |
| `PUT /alerts/{id}` | `200` | The updated, armed alert |
| `DELETE /alerts/{id}` | `204` | No body |
| `POST /alerts/{id}/rearm` | `200` | The armed alert |
| `POST /alerts/check` | `200` | The alerts this evaluation triggered |

Alerts carry a `version`, but no `ETag`: as with the CLI, every alert write is
unconditional, and the entity-tag contract above belongs to the portfolio
routes alone. A concurrent write that wins the race still turns this one into
a `409`.

Failures use the [ProblemDetails](#rest-api) contract above:

| Status | When |
|---|---|
| `400` | A missing body or malformed JSON; a blank `coinId` or `vsCurrency`; a `direction` that is not `above` or `below`; a missing `threshold`, or one of zero or below; an `id` of zero or below |
| `404` | No alert has that `id`, or the `id` is not a whole number (no route matches it) |
| `409` | Re-arming an alert that is already armed (`Alert 1 is already armed.`), or a concurrent write won the race |
| `422` | Creating an alert on a coin CoinGecko does not know, or that has no price in that vs_currency; the `detail` names it |
| `429`, `502`, `504` | CoinGecko failed while checking a new alert's coin or while evaluating; evaluating an alert whose coin CoinGecko no longer knows is a `502`, as described under [Alert evaluation](#alert-evaluation) |

### Health, OpenAPI, and Scalar

- `GET /health` — a liveness probe, available in every environment.
- `GET /openapi/v1.json` — the generated OpenAPI document, available in every
  environment.
- `GET /scalar/v1` — an interactive Scalar UI over the OpenAPI document,
  mapped in the Development environment only.

## Build, test, and format

```bash
dotnet build
dotnet test
dotnet format
```

CI (`.github/workflows/ci.yml`) runs the same three commands — build and test
in the `Release` configuration, plus a format-verification check — on both
`ubuntu-latest` and `windows-latest`, on every push.

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
