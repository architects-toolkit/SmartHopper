# Workflow Conventions

> Canonical reference for all GitHub Actions conventions in SmartHopper.
> Every contributor and every new workflow **must** follow these rules.

## Branch Protection Model

| Pattern | Protection |
|---------|-----------|
| `main`, `main-*` | PR + codeowner approval required |
| `dev`, `dev-*` | PR + codeowner approval required |
| `hotfix/*` | PR + codeowner approval required |
| `release/**` | PR + codeowner approval required |

**No workflow may push directly to a protected branch.** All automated
changes must go through a PR created by `peter-evans/create-pull-request@v6`
(preferred) or `gh pr create`.

## Action Version Pinning

| Action | Pin style |
|--------|-----------|
| First-party (`actions/*`) | **Tag** — e.g. `actions/checkout@v4` |
| Third-party (community) | **Tag** — e.g. `peter-evans/create-pull-request@v6` |
| Local composite actions | **Path** — e.g. `./.github/actions/…` |

Do **not** use SHA pinning (`@abc123…`). Tag pinning is easier to audit
and auto-update with Dependabot.

## PR Creation Convention

Use `peter-evans/create-pull-request@v6` for chore workflows that modify
files in the working tree. Use `gh pr create` only when constructing PRs
from existing branches (e.g. release pipeline).

Every automated PR must:
1. Carry the `automated` label.
2. Call `./.github/actions/milestone/assign-pr` to assign the current milestone.
3. Call `./.github/actions/dispatch-required-pr-checks` to trigger status checks.

## Concurrency & `cancel-in-progress`

| Workflow type | `cancel-in-progress` | Rationale |
|---------------|---------------------|-----------|
| PR validation / CI checks | `true` | Superseded by newer pushes |
| Chore (version-date, badge, manifest, …) | `false` | Must complete to avoid stale state |
| Release pipeline (`release-1` … `release-6`) | `false` | Irreversible side-effects |
| Issue/label management | `false` | Quick, idempotent |

## Timeout Policy

Every job **must** declare `timeout-minutes`. Defaults:

| Job type | Timeout |
|----------|---------|
| Lightweight (label, comment, branch delete) | 5 min |
| Standard (checkout + script) | 10 min |
| Build / test (.NET CI) | 30 min |
| Heavy (model fetch, AI generation) | 45 min |

## PowerShell Conventions

When a workflow step uses `shell: pwsh`, call scripts with the `&`
(call operator), **not** by spawning a nested `pwsh` process:

```yaml
# Good
shell: pwsh
run: |
  & .\tools\My-Script.ps1 -Param value

# Bad — spawns a child pwsh inside the pwsh shell
shell: pwsh
run: |
  pwsh -ExecutionPolicy Bypass -File .\tools\My-Script.ps1 -Param value
```

## Validation Tiers

### Provider Model Validation (`Update-ProviderModels.ps1`)

| Tier | Severity | Blocks merge? | Examples |
|------|----------|---------------|----------|
| Error | `::error::` | Yes | Invalid capability expression, realtime model in list, pending capability |
| Warning | `::warning::` | No | Missing default for a composite capability category |

### PR Validation (`pr-validation.yml`)

| Check | Blocks merge? | Notes |
|-------|---------------|-------|
| Version format | Yes | Must be valid semver |
| Code style | Yes | Trailing whitespace, namespace, using order |
| Changelog | **Warning only** | Skipped for `chore/ci/style/build/revert/docs` PRs |
| PR title | Yes | Must follow conventional commits |

## Node.js Runtime

Set `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` as a repository-level
environment variable (or in individual workflows) to opt into Node.js 24
before the June 2026 deadline.

## Cross-Reference Comments

When two workflows overlap in trigger or purpose, each must carry a
header comment referencing the other. Example:

```yaml
# Related workflows:
#   - chore-version-sync.yml (unified version date + badge update)
```
