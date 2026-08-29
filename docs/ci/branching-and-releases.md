# Branching and releases

SmartHopper uses a **single long-lived branch** (`main`), **on-demand stabilization branches**, and
**tags as the source of truth** for what was released.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `.github/workflows/` |
| **Since Version** | 2.0.0 |
| **Last Updated** | 2026-08-29 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

This document is the authoritative guide to SmartHopper's branch, version, tag, and release
automation. Read it before changing a workflow or preparing a release.

---

## End-User Guide

Use the procedures below to create releases, maintain stabilization lines, and ship hotfixes.

---

## Developer Reference

The canonical release version is read from `Solution.props`:

```csharp
var releaseVersion = solutionVersion;
```

Release automation targets the integration branch explicitly:

```csharp
var integrationBranch = "main";
```

---

## Architecture & Design

`main` is the only long-lived integration branch. Release-preparation branches carry version and
changelog metadata, while tags identify immutable release commits. Downstream build, Pages, and Yak
workflows resolve their inputs from those tags.

---

## 1. Branches

| Branch | Lifetime | Purpose |
| --- | --- | --- |
| `main` | permanent | The only integration branch. Always releasable. Linear history, rebase-only, merge queue. Carries the dated development version (`X.Y.Z-dev.YYMMDD`). |
| `feat/*`, `fix/*`, `docs/*`, `chore/*`, `refactor/*`, `test/*` | hours–days | Topic branches. PR to `main`, rebase merge, deleted on merge. |
| `release/X.Y.x` | one stabilization cycle | Created on demand to stabilize a line (alpha → beta → rc → stable) while `main` moves on, or to maintain a shipped line. |
| `release-prep/<version>` | minutes–days | Created by release automation. Contains only version/changelog/badge changes. PR into `main` or `release/X.Y.x`. Deleted on merge. |
| `hotfix/<version>-<slug>` | hours | Cut **from the release tag** being patched. |
| `backport/*`, `hash-update/*` | ephemeral | Opened by automation. |

There is no `dev` branch, and no `dev-X.Y.Z` / `main-X.Y.Z` pairs.

## 2. Versions

The canonical version is `SolutionVersion` in `Solution.props`. README badges and the changelog
heading are **derived** from it; `yak-package/manifest.yml` keeps its `{{VERSION}}` and
`{{NOTE_TEXT}}` placeholders and is resolved only when the Yak package is uploaded. Nothing else
writes the version except version automation.

| Situation | Version format | Example |
| --- | --- | --- |
| `main` between releases | `X.Y.Z-dev.YYMMDD` | `2.0.0-dev.260828` |
| Prerelease | `X.Y.Z-alpha.N` / `-beta.N` / `-rc.N` | `2.0.0-beta.2` |
| Stable | `X.Y.Z` | `2.0.0` |

The prerelease counter `N` starts at `1` and is derived from existing tags for the same
core version and stage. A legacy suffix-only tag (`1.4.2-alpha`) counts as `N = 1`.
The dated `-dev.YYMMDD` suffix is refreshed by `chore-version-sync.yml` when `src/` changes.

## 3. Tags

- One annotated tag per release, named with the bare version (`2.0.0`, `2.0.0-beta.2`) — **no `v`
  prefix**, matching all existing tags, hash manifest names (`hashes/<version>.json`), artifact
  names and Yak versions.
- Tags are created by automation **when a `release-prep/*` PR merges**, pointing at the merge commit.
  A tag is never created by hand, never moved, and never deleted.
- Everything downstream resolves from the tag: build artifacts, provider hash manifest, Yak package,
  GitHub Pages hash publication, release notes, and the base of a hotfix.

## 4. Daily development

1. Branch from `main`, commit, open a PR to `main`.
2. `pr-notes.yml` generates a Conventional Commits title and description from the commits and diff
   (LLM). Empty bodies, or bodies containing its markers, receive an updated marked region; a
   human-authored body without markers is preserved and receives a sticky suggestion comment instead.
   Titles are changed only when invalid or equal to the head branch name. A recorded head SHA makes
   the run idempotent, and unavailable AI uses a clearly marked deterministic fallback.
3. `pr-validation`, `pr-version-validation`, `pr-linear-history`, `ci-dotnet-tests`,
   `pr-build-hash-validation` and the style/license/doc checks must pass.
4. Rebase-merge through the merge queue. The branch is deleted automatically.

`CHANGELOG.md` entries go under `[Unreleased]` and stay there until a release moves them.

## 5. Releasing

### 5.1 Normal release (from `main`)

Run **`release-1-prepare.yml`** (`workflow_dispatch`) with:

- `bump`: `none` | `patch` | `minor` | `major`
- `stage`: `stable` | `alpha` | `beta` | `rc`
- `target-branch`: `main` (default) or a `release/X.Y.x` line

