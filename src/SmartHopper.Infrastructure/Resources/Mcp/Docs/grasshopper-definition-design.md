# Grasshopper Definition Design

## Recommended process

1. Define the intended outputs, acceptance criteria, units, and tolerance-sensitive operations.
2. Identify variable inputs, fixed assumptions, and referenced Rhino geometry.
3. Decompose the algorithm into cohesive stages with explicit data contracts.
4. Discover components available in the current installation.
5. Establish item/list/tree structure at each stage boundary.
6. Build from inputs toward outputs and verify each stage before extending it.
7. Test empty, single-item, multi-item, multi-branch, invalid, and boundary cases as relevant.
8. Inspect runtime messages, output geometry, item counts, paths, and profiler results.
9. Organize and annotate the final definition without changing its behavior.

## Maintainability

- Keep a readable dominant left-to-right flow and avoid unnecessary wire crossings.
- Use descriptive groups and scribbles for intent, not merely color decoration.
- Use relays or standalone parameters when they clarify long connections or stage boundaries.
- Keep important wires visible. Hidden wires should not conceal essential data flow.
- Expose meaningful inputs instead of unexplained constants. Use appropriately bounded sliders for values intended to vary.
- Prefer data-tree processing over copy-pasted branches when the same operation applies to many items.
- Extract clusters only for cohesive, reusable behavior with clear, named inputs and outputs. Avoid deep or opaque cluster nesting.
- Prefer the smallest clear native component network. Use scripts when they materially improve correctness, reuse, performance, or clarity.
- Preserve user layout and annotations when editing an existing definition unless reorganization is requested.

## Robustness

- Derive behavior from geometry or data when possible instead of relying on fragile indices, hard-coded directions, or incidental ordering.
- Make sorting keys, branch correspondence, domain assumptions, and orientation rules explicit.
- Keep side effects such as bake, file write, network access, or document mutation behind explicit controls.
- Treat plug-in dependencies as part of the definition contract and report unavailable components clearly.

## Sources

- [Algorithms and Data](https://developer.rhino3d.com/en/guides/grasshopper/gh-algorithms-and-data-structures/algorithms-data/)
- [Essential Algorithms and Data Structures](https://developer.rhino3d.com/en/guides/grasshopper/gh-algorithms-and-data-structures/)
