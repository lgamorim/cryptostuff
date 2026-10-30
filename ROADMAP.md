# Roadmap

Plan of record for building cryptostuff.

**Status: Phases 1–4 complete; Phases 5–7 planned.** All fifteen milestones of
Phases 1–4 have shipped, and their sections are kept as the historical plan of
record — see git history (`git log --oneline`) for the commit that closed each
milestone. Phases 5–7 add persistence-backed create, update, and delete
operations; none of their milestones has started, so the two projects they
introduce are not yet in the repo.

The deliverable is a cryptocurrency market data application: one core layer that
owns the domain workflow, an isolated CoinGecko API client beneath it, and two
presentation hosts above it — a console app and a minimal REST API. Phases 5–7
give the application state of its own — watchlists, a portfolio transaction
ledger, and price alerts — stored in a local SQLite database that both hosts
share.

## Product scope

| Capability                       | CLI command                                   | HTTP route                          |
|----------------------------------|-----------------------------------------------|-------------------------------------|
| Live coin prices                 | `price <coins> <currencies>`                   | `GET /prices`                       |
| Token prices by contract address | `token <platform> <addresses> <currencies>`    | `GET /token-prices`                 |
| Historical market series         | `history <coin> <currency> <days>`             | `GET /historical-market-data`       |
| Coin detail                      | `coin <id>`                                    | `GET /coins/{coin}`                 |
| Developer repository activity    | `developer <id> <date>`                        | `GET /coins/{coin}/developer-data`  |

The API additionally serves `GET /health` and an OpenAPI document, with a Scalar
UI in Development only.

Phases 5–7 add the persisted capabilities below, with the same CLI/API parity.
CLI commands are noun-first (`watchlist <verb> …`); `<method>` is `average-cost`
or `fifo`, `<kind>` is `buy` or `sell`, and `<direction>` is `above` or
`below`.

| Capability             | CLI command                                                                                                      | HTTP route                                                                                     |
|------------------------|------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| Manage watchlists      | `watchlist list`, `create <name>`, `show <id>`, `rename <id> <name>`, `delete <id>`                               | `GET`, `POST /watchlists`; `GET`, `PUT`, `DELETE /watchlists/{id}`                             |
| Watchlist coins        | `watchlist add <id> <coin>`, `remove <id> <coin>`                                                                 | `PUT`, `DELETE /watchlists/{id}/coins/{coin}`                                                  |
| Watchlist prices       | `watchlist prices <id> <currency>`                                                                               | `GET /watchlists/{id}/prices`                                                                  |
| Manage portfolios      | `portfolio list`, `create <name> <currency> <method>`, `show <id>`, `update <id> <name> <method>`, `delete <id>` | `GET`, `POST /portfolios`; `GET`, `PUT`, `DELETE /portfolios/{id}`                             |
| Portfolio transactions | `portfolio tx add <id> <kind> <coin> <qty> <price> <when> [--fee <fee>]`, `tx edit <id> <txid> …`, `tx delete <id> <txid>` | `POST /portfolios/{id}/transactions`; `PUT`, `DELETE /portfolios/{id}/transactions/{txId}` |
| Portfolio valuation    | `portfolio value <id>`                                                                                           | `GET /portfolios/{id}/valuation`                                                               |
| Manage price alerts    | `alert list`, `create <coin> <currency> <direction> <threshold>`, `show <id>`, `update <id> <direction> <threshold>`, `delete <id>`, `rearm <id>` | `GET`, `POST /alerts`; `GET`, `PUT`, `DELETE /alerts/{id}`; `POST /alerts/{id}/rearm` |
| Evaluate alerts once   | `alert check`                                                                                                    | `POST /alerts/check`                                                                           |

The API host also evaluates armed alerts periodically in the background; the
CLI, which exits after each command, evaluates only on `alert check`.

Upstream endpoints consumed: `simple/price`, `simple/token_price/{platform}`,
`coins/{id}/market_chart`, `coins/{id}`, and `coins/{id}/history`. Requests are
authenticated with a CoinGecko demo API key read from `CoinGecko:ApiKey` — user
secrets locally, the `CoinGecko__ApiKey` environment variable in deployment —
and sent as the `x-cg-demo-api-key` header.

