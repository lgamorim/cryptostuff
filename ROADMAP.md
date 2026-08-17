# Roadmap

Plan of record for building cryptostuff.

**Status: complete.** All fifteen milestones across all four phases have
shipped; every project in [Target structure](#target-structure) exists and is
built out. The sections below are kept as the historical plan of record — see
git history (`git log --oneline`) for the commit that closed each milestone.

The deliverable is a cryptocurrency market data application: one core layer that
owns the domain workflow, an isolated CoinGecko API client beneath it, and two
presentation hosts above it — a console app and a minimal REST API.

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

Upstream endpoints consumed: `simple/price`, `simple/token_price/{platform}`,
`coins/{id}/market_chart`, `coins/{id}`, and `coins/{id}/history`. Requests are
authenticated with a CoinGecko demo API key read from `CoinGecko:ApiKey` — user
secrets locally, the `CoinGecko__ApiKey` environment variable in deployment —
and sent as the `x-cg-demo-api-key` header.

## Target structure

Eleven projects: five under `src/`, six under `test/`.

```
cryptostuff.slnx
global.json                       Directory.Build.props
Directory.Packages.props          .editorconfig
.github/workflows/ci.yml          .gitattributes

src/CryptoStuff.CoinGecko/        API client — sole owner of the wire format
src/CryptoStuff.Core/             Queries, view records, results, mapping, caching, validation
src/CryptoStuff.Composition/      Registration and HTTP pipeline wiring
src/CryptoStuff.Cli/              Console host
src/CryptoStuff.Api/              Minimal REST host

test/CryptoStuff.CoinGecko.UnitTests/
test/CryptoStuff.Core.UnitTests/
test/CryptoStuff.Composition.UnitTests/
test/CryptoStuff.Cli.UnitTests/
test/CryptoStuff.Api.UnitTests/
test/CryptoStuff.Api.IntegrationTests/
```

Both hosts reference `CryptoStuff.Composition`, which references
`CryptoStuff.Core`, which references `CryptoStuff.CoinGecko`. Dependencies point
inward; no lower layer references a layer above it.

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
