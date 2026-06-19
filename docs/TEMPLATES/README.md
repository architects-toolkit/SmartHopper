# SmartHopper Documentation Guidelines

This document is the single source of truth for SmartHopper documentation conventions: the unified template, section guidelines, naming standards, authoring tips, and quality checks.

---

## Available Templates

| Template | When to Use |
| --- | --- |
| [component-index.md](./component-index.md) | Index page for a category of Grasshopper components (e.g., Input, Output) |
| [feature.md](./feature.md) | Documentation for a specific feature, class, or subsystem |
| [architecture.md](./architecture.md) | High-level architectural overview of a system or subsystem |

### How to Use a Template

1. Copy the appropriate template into your target directory.
2. Rename the file to match your topic.
3. Fill in every section. If a section does not apply, remove it and add a brief note explaining why.
4. Replace all `[placeholders]` with real content.
5. Update the `Metadata` table with accurate values.
6. Add `<!-- PLACEHOLDER: ... -->` comments for screenshots you plan to add later.

---

## Philosophy

**Single source of truth, multiple perspectives.** Instead of maintaining separate documentation for different audiences, we create **one comprehensive document per topic** with clearly labeled sections for each audience. This ensures:

- Consistency across all information
- Reduced maintenance burden
- Cross-audience context and relationships
- Clear progression from conceptual to technical

---

## Three Audience Sections

Every documentation file must include three sections, in this order:

### 1. End-User Guide

| | |
| --- | --- |
| **Audience** | Grasshopper users, UX designers, non-technical stakeholders |
| **Purpose** | "What does this do and how do I use it?" |
| **Tone** | Friendly, task-oriented, visual |

**Include:**

- Plain-language overview
- Common use cases (scenario -- expected outcome)
- Visual guide / screenshot placeholders
- Step-by-step workflows
- UI element descriptions (inputs, outputs, settings)
- Common questions (Q&A format)
- Troubleshooting (problem / cause / solution)

**Example opening:**

```markdown
### What Does This Component Do?

The **AI->Text component** takes results from an AI operation
and extracts the text output, converting it into standard
Grasshopper text that you can use in your designs.
```

---

### 2. Developer Reference

| | |
| --- | --- |
| **Audience** | C# developers, plugin developers, system integrators |
| **Purpose** | "How do I implement, extend, or integrate this?" |
| **Tone** | Technical, precise, code-focused |

**Include:**

- API reference (class/interface signature)
- Key methods table (method, parameters, returns, purpose)
- Properties table (name, type, access, description)
- Code examples (minimum 2 per feature)
- Integration points (dependencies, consumers, extension points)
- Error handling table (error, cause, solution)

**Example code block:**

```markdown
#### Example: Basic Usage

```csharp
// Extract text from AI result
var text = component.ExtractText(result);
```

**Output**: The extracted text as a string.
```

---

### 3. Architecture & Design

| | |
| --- | --- |
| **Audience** | System architects, maintainers, decision makers |
| **Purpose** | "How does this fit into the larger system?" |
| **Tone** | Strategic, analytical, relationship-focused |

**Include:**

- Design rationale (problem, goals, trade-offs)
- Architecture / system relationship diagrams
- Design patterns used and why
- Performance characteristics (complexity, scalability)
- Future considerations (enhancements, limitations)
- Related components (links)

**Example:**

```markdown
### Design Rationale

**Problem**: N input types x M output types would require N*M components.

**Decision**: Composable adapter pattern with a single wire type.

**Trade-offs**:
- More components on the canvas (3 instead of 1)
- Greater flexibility and maintainability
```

---

## File Header and Metadata

Every documentation file starts with:

```markdown
# [Component/Feature Name]

[One-line summary]

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/...` |
| **Since Version** | ? |
| **Last Updated** | YYYY-MM-DD |
| **Documentation Maintainer** | [Name/Team] |

---

## Why Read This?

[2-3 sentences: what problem does this solve? who should care?]

**You should read this if you:**

- [Use case 1]
- [Use case 2]
- [Use case 3]

---

## End-User Guide
...

## Developer Reference
...

