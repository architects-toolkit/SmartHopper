# Grasshopper Data Trees

Grasshopper uses one hierarchical data model: the data tree. Data structure is part of a component's input contract and can change the result even when the underlying values are identical.

## Structure

- An **item** is one value in one branch.
- A **list** is multiple values in one branch.
- A **tree** contains one or more uniquely addressed branches, each containing zero or more items.
- A path such as `{0;2}` identifies a branch. An item index identifies a position within that branch.
- Empty branches, null items, invalid items, and missing paths are different states; preserve those distinctions when they matter.

## Parameter access

- **Item access** asks Grasshopper to supply one item per component invocation.
- **List access** supplies a branch/list per invocation.
- **Tree access** supplies the complete tree and makes the component or script responsible for matching and output paths.
- In scripts, choose access intentionally and make output access consistent with the emitted value and topology.

## Data matching

- Grasshopper matches corresponding inputs according to their access and tree structure.
- Longest-list matching commonly extends a shorter list by repeating its last item.
- Shortest-list matching stops when the shortest input runs out.
- Cross-reference matching creates combinations and can multiply item counts dramatically.
- For trees, branch paths and branch counts affect matching. A shorter tree may be extended using its last branch under standard matching behavior.

Before connecting or transforming data, answer:

1. Which paths exist on each input?
2. How many items exist in each branch?
3. Which items or branches are intended to correspond?
4. What paths should the output retain or create?

## Tree operations

- **Flatten** combines all branches into one branch and discards branch separation.
- **Graft** creates additional branch depth, commonly placing items into separate branches.
- **Simplify** removes redundant shared path prefixes without changing branch membership.
- **Entwine** combines inputs while preserving or adding branch structure.
- **Merge** combines streams; inspect resulting paths and ordering.
- **Flip Matrix** exchanges row/column-like organization when branches have compatible shapes.
- **Path Mapper** rewrites paths and can lose or combine data if the mapping is wrong.

Do not flatten, graft, simplify, or remap merely to silence a mismatch. First establish the intended correspondence. Use Panel and Param Viewer, or SmartHopper runtime-data tools, at stage boundaries. Preserve paths in generated scripts unless the requested algorithm requires a topology change.

## Sources

- [Advanced Data Structures](https://developer.rhino3d.com/en/guides/grasshopper/gh-algorithms-and-data-structures/advanced-data-structures/)
- [Grasshopper data comparison modes](https://developer.rhino3d.com/api/grasshopper/html/T_Grasshopper_Kernel_GH_DataComparison.htm)
