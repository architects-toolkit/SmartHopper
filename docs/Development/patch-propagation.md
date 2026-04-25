# Patch Propagation (Multi-Branch)

Tool to fan-out one or more commits across several long-lived branches by opening a PR per target. Useful when a small change (AI provider model list, docs fix, CI tweak, isolated bugfix) is relevant to multiple active branches such as `main`, `dev`, `main-*`, `dev-*`, or feature branches.

## When to use

- Updating supported AI models for one or more providers and wanting the change in `dev`, `main`, and any active promotion branch.
- Backporting a small bugfix landed in `dev` to one or more `main-*` / `dev-*` lines.
- Propagating CI or documentation tweaks across active branches.

Do **not** use it for large feature ports — those should go through the normal release/promotion or hotfix workflows.

## How it works

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

## Inputs

- **`source-shas`** — Comma- or space-separated commit SHAs in chronological order.
- **`source-branch`** — Informational, shown in the PR body (default `dev`).
- **`target-branches`** — Comma-separated branches, e.g. `main,dev,main-1.4,dev-1.5`.
- **`pr-title-prefix`** — Title prefix (default `[patch]`).
- **`pr-body-extra`** — Optional markdown appended to each PR body.
- **`labels`** — Comma-separated labels (default `patch,automated`). `has-conflicts` is added automatically when applicable.
- **`draft-always`** — Force every PR to be a draft, even without conflicts.
- **`mainline`** — Parent number for `git cherry-pick -m <n>` when picking merge commits. Leave empty for normal commits.

## Example: propagate an AI models update

1. Land the change on `dev` with a focused commit, e.g. `abc12345`.
2. Go to **Actions** → **🍒 Patch Propagate (Multi-Branch)** → **Run workflow**.
3. Fill in:
   - `source-shas`: `abc12345`
   - `target-branches`: `main,main-1.4,dev-1.5`
   - keep defaults for the rest.
4. Run. The workflow opens one PR per target. Conflicting targets get a draft PR with `has-conflicts` label.
5. Review and merge each PR like any other change. Existing PR validations (build, tests, code style, changelog) still run.

## Conflict handling

- Conflicts do not abort the matrix — other targets keep going (`fail-fast: false`).
- Conflicting PRs are opened as **draft**, labelled `has-conflicts`, and contain the commit with conflict markers, ready to resolve via a follow-up commit on the patch branch.

## Branch & PR naming

- Patch branch: `patch/<sanitized-target>/<shortsha>-<timestamp>` (e.g., `patch/main-1.4/abc12345-20260425220000`).
- PR title: `[patch] <original commit subject> → <target>`.

## Limitations

- Merge commits require the `mainline` input.
- Long-running diverged branches may produce conflicts on every cherry-pick — consider a focused refactor or a normal release/promotion path instead.
