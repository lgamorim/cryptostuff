# cryptostuff

## Shared conventions
@.claude/rules/core/coding-standards.md
@.claude/rules/core/design-principles.md
@.claude/rules/core/architecture.md
@.claude/rules/core/testing-philosophy.md
@.claude/rules/core/workflow-core.md
@.claude/rules/overlays/workflow-solo.md
@.claude/rules/archetype/application.md
@.claude/rules/overlays/workflow-agent-review-solo.md
@.claude/rules/overlays/persistence-efcore.md

## Project-specific notes
- Rules under `.claude/rules/` are copies synced from the `claude-rules` repo
  (`tools/sync.ps1 -Target <this repo> -Profile application-solo -Add
  workflow-agent-review-solo,persistence-efcore`). Edit them there, not here.
  The sync stamps its source commit and that composition into
  `.claude/rules/.sync-source`,
  so audit for drift with `tools/sync.ps1 -Target <this repo> -Check` alone:
  it reads the set from the stamp and lists the upstream commits since the
  stamped one that touched a file in the set or an extra rule file the set
  does not hold. Flags passed with `-Check` must describe the stamped set, or it
  reports both sets and stops; a re-sync still takes the flags above and
  re-stamps the set. The composition matches no single profile, so the
  modules are imported directly above, without a profile manifest.
- This repo runs the two-role implement/review flow from
  `overlays/workflow-agent-review-solo.md`. When a `feature/` branch is
  complete, the implementer ends by invoking the `code-reviewer` subagent
  (`.claude/agents/code-reviewer.md`), passing only the handoff summary
  (intent, tests added, deliberate deviations), and relays its findings
  report verbatim; the maintainer adjudicates the findings and authorizes
  the squash-merge.
- That agent file is a filled-in copy of the `claude-rules` repo's
  `templates/reviewer-subagent.md` — placeholders resolved to `fable` at `high`
  effort, the template's copy-instructions comment dropped. Fable rather than
  the Opus model that implements most milestones, so the review adds a
  different model's judgement on top of the fresh context the overlay
  requires. Change the reviewer's instructions there, not here. `sync.ps1`
  covers only `.claude/rules/`, so `-Check` audits nothing about this file and
  its silence is not evidence the copy is current; diff it against the
  template by hand, ignoring those two deliberate differences.
- `.editorconfig` at the repo root encodes `core/coding-standards.md`'s naming
  and style rules so `dotnet format` and the build enforce them, not just prose.
  `EnforceCodeStyleInBuild` in `Directory.Build.props` makes style violations
  fail the build; naming violations are caught by `dotnet format`.
  Its `end_of_line = lf` is the exception that doesn't come from
  `coding-standards.md` — it exists to track `.gitattributes`' `eol=lf`. Leave
  the two in sync: with `end_of_line` unset, `dotnet format` falls back to
  `Environment.NewLine` and demands CRLF on Windows for any trivia it
  recomputes (a comment inside a fluent method chain, say), which git then
  normalizes back to LF, so `windows-latest` CI fails with a `WHITESPACE`
  error that reformatting cannot fix.
- A green local `dotnet format --verify-no-changes` on Windows is not by
  itself evidence CI will pass. `dotnet format` writes its preferred line
  endings to disk, git's `eol=lf` clean filter normalizes them away on commit,
  and `git status` compares normalized content — so the working tree can pass
  while the committed content, which is what CI checks out, fails. When a
  formatting fix looks green locally, confirm the bytes actually committed
  (`git show HEAD:<path> | xxd`), not just the working copy.
- `Directory.Build.props` centralizes `TargetFramework`, `Nullable`,
  `ImplicitUsings`, `TreatWarningsAsErrors`, and `IsPackable` per
  `core/architecture.md`, so no `.csproj` repeats them. `IsPackable` stays
  false per `archetype/application.md`: the deliverable is the running app.
  It also sets `GenerateDocumentationFile` so `IDE0005` (unused usings) runs
  on build, with `CS1591` suppressed: `archetype/application.md` requires XML
  docs only where intent isn't obvious.
- A generic type's file takes the type's bare name (`CoinGeckoResponse<TValue>`
  in `CoinGeckoResponse.cs`). When a non-generic sibling already holds that
  name, the generic one's file appends `OfT` (`ServiceResult<TValue>` in
  `ServiceResultOfT.cs`, beside `ServiceResult.cs`). This is how this repo meets
  `core/coding-standards.md`'s "file name matches the type name" for generic
  types; don't rename to `ServiceResult{TValue}.cs` or similar.
- Add projects to `cryptostuff.slnx` with plain `dotnet sln add <path>`, never
  `--in-root`. That flag suppresses the `<Folder Name="/src/">` and
  `<Folder Name="/test/">` elements, so IDEs show every project flat at the
  solution root instead of mirroring the directory layout.
- New code gets no null guard, and no test, for a parameter declared
  non-nullable when a null can only reach it from this repo's own code.
  `Nullable` is enabled and `TreatWarningsAsErrors` turns a possible null at
  such a call site into a build error, so a guard and a test that has to pass
  `null!` to reach it would only repeat the compiler.
  `core/testing-philosophy.md`'s "null/empty inputs" still applies to
  parameters declared nullable and to values that cross a boundary unchecked.
  Request bodies, CLI arguments and configuration are covered by the hosts'
  validators and the configuration readers. Upstream JSON is not covered by
  the compiler or by a wire type's `required` members: `required` rejects only
  a missing field, and an explicit JSON `null` leaves a non-nullable member
  null. The mappers and services that read those members check for null, and
  their tests set a member to `null!` to pin it; keep doing so for every
  provider.
  A guard is still worth adding where a null would otherwise go wrong somewhere
  misleading, as `CoinGeckoClient.GetAsync` does for `requestUri`. Without it,
  `HttpClient` sends a null URI to the base address, and the endpoint tag,
  worked out only after the send, then fails, or with no meter factory nothing
  fails at all. Such a guard is production code like any other: it gets a
  failing test first, which passes `null!`.
  Guards and tests already in the repo stay as they are; this note governs
  what new code adds, not what earlier milestones chose. Empty values are not
  covered by this note: test them wherever they can arrive.
