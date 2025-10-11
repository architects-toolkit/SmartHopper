---
trigger: glob
globs: src/**/*.cs
---

# Purpose
Detect and prevent duplicated logic; prefer reusable abstractions in parent/base classes. Default to targeted edits, but propose architectural refactors when duplication or incoherence is found. Stop processing and request confirmation on detected incoherences.

# Scope
- Applies across: `src/SmartHopper.Core/`, `src/SmartHopper.Core.Grasshopper/`, `src/SmartHopper.Components/`, `src/SmartHopper.Infrastructure/`, `src/SmartHopper.Providers.*/`.
- Targets shared logic (validation, encoding/decoding, provider orchestration, component bases, aicalls, aiinteractions, aimodels, utilities, etc.).

# Principles
- Prefer overall code logic deduplication (algorithms, workflows) over superficial line dedup.
- Place shared logic in parent/base classes first; children should remain thin adapters.
- Make minimal, targeted edits by default; propose larger refactors separately and await confirmation.
- Maintain security, performance, and architectural consistency.

# Assistant Workflow
1) Targeted Edit (default)
- Change only lines strictly required for the user’s request.

2) Duplicate Scan (lightweight)
- If similar blocks appear in ≥2 children or layers (≥5 identical/similar lines or same signature/behavior), flag as potential duplication.

3) Parent-First Reuse
- Propose moving the common logic to:
  - Core domain → `SmartHopper.Core/`
  - GH types/utilities → `SmartHopper.Core.Grasshopper/`
  - Component plumbing → `SmartHopper.Components/` base classes
  - Managers/settings/orchestration → `SmartHopper.Infrastructure/`
  - Provider-specific → `SmartHopper.Providers.<Name>/`

4) Refactor Proposal (non-breaking vs breaking)
- Non-breaking: introduce base method + adapt children.
- Breaking: requires schema/contract updates → prepare plan and STOP for confirmation.

5) Incoherence Guard (Stop-and-Confirm)
- If conflicts detected (e.g., mismatched schema keys, divergent tool-call encodings, contradictory enums/flags, inconsistent endpoint selection), WARN and STOP execution until user confirms the intended direction.

# Evidence & Proposal Template
- Collect examples: file paths, function names, short diffs/snippets.
- Provide a parent-extraction sketch and child adaptation list.
- Call out risks: behavior changes, API/ABI breaks, migration steps.

Template:
- Duplications found:
  - path:line → `Class.Method()` — brief note
  - path:line → `Class.Method()` — brief note
- Proposed parent API: `BaseClass.SharedMethod(args) : ReturnType`
- Children changes: list of classes to adapt
- Risk level: Non-breaking | Breaking (explain)
- Request: “Confirm to proceed with refactor?” (Stop until confirmed)

# Exceptions
- Performance-critical hot paths where virtual dispatch harms perf: prefer localized helpers and document rationale.
- Provider idiosyncrasies: keep provider-specific quirks in provider layer; do not force-fit into base if it obscures behavior.

# Security & Testing
- Run a brief OWASP/T12 threat review on external inputs/secrets touched by refactor.
- Add/adjust unit tests at the base level; keep child tests green.
- Preserve logs and error messages; avoid weakening diagnostics.

# Commit Behavior
- Default: targeted change only.
- If user confirms refactor: implement minimal viable parent extraction + child wiring, backed by tests.
- If user declines: proceed with user's instructions, whether targeted changes or suggest a deeper-reasoned new architectural approach.