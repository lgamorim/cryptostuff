# Workflow — Solo project (PR waived)

Compose with `core/workflow-core.md`. Use when you are the only contributor.

- A PR adds no second reviewer when you are the only contributor. You may
  squash-merge the `feature/` branch into the default branch yourself without
  opening one (e.g. `git switch master && git merge --squash feature/x`, then
  a single commit).
- The feature-branch and squash steps still stand — only the PR *mechanism* is
  waived, never the review itself: the maintainer reviews the finished branch
  and gives explicit approval before any squash-merge.
- Switch to `workflow-team.md` the moment a second contributor joins, and swap
  any posture-matched overlay with it (e.g. `workflow-agent-review-solo.md` →
  `workflow-agent-review-team.md`).
