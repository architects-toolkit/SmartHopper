---
description: Review the changelog and last commits for consistency
---

Omit this workflow if current branch is main or dev.

1. Get a list of all commits in the current branch that are not in dev branch.

2. Get the messages for all retrieved commits.

3. Compare the list of commits against the [Unreleased] section in CHANGELOG.md.

4. Suggest the user for missing mentions in CHANGELOG