## Target structure

Thirteen projects once Phase 5 lands: six under `src/`, seven under `test/`.
Phases 1–4 built the first eleven; Milestone 5.3 adds `CryptoStuff.Persistence`
and `CryptoStuff.Persistence.IntegrationTests`, along with the local tool
manifest that pins `dotnet-ef`.

```
cryptostuff.slnx
global.json                       Directory.Build.props
Directory.Packages.props          .editorconfig
.github/workflows/ci.yml          .gitattributes
.config/dotnet-tools.json         (Phase 5) pins dotnet-ef

src/CryptoStuff.CoinGecko/        API client — sole owner of the wire format
src/CryptoStuff.Core/             Queries, view records, results, mapping, caching, validation;
                                  (Phases 5–7) domain entities, repository interfaces, feature services
src/CryptoStuff.Persistence/      (Phase 5) EF Core + SQLite: DbContext, mappings, repositories, migrations
src/CryptoStuff.Composition/      Registration and HTTP pipeline wiring
src/CryptoStuff.Cli/              Console host
src/CryptoStuff.Api/              Minimal REST host

test/CryptoStuff.CoinGecko.UnitTests/
test/CryptoStuff.Core.UnitTests/
test/CryptoStuff.Composition.UnitTests/
test/CryptoStuff.Cli.UnitTests/
test/CryptoStuff.Api.UnitTests/
test/CryptoStuff.Api.IntegrationTests/
test/CryptoStuff.Persistence.IntegrationTests/   (Phase 5)
```

Both hosts reference `CryptoStuff.Composition`, which references
`CryptoStuff.Core`, which references `CryptoStuff.CoinGecko`. From Phase 5,
`CryptoStuff.Composition` also references `CryptoStuff.Persistence`, which
references `CryptoStuff.Core`. Dependencies point inward; no lower layer
references a layer above it, and `CryptoStuff.Core` never references EF Core.

## Definition of done

Every milestone is one `feature/`-prefixed branch, developed test-first, and
squash-merged into `master` only when all of the following hold:

- `dotnet build` completes with zero warnings.
- `dotnet format --verify-no-changes` reports no changes.
- `dotnet test` is green.
- The README reflects everything the milestone added.

Each milestone below states its README delta explicitly. Documentation is not a
phase of its own; it accrues continuously.

## Decisions taken

- **Project naming** uses the `CryptoStuff.*` prefix throughout.
- **Composition lives in its own project**, not in the core layer. Registration
  and the HTTP resilience pipeline are infrastructure concerns; keeping them out
  of `CryptoStuff.Core` leaves that layer free of dependency-injection and
  resilience package references, and avoids duplicating registration across two
  hosts.
- **The caching decorator depends on `ICryptocurrencyService`**, not on the
  concrete service. The undecorated implementation is registered under a service
  key and the decorator resolves it by that key, so further decoration stays
  possible.
- **FluentAssertions is pinned to v7**, whose licence terms carry no commercial
  restriction.
- **Test framework is xUnit v3 on Microsoft.Testing.Platform**, not classic
  VSTest — chosen in Milestone 1.2, matching the `OutputType=Exe`
  test-project scoping Milestone 1.1 already added in anticipation of it.

Taken for Phases 5–7:

- **EF Core with SQLite, confined to `CryptoStuff.Persistence`.** Core owns the
  domain entities and the repository interfaces and stays free of EF; the
  persistence project implements them, and Composition wires them in. Mapping
  is never done with data annotations on Core types, per the
  `persistence-efcore` rules overlay imported in Milestone `5.0`. Of the two
  places the overlay allows, the project uses only `IEntityTypeConfiguration<T>`
  types — one per entity, applied from the assembly — and keeps
  `OnModelCreating` empty of entity mapping, so each feature's mapping sits
  beside its repository.
- **One database file, shared by both hosts.** The default is
  `cryptostuff/cryptostuff.db` under the per-user local application-data folder,
  so the CLI and the API see the same data wherever they are launched from.
  `ConnectionStrings:CryptoStuff` overrides it.
