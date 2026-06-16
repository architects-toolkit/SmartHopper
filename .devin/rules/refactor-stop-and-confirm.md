---
trigger: model_decision
description: Only for /src/**. Duplicated code with diverging logic paths that cannot be reconciled without a refactor. Enum/flag contradictions or mismatched defaults that change behavior. Divergent implementations of the same contract in different modules.
---

# Incoherence gate and stop-processing policy

Use this rule with `code-dedup-architecture.md`. It exists to prevent local fixes that hide architectural contradictions.

## Stop immediately when

- Duplicated code has diverging behavior and the correct shared behavior is unclear.
- Enum values, flags, schema keys, defaults, or settings contradict each other across modules.
- Different providers, tools, or components implement the same contract with incompatible semantics.
- A fix would require breaking a public API, component contract, persisted data format, tool schema, or provider response shape.

## Assistant behavior

1. Emit a clear warning with concrete evidence:
   - File paths.
   - Symbols or schema keys.
   - Short snippets or behavior summaries.
2. Stop further edits in the affected area.
3. Ask for explicit confirmation and present:
   - The minimal coherent fix.
   - The broader refactor option.
   - Risk level: non-breaking or breaking.
4. Resume only after user confirmation, or keep the change targeted and localized if the user declines refactoring.

## Default preference

Prefer parent/base extraction for confirmed duplication fixes, but do not force provider-specific or platform-specific quirks into a shared abstraction if it would obscure behavior.