It computes the next version, writes `Solution.props`, runs the LLM changelog review, moves
`[Unreleased]` into `## [X.Y.Z] - YYYY-MM-DD`, refreshes badges, ensures the milestone exists, and
opens **one** PR from `release-prep/<version>` into the target branch.

Review and merge it. `release-2-tag-on-merge.yml` then:

1. creates and pushes the annotated tag at the merge commit,
2. creates a **draft** GitHub Release with generated notes (prerelease auto-detected),
3. opens a `chore/bump-<next-dev-version>` PR so `main` returns to dated dev versioning
   (auto-merge with rebase).

Publishing the draft release triggers the existing build → hash → Pages → Yak chain
(`release-4-build.yml`, `release-5-deploy-pages.yml`, `release-6-upload-yak.yml`).

### 5.2 Stabilization line

Run **`stabilization-1-start.yml`** with a line (`X.Y`) and an existing release tag from that line.
The line starts from an immutable, identifiable release commit rather than a moving branch:
`release/X.Y.x`.

To start a line at the current `main`, first cut a release from `main` (for example, an alpha via
`release-1-prepare.yml`), then start stabilization from the resulting tag.

**`stabilization-2-promote.yml`** (daily at 04:00 UTC, plus manual dispatch) promotes each active
line through `alpha → beta → rc → stable` by dispatching `release-1-prepare.yml` against the line,
when **all** of these hold for the current staged version:

- no open issues labelled `version: X.Y.Z`,
- no open PRs targeting `release/X.Y.x`,
- the current staged release was published at least `PROMOTION_AGE_DAYS` ago
  (repository variable, default **15**),
- the last closed issue for the version is at least `PROMOTION_AGE_DAYS` old,
- no `promotion: freeze` label is active for the version.

`force-promote` (dispatch input) overrides the age and freeze conditions. An open issue with the
`promotion: freeze` label and matching `version: X.Y` label freezes that line. When a line is older than
the threshold but blocked, automation opens/updates an issue
`⛔ Promotion blocked: X.Y.Z-stage` with the current reason.

When the line reaches **stable**, `stabilization-3-complete.yml` opens a backport PR
`release/X.Y.x` → `main` and closes the milestone. The release line is retained for maintenance.

### 5.3 Hotfix

1. **`hotfix-1-start.yml`** — inputs: `tag` (defaults to the latest stable tag) and `description`.
   Creates `hotfix/<version>-<slug>` **from the tag**.
2. Commit the fix on that branch (`CHANGELOG.md` under `[Unreleased]`).
3. **`hotfix-2-release.yml`** — cherry-picks the hotfix commits onto `main` as
   `release-prep/<patch-version>` and opens the release PR, plus `backport/*` PRs into every active
   `release/X.Y.x`. Conflicts stop the workflow and open an issue instead of guessing.
4. Merging the release PR tags and releases exactly as in §5.1.

### 5.4 Version bumps outside a release

- **Manual:** `version-bump.yml` (`bump` + `stage` + `base-branch`) opens a
  `chore/bump-<version>` PR.
- **Automatic:** `chore-version-sync.yml` refreshes the `-dev.YYMMDD` date and badges when `src/`
  changes; `release-2-tag-on-merge.yml` opens the post-release bump PR.

## 6. Rollback

Never force-push or delete a published tag. Identify the last good tag, run `hotfix-1-start.yml`
from it, revert the offending change there, and ship a new patch release.

## 7. Repository configuration this model expects

- Rulesets on `main` and `release/*`: PR required, linear history, rebase-only merges, merge queue on
  `main`, required checks `ci-dotnet-tests`, `pr-validation`, `pr-version-validation`,
  `pr-build-hash-validation`.
- `github-actions[bot]` may create/delete `release-prep/*`, `hotfix/*`, `backport/*`, `chore/*`,
  `hash-update/*` and push tags.
- Repository variables: `SMARTHOPPER_BOT_NAME`, `SMARTHOPPER_BOT_EMAIL`, `PROMOTION_AGE_DAYS` (15).
- Secrets: `MISTRAL_API_KEY` (LLM PR notes, changelog review, release notes), `YAK_AUTH_TOKEN`,
  signing secrets.
- Milestones are **metadata only** — they group issues and PRs, and never trigger a release.

## 8. Workflow map