- **Migrations are checked in and applied automatically** when either host
  starts. A design-time `DbContext` factory in the persistence project means
  `dotnet ef` never needs a CoinGecko key, and `dotnet-ef` is pinned as a local
  tool. The scaffolded `Migrations/` folder is marked `generated_code` in
  `.editorconfig`, so the style analyzers and `dotnet format` skip tool output.
- **Provider behaviour is tested against real SQLite database files.**
  Translation, migrations, unique constraints, and concurrency tokens are
  asserted only in `.IntegrationTests` projects, each test against its own
  temporary database file — the same engine and the same file-backed storage
  the application ships with, so the overlay's Testcontainers/Npgsql example
  doesn't apply. Files live in a `cryptostuff-tests` folder under the OS temp
  directory, named by GUID so parallel tests never share one, opened with
  `Pooling=False` and deleted on dispose; tests always set the connection
  string explicitly, so they never touch the user's real database. The overlay
  also permits the EF in-memory provider for mapping smoke tests; the project
  chooses not to use it, because the integration tests already check the
  mapping against the real engine. Core unit tests use hand-written fake
  repositories.
- **Persistence services are scoped.** The `DbContext`, repositories, and feature
  services live per scope; `ICryptocurrencyService` stays a singleton. The CLI
  uses one scope per invocation, and the alert background service one per tick.
- **Integer ids and case-insensitive unique names.** SQLite autoincrement keys
  keep CLI commands short and are never reused after a delete; watchlist and
  portfolio names are unique under `NOCASE` collation.
- **Decimals are stored as TEXT and computed in memory.** SQLite has no decimal
  type; TEXT keeps values exact, and no query orders or aggregates on a decimal
  or timestamp column server-side.
- **Write failures get their own error codes.** `Conflict` (`409`) for a
  duplicate name or a lost concurrency race, `RuleViolated` (`422`) for a broken
  domain rule such as an oversell or an unknown coin, and `PreconditionFailed`
  (`412`) for a stale `If-Match`. A failure may carry a detail message, which
  the API puts in ProblemDetails `detail` and the CLI prints. A non-generic
  `ServiceResult` covers operations with no value to return.
- **Coins are validated against CoinGecko on write**, so a stored id can never
  break a later price lookup. The CoinGecko API key therefore stays mandatory for
  every command.
- **Optimistic concurrency.** Portfolios and alerts carry a version token; a
  lost race is a `Conflict`. The portfolio API also exposes it as an `ETag` and
  honours an optional `If-Match`. The CLI, driven by one user, does not use
  ETags.
- **Updates replace the whole resource**, so CLI arguments stay positional; the
  one new option is `--fee`, beside `--json`.
- **Enumerations travel as lowercase text tokens** (`average-cost`, `fifo`,
  `buy`, `sell`, `above`, `below`, `armed`, `triggered`), parsed in Core so both
  hosts accept identical input and print identical output.
- **The ledger records Buy and Sell only**, each with an optional fee. Transfers
  are out of scope.
- **Cost basis uses the Strategy pattern**: average cost and FIFO, selectable
  per portfolio. Two algorithms chosen at runtime would otherwise be a switch
  repeated through the valuation code.
- **One alert evaluator, two triggers.** Core owns the evaluation; the API host
  runs it periodically in a background service, and the CLI's `alert check` and
  `POST /alerts/check` run it once. A triggered alert is recorded in its state
  and logged — no outbound notifications.
- **Background evaluation uses a plain `BackgroundService`, not a job
  library.** Alert evaluation is one job on a fixed interval in one process,
  and a missed or doubled tick is harmless: the next tick catches up, and the
  version token turns a race into a skipped alert. A `PeriodicTimer` driven by
  `TimeProvider` covers that with no new dependency and stays testable through
  a fake clock. Hangfire was considered and rejected: its SQLite storage is
  community-maintained, creates and migrates its own tables outside the
  checked-in EF migrations, and would contend with both hosts for the shared
  database file, while its durable queues, retries, and dashboard would go
  unused. Quartz.NET and Coravel were rejected as schedulers for a single
  fixed interval. Revisit if alerts gain outbound notifications that need
  durable retries, if the API runs as more than one instance, or if several
  scheduled jobs appear.

