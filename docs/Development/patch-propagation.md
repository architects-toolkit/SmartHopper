# Patch Propagation (Multi-Branch)

Tool to fan-out one or more commits across several long-lived branches by opening a PR per target. Useful when a small change (AI provider model list, docs fix, CI tweak, isolated bugfix) is relevant to multiple active branches such as `main`, `dev`, `main-*`, `dev-*`, or feature branches.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document explains how to propagate small, focused changes across multiple active branches efficiently without going through full release workflows. It covers the manual workflow, input parameters, conflict handling, and naming conventions.

**You should read this if you:**

- Maintain multiple release or promotion branches and need to backport fixes
- Update AI provider model lists and want the change applied to all active lines
- Propagate CI or documentation tweaks across several branches at once

---

## End-User Guide

### When to Use

- Updating supported AI models for one or more providers and wanting the change in `dev`, `main`, and any active promotion branch.
- Backporting a small bugfix landed in `dev` to one or more `main-*` / `dev-*` lines.
- Propagating CI or documentation tweaks across active branches.

Do **not** use it for large feature ports — those should go through the normal release/promotion or hotfix workflows.

### How It Works

- Workflow: `.github/workflows/patch-propagate.yml` (manual `workflow_dispatch`).
- Composite action: `.github/actions/cherry-pick-to-branch` does the actual cherry-pick + PR creation.
- For each target branch, the workflow:
  1. Checks out the repository at full depth.
  2. Creates a `patch/<target>/<shortsha>-<timestamp>` branch from the target.
  3. Runs `git cherry-pick -x` for each provided SHA.
  4. On conflicts: commits with markers, opens the PR as **draft**, adds `has-conflicts` label.
  5. Skips a SHA if it is already in the target branch history.
  6. Pushes the patch branch and opens a PR via `gh pr create`.

It never pushes to the target branch directly, so branch protection on `main` / `dev` / `main-*` / `dev-*` is respected.

### Inputs

- **`source-shas`** — Comma- or space-separated commit SHAs in chronological order.
- **`source-branch`** — Informational, shown in the PR body (default `dev`).
- **`target-branches`** — Comma-separated branches, e.g. `main,dev,main-1.4,dev-1.5`.
- **`pr-title-prefix`** — Title prefix (default `[patch]`).
- **`pr-body-extra`** — Optional markdown appended to each PR body.
- **`labels`** — Comma-separated labels (default empty). Labels are applied best-effort after the PR is created; any label that doesn't exist in the repo is logged as a warning and skipped (PR is **not** aborted). `has-conflicts` is also applied (best-effort) when conflicts occur.
- **`draft-always`** — Force every PR to be a draft, even without conflicts.
- **`mainline`** — Parent number for `git cherry-pick -m <n>` when picking merge commits. Leave empty for normal commits.

### Example: Propagate an AI Models Update

1. Land the change on `dev` with a focused commit, e.g. `abc12345`.
2. Go to **Actions** → **🍒 Patch Propagate (Multi-Branch)** → **Run workflow**.
3. Fill in:
   - `source-shas`: `abc12345`
   - `target-branches`: `main,main-1.4,dev-1.5`
   - keep defaults for the rest.
4. Run. The workflow opens one PR per target. Conflicting targets get a draft PR with `has-conflicts` label.
5. Review and merge each PR like any other change. Existing PR validations (build, tests, code style, changelog) still run.

### Conflict Handling

- Conflicts do not abort the matrix — other targets keep going (`fail-fast: false`).
- Conflicting PRs are opened as **draft**, labelled `has-conflicts`, and contain the commit with conflict markers, ready to resolve via a follow-up commit on the patch branch.

### Branch & PR Naming

- Patch branch: `patch/<sanitized-target>/<shortsha>-<timestamp>` (e.g., `patch/main-1.4/abc12345-20260425220000`).
- PR title: `[patch] <original commit subject> → <target>`.

### Limitations

- Merge commits require the `mainline` input.
- Long-running diverged branches may produce conflicts on every cherry-pick — consider a focused refactor or a normal release/promotion path instead.

---

## Developer Reference

### Input Validation

When building automation around patch propagation, ensure commit SHAs are well-formed before submitting them to the workflow:

```csharp
public static bool IsValidSha(string sha)
{
    if (string.IsNullOrWhiteSpace(sha))
        return false;

    // Git SHA-1 are 40 hex characters (full) or at least 7 (short)
    return sha.Length >= 7 && sha.Length <= 40
        && sha.All(c => "0123456789abcdef".Contains(char.ToLower(c)));
}

```

### Branch Name Sanitization

Target branch names are sanitized for use in patch branch names and PR titles. A typical sanitization step replaces characters that are unsafe for Git ref names:

```csharp
public static string SanitizeBranchName(string branch)
{
    var invalid = Path.GetInvalidFileNameChars()
        .Concat(new[] { ' ', '~', '^', ':', '\\' })
        .ToArray();

    foreach (var c in invalid)
    {
        branch = branch.Replace(c, '-');
    }

    // Collapse multiple dashes
    while (branch.Contains("--"))
    {
        branch = branch.Replace("--", "-");
    }

    return branch.Trim('-');
}

```

---

## Architecture & Design

### Workflow Architecture

The patch propagation system is composed of two layers:

1. **Orchestrator Workflow** (`.github/workflows/patch-propagate.yml`)
   - Defines the `workflow_dispatch` inputs
   - Builds a matrix of target branches
   - Calls the composite action for each target

2. **Composite Action** (`.github/actions/cherry-pick-to-branch`)
   - Checks out the repository at full depth
   - Creates the patch branch from the target
   - Runs `git cherry-pick -x` for each SHA
   - Handles conflicts by committing with markers
   - Creates the PR via `gh pr create`

### Safety Guarantees

- **No direct pushes** to protected branches — every change goes through a PR
- **Fail-fast disabled** — one target's conflict does not block others
- **Best-effort labels** — missing labels are warnings, not fatal errors
- **Idempotent skips** — SHAs already present in a target are silently skipped
