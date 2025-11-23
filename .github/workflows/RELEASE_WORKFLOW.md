# 🏁 Regular Release Workflow Guide

This guide explains the standard release process for SmartHopper, which is triggered when a milestone is closed and follows the dev → main flow.

## Overview

The regular release workflow allows you to:

- Release planned features and improvements from the `dev` branch
- Automatically prepare release documentation and version updates
- Create a structured PR flow: `release/*` → `dev` → `main`
- Build and publish to GitHub Releases and Yak package manager
- Maintain clean version history with milestone-based releases

## When to Use Regular Releases

Use the regular release workflow for:

- **Planned feature releases** with multiple changes
- **Minor/major version bumps** (X.Y.0 or X.0.0)
- **Milestone completions** with grouped issues/PRs
- **Scheduled releases** following your development cycle

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

### Step 2: Close the Milestone

1. Go to **Issues** → **Milestones**
2. Ensure all issues/PRs are completed
3. Click **Close milestone** for the target version (e.g., `1.2.0`)

**This automatically triggers:** 🏁 1 Prepare Release on Milestone Close

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
   - ✅ Using Directives Check
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
  - `SmartHopper-X.Y.Z-Rhino8-Mac-not-tested.zip`
- Validates all expected DLLs are present
- Uploads artifacts to the GitHub Release

### Step 9: Publish the GitHub Release

1. Go to **Releases** → find the draft release
2. Edit the title to add an engaging description
3. Review the release notes
4. Click **Publish release**

### Step 10: Upload to Yak (Manual)

1. Go to **Actions** → **🚀 5 Upload to Yak Rhino Server**
2. Click **Run workflow**
3. Configure:
   - **Version**: Leave empty (uses main branch version) or specify
   - **Confirm upload to Yak**: Check this box
   - **Just testing**: Uncheck for production, check for test server
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

- **release-1-milestone.yml** - Prepares release branch when milestone closes
- **release-2-pr-to-dev-closed.yml** - Creates PR from dev to main
- **release-3-pr-to-main-closed.yml** - Creates GitHub Release (draft)
- **release-4-build.yml** - Builds and uploads artifacts
- **release-5-upload-yak.yml** - Uploads to Yak package manager

## Validations

All PRs (release → dev, dev → main) run:

- Version format validation
- Code style checks
- Using directives validation
- Changelog update verification
- .NET build and tests

## Branch Protection

- **dev**: Protected branch, requires PR reviews
- **main**: Protected branch, requires PR reviews
- **release/\***: Temporary branches, deleted after merge

## Example Scenario

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

- **Solution:** Run `chore-version-badge.yml` workflow manually

## Related Workflows

- **chore-version-badge.yml** - Updates README version badge
- **chore-version-date.yml** - Updates version date in dev branch
- **pr-validation.yml** - Validates all PRs
- **ci-dotnet-tests.yml** - Runs .NET tests