---

## Phase 1 — Foundations

### Milestone 1.1 — `feature/repo-foundations`

`global.json` pinning the SDK, `Directory.Packages.props` for central package
management, and `.gitattributes`. Amend `Directory.Build.props` to add
`ManagePackageVersionsCentrally`, and scope the test-project properties
(`OutputType`, `IsPackable`, `NoWarn`) with a condition matching `Tests` rather
than `UnitTests`, so the integration-test project is not silently excluded.

The CI workflow is deliberately deferred to Milestone 1.2: it builds the
solution file, which does not exist until then, and adding it here would put a
red build on `master` for the duration of one milestone.

**README:** title and description, prerequisites (pinned SDK version, CoinGecko
demo key), and the build, test, and format commands.

### Milestone 1.2 — `feature/solution-skeleton`

Ten of the eleven projects and `cryptostuff.slnx`, each test project carrying
one trivial passing test to prove the harness runs. Add the CI workflow:
build, test, and format check, on `ubuntu-latest` and `windows-latest`, in
Release.

`CryptoStuff.Api.IntegrationTests` is deliberately deferred to Milestone 4.3:
`archetype/application.md` only licenses an `.IntegrationTests` project once
it exercises a real dependency, and this one has nothing to exercise until
the API host exists.

**README:** the project table and the dependency map, plus a CI badge and a note
on what CI runs. Also update `CLAUDE.md`, whose "No code yet" note goes stale
the moment this lands.

## Phase 2 — API client

### Milestone 2.1 — `feature/client-contracts`

The client interface, the response contract carrying success plus HTTP status
code plus a timeout flag, the base-address and API-key configuration helper, and
the URL-encoding helpers. Build the fake `HttpMessageHandler` test harness here;
every later milestone reuses it. No test performs real network I/O, ever.

**README:** the configuration section — where the API key comes from locally and
in deployment, and how it reaches CoinGecko.

### Milestone 2.2 — `feature/simple-price-endpoints`

Simple price and simple token price. Both parse the same nested
coin-to-currency-to-value price matrix. Tests cover comma-joined and
percent-encoded identifiers, the happy path, HTTP failure with the status code
captured, malformed JSON, an empty body, and a timeout distinguished from
caller-initiated cancellation.

**README:** the client project's row, listing the upstream endpoints covered so
far.

### Milestone 2.3 — `feature/market-chart-endpoint`

The market chart endpoint, including the custom JSON converter for CoinGecko's
two-element `[unixMilliseconds, value]` rows. Tests cover a round trip, the
wrong element count, a non-numeric element, and all three returned series.

**README:** extend the endpoint coverage list.

### Milestone 2.4 — `feature/coin-detail-endpoints`

Coin data and coin history. Both wire formats are large and deeply optional, so
tests must cover absent market data, absent developer data, and an absent nested
code-churn object.

**README:** endpoint coverage list complete; note the `dd-MM-yyyy` date format
the history endpoint requires.

## Phase 3 — Core

### Milestone 3.1 — `feature/service-result`

The result type, the error-code enumeration, and the mapping from HTTP status to
error code: `404` to not-found, `429` to rate-limited, a timeout to
request-timed-out, and anything else to upstream-unavailable. A small milestone,
but every later type depends on its shape.

**README:** the error-code table — each code, its meaning, and the upstream
condition that produces it.

### Milestone 3.2 — `feature/core-service`

Five query records in, five view records out, and the mapping between them:
price matrices flatten into per-coin price lists, Unix millisecond timestamps
become `yyyy-MM-dd` strings under the invariant culture, the English description
is selected out of the localization dictionary, the image URL falls back from
large to small to thumbnail, and per-currency snapshots join price, market cap,
and volume. Failure propagation and missing-identifier detection — a requested
coin absent from the upstream response is a not-found failure, not a silent gap
— also land here.

This is the largest milestone in the roadmap; budget accordingly.

