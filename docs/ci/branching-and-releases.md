# Branching and releases

SmartHopper uses a **single long-lived branch** (`main`), **on-demand stabilization branches**, and
**tags as the source of truth** for what was released.

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

The canonical version is `SolutionVersion` in `Solution.props`. README badges, the Yak manifest and
the changelog heading are **derived** from it; nothing else writes it except version automation.

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
2. `pr-notes.yml` rewrites the PR title into Conventional Commits form and generates the description
   from the commits and diff (LLM). It only overwrites descriptions it generated itself.
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

Run **`stabilization-1-start.yml`** with a line (`X.Y`) and a source (`main` or a tag). It creates
`release/X.Y.x`.

**`stabilization-2-promote.yml`** (daily at 04:00 UTC, plus manual dispatch) promotes each active
line through `alpha → beta → rc → stable` by dispatching `release-1-prepare.yml` against the line,
when **all** of these hold for the current staged version:

- no open issues labelled `version: X.Y.Z`,
- no open PRs targeting `release/X.Y.x`,
- the current staged release was published at least `PROMOTION_AGE_DAYS` ago
  (repository variable, default **15**),
- the last closed issue for the version is at least `PROMOTION_AGE_DAYS` old,
- no `promotion: freeze` label is active for the version.

`force-promote` (dispatch input) overrides the age and freeze conditions. When a line is older than
the threshold but blocked, automation opens/updates an issue
`⛔ Promotion blocked: X.Y.Z-stage` with the current reason.

When the line reaches **stable**, `stabilization-3-complete.yml` opens a backport PR
`release/X.Y.x` → `main`, closes the milestone, and the line branch is deleted after that PR merges.

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

| Concern | Workflow |
| --- | --- |
| PR title/description generation | `pr-notes.yml` |
| PR gates | `pr-validation.yml`, `pr-version-validation.yml`, `pr-linear-history.yml`, `pr-build-hash-validation.yml`, `pr-documentation-validation.yml`, `pr-license-headers.yml`, `ci-dotnet-tests.yml`, `check-provider-models.yml` |
| Version bumps | `version-bump.yml`, `chore-version-sync.yml` |
| Release preparation | `release-1-prepare.yml` |
| Tag + release creation | `release-2-tag-on-merge.yml` |
| Build / publish | `release-4-build.yml`, `release-5-deploy-pages.yml`, `release-6-upload-yak.yml` |
| Stabilization | `stabilization-1-start.yml`, `stabilization-2-promote.yml`, `stabilization-3-complete.yml` |
| Hotfix | `hotfix-1-start.yml`, `hotfix-2-release.yml` |
| Cleanup | `pr-delete-auto-branches.yml`, `chore-cleanup-stale-branches.yml` |
| Maintenance chores (auto-PRs into `main`) | `chore-update-*.yml`, `pr-anonymize-public-key.yml` |
| Issue/label/milestone metadata | `github-*.yml`, `milestone-management.yml`, `pr-milestone.yml`, `model-verification.yml` |
