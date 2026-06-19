# 🏁 Regular Release Workflow Guide

This guide explains the standard release process for SmartHopper, which is triggered when a milestone is closed and follows the dev → main flow.

## Overview

The release workflow supports three paths:

1. **Regular Releases**: Planned releases from milestones (dev → main)
2. **Stabilization Path**: Milestone-driven stage progression for specific versions, on isolated branches
3. **Hotfix Releases**: Emergency patches from main

## Regular Release Flow

Triggered manually via `release-1-milestone.yml` when a milestone is ready to release:

1. **Manual trigger** with milestone title (e.g., `1.4.3-alpha`)
2. Creates `release/X.Y.Z-stage` branch from `dev`
3. Updates version, changelog, badges
4. Creates PR to `dev` → merge → PR to `main` → merge
5. Publishes release and builds artifacts

## Stabilization Path Flow

Use when you want to promote a specific version to stable without conflicting with other active development.

### Starting a Stabilization Path

1. **Create a milestone** with no suffix (e.g., `1.4.2`) in GitHub
2. `stabilization-0-init.yml` automatically:
   - Finds the latest prerelease tag matching `1.4.2-*`
   - Creates `dev-1.4.2` and `main-1.4.2` branches from that tag
3. The daily `release-promotion.yml` run detects the open `1.4.2` milestone and checks the current staged release (`1.4.2-alpha`) for promotion eligibility

### Promotion Loop (per stage)

When ALL conditions are met for the staged release:

- ✅ No open issues with version label
- ✅ Release published at least 30 days ago
- ✅ Last closed issue at least 30 days ago

Automation:

1. Creates next-stage milestone (e.g., `1.4.2-beta`)
2. Closes current staged milestone (e.g., `1.4.2-alpha`)
3. Dispatches `release-1-milestone.yml` targeting `dev-1.4.2`
4. PR flow: `release/1.4.2-beta` → `dev-1.4.2` → `main-1.4.2` → GitHub Release `1.4.2-beta`
5. Repeat for beta → rc → stable

### Completing a Stabilization Path

1. When `1.4.2` (stable) is released on `main-1.4.2`, close the `1.4.2` milestone
2. `stabilization-2-complete.yml` automatically creates a backport PR: `main-1.4.2` → `main`
3. After manual approval and merge, `dev-1.4.2` and `main-1.4.2` branches are deleted

### Cancelling a Stabilization Path

Close the `1.4.2` milestone while sub-milestones (`1.4.2-alpha`, etc.) are still open.
`stabilization-1-cancel.yml` automatically:

- Closes all open `1.4.2-*` sub-milestones
- Migrates their open issues to the next dev alpha milestone
- Deletes `dev-1.4.2` and `main-1.4.2` branches

## When to Use Regular Releases

Use the regular release workflow for:

- **Planned feature releases** with multiple changes
- **Minor/major version bumps** (X.Y.0 or X.0.0)
- **Milestone completions** with grouped issues/PRs
- **Manual release of any version** from an existing milestone

**Do NOT use for:**

- Emergency production fixes (use hotfix workflow)
- Security vulnerabilities requiring immediate patches (use hotfix workflow)
- Single critical bug fixes (use hotfix workflow)

## Workflow Steps

### Step 1: Develop Features in Dev Branch

1. Work on features, fixes, and improvements in the `dev` branch
2. Create PRs to `dev` for each feature/fix
3. Associate PRs and issues with a milestone (e.g., `1.2.0`)
4. Ensure all changes update `CHANGELOG.md` under `[Unreleased]`

### Step 2: Trigger Release Preparation

1. Go to **Actions** → **🏁 1 Prepare Release Branch**
2. Click **Run workflow**
3. Enter the **milestone title** (e.g., `1.2.0` or `1.4.3-alpha`)
4. Click **Run workflow**

**This automatically triggers:** 🏁 1 Prepare Release Branch

### Step 3: Automatic Release Preparation (Workflow 1)

The workflow automatically:

- Creates a `release/X.Y.Z` branch from `dev`
- Updates version in `Solution.props`
- Includes missing issues in `CHANGELOG.md`
- Creates release section in `CHANGELOG.md` (moves `[Unreleased]` → `[X.Y.Z]`)
- Updates README badges
- Updates Ready badge to `YES` (brightgreen)
- Creates a PR to `dev` with all changes

### Step 4: Review and Merge Release PR to Dev

1. Review the PR created by workflow 1
2. Ensure all validations pass:
   - ✅ Version Check
   - ✅ Code Style Check
   - ✅ Changelog Check
   - ✅ .NET CI Tests
3. Merge the PR to `dev`

**This automatically triggers:** 🏁 2 PR Release to Main from Dev

### Step 5: Automatic PR from Dev to Main (Workflow 2)

The workflow automatically:

- Creates a PR from `dev` to `main`
- Uses the same title and body from the release PR
- Includes all changes from the release

### Step 6: Review and Merge Dev to Main