**README:** the core project's row with the query and view type names, and the
documented missing-identifier behaviour.

### Milestone 3.3 — `feature/input-validation`

The shared validators: non-empty scalar, non-empty collection, positive day
count, and a well-formed `dd-MM-yyyy` date. Small, and both hosts consume it, so
it precedes them.

**README:** the validation rules, described as behaviour a user will encounter.

### Milestone 3.4 — `feature/response-caching`

The caching decorator: configurable time-to-live, successes cached and failures
not, keyed per coin and per coin-and-date. Prices and historical series stay
uncached — they change too quickly to be worth it. Tests drive expiry through a
fake clock or the cache itself; no `Task.Delay`.

**README:** the `CoinGecko:CacheSeconds` setting, its default, and which
operations it affects.

### Milestone 3.5 — `feature/http-resilience`

Retry on `429` with `Retry-After` honoured in both its delta and absolute-date
forms, a bounded attempt count, and constant backoff. Lives in the composition
project, so the resilience packages stay out of the core layer.

**README:** rate-limit behaviour — that retries happen automatically, and when a
rate-limited failure still reaches the caller.

### Milestone 3.6 — `feature/composition-project`

The registration extension: read the API key, fail fast with an actionable
message when it is absent, wire the typed `HttpClient` with the resilience
pipeline, register the undecorated service under a key, and resolve the
interface to the caching decorator.

**README:** the exact error message shown when the key is missing, and the
`dotnet user-secrets set` command that resolves it.

## Phase 4 — Hosts

### Milestone 4.1 — `feature/cli-host`

Argument parsing, the runner — with its output and error writers injected so
tests assert on strings rather than the console — output formatting, the `--json`
flag, the usage message, and the exit codes: `0` on success, `1` on a usage,
validation, or service failure, and `130` on cancellation. The program entry
point composing it all follows.

**README:** the full CLI section — how to invoke it, the five commands with
argument tables and worked examples, the `--json` flag, and the exit-code table.

### Milestone 4.2 — `feature/rest-api-host`

The five routes, the mapping from service result to HTTP result (`404`, `429`,
`504`, `502`, and `500`), validation failures returned as `400` ProblemDetails,
the health endpoint, the OpenAPI document, the Scalar UI gated to Development,
and the `appsettings` files.

**README:** the API section — how to run it, the route table, the ProblemDetails
contract, and where to find the OpenAPI document, the Scalar UI, and the health
check.

### Milestone 4.3 — `feature/api-integration-tests`

The eleventh project, `CryptoStuff.Api.IntegrationTests`, held back from
Milestone 1.2 until there was a real dependency for it to exercise. A web
application factory over the API with a fake CoinGecko client substituted,
asserting real status codes and JSON response bodies end to end. This is the
first point at which the whole stack runs together.

**README:** final pass — the integration-test project row, a note that the suite
needs neither an API key nor network access, and a read-through of the whole
document for drift accumulated across fifteen milestones.

## Phase 5 — Persistence and watchlists

### Milestone 5.0 — `feature/persistence-efcore-rules`

Import the `persistence-efcore` overlay from the `claude-rules` repo before any
persistence code exists, so every later milestone is written and reviewed
against it. Audit the current rule set with `sync.ps1 -Check` first, sync with
`-Add workflow-agent-review-solo,persistence-efcore`, add the overlay's
`@import` to `CLAUDE.md`, update the sync command recorded there to name both
overlays, and re-run `-Check` to confirm the new set is in sync. Rules only, so
there is no red step.

**README:** none — the rule set is contributor tooling, documented in
`CLAUDE.md`.

### Milestone 5.1 — `feature/write-contracts`

The write-side additions to the result contract: the `Conflict`,
`RuleViolated`, and `PreconditionFailed` error codes, an optional detail message
on a failure, and a non-generic `ServiceResult` for operations with nothing to
return. The generic result moves to `ServiceResultOfT.cs`, following ASP.NET
Core's naming. Both hosts map the new codes — `409`, `422`, and `412` in the API,
a message each in the CLI — and prefer a failure's detail over the fixed text
when one is present. Every existing call site keeps compiling unchanged.