| Workflow | Trigger | Role |
| --- | --- | --- |
| `check-provider-models.yml` | pull requests | Validates provider model definitions. |
| `chore-changelog-review.yml` | release-prep pull requests to `main` or `release/**` | Simplifies release changelog text. |
| `chore-cleanup-stale-branches.yml` | schedule / manual | Deletes old eligible automation branches. |
| `chore-update-aitools.yml` | pushes to `main`, `hotfix/**`, `release/**` | Updates the `DEV.md` AI Tools table. |
| `chore-update-contributors.yml` | pushes on protected lines | Updates release contributors. |
| `chore-update-copyright-year.yml` | schedule / manual | Updates copyright years and opens a PR. |
| `chore-update-ghjson-spec-docs.yml` | pushes and pull requests on protected lines | Updates GhJSON documentation. |
| `chore-update-model-verification-template.yml` | provider-model pushes / manual | Updates the model-verification issue template. |
| `chore-update-provider-models-on-push.yml` | provider-model pushes on protected lines | Updates the `DEV.md` provider model table. |
| `chore-update-provider-models.yml` | schedule / manual | Retrieves provider model data and proposes it to `main`. |
| `chore-version-sync.yml` | source pushes on protected lines | Refreshes development-version dates and badges. |
| `ci-dotnet-tests.yml` | push, pull request, merge queue | Builds and tests the solution. |
| `github-issue-label-by-content.yml` | issue events | Applies labels from issue content. |
| `github-issue-label-version-from-template.yml` | issue events | Applies version labels from issue templates. |
| `github-issue-labels-close.yml` | issue events | Closes or updates issue labels. |
| `github-issue-labels-on-close.yml` | issue close | Maintains labels when issues close. |
| `github-labels-sync.yml` | schedule / manual | Synchronizes repository labels. |
| `github-pr-auto-label.yml` | pull requests | Applies pull-request labels. |
| `github-stale-management.yml` | schedule | Manages stale issues and pull requests. |
| `hotfix-1-start.yml` | manual | Creates a hotfix branch from a stable tag. |
| `hotfix-2-release.yml` | manual | Prepares a hotfix release and optional backports. |
| `milestone-management.yml` | milestone events | Maintains milestone metadata. |
| `model-verification.yml` | issue events / manual | Verifies provider models and opens update PRs. |
| `pr-anonymize-public-key.yml` | pull requests on protected lines | Removes identifying public-key metadata. |
| `pr-build-hash-validation.yml` | pull requests, merge queue | Validates build and provider hashes. |
| `pr-delete-auto-branches.yml` | pull request close | Deletes merged automation branches. |
| `pr-documentation-validation.yml` | documentation pull requests | Validates documentation structure. |
| `pr-license-headers.yml` | pull requests | Checks license headers. |
| `pr-linear-history.yml` | pull requests, merge queue | Enforces linear history. |
| `pr-milestone.yml` | pull requests to `main`, `release/**`, `hash-update/*` | Assigns pull requests to milestones. |
| `pr-notes.yml` | pull request open/update/reopen/ready | Generates PR titles and descriptions. |
| `pr-update-changelog-issues.yml` | manual | Adds eligible closed issues to the changelog. |
| `pr-validation.yml` | pull requests, merge queue | Runs pull-request metadata and style gates. |
| `pr-version-validation.yml` | pull requests | Validates version progression. |
| `release-1-prepare.yml` | manual | Creates a release-preparation pull request. |
| `release-2-tag-on-merge.yml` | merged release-prep pull requests | Creates release tags and draft releases. |
| `release-4-build.yml` | published releases | Builds release artifacts. |
| `release-5-deploy-pages.yml` | published releases | Deploys release documentation and hashes to Pages. |
| `release-6-upload-yak.yml` | published releases / manual | Uploads the tagged package to Yak. |
| `stabilization-1-start.yml` | manual | Creates a `release/X.Y.x` stabilization line. |
| `stabilization-2-promote.yml` | daily schedule / manual | Promotes eligible stabilization stages. |
| `stabilization-3-complete.yml` | published stable release / manual | Backports a stable line release to `main`. |
| `user-build-and-hash.yml` | manual | Builds and validates hashes for a selected ref. |
| `user-code-style.yml` | manual | Runs the code-style tooling. |
| `version-bump.yml` | manual | Opens a version-bump pull request. |

### Removed in the redesign

- `sync-dev-from-main.yml` — removed; `main` is now the only integration branch.
- `pr-block-dev-to-main.yml` — removed; obsolete cross-branch warning/blocking behavior.
- `release-promotion.yml` — replaced by `stabilization-2-promote.yml`.
- `stabilization-0-init.yml` and `stabilization-1-cancel.yml` — replaced by
  `stabilization-1-start.yml`; milestones are metadata only.
- `stabilization-2-complete.yml` — replaced by `stabilization-3-complete.yml`.
- `hotfix-0-new-branch.yml` — replaced by `hotfix-1-start.yml`.
- `hotfix-1-release-hotfix.yml` and `hotfix-backport.yml` — replaced by
  `hotfix-2-release.yml`.
