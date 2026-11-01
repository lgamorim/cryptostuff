# cryptostuff

[![CI](https://github.com/lgamorim/cryptostuff/actions/workflows/ci.yml/badge.svg)](https://github.com/lgamorim/cryptostuff/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

A cryptocurrency market data application: a core layer that owns the domain
workflow, an isolated CoinGecko API client beneath it, and two presentation
hosts above it — a console app and a minimal REST API. See
[`ROADMAP.md`](ROADMAP.md) for the full plan of record; Phases 5–7, which add
watchlists, a portfolio ledger, and price alerts stored in SQLite, are planned
there but not yet built.

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
| `CryptoStuff.CoinGecko` | API client — sole owner of the wire format; covers `simple/price`, `simple/token_price/{platform}`, `coins/{id}/market_chart`, `coins/{id}`, and `coins/{id}/history` (the latter's `date` query parameter must be `dd-MM-yyyy`) |
| `CryptoStuff.Core` | Queries (`CoinPriceQuery`, `TokenPriceQuery`, `CoinMarketChartQuery`, `CoinQuery`, `CoinDeveloperDataQuery`) and view records (`CoinPriceView`, `TokenPriceView`, `CoinMarketChartView`, `CoinView`, `CoinDeveloperDataView`), results, mapping, caching, validation; watchlists (`Watchlists/`): the `Watchlist` entity, `IWatchlistRepository`, `IWatchlistService` and `WatchlistService`, the `CreateWatchlistCommand`, `RenameWatchlistCommand` and `WatchlistCoinCommand` commands, the `WatchlistPriceQuery` query, the `WatchlistView` view, and `WatchlistMapper`; and, shared by the stored features, `PersistenceConflictException` and the `NameValidator` and `PositiveIdValidator` rules |
| `CryptoStuff.Persistence` | EF Core over SQLite: `CryptoStuffDbContext`, the watchlist mapping (`WatchlistConfiguration`) and `WatchlistRepository`, translation of unique-constraint and concurrency failures into `PersistenceConflictException`, `CryptoStuffDatabase.MigrateAsync` (creates a file database's directory, then migrates), the design-time context factory, and the checked-in migrations (`Migrations/`) |
| `CryptoStuff.Composition` | Registration and HTTP pipeline wiring: `AddCryptoStuff`, `DatabaseConnectionString` (locates the shared database), and `MigrateCryptoStuffDatabaseAsync` (the start-up migration both hosts run) |
| `CryptoStuff.Cli` | Console host |
| `CryptoStuff.Api` | Minimal REST host |
| `CryptoStuff.CoinGecko.UnitTests` | Unit tests for `CryptoStuff.CoinGecko` |
| `CryptoStuff.Core.UnitTests` | Unit tests for `CryptoStuff.Core` |
| `CryptoStuff.Composition.UnitTests` | Unit tests for `CryptoStuff.Composition` |
| `CryptoStuff.Cli.UnitTests` | Unit tests for `CryptoStuff.Cli` |
| `CryptoStuff.Api.UnitTests` | Unit tests for `CryptoStuff.Api` |
| `CryptoStuff.Api.IntegrationTests` | Integration tests over `CryptoStuff.Api`, hosted for real with only its CoinGecko client faked and each test's host on a migrated temporary database file of its own — needs neither an API key nor network access, and never touches the real database |

`CryptoStuff.Cli` and `CryptoStuff.Api` both reference `CryptoStuff.Composition`,
which references `CryptoStuff.Core`, which references `CryptoStuff.CoinGecko`.
`CryptoStuff.Composition` also references `CryptoStuff.Persistence`, which
references `CryptoStuff.Core`, implementing its repository interfaces.
Dependencies point inward; no lower layer references a layer above it, and
`CryptoStuff.Core` never references EF Core.

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
[shared database](#data-storage): the EF Core context, `IWatchlistRepository`,
and `IWatchlistService`. Both hosts reference `CryptoStuff.Composition` to
call it.

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

Watchlists are stored in a single SQLite database file that both hosts share:
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

There is no setup step. Both hosts create the file and its folder when they
are missing and apply any pending [migrations](#database-migrations) at start
up — the API before it starts listening, the CLI before it runs a command that
uses the database (the `watchlist` commands) — so whichever host runs first
after an upgrade brings the schema up to date. The CLI's CoinGecko-only
commands (`price`, `token`, `history`, `coin`, `developer`) never open the
database, so they skip migration and start as fast as they always did. If the
database cannot be opened or migrated (an unwritable folder, or a file that is
not a SQLite database), the CLI prints
`Error: the database could not be opened or migrated. …` and exits with code
`1`, and the API fails to start with the underlying exception.

## Command-line interface

`CryptoStuff.Cli` is a console front end over the same five operations as
`ICryptocurrencyService`, plus the `watchlist` commands that manage stored
watchlists. Run it with `dotnet run --project src/CryptoStuff.Cli
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

Each command applies the applicable [input validation](#input-validation)
rules above to its arguments before calling CoinGecko, and reports the first
violation it finds. The non-empty-collection rule doesn't apply here: a
comma-separated list argument can never be truly empty, only contain a blank
entry (e.g. `bitcoin,,ethereum`), which is reported as such.

The `watchlist` commands take a watchlist's id as a positive whole number, which
`create` prints and `list` shows; anything else is reported as a validation
failure. They read and write the shared [database](#data-storage), so these are
the only commands that open it (and migrate it first). A failure the service
reports for them — an unknown id, a duplicate name, a coin CoinGecko doesn't
know — is printed as `Error: …` with the service's own message, and exits `1`.
`delete` prints `Deleted watchlist <id>.`; with `--json` it prints
`{"Id": <id>, "Deleted": true}`, indented.

### JSON output

Add `--json` anywhere on the command line to get indented JSON instead of the
default human-readable text:

```bash
dotnet run --project src/CryptoStuff.Cli -- coin bitcoin --json
```

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
`ICryptocurrencyService`. Run it with:

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

Each route applies the applicable [input validation](#input-validation) rules
above to its parameters before calling CoinGecko, and reports the first
violation it finds. As with the CLI, the non-empty-collection rule doesn't
apply here: a comma-separated list parameter can never be truly empty, only
contain a blank entry (e.g. `coins=bitcoin,,ethereum`), which is reported as
such.

A successful request returns `200` with the matching view record as JSON,
serialized with ASP.NET Core's default (camelCase) naming — a validation
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