**README:** the error-code table, with `NotFound` broadened to cover a missing
persisted record, and the REST status table.

### Milestone 5.2 — `feature/watchlist-domain`

The watchlist entity — a name and an ordered, distinct list of coin ids — its
repository interface, and the watchlist service: list, get, create, rename,
delete, add a coin, remove a coin, and price the watchlist. A duplicate name is
a `Conflict`, including when a concurrent save loses the race; a coin CoinGecko
doesn't know is a `RuleViolated` with a detail naming it; adding a coin twice or
removing an absent one is a no-op. Pricing reuses `GetPricesAsync`, and an empty
watchlist makes no upstream call. Shared name and id validators land here, ready
for portfolios. Tests use a hand-written in-memory repository; no EF yet.

**README:** the core project's row lists the watchlist types.

### Milestone 5.3 — `feature/sqlite-persistence`

The twelfth and thirteenth projects. `CryptoStuff.Persistence` holds the
`DbContext`, the watchlist mapping, the repository, translation of SQLite
unique-constraint and concurrency failures into a persistence conflict, a
migrate helper that creates the database directory, the design-time factory,
and the `InitialCreate` migration. `CryptoStuff.Persistence.IntegrationTests`
exercises it against real SQLite, using a temporary database file per test;
its test helper deletes each test's file on dispose and, once at the start of
each run, sweeps files more than a day old left by a crashed run. Also the
`dotnet-ef` local tool manifest and the `.editorconfig` section marking
`Migrations/` as generated code.

Tests assert through a fresh context so the change tracker cannot mask a missing
write: round trips keep coin order, a case-only duplicate name is a conflict,
ids are not reused after a delete, and the model has no pending changes — the
guard against a forgotten migration. Confirm the committed migration files are
LF, not just the working copy.

**README:** the two project rows, the dependency map, and a contributor note on
adding a migration.

### Milestone 5.4 — `feature/persistence-composition`

Wire persistence into both hosts. `AddCryptoStuff` resolves the connection
string — the configured value, or the per-user default path, failing fast with
an actionable message when no application-data folder exists — and registers the
scoped context, repository, and service. A migrate extension runs before either
host serves anything: the API before it starts listening, the CLI before it
runs a command, exiting `1` if the database cannot be opened or migrated. The
CLI builds its provider with scope validation on and resolves from a scope. The
API integration-test factory gives each test its own temporary database file, so
the existing suite now runs against a migrated database. Measure the CLI's added
start-up cost here.

**README:** a data-storage section — the default path on each OS, how to
override it, and that migrations run automatically — and the new exit-code `1`
cause.

### Milestone 5.5 — `feature/watchlist-cli`

The `watchlist` command family: `list`, `create`, `show`, `rename`, `delete`,
`add`, `remove`, and `prices`, with text and `--json` output. A
behaviour-preserving refactor comes first, extracting the runner's output and
argument helpers so each command family can live in its own class while the
existing CLI tests pass untouched.

**README:** the watchlist rows in the CLI command table.

### Milestone 5.6 — `feature/watchlist-api`

The `/watchlists` routes, returning `201` with a `Location` header on create and
`204` on delete; adding and removing a coin are idempotent `PUT` and `DELETE`
on `/watchlists/{id}/coins/{coin}`. A refactor comes first, extracting the
endpoint validation, result mapping, and OpenAPI metadata helpers so the new
route group can share them. Integration tests run the whole flow against real
SQLite, including `409`, `404`, `422`, and `400` responses.

**README:** the watchlist routes in the API table, with request-body examples.

## Phase 6 — Portfolio ledger

### Milestone 6.1 — `feature/portfolio-ledger`

The portfolio — a name, a fixed vs_currency, a cost-basis method, and a version
— and its transactions: buy or sell, a coin, a positive quantity, a
non-negative unit price, an optional fee, and a UTC timestamp. The invariant:
replaying a portfolio's transactions in time order, with an insertion sequence
breaking ties, never takes a coin's holding below zero. It is checked on every
add, edit, and delete, because deleting an earlier buy or moving a sell earlier
can break a later transaction. A breach is a `RuleViolated` naming the coin and
date. Future-dated transactions are rejected through `TimeProvider`.

