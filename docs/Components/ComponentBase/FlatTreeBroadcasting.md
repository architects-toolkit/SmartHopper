# Flat Tree Broadcasting Rules

## Overview

In Grasshopper data tree processing, a **flat tree** is a tree with a single path `{0}`. Such trees have special broadcasting behavior that determines when their data should be applied across paths in other input trees.

## Core Broadcasting Logic

A flat tree with path `{0}` (where `PathCount == 1`) broadcasts its data to other inputs based on **topology complexity**:

### Rule 1: Same-Depth Single Paths → No Broadcasting

When comparing a flat `{0}` tree (A) against another tree (B) that has:

- **Only one path** at the **same depth level**
- With a **different index** (e.g., `{1}`)

**Result:** No broadcasting. They are treated as **separate independent data streams**.

**Examples:**

```text
A: {0}      B: {1}           → A{0} does NOT match B{1}
A: {0} [1,2,3]   B: {1} [4,5,6]   → Output: {0}→[1,2,3], {1}→[4,5,6] (passthrough)
```

**Rationale:** Both inputs have structurally similar trees (single branch, same depth). They represent parallel but independent data channels.

---

### Rule 2: Multiple Paths → Broadcasting Enabled

When B has **multiple paths at the same depth level**, A's flat `{0}` tree broadcasts to **all** B paths.

**Examples:**

```text
A: {0}      B: {0}, {1}      → A{0} matches all paths in B
A: {0}      B: {1}, {2}      → A{0} matches all paths in B
A: {0}      B: {0}, {1}, {2} → A{0} matches all paths in B
```

**Rationale:** B has structural complexity (multiple branches). A's single-path tree is treated as a **scalar/broadcast parameter** that applies uniformly across all B branches.

---

### Rule 3: Different Topology Depth → Broadcasting Enabled

When B has paths at a **different depth level** (deeper hierarchy with `;` separator), A's flat `{0}` broadcasts to **all** B paths.

**Examples:**

```text
A: {0}      B: {0;0}              → A{0} matches all paths in B
A: {0}      B: {0;0}, {0;1}       → A{0} matches all paths in B
A: {0}      B: {1;0}              → A{0} matches all paths in B
A: {0}      B: {1;0}, {1;1}       → A{0} matches all paths in B
A: {0}      B: {0;0}, {1}         → A{0} matches all paths in B
A: {0}      B: {0;0}, {1;0}       → A{0} matches all paths in B
A: {0}      B: {0;1}, {1;0;0}     → A{0} matches all paths in B
```

**Rationale:** B has hierarchical/depth complexity. A's flat tree is treated as a **base-level parameter** that applies to all deeper structures.

---

### Rule 4: Direct Path Match Takes Precedence

When B has **both** a direct matching path `{0}` **and** deeper paths under the same root:

**Result:** A's `{0}` matches **only** B's `{0}` path, not the deeper `{0;...}` paths.

**Examples:**

```text
A: {0}      B: {0}, {0;0}            → A{0} matches B{0} only, NOT B{0;0}
A: {0}      B: {0}, {0;0}, {0;1}     → A{0} matches B{0} only, NOT B{0;0} or B{0;1}
```

**Rationale:** There is a **direct path match** between A and B at `{0}`. This is a normal branch-to-branch match. Broadcasting logic does not apply because the paths are identical. The deeper `{0;0}`, `{0;1}` paths in B represent a structured hierarchy that should not receive the flat `{0}` data.

**However**, if B has multiple top-level paths including `{0}`:

```text
A: {0}      B: {0}, {1}              → A{0} matches ALL paths in B (Rule 2 applies)
A: {0}      B: {0}, {1}, {2}         → A{0} matches ALL paths in B (Rule 2 applies)
```

In these cases, the presence of multiple top-level branches triggers Rule 2 (structural complexity), so A broadcasts to all paths including `{1}`, `{2}`.

---

## Summary Decision Matrix

| B's Structure                                  | A's {0} Behavior          | Rule Applied |
|------------------------------------------------|---------------------------|--------------|
| Single path, same depth, different index: `{1}` | NO broadcasting          | Rule 1       |
| Multiple paths, same depth: `{0}, {1}`         | Broadcast to ALL          | Rule 2       |
| Multiple paths, same depth: `{1}, {2}`         | Broadcast to ALL          | Rule 2       |
| Deeper paths: `{0;0}`, `{1;0}`, etc.           | Broadcast to ALL          | Rule 3       |
| Mixed depths: `{0;0}, {1}`                     | Broadcast to ALL          | Rule 3       |
| Direct match + deeper: `{0}, {0;0}`            | Match `{0}` ONLY          | Rule 4       |
| Direct match + multiple: `{0}, {1}`            | Broadcast to ALL          | Rule 2       |

---

## Implementation Notes

### In `DataTreeProcessor.GetBranchFromTree`

The broadcasting logic is implemented when:

1. A branch is requested at a path that doesn't exist in the tree
2. The tree has `PathCount == 1` (single path)
3. Broadcasting rules are evaluated to determine if the flat tree should be applied

### In `DataTreeProcessor.GetProcessingPaths`

When building the processing plan:

- Flat `{0}` trees are excluded from the primary processing paths when their data should be broadcast
- This prevents duplicate outputs at the `{0}` path when broadcasting is active
- Exception: When there's a direct path match (Rule 4), `{0}` is included in processing paths

### Scalar Trees (DataCount == 1)

Trees with a single item (`DataCount == 1`) always broadcast to any requested path, regardless of path topology. This is a special case that overrides all other rules.

---

## Test Cases Coverage

The following test components validate these rules:

- **TEST-DTP-DIFF-1**: A `{0}` [1 item], B `{1}` [1 item] → Both scalar, broadcast applies (special case)
- **TEST-DTP-DIFF-3-1**: A `{0}` [3 items], B `{1}` [1 item] → B is scalar, broadcasts to A's path
- **TEST-DTP-DIFF-3**: A `{0}` [3 items], B `{1}` [3 items] → Rule 1: no broadcasting, passthrough
- **TEST-DTP-ITEM**: Both at `{0}` → Direct match, normal item-to-item processing
- **TEST-DTP-GRAFT**: Both at `{0}` → Direct match, normal grafting

---

## Revision History

- 2025-11-23: Initial documentation based on comprehensive test case analysis
