---
description: Create or update architecture documentation from existing code
auto_execution_mode: 1
---

# Documentation Update Workflow

1. **Inventory the codebase**
   - Scan `src/` projects and namespaces.
   - Github workflows and other code not in `src/` are out of scope in the documentation.

2. **Map core abstractions**
   - List interfaces, base classes, public APIs, and extension points.

3. **Trace dependencies & data flows**
   - Identify managers/services and the request → provider → response pipeline (validation/logging points).

4. **Draft Architecture Overview (`docs/Architecture.md`)**
   - Start from this template (keep concise; link to deeper docs):
     ```
     # Architecture Overview

     ## System Components
     - [Component descriptions]

     ## Key Design Patterns
     - [Patterns identified]

     ## Component Relationships
     - [How components interact]

     ## Data Flow
     - [Key data flows]
     ```

5. **Add diagrams (Mermaid)**
   - Provide graphs when relevant to help undestand relationships, dependencies and flows.
     ```
     ```mermaid
     graph TD
       Core[Core] --> Infra[Infrastructure]
       Infra --> Providers[Providers.*]
       Infra --> Components[Components]
       Components --> GH[Grasshopper UI]
     ```
     ```

6. **Component docs**
   - For each main component, create/update `docs/components/<name>.md` with:
     - Purpose
     - Public APIs (signatures)
     - Dependencies
     - Interactions

7. **Review & optimize**
   - Completeness: major components covered.
   - Accuracy: interfaces/methods match code.
   - Decisions: key architectural trade‑offs captured.
   - Token efficiency: bullets, deduplication, link instead of repeat.