1. Review the PR from `dev` to `main`
2. Ensure all validations pass (same checks as step 4)
3. Merge the PR to `main`

**This automatically triggers:** 🏁 3 Create Release on Release PR Close

### Step 7: Automatic GitHub Release Creation (Workflow 3)

The workflow automatically:

- Extracts version from `Solution.props` on `main`
- Generates release notes from GitHub
- Checks if version is a prerelease (contains `-` or `+`)
- Creates a **draft** GitHub Release with:
  - Tag: version number (e.g., `1.2.0`)
  - Title: "SmartHopper X.Y.Z: <add an engaging title here>"
  - Body: PR description + auto-generated release notes
  - Draft: true (requires manual publishing)
  - Prerelease: auto-detected

**This automatically triggers:** 🏁 4 Build Project

### Step 8: Automatic Build and Artifact Upload (Workflow 4)

The workflow automatically:

- Checks out the version tag from `main`
- Builds the solution in Release configuration
- Creates platform-specific artifacts:
  - `SmartHopper-X.Y.Z-Rhino8-Windows.zip`
  - `SmartHopper-X.Y.Z-Rhino8-Mac.zip`
- Validates all expected DLLs are present
- Uploads artifacts to the GitHub Release

### Step 9: Publish the GitHub Release

1. Go to **Releases** → find the draft release
2. Edit the title to add an engaging description
3. Review the release notes
4. Click **Publish release**

### Step 10: Upload to Yak (Automatic for Stable Releases)

**For stable releases (X.Y.Z without prerelease suffix):**
- Automatically triggered by `release-4-build.yml` after a successful build (race-condition-free)
- The `trigger-yak-upload` job waits for `merge-and-release` to complete before dispatching `release-6-upload-yak.yml`
- No manual action required

**For prerelease versions (alpha/beta/rc) or manual re-upload:**
1. Go to **Actions** → **🚀 6 Upload to Yak Rhino Server**
2. Click **Run workflow**
3. Configure:
   - **Version**: Leave empty (uses main branch version) or specify
   - **Platform**: Select `both` (default)
   - **Confirm upload to Yak**: Check this box
   - **Just testing**: Check for test server, uncheck for production
4. Click **Run workflow**

The workflow will:

- Download release artifacts from GitHub
- Build Yak package with manifest
- Upload to Yak server (production or test)

## Version Numbering

Regular releases follow semantic versioning:

- **Major** (X.0.0): Breaking changes
- **Minor** (X.Y.0): New features, backward compatible
- **Patch** (X.Y.Z): Bug fixes, backward compatible
- **Prerelease** (X.Y.Z-alpha.N): Development versions

Version is determined by the milestone title (e.g., milestone `1.2.0` → release `1.2.0`).

## Workflow Files

### Release Workflows

- **release-1-milestone.yml** — Prepares release branch; auto-detects `dev-X.Y.Z` for stabilization paths
- **release-2-pr-to-dev-closed.yml** — Creates PR from `dev` (or `dev-X.Y.Z`) to `main` (or `main-X.Y.Z`)
- **release-3-pr-to-main-closed.yml** — Creates GitHub Release; supports `main-*` branches
- **release-4-build.yml** — Builds artifacts and auto-triggers Yak upload after successful build (stable only)
- **release-promotion.yml** — Scans open no-suffix milestones daily; promotes eligible staged releases; supports `promotion: freeze` label
- **release-6-upload-yak.yml** — Uploads to Yak package manager (manual or dispatched by build)

### Stabilization Workflows

- **stabilization-0-init.yml** — Triggered on `milestone.created` for `X.Y.Z` titles; creates `dev-X.Y.Z` / `main-X.Y.Z` branches
- **stabilization-1-cancel.yml** — Triggered on `milestone.closed` (with open sub-milestones); cancels path, migrates issues, deletes branches
- **stabilization-2-complete.yml** — Triggered on `milestone.closed` (no open sub-milestones); creates backport PR and cleans up branches

## Validations

All PRs (release → dev, dev → main) run:

- Version format validation
- Code style checks
- Changelog update verification
- .NET build and tests

## Branch Protection

- **dev**: Protected branch, requires PR reviews
- **main**: Protected branch, requires PR reviews
- **dev-X.Y.Z**: Protected stabilization branch (created by automation); `github-actions[bot]` has bypass for create/delete
- **main-X.Y.Z**: Protected stabilization branch (created by automation); `github-actions[bot]` has bypass for create/delete
- **release/\***: Temporary branches, deleted after merge

All CI checks (`ci-dotnet-tests`, `pr-validation`, `pr-version-validation`, `pr-build-hash-validation`) run on PRs to `dev-*` and `main-*` branches identical to `dev` and `main`. Manifest text validation was removed — `manifest.yml` now uses the `{{NOTE_TEXT}}` placeholder, resolved at build time by `release-6-upload-yak.yml`.

### Stabilization Path Example

**Scenario**: Promote version `1.4.2` from alpha through to stable.

