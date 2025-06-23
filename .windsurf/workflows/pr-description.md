---
description: PR title and description generation
---

The aim is to return the title and the description for a PR, following the rules in CONTRIBUTING.md.

1. Analyze CONTRIBUTING.md

2. Read the [Unreleased] section in CHANGELOG.md

3. Check for an active PR in the current branch

4. Analyze the changes in the active PR, or the changes between the current branch and the dev branch if no PR

5. Return the PR title in a codeblock as plain text

6. Return the PR description in a seperate codeblock in markdown format