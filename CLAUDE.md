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
- That agent file is a filled-in copy of the `claude-rules` repo's
  `templates/reviewer-subagent.md` — placeholders resolved to `opus` at `high`
  effort, the template's copy-instructions comment dropped. Change the
  reviewer's instructions there, not here. `sync.ps1` covers only
  `.claude/rules/`, so `-Check` audits nothing about this file and its silence
  is not evidence the copy is current; diff it against the template by hand,
  ignoring those two deliberate differences.
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
- Add projects to `cryptostuff.slnx` with plain `dotnet sln add <path>`, never
  `--in-root`. That flag suppresses the `<Folder Name="/src/">` and
  `<Folder Name="/test/">` elements, so IDEs show every project flat at the
  solution root instead of mirroring the directory layout.
