# Flat Tree Broadcasting Rules

Rules governing how single-path flat trees broadcast their data across other input trees in Grasshopper processing.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/FlatTreeBroadcasting.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Flat tree broadcasting determines when a single-path `{0}` tree should apply its data to all branches of another input tree, versus when it should remain isolated. Getting this wrong leads to duplicated outputs, missing data, or unexpected passthrough behavior.

**You should read this if you:**

- Are debugging why a flat input tree is (or is not) broadcasting across branches
- Need to understand how `DataTreeProcessor` resolves path matching for mixed topologies
- Want to predict component output paths when one input is a flat list and another is a structured tree

---

## End-User Guide

### Overview

In Grasshopper data tree processing, a **flat tree** is a tree with a single path `{0}`. Such trees have special broadcasting behavior that determines when their data should be applied across paths in other input trees.

### Core Broadcasting Logic

A flat tree with path `{0}` (where `PathCount == 1`) broadcasts its data to other inputs based on **topology complexity**:

#### Rule 1: Same-Depth Single Paths â†’ No Broadcasting

When comparing a flat `{0}` tree (A) against another tree (B) that has:

- **Only one path** at the **same depth level**
- With a **different index** (e.g., `{1}`)

**Result:** No broadcasting. They are treated as **separate independent data streams**.

**Examples:**

```text
A: {0}      B: {1}           â†’ A{0} does NOT match B{1}
A: {0} [1,2,3]   B: {1} [4,5,6]   â†’ Output: {0}â†’[1,2,3], {1}â†’[4,5,6] (passthrough)

```

**Rationale:** Both inputs have structurally similar trees (single branch, same depth). They represent parallel but independent data channels.

---

#### Rule 2: Multiple Paths â†’ Broadcasting Enabled

When B has **multiple paths at the same depth level**, A's flat `{0}` tree broadcasts to **all** B paths.

**Examples:**

```text
A: {0}      B: {0}, {1}      â†’ A{0} matches all paths in B
A: {0}      B: {1}, {2}      â†’ A{0} matches all paths in B
A: {0}      B: {0}, {1}, {2} â†’ A{0} matches all paths in B

```

**Rationale:** B has structural complexity (multiple branches). A's single-path tree is treated as a **scalar/broadcast parameter** that applies uniformly across all B branches.

---

#### Rule 3: Different Topology Depth â†’ Broadcasting Enabled

When B has paths at a **different depth level** (deeper hierarchy with `;` separator), A's flat `{0}` broadcasts to **all** B paths.

**Examples:**

```text
A: {0}      B: {0;0}              â†’ A{0} matches all paths in B
A: {0}      B: {0;0}, {0;1}       â†’ A{0} matches all paths in B
A: {0}      B: {1;0}              â†’ A{0} matches all paths in B
A: {0}      B: {1;0}, {1;1}       â†’ A{0} matches all paths in B
A: {0}      B: {0;0}, {1}         â†’ A{0} matches all paths in B
A: {0}      B: {0;0}, {1;0}       â†’ A{0} matches all paths in B
A: {0}      B: {0;1}, {1;0;0}     â†’ A{0} matches all paths in B

```

**Rationale:** B has hierarchical/depth complexity. A's flat tree is treated as a **base-level parameter** that applies to all deeper structures.

---

#### Rule 4: Direct Path Match Takes Precedence

When B has **both** a direct matching path `{0}` **and** deeper paths under the same root:

**Result:** A's `{0}` matches **only** B's `{0}` path, not the deeper `{0;...}` paths.

**Examples:**

```text
A: {0}      B: {0}, {0;0}            â†’ A{0} matches B{0} only, NOT B{0;0}
A: {0}      B: {0}, {0;0}, {0;1}     â†’ A{0} matches B{0} only, NOT B{0;0} or B{0;1}

```

**Rationale:** There is a **direct path match** between A and B at `{0}`. This is a normal branch-to-branch match. Broadcasting logic does not apply because the paths are identical. The deeper `{0;0}`, `{0;1}` paths in B represent a structured hierarchy that should not receive the flat `{0}` data.

**However**, if B has multiple top-level paths including `{0}`:

```text
A: {0}      B: {0}, {1}              â†’ A{0} matches ALL paths in B (Rule 2 applies)
A: {0}      B: {0}, {1}, {2}         â†’ A{0} matches ALL paths in B (Rule 2 applies)

```

In these cases, the presence of multiple top-level branches triggers Rule 2 (structural complexity), so A broadcasts to all paths including `{1}`, `{2}`.

---

### Summary Decision Matrix

