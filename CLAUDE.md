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

## Project-specific notes
- Rules under `.claude/rules/` are copies synced from the `claude-rules` repo
  (`tools/sync.ps1 -Target <this repo> -Profile application-solo -Add
  workflow-agent-review-solo`). Edit them there, not here; re-run the same
  command with `-Check` to audit for drift — the `-Add` flag must be repeated
  or the audit compares against the wrong set. The composition matches no
  single profile, so the modules are imported directly above, without a
  profile manifest.
- This repo runs the two-role implement/review flow from
  `overlays/workflow-agent-review-solo.md`. When a `feature/` branch is
  complete, the implementer ends by invoking the `code-reviewer` subagent
  (`.claude/agents/code-reviewer.md`), passing only the handoff summary
  (intent, tests added, deliberate deviations), and relays its findings
  report verbatim; the maintainer adjudicates the findings and authorizes
  the squash-merge.
- `.editorconfig` at the repo root encodes `core/coding-standards.md`'s naming
  and style rules so `dotnet format` and the build enforce them, not just prose.
  `EnforceCodeStyleInBuild` in `Directory.Build.props` makes style violations
  fail the build; naming violations are caught by `dotnet format`.
- `Directory.Build.props` centralizes `TargetFramework`, `Nullable`,
  `ImplicitUsings`, `TreatWarningsAsErrors`, and `IsPackable` per
  `core/architecture.md`, so no `.csproj` repeats them. `IsPackable` stays
  false per `archetype/application.md`: the deliverable is the running app.
  It also sets `GenerateDocumentationFile` so `IDE0005` (unused usings) runs
  on build, with `CS1591` suppressed: `archetype/application.md` requires XML
  docs only where intent isn't obvious.
- No code yet. The first project goes under `src/<ProjectName>/` with unit
  tests under `test/<ProjectName>.UnitTests/` and a single `.slnx` at this
  root, per `core/architecture.md` and `archetype/application.md`.
