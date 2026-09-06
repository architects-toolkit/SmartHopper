# SmartHopper Operational Instructions

SmartHopper provides focused runtime knowledge instead of one oversized prompt. In WebChat, call `smarthopper_readme` with a topic. In MCP, read the corresponding `docs:///` resource.

## Topics

- `foundations`: components, parameters, wires, dependency flow, and solution behavior.
- `data-trees`: items, lists, trees, paths, access modes, matching, grafting, and flattening.
- `definition-design`: algorithm design, organization, robustness, and maintainability.
- `geometry`: units, tolerances, domains, directions, topology, and Rhino geometry types.
- `debugging`: deterministic diagnosis of component, data, geometry, environment, and performance failures.
- `performance`: profiling, data growth, preview, recomputation, caching, and concurrency.
- `canvas`, `selected`, `locks`, `visibility`, or `discovery`: SmartHopper canvas inspection, component discovery, mutation, and verification strategy.
- `scripting` or `csharp`: Rhino 8 C# script-component guidance.
- `python`: Rhino 8 Python 3 and legacy IronPython guidance.
- `vb`: VB script-component guidance.
- `ghjson`: routes to the authoritative `smarthopper_ghjson_reference` topics.
- `providers`: provider/model discovery and configuration.
- `knowledge`, `mcneel-forum`, `ladybug-forum`, `discourse-forum`, `research`, or `web`: source-aware research workflows.
- `sources`: evidence priority and source-quality rules.

## Core operating pattern

1. Identify the user's goal, scope, and expected output.
2. Inspect current canvas/runtime evidence instead of assuming state.
3. Retrieve the focused topic or workflow needed for the task.
4. Use the smallest specific tool call that answers the current question.
5. Understand item/list/tree topology before connecting or transforming data.
6. Discover installed components before generating unfamiliar component networks.
7. Ask before destructive or broad changes, then preserve unrelated user work.
8. Verify errors and relevant outputs after every mutation.

For canonical step-by-step tool chains, call `smarthopper_workflows` or read `docs:///smarthopper-workflows`. For individual tool schemas, call `smarthopper_tool_help` or read `docs:///tool-help/{toolName}`.
