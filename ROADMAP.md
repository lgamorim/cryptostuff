# Roadmap

Plan of record for building cryptostuff.

**Status: Phases 1–7 complete; Phase 8 planned.** All thirty-three milestones
of Phases 1–7 have shipped — the fifteen of Phases 1–4 and the eighteen of
Phases 5–7, which added persistence-backed create, update, and delete
operations — and every project those phases name in
[Target structure](#target-structure) exists and is built out. Their sections
are kept as the historical plan of record — see git history
(`git log --oneline`) for the commit that closed each milestone. Phase 8's
seven milestones, which add CoinMarketCap as a second market data provider,
have not started.

Between Phases 7 and 8 a polish pass hardened what existed, as single changes
that are not milestones of this plan: an API key on every REST route but
health and OpenAPI (`Api:Key`, sent as `X-Api-Key`); a readiness probe at
`GET /health/ready`; counters for upstream requests, rate-limit retries, and
cache lookups on the `cryptostuff.upstream` meter; a container image for the
API, with a smoke check in CI; `UpstreamUnavailable` for a request that gets
no response at all; each coin's allocation in a portfolio valuation; and the
`lifo` and `hifo` cost-basis methods. The README describes each. The sections
for Phases 1–7 predate them and do not mention them; Phase 8's decisions and
milestones account for the ones a second provider touches.

The deliverable is a cryptocurrency market data application: one core layer that
owns the domain workflow, an isolated CoinGecko API client beneath it, and two
presentation hosts above it — a console app and a minimal REST API. Phases 5–7
give the application state of its own — watchlists, a portfolio transaction
ledger, and price alerts — stored in a local SQLite database that both hosts
share. Phase 8 adds a second API client, for CoinMarketCap, beside the
CoinGecko one, and a setting that chooses between them at start-up.

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

Phase 8 serves the same capabilities, with no new command or route, from
CoinMarketCap when `MarketData:Provider` is `coinmarketcap`. Upstream endpoints
consumed then: `v3/cryptocurrency/quotes/latest`, `v2/cryptocurrency/info`, and
`v3/cryptocurrency/quotes/historical`, on the free Basic plan. Requests are
authenticated with a key read from `CoinMarketCap:ApiKey`, the same way, and
sent as the `X-CMC_PRO_API_KEY` header. CoinMarketCap has no developer data,
so `developer` and `GET /coins/{coin}/developer-data` answer `Unsupported`
under it.

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
src/CryptoStuff.CoinMarketCap/    (Phase 8) second API client — sole owner of its wire format
src/CryptoStuff.Core/             Queries, view records, results, mapping, caching, validation;
                                  (Phases 5–7) domain entities, repository interfaces, feature services
src/CryptoStuff.Persistence/      (Phase 5) EF Core + SQLite: DbContext, mappings, repositories, migrations
src/CryptoStuff.Composition/      Registration and HTTP pipeline wiring
src/CryptoStuff.Cli/              Console host
src/CryptoStuff.Api/              Minimal REST host

test/CryptoStuff.CoinGecko.UnitTests/
test/CryptoStuff.CoinMarketCap.UnitTests/        (Phase 8)
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

Since then the CoinGecko reference has been inverted to match Persistence:
`CryptoStuff.CoinGecko` references `CryptoStuff.Core` and implements its
`ICryptocurrencyService`, `CryptoStuff.Composition` references it directly, and
`CryptoStuff.Core` references no other project. The README's Projects section
describes the structure as it stands.

Phase 8 adds the fourteenth and fifteenth projects, in Milestone 8.4:
`src/CryptoStuff.CoinMarketCap/`, the CoinMarketCap client and its
`ICryptocurrencyService` implementation, and
`test/CryptoStuff.CoinMarketCap.UnitTests/`. It sits where
`CryptoStuff.CoinGecko` does: it references `CryptoStuff.Core`, and
`CryptoStuff.Composition` references it. Neither client project references the
other.

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
  starts (the CLI only before a command that uses the database). A design-time `DbContext` factory in the persistence project means
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

Taken for Phase 8:

- **CoinMarketCap, on its free Basic plan, is the second provider.** Basic
  allows commercial use, serves a year of daily history carrying price, market
  cap, and volume in any currency, and accepts slugs that mostly match
  CoinGecko's ids. CoinPaprika was the runner-up and was rejected: its free
  tier is licensed for personal use only, has no multi-coin price call, serves
  history in `usd` or `btc` only, and puts its CoinGecko id mapping behind a
  paid plan. CoinDesk Data (formerly CryptoCompare) reportedly retired its free
  tier in May 2026, CoinCap's free tier is about 500 credits a month, and
  DefiLlama covers token prices only. Revisit if Basic's terms change.
- **One provider per process, chosen at start-up.** `MarketData:Provider` is
  `coingecko`, the default, or `coinmarketcap`; any other value fails fast.
  Only the selected provider's API key is required. There is no automatic
  failover: the providers differ in currency coverage, series granularity, and
  developer data, so mixing them within one run would make output depend on
  which one answered.
- **CoinGecko ids stay the application's ids.** Stored watchlists, portfolios,
  and alerts stay valid under either provider. The CoinMarketCap adapter sends
  an id as a CoinMarketCap slug — the same text for most coins — through a
  checked-in override table where the two differ (`binancecoin` is `bnb`,
  `ripple` is `xrp`), and maps each result back to the id asked for. Platform
  ids in token queries are translated the same way. A coin with no
  CoinMarketCap match is `NotFound`, as an unknown coin is today, and adding
  an override is a one-line change. Neither Core nor the hosts ever see a
  CoinMarketCap slug.
- **A capability the provider lacks is a result, not an exception.** A new
  `Unsupported` error code (`501`) answers developer data under CoinMarketCap.
  That honours the Liskov rule against throwing `NotSupportedException` with
  one small contract addition. Splitting developer data into its own interface
  was rejected: it would refactor Core, both hosts, and the caching decorator
  for one operation.
- **Host text names no provider.** Messages and log text that say "CoinGecko"
  say "the market data provider" instead, so they stay true under either. So
  does README and comment text that says CoinGecko where it means whichever
  provider is called, such as what the readiness probe leaves alone and what
  the upstream counters count. Text about CoinGecko itself — its API key, its
  ids, its endpoints — keeps the name.
- **A request that gets no response is a result under either provider.** The
  CoinGecko client turns a refused connection, a DNS or TLS failure, or a
  dropped connection into a failed response with no status code, and its
  service maps that to `UpstreamUnavailable`. The CoinMarketCap client and
  service do the same; left as an exception it would surface as a `500`.
- **Metrics name the provider that was called.** The `provider` tag on
  `cryptostuff.upstream.requests` and `cryptostuff.upstream.retries` is
  `coingecko` or `coinmarketcap`, the tokens `MarketData:Provider` takes. Each
  client records its own request counter, with its own fixed set of `endpoint`
  templates and the `outcome` values `CoinGeckoClient` already uses, and
  `RateLimitRetryHandler` is told the provider's name instead of reading
  CoinGecko's. `cryptostuff.cache.lookups` gains no tag: a process has one
  provider.
- **Shared settings leave the CoinGecko section.** `CoinGecko:CacheSeconds`
  becomes `MarketData:CacheSeconds`, because the cache wraps whichever
  provider is selected. The rename is deliberate and keeps no fallback for the
  old key; the README calls it out.
- **Calls are shaped to Basic's limits, not metered against them.** Basic
  converts to one currency per call, so a price lookup makes one call per
  currency, and history is requested at daily granularity. The README states
  each command's credit cost; the client keeps no credit accounting of its
  own. The shipped `Alerts:EvaluationSeconds` of 60 does not fit: with one
  alert currency it spends the month's 15,000 credits in about ten days. The
  default stays, because CoinGecko is the default provider and the interval
  that fits depends on how many alert currencies exist, which start-up cannot
  know. Under CoinMarketCap the operator raises it, and the README says to
  what. A start-up warning and a longer default under CoinMarketCap were both
  rejected for that reason.
- **The container smoke check stays on the default provider.**
  `test/container-smoke.sh` gains no run under `coinmarketcap`. That only the
  selected provider's key is demanded is proved by Composition's unit tests,
  and a second container start would repeat them at the cost of CI time.
- **A price below zero is no price.** Nothing checks today that a live price
  is zero or more. A negative one would give a holding a negative market
  value, put the valuation's totals and unrealized profit and loss wrong with
  it, and take a coin's allocation outside 0 to 100 or, where the priced
  values nearly cancel, overflow the division and fail the valuation with
  `RuleViolated`. Alert evaluation reads live prices the same way. CoinGecko
  is not expected to send one, but a second provider is a second source to
  trust. Core therefore drops such an amount, so the coin comes back without
  one in that currency, as a coin the provider does not quote in it already
  does: a valuation leaves it out of the totals and lists it, an alert on it
  stays armed, and every other coin is still priced. The result does not say
  whether a quote was missing or dropped. Failing the whole lookup as
  `UpstreamUnavailable` was rejected: one bad quote would stop the valuation
  of a whole portfolio. The rule sits in Core, above every provider's
  adapter, and Milestone 8.3 delivers it.

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
runs a command that uses the database, exiting `1` if the database cannot be
opened or migrated. The CLI builds its provider with scope validation on and
resolves from a scope. The API integration-test factory gives each test its own
temporary database file, so the existing suite now runs against a migrated
database. Measure the CLI's added start-up cost here.

The measurement found that migrating before every command added about 640 ms
per CLI run (median 176 ms to 817 ms, Release, Windows), almost all of it EF
Core's cold start rather than the migration check itself (about 28 ms). The
maintainer therefore moved the scope in Milestone 5.5: the CLI migrates only
before database-backed commands, and the CoinGecko-only ones skip migration and
EF Core start-up entirely.

**README:** a data-storage section — the default path on each OS, how to
override it, and that migrations run automatically — and the new exit-code `1`
cause.

### Milestone 5.5 — `feature/watchlist-cli`

The `watchlist` command family: `list`, `create`, `show`, `rename`, `delete`,
`add`, `remove`, and `prices`, with text and `--json` output. A
behaviour-preserving refactor comes first, extracting the runner's output and
argument helpers so each command family can live in its own class while the
existing CLI tests pass untouched. The CLI also stops migrating before every
command: it migrates only before commands that use the database, as decided
after Milestone 5.4's start-up measurement.

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

## Phase 8 — Second market data provider

### Milestone 8.1 — `feature/provider-neutral-text`

Wording only, with no change in behaviour: every message, log text, comment,
test name, and README passage that says CoinGecko where it means whichever
provider is selected says "the market data provider", or in a test name
`Upstream`, as the alert evaluator's tests already do. Text about CoinGecko
itself keeps the name: its API key and the message for a missing one, its
ids, its endpoints, the `CoinGecko` configuration section, and its client
project. It comes first and alone because it touches many files and changes
nothing a user can do, so the milestones after it diff cleanly against it.

The places a search finds today:

- **Messages and log text:** the rate-limit, timeout, and unavailable messages
  in both hosts, the CLI's unpriced-coin line in portfolio output, and the
  background service's unpriced-alert log.
- **Comments:** `Program.cs` on what the readiness probe does not call;
  `AlertCheckRateLimit`, `AlertEndpoints`, and
  `AlertEvaluationBackgroundService`; `PortfolioEndpoints`; and the CLI's
  `CliOutputFormatter` and `PortfolioOutputFormatter`.
- **Tests:** nine method names in the Core unit tests of the alert, portfolio,
  and watchlist services, and a fixture string in the CLI's
  `AlertFailureTests`. Renaming a test changes nothing it checks. The twelve
  method names in `CryptoStuff.Api.IntegrationTests` that say CoinGecko keep
  it: those tests run the CoinGecko adapter itself, over a fake client, so
  the name is about CoinGecko.

That list is a starting point, not the test. The milestone is done when a
search for "CoinGecko" outside `CryptoStuff.CoinGecko` and its tests leaves
only text about CoinGecko itself, plus the doc comments in Core and Composition
that describe the one adapter there is: those change in Milestones 8.6 and
8.7, which add the second. Host tests quote the messages, so they change
first.

**README:** the same rule, among others in the quoted messages under Error
codes, Unpriced coins, Check rate limit, the REST status tables, the CLI's
command table and exit codes, the readiness probe's paragraph, and the prose
of Upstream metrics.

### Milestone 8.2 — `feature/unsupported-error-code`

`ServiceErrorCode.Unsupported`, for an operation the selected provider cannot
serve, carrying a detail that names the provider. The API maps it to `501`
ProblemDetails, and the developer-data route's OpenAPI metadata lists that
response, beside the `401` every protected route lists; the CLI prints a
message and exits `1`. Nothing produces `Unsupported` yet, so tests drive the
host mappings directly, as Milestone 5.1's did.

**README:** the error-code table and the REST status table gain `Unsupported`.

### Milestone 8.3 — `feature/negative-price-guard`

Core drops an amount below zero from a coin or token price result, so the
coin or token comes back without an amount in that currency, as the contract
already allows for a currency the provider does not quote it in. Zero stays a
price. The rule sits at one point both providers' results pass through: a
decorator over `ICryptocurrencyService` beside the caching one is the
candidate, since each provider's service is registered beneath it, and the
milestone opens by confirming that placement with the maintainer. Market
charts and coin detail are shown, not computed from, and are left alone.

Tests cover a negative amount dropped for a coin and for a token, zero and
positive amounts kept, the same coin's other currencies kept, and a failed
result passed through unchanged. The valuation and the alert evaluator already
treat a coin without an amount as unpriced, which their existing tests pin.
The registration is test-driven too: a Composition unit test fails until the
guard is in the chain `ICryptocurrencyService` resolves to. Four existing
Composition tests expect the resolved service to be the caching decorator
itself: one asserts its exact type, and the three on the cache duration read
that duration from it by reflection. All four change if the guard wraps the
cache and stand if the cache wraps the guard, which is part of what confirming
the placement decides.

**README:** Unpriced coins and alert evaluation — a price below zero counts as
no price.

### Milestone 8.4 — `feature/coinmarketcap-client`

The fourteenth and fifteenth projects, `CryptoStuff.CoinMarketCap` and
`CryptoStuff.CoinMarketCap.UnitTests`, added with plain `dotnet sln add`. The
client interface; the options — base address
`https://pro-api.coinmarketcap.com` and the `X-CMC_PRO_API_KEY` header; a
response contract carrying success, HTTP status code, a timeout flag, a
no-response case with neither a status code nor the flag, and CoinMarketCap's
own `status.error_code` and `status.error_message`, so the service can tell
one refusal from another; and the wire types for
`v3/cryptocurrency/quotes/latest`, `v2/cryptocurrency/info`, and
`v3/cryptocurrency/quotes/historical`. Slugs and currencies are comma-joined
and percent-encoded, and quote requests send `skip_invalid=true`, so an
unknown slug is missing from the response instead of failing the call.

The client counts each call on `cryptostuff.upstream.requests`, as
`CoinGeckoClient` does: `provider` is `coinmarketcap`, `endpoint` is one of
the three paths above or `other`, never a URI carrying slugs or dates, and
`outcome` takes the values `CoinGeckoClient` already uses.

The fake `HttpMessageHandler` harness and the recording meter factory are
copied from `CryptoStuff.CoinGecko.UnitTests` rather than shared. The harness
would have two consumers; the meter factory already has three copies, in the
CoinGecko, Composition, and Core unit tests, and this is its fourth. A shared
test library is still left out: it would be a new project and would pull three
existing test projects into this phase. Tests cover the key header, the
encoding, the happy path for each endpoint, an HTTP failure with
CoinMarketCap's error code and message captured, malformed JSON, an empty body,
a timeout distinguished from caller-initiated cancellation, a connection
failure reported as no response rather than thrown, and the request counter's
tags for each endpoint and outcome. No test performs real network I/O. The
wire types follow CoinMarketCap's documentation; Milestone 8.7's live run is
where they meet real responses.

**README:** the Projects section — a `CryptoStuff.CoinMarketCap` row in the
project table, with `CryptoStuff.CoinMarketCap.UnitTests` in its Tests column
and the project count above the table raised to seven; its node and edges in
the dependency diagram; and its row in "Inside the other projects" — and the
Configuration section, which lists the CoinMarketCap endpoints consumed beside
CoinGecko's.

### Milestone 8.5 — `feature/coinmarketcap-id-mapping`

The translation between application ids and CoinMarketCap's: a coin id to a
slug, which is the id itself unless the override table lists it; a slug in a
response back to the id that was requested; and a CoinGecko platform id to a
CoinMarketCap platform for token queries, for the platforms the table lists.
An unlisted platform is `NotFound`. The coin overrides are seeded from the
largest coins by market cap whose ids differ, checked against CoinMarketCap's
`v1/cryptocurrency/map`, which costs no credits, when the milestone is
implemented. Tests pin each override in both directions, the identity fallback,
an unlisted platform, and a guard that no two ids map to one slug.

As implemented, the overrides were checked against coinmarketcap.com's pages
and CoinGecko's keyless public API instead: the map endpoint needs a
CoinMarketCap key, and none was available before Milestone 8.7. Milestone
8.7's live run is the first check against CoinMarketCap's API itself.

**README:** how ids are translated, and how to add an override.

### Milestone 8.6 — `feature/coinmarketcap-service`

The CoinMarketCap `ICryptocurrencyService`, honouring every remark on the
contract:

- **Prices** make one `quotes/latest` call per currency, because Basic converts
  to one currency per call, with every coin in each. Currencies go out upper
  case and come back lower case. A coin missing from a response fails the
  whole call `NotFound`. A currency CoinMarketCap rejects leaves the coins
  without an amount in it, told apart from other `400`s by the parameter the
  error names. When every requested currency is rejected, no call has said
  whether the coins exist, so one more call, in `USD`, settles it: `NotFound`
  if any coin is missing from it, and otherwise the coins come back with no
  amounts. A failure in any one call fails the result, and a query naming no
  coins makes no call. The service applies no rule of its own to a price below
  zero: Milestone 8.3's guard sits above every provider.
- **Token prices** translate the platform, resolve each address through
  `info?address=`, and require the resolved coin's contract list to hold that
  address on that platform, otherwise `NotFound`. They are then priced as
  coins are and keyed by the address as requested. Milestone 8.5's platform
  slug names the platform's coin, which BNB Smart Chain and opBNB share, so
  the check also needs the platform's name, which the Milestone 8.4 wire type
  does not yet read, to tell those two apart.
- **Market chart** makes one `quotes/historical` call at daily interval over
  the requested days, giving the price, market-cap, and volume series. It
  returns fewer points than CoinGecko's hourly granularity for 2 to 90 days,
  but the view's dates are whole days already. A range deeper than Basic's
  year of daily history fails as a plan refusal does, below.
- **Coin detail** takes the description and logo from `info`, and a snapshot
  for each configured detail currency — `usd` by default, one call each — from
  `quotes/latest`. Its id is the application's id, not the slug.
- **Developer data** fails `Unsupported`, naming CoinMarketCap, with no
  upstream call.
- **Errors** map `429` to `RateLimited`, a timeout to `RequestTimedOut`, and
  everything else — the `401`, `402`, and `403` plan refusals, any `5xx`, and
  a request that got no response at all — to `UpstreamUnavailable`.
  CoinMarketCap answers a bad argument with `400`, never `404`, so `NotFound`
  arises only from the missing-coin and address checks above.

Core's doc comments stop describing one adapter: `ICryptocurrencyService`'s
summary no longer names CoinGecko's service as the implementation, and
`ServiceErrorCode.NotFound`'s no longer says the provider returned `404`,
which CoinMarketCap never does. `KnownCoinPrices`' remarks say the same of
"the adapter" without naming CoinGecko, so Milestone 8.1's search does not
find them; they are corrected here.

The largest milestone of the phase; budget accordingly.

**README:** how CoinMarketCap's answers differ from CoinGecko's — the detail
currencies, the daily series, the history depth, and unsupported developer
data.

### Milestone 8.7 — `feature/market-data-provider-switch`

Composition reads `MarketData:Provider`, defaulting to `coingecko` and failing
fast, listing the accepted values, on any other. It registers the selected
provider's typed `HttpClient` — with `RateLimitRetryHandler` and the same
timeout — and its service under the existing undecorated key, so the
decorators and everything above them are unchanged. The handler takes the
provider's name as a constructor argument for its `provider` tag, where it
reads `CoinGeckoClient.ProviderName` today. Only the selected provider's
key is required; a missing `CoinMarketCap:ApiKey` fails fast with a message
mirroring the CoinGecko one. `CoinMarketCap:DetailCurrencies` is read and
validated at start-up, and `CoinGecko:CacheSeconds` becomes
`MarketData:CacheSeconds`, in `appsettings.json` too. CoinMarketCap's `429`
carries no `Retry-After`, and its per-minute window outlasts three 2-second
retries, so a burst still surfaces as `RateLimited`; the handler is not tuned
for it. Composition's doc comments, which describe a CoinGecko-backed service,
are widened to the selected provider.

Composition unit tests resolve the decorated service under each provider,
reject an unknown provider, demand only the selected provider's key, and
check that requests and retries are recorded under the selected provider's
name. The API and CLI unit tests substitute `ICryptocurrencyService` and stay
as they are. `CryptoStuff.Api.IntegrationTests` substitutes `ICoinGeckoClient`
and runs `AddCryptoStuff` for real, so it passes through the rewritten
registration under the default provider and must stay green with its tests
unedited. It gains no CoinMarketCap variant: Composition's unit tests cover
the selection, and the live run below covers the adapter.
`test/container-smoke.sh` stays as it is too, running under the default
provider. Two comments that say a host needs a CoinGecko API key become true
only of the default provider and are reworded to the selected provider's key:
the `Dockerfile`'s header, and `DesignTimeCryptoStuffDbContextFactory`'s
summary in Persistence.

Before the merge, run every CoinGecko-backed CLI command, plus
`watchlist prices`, `portfolio value`, and `alert check`, against the real
CoinMarketCap API with a Basic key, and record the results in the handoff
summary. It is the one point where the adapter meets the real wire format,
which no test exercises; any difference is fixed in the Milestone 8.4 and 8.6
types on this branch.

**README:** Prerequisites — which upstream key is needed, now that it depends
on the provider; the configuration section — the provider setting, the
CoinMarketCap key and its `dotnet user-secrets set` command, the detail
currencies, and the sentence fixing what "CoinGecko API key" and a bare "API
key" mean, extended to "CoinMarketCap API key"; the renamed cache setting
wherever it is named, in Caching and in the CLI's exit codes; the registration
section; rate limiting — each command's credit cost and the per-minute window;
Upstream metrics — `coinmarketcap` among the `provider` values,
CoinMarketCap's client as a recorder of requests, and its endpoint templates;
the Container image section — `MarketData__Provider`, and that the upstream
key to supply is the selected provider's; and alert evaluation — that the
default `Alerts:EvaluationSeconds` of 60 overspends Basic's 15,000 credits a
month, and what to raise it to: about 180 seconds for each distinct alert
currency before any other use.
