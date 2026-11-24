# GitHub Actions, releases, and model maintenance

## Protected branches and merging

- `main` and release integration branches may be protected and rebase-only. Never push directly to `main`, use administrator overrides, or assume Devin can merge protected PRs.
- Programmatic auto-merge must use rebase (`gh pr merge <pr> --auto --rebase`), never squash or merge-commit mode.
- If CodeQL or another required code-quality gate is missing, treat it as a repository/security configuration issue; do not bypass it in a workflow.

## Workflow architecture

- Do not hardcode changing protected-branch patterns in workflows. Detect protection dynamically or centralize the behavior.
- Centralize repeated commit-to-protected-branch fallback logic in shared composite actions under `.github/actions/`.
- Workflow-generated changes should be reproducible. Fix generators or parsing scripts rather than hand-patching individual generated entries when the generator is wrong.
- Never change version files, branch rules, signing configuration, or release tags merely to make a workflow pass.

## Release model

- Releases are tag-driven from `main`; tags use `X.Y.Z` without a leading `v`.
- Prereleases use suffixes such as `alpha.N`, `beta.N`, or `rc.N`.
- Only the designated release-preparation PR may alter version files.
- Release automation must not mutate source code after the release commit has been approved.

## Provider model maintenance

- The canonical model name must be the direct provider API name; spelling or formatting variants belong in aliases.
- Verify added/deprecated model IDs, capabilities, context limits, and pricing against direct provider documentation/catalogs, using public aggregator catalogs only as secondary evidence.
- Catalog absence alone is not proof that a native provider model is retired.
- Do not guess model capabilities or deprecate models merely to make CI green.

## AI tool and test conventions

- Keep one AI tool per file and shared tool logic in static helpers or the appropriate shared infrastructure layer.
- Do not add unit tests requiring Rhino/Grasshopper references; use the test-component project when Rhino runtime integration is required.
- For signed test projects, use `tools/Build-Solution.ps1 -Testing`; signing workarounds can create false `InternalsVisibleTo` compile failures.
