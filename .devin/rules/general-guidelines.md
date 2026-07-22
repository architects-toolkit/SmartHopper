---
trigger: always_on
---

# General guidelines

1. Follow established best practices (SOLID, clear naming, error handling, tests, and security) unless the user explicitly asks otherwise.
2. Use this decision priority:
   1. Meet the user requirements.
   2. Follow the documented SmartHopper architecture.
   3. Fix root causes instead of patching symptoms.
   4. Protect security and privacy, especially external inputs and secrets.
   5. Preserve performance and responsiveness in Rhino/Grasshopper.
   6. Keep future maintenance simple.
3. If implementation options become ambiguous or contradictory, stop, summarize the concrete evidence, and ask the user to choose a direction.
4. Conduct a brief threat review for changes that touch external inputs, provider calls, secrets, file/network access, or AI tool execution.
5. In post-edit summaries, include alternatives considered, why the chosen approach fits, best practices applied, and follow-up improvement suggestions when useful.

## Project-specific guidelines

- Use English only in code, documentation, rules, and user-facing text.
- Prefer native Grasshopper/Rhino types and APIs when working with canvas, data-tree, or geometry logic.
- Use https://developer.rhino3d.com/ as the official Rhino/Grasshopper API reference.
- Check `/docs` before changing existing architecture; those docs are the local source of truth for module responsibilities and data flows.
- Use commands appropriate to the current execution environment. Windows-only build/signing flows require Developer PowerShell for Visual Studio; do not assume every assistant or GitHub Actions runner is on Windows.
- Never add unit tests that require Rhino/Grasshopper references. For tests that require Rhino/Grasshopper references, create a testing component in the `SmartHopper.Components.Test` project.
- When testing for compilation errors, use the `.\tools\Build-Solution.ps1` script that automatically handles authenticode and prompts the user for the signing password.
- Do not commit secrets, signing keys, local provider API keys, or generated private credentials.

## Context persistence

Persist durable project knowledge when it is general enough to help future work:

1. High-level architecture and module responsibilities.
2. Stable public APIs, extension points, and contracts.
3. Project conventions, naming, workflows, and configuration rules.
4. Design decisions with rationale and alternatives.
5. Third-party dependencies and integration patterns.

Dedupe or refresh stale knowledge instead of creating conflicting entries.
