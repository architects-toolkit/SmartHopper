---
name: changelog-review
description: Review the changelog and last commits for consistency
allowed-tools:
  - read
  - grep
  - glob
  - exec
triggers:
  - user
  - model
---

Omit this skill if current branch is main or dev.

1. Get a list of all commits in the current branch that are not in dev branch.

2. Get the messages for all retrieved commits.

3. Compare the list of commits against the [Unreleased] section in CHANGELOG.md.

4. Suggest missing `CHANGELOG.md` entries to the user. Pay special attention to features that affect the user experience or break existing functionality.