| B's Structure                                  | A's {0} Behavior          | Rule Applied |
| --- | --- | --- |--------------|
| Single path, same depth, different index: `{1}` | NO broadcasting          | Rule 1       |
| Multiple paths, same depth: `{0}, {1}`         | Broadcast to ALL          | Rule 2       |
| Multiple paths, same depth: `{1}, {2}`         | Broadcast to ALL          | Rule 2       |
| Deeper paths: `{0;0}`, `{1;0}`, etc.           | Broadcast to ALL          | Rule 3       |
| Mixed depths: `{0;0}, {1}`                     | Broadcast to ALL          | Rule 3       |
| Direct match + deeper: `{0}, {0;0}`            | Match `{0}` ONLY          | Rule 4       |
| Direct match + multiple: `{0}, {1}`            | Broadcast to ALL          | Rule 2       |

### Test Cases Coverage

The following test components validate these rules:

- **TEST-DTP-DIFF-1**: A `{0}` [1 item], B `{1}` [1 item] â†’ Both scalar, broadcast applies (special case)
- **TEST-DTP-DIFF-3-1**: A `{0}` [3 items], B `{1}` [1 item] â†’ B is scalar, broadcasts to A's path
- **TEST-DTP-DIFF-3**: A `{0}` [3 items], B `{1}` [3 items] â†’ Rule 1: no broadcasting, passthrough
- **TEST-DTP-ITEM**: Both at `{0}` â†’ Direct match, normal item-to-item processing
- **TEST-DTP-GRAFT**: Both at `{0}` â†’ Direct match, normal grafting

### Revision History

- 2025-11-23: Initial documentation based on comprehensive test case analysis

---

## Developer Reference

### Implementation Notes

#### In `DataTreeProcessor.GetBranchFromTree`

The broadcasting logic is implemented when:

1. A branch is requested at a path that doesn't exist in the tree
2. The tree has `PathCount == 1` (single path)
3. Broadcasting rules are evaluated to determine if the flat tree should be applied

#### In `DataTreeProcessor.GetProcessingPaths`

When building the processing plan:

- Flat `{0}` trees are excluded from the primary processing paths when their data should be broadcast
- This prevents duplicate outputs at the `{0}` path when broadcasting is active
- Exception: When there's a direct path match (Rule 4), `{0}` is included in processing paths

#### Scalar Trees (DataCount == 1)

Trees with a single item (`DataCount == 1`) always broadcast to any requested path, regardless of path topology. This is a special case that overrides all other rules.

### Code Examples

```csharp
// Example: Building a processing plan that respects flat tree broadcasting
var options = new ProcessingOptions
{
    Topology = ProcessingTopology.ItemToItem,
    OnlyMatchingPaths = false,
    GroupIdenticalBranches = true
};

var result = await DataTreeProcessor.RunAsync<GH_String, GH_String>(
    inputTrees,
    async (branchData) =>
    {
        // Process each logical unit; flat tree data is automatically broadcast
        var outputs = new Dictionary<string, List<GH_String>>();
        outputs["Result"] = new List<GH_String>
        {
            new GH_String(branchData["Text"][0].Value.ToUpper())
        };
        return outputs;
    },
    options);

```

```csharp
// Example: Worker using DataTreeProcessor with flat tree broadcasting
protected override async Task DoWorkAsync(CancellationToken token)
{
    var textTree = this.inputTree["Text"];
    var filterTree = this.inputTree["Filter"];

    // Flat tree broadcasting is automatically handled by DataTreeProcessor
    // when building the processing plan via GetProcessingPaths.
    // If Filter is a flat {0} tree, it broadcasts to all Text paths.
    this.result = await this.parent.RunProcessingAsync(
        this.inputTree,
        ProcessData,
        new ProcessingOptions
        {
            Topology = ProcessingTopology.BranchToBranch
        },
        token);
}

```

---

## Architecture & Design

- **Rule 1 rationale:** When both trees are structurally simple (single branch, same depth), they represent independent data channels. Broadcasting would incorrectly merge data that the user intended to keep separate.
- **Rule 2 rationale:** Multiple paths indicate the user is working with a structured dataset. A flat `{0}` tree is most naturally interpreted as a parameter that applies to every branch of that dataset.
- **Rule 3 rationale:** Deeper hierarchy (`;` separators) represents nested or grouped data. A flat tree at the root level is semantically a global parameter for all nested branches.
- **Rule 4 rationale:** When an exact path match exists, Grasshopper's normal branch-to-branch semantics should take precedence. Broadcasting would incorrectly override the explicit structure the user created.
- **Scalar override:** A tree containing exactly one item is universally treated as a scalar value. Scalars are always broadcast because they cannot meaningfully participate in path-specific matching.
