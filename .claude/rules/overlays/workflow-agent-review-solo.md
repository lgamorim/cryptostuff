# Workflow — Agent-assisted review (solo, branch-based)

Add when a solo repo runs a standing two-role flow without pull requests: one
agent implements a change on a `feature/` branch, a separate agent reviews that
branch's diff before the squash-merge. Compose with `workflow-solo.md` — the
flow supplies the second look the solo posture otherwise lacks, without
reinstating the PR, so it does not combine with
`workflow-team.md` (a team repo with PRs uses `workflow-agent-review-team.md`
instead).

This overlay defines the *contract* between the two roles. It deliberately says
nothing about which models run them — that pinning is repo automation,
configured outside the rule set, not policy that belongs here. The reviewer
runs as the agent named `code-reviewer` in the consuming repo's
`.claude/agents/` when one exists, for which `templates/reviewer-subagent.md`
in this rule set's source repository is a skeleton, and otherwise as a fresh
in-session subagent given this contract; the maintainer may instead run it
in a separate session. A `.claude/agents/` without that agent is not a
reason to skip the review, and an implementer never writes the definition
as part of the change it is delivering: adding it is a change of its own.

Roles — never the same context:
- **Implementer** — delivers the change on a `feature/` branch. With no PR
  description to write, it ends with a handoff summary stating the intent, the
  tests added, and any deliberate rule deviations, so the reviewer judges them
  as choices rather than misses. A fix round ends the same way, with a handoff
  summary naming the accepted findings it resolves and any deliberate
  deviation.
- **Reviewer** — reads the branch's diff against the default branch fresh
  (e.g. `git diff master...feature/x`), with none of the implementer's working
  context — the handoff summary is the only implementer-authored input it
  receives. It checks the change against the rules this repo already imports,
  plus any path-scoped rules matching the touched files, and returns an
  ephemeral findings report naming the commit it reviewed and giving the
  `file:line` each finding applies to. It never edits code, never commits, and
  never merges; nothing it produces is checked in.

Review rounds:
- A branch gets exactly one full review, in which the reviewer reads the
  branch's whole diff against the default branch.
- After the maintainer adjudicates and the accepted findings are fixed, any
  further review is a follow-up review. It covers only the fix commits: the
  range from the commit the previous review's report names to the new branch
  tip (e.g. `git diff <reviewed-sha>..feature/x`). It answers exactly two
  questions — is each accepted finding resolved, and do the changed lines
  violate an imported rule — and does not reopen content outside that range.
- The follow-up reviewer's inputs are the fix round's handoff summary, the
  reviewed commit's SHA, and the list of accepted findings. The SHA and the
  list come from the maintainer's adjudication, never from the implementer,
  so the handoff summary stays the only implementer-authored input.
- A follow-up review is optional: the maintainer may waive it (e.g. for a
  one-line fix) and authorize the squash-merge directly. A second full review
  happens only when the maintainer asks for one, e.g. when the fixes changed
  the branch's scope substantially. Before it, an implementer context writes
  a fresh handoff summary covering the whole branch as it stands, and that
  summary is the review's only implementer-authored input.

Boundaries:
- The reviewer advises; it does not gate. The maintainer adjudicates the
  findings, relays the accepted ones back to an implementer context for fixes,
  and authorizes the squash-merge — the merge permission `workflow-solo.md`
  grants is reserved to the maintainer and does not extend to either agent: an
  agent runs the merge commands only on the maintainer's explicit approval,
  and the full review happens before any merge.
- The reviewer's authority is the repo's rule set — the imported modules plus
  any path-scoped rules matching the touched files: each finding cites the
  specific rule it violates. The reviewer does not invent conventions in the
  review, and "not how I would have written it" is not a finding unless a rule
  says so.
- Keep the two contexts genuinely separate. An agent reviewing a diff it wrote
  re-checks its own assumptions — the exact failure this flow exists to avoid.