**README:** the core project's row lists the portfolio types.

### Milestone 6.2 — `feature/portfolio-persistence`

The portfolio and transaction mappings, the `AddPortfolios` migration, cascade
delete, and the version as a concurrency token. Integration tests prove a stale
version is a conflict and that decimal amounts round-trip exactly.

**README:** the data-storage section, if the schema change needs a note there.

### Milestone 6.3 — `feature/cost-basis-methods`

Average cost and FIFO as two implementations of one cost-basis strategy, each
reducing one coin's ordered transactions to a position: quantity held, remaining
cost basis, and realized profit and loss. Fees add to cost on a buy and reduce
proceeds on a sell. Tests pin worked examples where the two methods diverge, a
sell spanning several FIFO lots, a full exit followed by a new buy, and average
cost returning to exactly zero with the quantity.

**README:** how each cost-basis method works, with a worked example.

### Milestone 6.4 — `feature/portfolio-valuation`

Valuation: one `GetPricesAsync` call for the open positions — none when nothing
is held — joined with the cost-basis positions to give market value, cost basis,
and unrealized and realized profit and loss per coin and in total. Core does no
rounding.

**README:** what each valuation figure means.

### Milestone 6.5 — `feature/portfolio-cli`

The `portfolio` command family, including `portfolio tx add`, `edit`, and
`delete`, the `--fee` option, and `portfolio value`. Numbers and timestamps are
parsed under the invariant culture.

**README:** the portfolio rows in the CLI command table.

### Milestone 6.6 — `feature/portfolio-api`

The `/portfolios` routes, the transaction sub-resource, and the valuation route.
Portfolio responses carry an `ETag`; writes honour an optional `If-Match`.
Integration tests cover an oversell (`422`), a stale `If-Match` (`412`), and a
concurrent write (`409`).

**README:** the portfolio routes and the `ETag` contract.

## Phase 7 — Price alerts

### Milestone 7.1 — `feature/price-alerts`

The price alert — a coin, a vs_currency, a direction, a positive threshold, a
state, and when and at what price it last triggered — and the alert service.
An alert is met inclusively: at or above the threshold for `above`, at or below
it for `below`. Triggering moves it from armed to triggered; re-arming moves it
back, and re-arming an armed alert is a `Conflict`. Editing the direction or
threshold re-arms it. Creation validates the coin and currency together.

**README:** the core project's row lists the alert types.

### Milestone 7.2 — `feature/alert-persistence`

The alert mapping, the `AddPriceAlerts` migration, a query for armed alerts, and
the version token, with integration tests.

**README:** the data-storage section, if the schema change needs a note there.

### Milestone 7.3 — `feature/alert-evaluator`

The single evaluator both hosts use: load the armed alerts, fetch every needed
coin and currency in one `GetPricesAsync` call — none when nothing is armed —
trigger each alert that is met, timestamped through `TimeProvider`, and report
what triggered. An upstream failure leaves every alert armed and propagates; an
alert changed concurrently is skipped until the next run.

**README:** how and when alerts are evaluated.

### Milestone 7.4 — `feature/alert-cli`

The `alert` command family, including `rearm` and `check`, which runs the
evaluator once and prints what triggered.

**README:** the alert rows in the CLI command table, and a note that continuous
evaluation happens only in the API host.

### Milestone 7.5 — `feature/alert-api`

The `/alerts` routes, `POST /alerts/{id}/rearm`, `POST /alerts/check`, and the
background service that runs the evaluator on a `PeriodicTimer` driven by
`TimeProvider`. Each tick runs in a fresh scope, logs each trigger, and catches
every failure so one bad tick cannot stop the host. The interval comes from
`Alerts:EvaluationSeconds`, defaulting to `60` and validated at start-up like
`CoinGecko:CacheSeconds`. Unit tests drive the timer through a fake clock; the
integration-test factory removes the background service so tests stay
deterministic.

**README:** the alert routes, the `Alerts:EvaluationSeconds` setting, and what a
triggered alert looks like.
