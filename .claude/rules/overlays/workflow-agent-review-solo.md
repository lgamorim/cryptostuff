# Workflow — Agent-assisted review (solo, branch-based)

Add when a solo repo runs a standing two-role flow without pull requests: one
agent implements a change on a `feature/` branch, a separate agent reviews that
branch's diff before the squash-merge. Compose with `workflow-solo.md` — the
flow supplies the second look the solo posture otherwise lacks, without
reinstating the PR, so it does not combine with
`workflow-team.md` (a team repo with PRs uses `workflow-agent-review-team.md`
instead).

This overlay defines the *contract* between the two roles. It deliberately says
nothing about which models run or how the agents are triggered (in-session
subagents, a separate session, an SDK harness) — that wiring is repo
automation, configured outside the rule set, not policy that belongs here.
A starting skeleton for the reviewer agent lives in
`templates/reviewer-subagent.md`.

Roles — never the same context:
- **Implementer** — delivers the change on a `feature/` branch. With no PR
  description to write, it ends with a handoff summary stating the intent, the
  tests added, and any deliberate rule deviations, so the reviewer judges them
  as choices rather than misses.
- **Reviewer** — reads the branch's diff against the default branch fresh
  (e.g. `git diff master...feature/x`), with none of the implementer's working
  context — the handoff summary is the only implementer-authored input it
  receives. It checks the change against the rules this repo already imports,
  plus any path-scoped rules matching the touched files, and returns an
  ephemeral findings report giving the `file:line` each finding applies to. It
  never edits code, never commits, and never merges; nothing it produces is
  checked in.

Boundaries:
- The reviewer advises; it does not gate. The maintainer adjudicates the
  findings, relays the accepted ones back to an implementer context for fixes,
  and authorizes the squash-merge — the merge permission `workflow-solo.md`
  grants is reserved to the maintainer and does not extend to either agent: an
  agent runs the merge commands only on the maintainer's explicit approval,
  and the review happens before any merge.
- The reviewer's authority is the repo's rule set — the imported modules plus
  any path-scoped rules matching the touched files: each finding cites the
  specific rule it violates. The reviewer does not invent conventions in the
  review, and "not how I would have written it" is not a finding unless a rule
  says so.
- Keep the two contexts genuinely separate. An agent reviewing a diff it wrote
  re-checks its own assumptions — the exact failure this flow exists to avoid.