## Architecture & Design
...
```

---

## Visual Placeholders

Use HTML comments so placeholders are invisible in rendered markdown but visible in source:

```markdown
<!-- PLACEHOLDER: Screenshot showing the component in Grasshopper -->
<!-- - Component location: SmartHopper tab -> [Panel] -->
<!-- - Typical wiring: [describe a common connection pattern] -->
```

Common placeholder types:

- **Component**: canvas screenshot, input/output ports, right-click menu
- **Workflow**: data-flow diagram (Step 1 -> Step 2 -> Result)
- **Architecture**: system relationship diagram, dependency graph
- **UI**: settings panel, dialog screenshots

---

## Code Examples Best Practices

**Do:**

- Keep examples concise and compilable
- Add a 1-2 line comment explaining what the code does
- Show realistic, practical usage
- Include expected output or result

**Don't:**

- Include 10+ configuration options in one example
- Omit imports/context that make the example ambiguous
- Leave incomplete snippets that won't compile

---

## Cross-References

### Linking to Other Docs

```markdown
[Component Name](./ComponentName.md)
[Feature Overview](../Features/overview.md)
[Architecture Guide](../../Architecture.md)
```

### Linking to Sections within a File

```markdown
[See End-User Guide](#end-user-guide)
[See Developer Reference](#developer-reference)
[See Architecture & Design](#architecture--design)
```

---

## File Organization

### Directory Structure

```text
docs/
├── index.md                 -- Documentation home
├── Architecture.md          -- System overview
├── Architecture/            -- Architectural deep-dives
├── Providers/               -- Provider documentation
├── Components/              -- Component documentation
│   ├── ComponentBase/
│   ├── Input/
│   ├── Output/
│   ├── AI/
│   └── ...
├── Context/                 -- Context system
├── Tools/                   -- Tool documentation
├── UI/                      -- UI documentation
├── Usage/                   -- How-to guides
├── Reviews/                 -- Design reviews
├── GETTING_STARTED/         -- End-user entry point
├── API_REFERENCE/           -- Developer entry point
├── DESIGN_DECISIONS/        -- Architect entry point
└── TEMPLATES/               -- Templates and guidelines (this folder)
```

### Naming Conventions

| Type | Pattern | Example |
| --- | --- | --- |
| Component docs | `ComponentName.md` | `AI2TextComponent.md` |
| Index files | `index.md` | `docs/Components/Output/index.md` |
| Architecture docs | `FeatureName.md` | `AICapability.md` |
| Guides | `action-noun.md` | `file-to-markdown.md` |

---

## Audience Navigation

| Audience | Start here | Then read |
| --- | --- | --- |
| **End-users** | `docs/GETTING_STARTED/` | End-User Guide sections |
| **UX designers** | `docs/GETTING_STARTED/` + `docs/UI/` | End-User Guide + visual sections |
| **Developers** | `docs/API_REFERENCE/` | Developer Reference sections |
| **Architects** | `docs/Architecture.md` + `docs/DESIGN_DECISIONS/` | Architecture & Design sections |

---

## Migration Plan

| Phase | Scope | Status |
| --- | --- | --- |
| **1. Foundation** | Create TEMPLATES/, GETTING_STARTED/, API_REFERENCE/, DESIGN_DECISIONS/; update index.md | Done |
| **2. High-priority** | Migrate Architecture.md, Input/Output/AI/ComponentBase index, AICapability.md | Done |
| **3. Components** | Migrate all `docs/Components/*/index.md` and per-component files | Pending |
| **4. API reference** | Migrate `docs/Providers/AICall/*.md`, `docs/Architecture/*.md` | Pending |
| **5. Polish** | Consistency pass, visual placeholder review, cross-reference verification, metadata completion | Pending |

---

## Quality Checklist

Before submitting documentation, verify:

- [ ] **Metadata** -- all fields filled in
- [ ] **"Why Read This?"** -- clear purpose and audience listed
- [ ] **Three sections present** -- End-User Guide, Developer Reference, Architecture & Design (or justified omission)
- [ ] **Code examples** -- minimum 2 working examples in Developer Reference
- [ ] **Visual placeholders** -- `<!-- PLACEHOLDER: ... -->` for future screenshots
- [ ] **Cross-references** -- links to related documentation
- [ ] **Tone** -- appropriate for each audience section
- [ ] **No incomplete content** -- no `TODO` or `[To be added]` left behind
- [ ] **Markdown valid** -- no formatting errors, tables render correctly
- [ ] **Links work** -- all relative links resolve to existing files

---

## Validation Script

A PowerShell validation script is available at [`tools/Validate-Documentation.ps1`](../../tools/Validate-Documentation.ps1).

Usage:

```powershell
# Validate all docs (defaults to docs/ relative to repo root)
.\tools\Validate-Documentation.ps1

# Validate a specific path
.\tools\Validate-Documentation.ps1 -Path "docs\Components"

# Verbose output
.\tools\Validate-Documentation.ps1 -Verbose
```

The script checks for:

- Required sections (End-User Guide, Developer Reference, Architecture & Design)
- Complete metadata tables
- Minimum code examples (2+ in Developer Reference)
- Broken local links
- Old emoji-based section headers
- Incomplete placeholders
