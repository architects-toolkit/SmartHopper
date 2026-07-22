---
name: changelog-summary
description: Simplify the [Unreleased] section of CHANGELOG.md (or a specific version) into a user-focused changelog summary using the instructions from the CI workflow.
argument-hint: "[version]"
allowed-tools:
  - read
  - grep
  - exec
  - ask_user_question
triggers:
  - user
---

# Changelog Summary

1. Read the canonical instructions from `.github/workflows/chore-changelog-review.yml` (the `AI review & simplification` step / system prompt). Use those rules as the source of truth for how to simplify the changelog.

2. Determine the target section:
   - If the user provides an argument (e.g. `/changelog-summary 2.0.0`), use that version.
   - Otherwise, use `[Unreleased]`.

3. Read `CHANGELOG.md` and extract the section between `## [<target>]` and the next `## [`.
   If the section is empty or missing, report that and stop.

4. Apply the instructions from the workflow to simplify the section, but ignore the workflow's JSON output format requirement. Instead, return the simplified Markdown section directly.