**Setup:**

1. Create milestone `1.4.2` (no suffix) in GitHub
2. `stabilization-0-init.yml` creates `dev-1.4.2` and `main-1.4.2` from tag `1.4.2-alpha`

**Daily promotion loop (alpha → beta):**

1. `release-promotion.yml` scans open no-suffix milestones → finds `1.4.2`
2. Looks up staged release → finds `1.4.2-alpha` tag
3. Validates:
   - ✅ No open issues labeled `version: 1.4.2`
   - ✅ `1.4.2-alpha` published 35 days ago
   - ✅ Last closed issue 32 days ago
4. Creates milestone `1.4.2-beta`, closes `1.4.2-alpha`
5. Dispatches `release-1-milestone.yml` with `milestone-title: 1.4.2-beta`
6. Workflow 1 detects `dev-1.4.2` → creates `release/1.4.2-beta` → PR to `dev-1.4.2`
7. PR merged → Workflow 2 creates PR `dev-1.4.2` → `main-1.4.2`
8. PR merged → Workflow 3 creates draft release `1.4.2-beta` on `main-1.4.2`
9. Publish release → Workflow 4 builds artifacts

**Repeat for beta → rc → stable.**

**Completion:**

1. `1.4.2` released on `main-1.4.2`
2. Close milestone `1.4.2`
3. `stabilization-2-complete.yml` creates backport PR: `main-1.4.2` → `main`
4. After merge, branches `dev-1.4.2` and `main-1.4.2` are deleted

**Blocking Scenarios** (promotion will NOT happen):

- ❌ Any open issue labeled `version: 1.4.2` (any stage)
- ❌ `1.4.2-alpha` release published < 30 days ago
- ❌ No open `1.4.2` milestone exists (stabilization path not initialized)
- ❌ A `promotion: freeze` label is active for the version (see below)

### Controlling Promotion

**Freezing promotion:**
- Add the `promotion: freeze` label to any open issue that also has a `version: X.Y.Z` label
- Promotion will be skipped for that version until the label is removed or the issue is closed
- `force-promote` in workflow_dispatch overrides the freeze

**Blocked promotion notifications:**
- When a release is older than 30 days but cannot be promoted, an issue titled `\u26d4 Promotion blocked: X.Y.Z-stage` is auto-created
- The issue is updated daily with the latest blocking reason
- Close the issue manually once the blocking condition is resolved

### Regular Release Example

**Goal:** Release version `1.2.0` with new AI features.

**Process:**

1. Develop features in `dev` branch over several weeks
2. Create milestone `1.2.0` and associate all PRs/issues
3. Close milestone `1.2.0`
4. **Workflow 1** creates `release/1.2.0` branch and PR to `dev`
5. Review and merge PR to `dev`
6. **Workflow 2** creates PR from `dev` to `main`
7. Review and merge PR to `main`
8. **Workflow 3** creates draft release `1.2.0`
9. **Workflow 4** builds and uploads artifacts
10. Edit release title: "SmartHopper 1.2.0: Enhanced AI Capabilities"
11. Publish release
12. Run **Workflow 5** to upload to Yak

## Comparison with Hotfix Release

| Aspect | Regular Release | Hotfix Release |
|--------|----------------|----------------|
| **Source Branch** | `dev` | `main` |
| **Trigger** | Milestone close | Manual workflow |
| **Version Increment** | Major/Minor/Patch | Patch only |
| **Flow** | `release/*` → `dev` → `main` | `hotfix/*` → `release/*` → `main` |
| **Use Case** | Planned features | Emergency fixes |
| **Testing** | Full QA cycle | Rapid validation |
| **Milestone** | Required | Not required |

## Tips and Best Practices

### Before Closing Milestone

- Ensure all PRs are merged to `dev`
- Verify `CHANGELOG.md` has all changes under `[Unreleased]`
- Test thoroughly in `dev` branch
- Update documentation if needed

### During Release

- Review the auto-generated PR carefully
- Don't skip validation checks
- Add meaningful release title when publishing
- Test the release artifacts before uploading to Yak

### After Release

- Monitor for issues in production
- Update project boards/roadmaps
- Communicate release to users
- Start planning next milestone

## Troubleshooting

**Problem:** Milestone close didn't trigger workflow

- **Solution:** Check that milestone title is a valid version (e.g., `1.2.0`)

**Problem:** Build artifacts are missing DLLs

- **Solution:** Check build logs in workflow 4, ensure all projects compiled

**Problem:** Yak upload fails

- **Solution:** Verify `YAK_AUTH_TOKEN` secret is configured correctly

**Problem:** Version badge not updated

- **Solution:** Run `chore-version-sync.yml` workflow manually

## Related Workflows

- **chore-version-sync.yml** - Unified version date + badge update
- **chore-version-main-release.yml** - Strips date suffix for main release
- **pr-validation.yml** - Validates all PRs
- **ci-dotnet-tests.yml** - Runs .NET tests
