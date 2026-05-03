---
trigger: model_decision
description: When adding or changing Grasshopper components, workers, data-tree handling, branch matching, grafting, flattening, broadcasting, or output paths
globs: ["src/SmartHopper.Components/**/*.cs", "src/SmartHopper.Core/DataTree/**/*.cs", "src/SmartHopper.Core.Grasshopper/**/*.cs"]
---

# Data-tree processing

Use `DataTreeProcessor` as the single source of truth for Grasshopper data-tree mechanics.

## Responsibilities

- Components define UI contracts, parameters, options, state, and processing intent.
- Workers gather inputs, prepare semantic data, call runner APIs, report progress, and set persistent outputs.
- `DataTreeProcessor` handles paths, branch matching, grouping, length normalization, grafting, flattening, and output path strategies.
- Tool/model calls process one logical unit and should not know about `GH_Path` fan-out unless the tool's purpose is explicitly data-tree manipulation.

## Processing topology

Choose a `ProcessingTopology` instead of writing custom branch loops:

- `ItemToItem`: each input item creates a corresponding output item at the same path/index.
- `ItemGraft`: each item creates its own output branch.
- `BranchToBranch`: each branch is one logical unit and keeps its path.
- `BranchFlatten`: each branch is one logical unit and outputs to a flattened result.

## Broadcasting expectations

Flat `{0}` trees have special broadcasting behavior:

- A single flat `{0}` tree does not broadcast to another single same-depth path such as `{1}`.
- It broadcasts when the other input has multiple same-depth paths or deeper/mixed topology.
- A direct `{0}` match with deeper `{0;...}` paths matches only `{0}` unless multiple top-level paths trigger broadcasting.

See `docs/Components/ComponentBase/DataTreeProcessingSchema.md` and `docs/Components/ComponentBase/FlatTreeBroadcasting.md`.
