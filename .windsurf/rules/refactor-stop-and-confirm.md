---
trigger: model_decision
description: Only for /src/**. Duplicated code with diverging logic paths that cannot be reconciled without a refactor. Enum/flag contradictions or mismatched defaults that change behavior. Divergent implementations of the same contract in different modules.
---

# Incoherence Gate & Stop-Processing Policy

## Assistant Behavior
1) Emit a clear warning with concrete evidence:
   - Files, symbols, brief diffs/snippets demonstrating the inconsistency.
2) Stop processing further edits.
3) Ask for explicit confirmation:
   - Provide a short plan outlining the minimal coherent fix vs the refactor option (non-breaking vs breaking).
4) Resume only after user confirmation; otherwise keep changes targeted and localized.

## Notes
- Prefer parent/base extraction when resolving duplication.
- Avoid masking incoherences with local fixes; surface and gate on user intent.