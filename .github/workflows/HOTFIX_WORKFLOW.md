# Hotfix workflow guide

Hotfixes are shipped from a stable release tag without waiting for the next
normal release cycle.

## Workflow

1. Run **`hotfix-1-start.yml`** with a stable `tag` (or accept the newest
   stable tag) and a short `description`. It creates
   `hotfix/<tag>-<slug>` from the tag.
2. Make and test the fix on that branch. Add the user-facing change to
   `CHANGELOG.md` under `[Unreleased]`.
3. Run **`hotfix-2-release.yml`** for the hotfix branch. It cherry-picks the
   fix onto the selected `target-branch` (`main` by default, or `release/X.Y`), prepares the next
   stable patch release, and opens a `release-prep/<version>` pull request. When enabled, it also
   opens fix-only backport pull requests for active `release/X.Y` lines.
4. Review and merge the release-preparation pull request into `main`. The
   normal tag, draft release, build, Pages, and Yak workflows then handle the
   release.

Conflicts never force a cherry-pick. The workflows stop and open an actionable
issue for manual resolution.

## Workflow files

- **`hotfix-1-start.yml`** — creates a tag-based hotfix branch.
- **`hotfix-2-release.yml`** — prepares the stable patch release and optional
  release-line backports.
- **`release-2-tag-on-merge.yml`** — tags the merged release-preparation PR and
  creates the draft GitHub release.
- **`release-3-build.yml`**, **`release-4-deploy-pages.yml`**, and
  **`release-5-upload-yak.yml`** — publish the release artifacts and package.

## Safety

- The hotfix branch always starts at the selected stable tag, never at `main`.
- A stabilization-line hotfix starts from that line's release tag, targets `release/X.Y`, and is
  tagged when its release-preparation PR merges.
- Release preparation goes through the usual required checks and merge queue.
- `release/X.Y` branches remain available for maintenance.
- Existing cherry-pick/backport automation propagates stabilization-line fixes to `main` and
  newer active release lines.
- Published tags are immutable; use another hotfix for a subsequent correction.
