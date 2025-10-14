# 🔥 Hotfix Workflow Guide

This guide explains how to create and release hotfixes for production issues that need immediate attention, bypassing the normal dev branch workflow.

## Overview

The hotfix workflow allows you to:

- Branch directly from `main` for emergency fixes
- Increment only the patch version (X.X.**Z**)
- Create a release without waiting for the next milestone
- Merge directly to `main` without going through `dev`

## When to Use Hotfixes

Use the hotfix workflow for:

- **Critical bugs** in production
- **Security vulnerabilities** requiring immediate patches
- **Data loss** or corruption issues
- **Breaking functionality** that affects users

**Do NOT use for:**

- Regular feature development (use `dev` branch)
- Non-critical bug fixes (wait for next milestone)
- Improvements or enhancements

## Workflow Steps

### Step 1: Create Hotfix Branch

1. Go to **Actions** → **🔥 0 Create Hotfix Branch**
2. Click **Run workflow**
3. Enter a brief description (e.g., `fix-null-reference-crash`)
4. Click **Run workflow**

This creates a branch: `hotfix/X.X.X-description` (e.g., `hotfix/1.2.5-fix-null-reference-crash`)

### Step 2: Make Your Changes

1. Checkout the newly created `hotfix/X.X.X-description` branch
2. Make your fix
3. Commit and push your changes
4. **Test thoroughly** - this will go directly to production!


### Step 3: Prepare Hotfix Release

1. Go to **Actions** → **🔥 1 Prepare Hotfix Release**
2. Select the `hotfix/X.X.X-description` branch
3. Click **Run workflow**

This workflow will:
- Create a `release/X.X.X-hotfix-description` branch
- Update version in `Solution.props`
- Update `CHANGELOG.md` with release notes
- Update README badges
- Create a PR to `main` with priority label

### Step 4: Review and Merge PR

1. Review the PR created by the workflow
2. Ensure all validations pass:
   - ✅ Version Check
   - ✅ Code Style Check
   - ✅ Using Directives Check
   - ✅ Changelog Check
   - ✅ .NET CI Tests
3. Merge the PR to `main`


### Step 5: Automatic Release (Triggered on Merge)

Once merged to `main`, the following happens automatically:

1. **🏁 3 Create Release** - Creates a GitHub Release (draft)
2. **🏁 4 Build Project** - Builds the project and attaches artifacts
3. **🏁 5 Upload to Yak** - Publishes to Yak package manager


### Step 6: Sync Changes Back to Dev

After the hotfix is released, you should sync the changes back to `dev`:

#### Option A: Cherry-pick (Recommended)

```bash
git checkout dev
git cherry-pick <hotfix-commit-sha>
git push origin dev
```

**Option B: Merge main into dev**
```bash
git checkout dev
git merge main
git push origin dev
```

#### Option C: Manual PR

Create a PR from `main` to `dev` with the hotfix changes.

## Version Numbering

Hotfixes increment the **patch** version:

- Current main version: `1.2.4`
- Hotfix version: `1.2.5`

### Automatic Conflict Resolution

The workflow automatically handles version conflicts:

**Milestone Conflicts:**

- All open milestones with patch ≥ hotfix patch (same major.minor) are incremented
- Milestones are updated from highest to lowest to prevent collisions
- Each milestone shifts up by one patch version
- Example with hotfix `1.0.1`:
  - `1.0.3` → `1.0.4`
  - `1.0.2` → `1.0.3`
  - `1.0.1` → `1.0.2`

**Dev Branch Conflicts:**

- If dev branch has version `1.2.4` or `1.2.5` (≤ hotfix patch)
- A PR is created to update dev to `1.2.6` (next patch after hotfix)
- Solution.props and README.md are automatically updated in the PR
- Branch: `chore/bump-dev-version-for-hotfix-X.X.X`
- You'll need to review and merge this PR separately
- Example: Dev `1.2.4-alpha.1` → `1.2.6-alpha.1`

## Workflow Files

- **hotfix-0-new-branch.yml** - Creates hotfix branch from main
- **hotfix-1-release-hotfix.yml** - Prepares release branch and PR
- **release-3-pr-to-main-closed.yml** - Creates GitHub Release (existing)
- **release-4-build.yml** - Builds and uploads artifacts (existing)
- **release-5-upload-yak.yml** - Publishes to Yak (existing)


## Validations

All PRs to `main` (including hotfixes) run:

- Version format validation
- Code style checks
- Using directives validation
- Changelog update verification
- .NET build and tests


## Comparison with Regular Release

| Aspect | Regular Release | Hotfix Release |
|--------|----------------|----------------|
| **Source Branch** | `dev` | `main` |
| **Trigger** | Milestone close | Manual workflow |
| **Version Increment** | Major/Minor/Patch | Patch only |
| **Target Branch** | `dev` → `main` | `main` directly |
| **Use Case** | Planned features | Emergency fixes |
| **Testing** | Full QA cycle | Rapid validation |

## Example Scenario

**Problem:** Users report a crash when opening settings dialog.

**Solution:**

1. Run **🔥 0 Create Hotfix Branch** with description: `fix-settings-dialog-crash`
2. Branch created: `hotfix/1.2.5-fix-settings-dialog-crash`
3. Fix the bug in `SettingsDialog.cs`
4. Commit: `fix: prevent null reference in settings dialog initialization`
5. Run **🔥 1 Prepare Hotfix Release**
6. Review and merge PR to `main`
7. Release `1.2.5` is automatically created and published
8. Cherry-pick fix back to `dev` branch
