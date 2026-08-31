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

Start a stabilization line from an existing release tag:

```bash
gh workflow run stabilization-1-start.yml -f line=2.0 -f source=2.0.0-alpha.1
```

The canonical version source is the `Solution.props` property:

```xml
<SolutionVersion>2.0.0-dev.260828</SolutionVersion>
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
| `release/X.Y` | one stabilization cycle | Created on demand to stabilize a line (alpha → beta → rc → stable) while `main` moves on, or to maintain a shipped line. |
| `release-prep/<version>` | minutes–days | Created by release automation. Contains only version/changelog/badge changes. PR into `main` or `release/X.Y`. Deleted on merge. |
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

Development versions are legitimate dated prereleases intended mainly for testers. If the base
`X.Y.Z-dev.YYMMDD` tag already exists, same-day development bumps append a sequence:
`X.Y.Z-dev.YYMMDD.1`, then `.2`, and so on. The prerelease counter `N` for alpha, beta, and rc
starts at `1` and is derived from existing tags for the same core version and stage. A legacy
suffix-only tag (`1.4.2-alpha`) counts as `N = 1`.
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
   `pr-build-hash-validation` and the style/license/doc checks must pass. The released-version
   guard only rejects a version newly introduced by the PR when that exact tag already exists;
   inheriting the target branch's already-released version is allowed. The hash guard blocks
   edits, deletions, renames, and copies, while allowing only a single bot-authored
   `hashes/<version>.json` addition from a `hash-update/*` branch when its version matches the
   PR head or an existing release tag.
4. Rebase-merge through the merge queue. The branch is deleted automatically.

`CHANGELOG.md` entries go under `[Unreleased]` and stay there until a release moves them.

## 5. Releasing

### 5.1 Normal release (from `main`)

Run **`release-1-prepare.yml`** (`workflow_dispatch`) with:

- `bump`: `none` | `patch` | `minor` | `major`
- `stage`: `dev` | `alpha` | `beta` | `rc` | `stable`
- `target-branch`: `main` (default) or a `release/X.Y` line

It computes the next version, writes `Solution.props`, runs the LLM changelog review, moves
`[Unreleased]` into `## [X.Y.Z] - YYYY-MM-DD`, refreshes badges, ensures the milestone exists, and
opens **one** PR from `release-prep/<version>` into the target branch.

Review and merge it. `release-2-tag-on-merge.yml` then:

1. creates and pushes the annotated tag at the merge commit,
2. creates a **draft** GitHub Release with generated notes (prerelease auto-detected),
3. opens a `chore/bump-<next-dev-version>` PR so `main` returns to dated dev versioning
   (auto-merge with rebase).

The post-release workflow always opens the development-version bump PR for releases targeting
`main`. Same-day development releases use the next available sequence suffix, so a release of
`X.Y.Z-dev.YYMMDD` is followed by `X.Y.Z-dev.YYMMDD.1`, then `.2` as needed.

Publishing the draft release triggers the existing build → hash → Pages → Yak chain
(`release-3-build.yml`, `release-4-deploy-pages.yml`, `release-5-upload-yak.yml`).

### 5.2 Stabilization line

Run **`stabilization-1-start.yml`** with a line (`X.Y`) and an existing release tag from that line.
The source must be an existing tag whose `X.Y` prefix matches the requested line, so the line
starts from an immutable, identifiable release commit rather than a moving branch. The new line is
named `release/X.Y`.

To start a line at the current `main`, first cut a release from `main` (for example, an alpha via
`release-1-prepare.yml`), then start stabilization from the resulting tag.

**`stabilization-2-promote.yml`** (daily at 04:00 UTC, plus manual dispatch) promotes each active
line through `alpha → beta → rc → stable` by dispatching `release-1-prepare.yml` against the line,
when **all** of these hold for the current staged version:

- no open issues labelled for **any tagged version on the `X.Y` line**,
- the current staged release was published at least `PROMOTION_AGE_DAYS` days ago,
- no `promotion: freeze` label is active for the version.

`PROMOTION_AGE_DAYS` is a repository variable; when it is unset, the workflow uses a default of
**15**. There is no per-dispatch age override. A blocked-promotion issue lists the exact tagged
versions that still have open issues. Promotion does not move items between milestones or close
milestones; milestones remain metadata only.

`force-promote` (dispatch input) overrides all eligibility conditions. An open issue with the
`promotion: freeze` label and matching `version: X.Y` label freezes that line. When a line is older than
the threshold but blocked, automation opens/updates an issue
`⛔ Promotion blocked: X.Y.Z-stage` with the current reason.

When the line reaches **stable**, `stabilization-3-complete.yml` opens a backport PR
`release/X.Y` → `main`. The release line is retained for maintenance and milestone metadata is
not moved automatically.

### 5.3 Hotfix

1. **`hotfix-1-start.yml`** — inputs: `tag` (defaults to the latest stable tag) and `description`.
   It accepts a release tag from any line and creates `hotfix/<version>-<slug>` **from the tag**.
2. Commit the fix on that branch (`CHANGELOG.md` under `[Unreleased]`).
3. **`hotfix-2-release.yml`** — cherry-picks the hotfix commits onto `main` as
   `release-prep/<patch-version>` and opens the release PR, plus `backport/*` PRs into every active
   `release/X.Y`. Conflicts stop the workflow and open an issue instead of guessing.
4. Merging the release PR tags and releases exactly as in §5.1.

For a stabilization-line hotfix, select the tag from that line and run
`hotfix-2-release.yml` with `target-branch=release/X.Y`. The workflow opens the
`release-prep/<version>` PR into that line; merging it creates the tag, while the existing
cherry-pick/backport machinery propagates the fix to `main` and newer active release lines.

Released versions are immutable and cannot be reused. Version validation rejects a PR whose
`Solution.props` version already exists as a tag; move the version forward instead.

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
- Milestones are **metadata only** — they group issues and PRs, and never trigger a release. A published release does trigger `milestone-management.yml`:
  - `alpha.N` keeps the `X.Y.Z-alpha` milestone open.
  - `beta.1` (first beta) closes `X.Y.Z-alpha` and moves open items to `X.Y.Z-beta`.
  - `rc.1` (first rc) closes `X.Y.Z-alpha` and `X.Y.Z-beta` and moves items to `X.Y.Z-rc`.
  - `stable X.Y.Z` closes every `X.Y.Z*` milestone and opens `X.(Y+1).0-alpha`.
- The post-release development version stays on the same pre-release stage and increments its sequence (`alpha.1` → `alpha.2`); only a stable release bumps the minor version and returns to `dev`.

## 8. Workflow map

| Workflow | Trigger | Role |
| --- | --- | --- |
| `check-provider-models.yml` | pull requests | Validates provider model definitions. |
| `chore-changelog-review.yml` | release-prep pull requests to `main` or `release/**` | Simplifies release changelog text. |
| `chore-cleanup-stale-branches.yml` | schedule / manual | Deletes old eligible automation branches. |
| `chore-update-aitools.yml` | pushes to `main` | Updates the `DEV.md` AI Tools table. |
| `chore-update-contributors.yml` | pushes on protected lines | Updates release contributors. |
| `chore-update-copyright-year.yml` | schedule / manual | Updates copyright years and opens a PR. |
| `chore-update-ghjson-spec-docs.yml` | pushes and pull requests on protected lines | Updates GhJSON documentation. |
| `chore-update-model-verification-template.yml` | provider-model pushes / manual | Updates the model-verification issue template. |
| `chore-update-provider-models-on-push.yml` | provider-model pushes on `main` | Updates the `DEV.md` provider model table. |
| `chore-update-provider-models.yml` | schedule / manual | Retrieves provider model data and proposes it to `main` and active `release/X.Y` lines. |
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
| `milestone-management.yml` | milestone events, release published, pull request merged | Promotes milestones through the release cycle (alpha → beta → rc → stable → next minor alpha) and moves open issues/PRs. |
| `model-verification.yml` | issue events / manual | Verifies provider models and opens update PRs. |
| `pr-anonymize-public-key.yml` | pull requests on protected lines | Removes identifying public-key metadata. |
| `pr-build-hash-validation.yml` | pull requests, merge queue | Validates build and provider hashes. |
| `pr-delete-auto-branches.yml` | pull request close | Deletes merged automation branches. |
| `pr-documentation-validation.yml` | documentation pull requests | Validates documentation structure. |
| `pr-license-headers.yml` | pull requests | Checks license headers. |
| `pr-linear-history.yml` | pull requests, merge queue | Enforces linear history. |
| `pr-milestone.yml` | pull requests to `main`, `release/**`, `hotfix/**`, `hash-update/*` | Assigns pull requests to milestones. |
| `pr-notes.yml` | pull request open/update/reopen/ready | Generates PR titles and descriptions. |
| `pr-update-changelog-issues.yml` | manual | Adds eligible closed issues to the changelog. |
| `pr-validation.yml` | pull requests, merge queue | Runs pull-request metadata and style gates. |
| `pr-version-validation.yml` | pull requests, merge queue | Validates version progression. |
| `release-1-prepare.yml` | manual | Creates a release-preparation pull request. |
| `release-2-tag-on-merge.yml` | merged release-prep pull requests | Creates release tags and draft releases. |
| `release-3-build.yml` | published releases | Builds release artifacts. |
| `release-4-deploy-pages.yml` | published releases | Deploys release documentation and hashes to Pages. |
| `release-5-upload-yak.yml` | published releases / manual | Uploads the tagged package to Yak. |
| `stabilization-1-start.yml` | manual | Creates a `release/X.Y` stabilization line. |
| `stabilization-2-promote.yml` | daily schedule / manual | Promotes eligible stabilization stages. |
| `stabilization-3-complete.yml` | published stable release / manual | Backports a stable line release to `main`. |
| `user-build-and-hash.yml` | manual | Builds and validates hashes for a selected ref. |
| `user-code-style.yml` | manual | Runs the code-style tooling. |
| `version-bump.yml` | manual | Opens a version-bump pull request. |
