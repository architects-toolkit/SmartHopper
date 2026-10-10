# SmartHopper Tool Strategy

## Read the canvas

- Start with `gh_get_selected` when the user refers to "this", "these", or selected objects.
- Use `gh_report` for a broad canvas summary and `gh_get_errors` for failures.
- Use `gh_get` with `viewportOnly: true` for objects currently in the canvas viewport.
- Use `gh_get` with `typeFilter: ['+startnodes']` / `['+endnodes']` for wide source/sink views without runtime values; add `includeRuntimeData: true` when computed values are required.
- Use `gh_get` `attrFilters` for attribute-specific queries: `+disabled` (locked), `+previewoff`/`+previewon`, `+selected`, `+error`.
- Use `gh_get` `detail: 'summary'` with a `fields` projection for compact reads; request `fields: ['runtimeData']` or `['internalizedData']` only when those values are needed.
- Use `gh_get_by_guid` only after obtaining GUIDs from prior tool output.
- Use `canvas_screenshot` or `viewport_screenshot` only when visual layout, preview, or geometry appearance is needed; prefer structural queries when an image is unnecessary.

## Discover components

1. Call `gh_list_categories` to discover the installed catalogue.
2. Call `gh_list_components` with a narrow category filter and `maxResults`.
3. Request `name`, `description`, `inputs`, and `outputs`; request additional details only when needed.
4. Do not assume a third-party component exists because it appears in online documentation.

## Modify safely

- Inspect the affected objects and immediate context before changing them.
- Use selected-object actions when selection already expresses scope.
- Use GhJSON/GhPatch for structured generation and replacement. Read `smarthopper_ghjson_reference` before manually authoring those formats.
- `gh_put` replaces an existing object when its `instanceGuid` matches; preserve identity only for intentional edits.
- Prefer `gh_set_value` for simple value edits (panel text, toggles, sliders, value lists, persistent parameter data) instead of rebuilding objects through `gh_put`.
- Use `gh_connect` / `gh_disconnect` for known wiring and `gh_smart_connect` when connection intent requires AI inference.
- Ask before `gh_clear`, broad replacement, or changes that may discard user work.
- SmartHopper canvas mutations should support Grasshopper undo. After mutation, inspect errors and relevant outputs and summarize what changed.

## Node terminology

- **startnodes**: no incoming connections.
- **endnodes**: no outgoing connections.
- **middlenodes**: both incoming and outgoing connections.
- **isolatednodes**: neither incoming nor outgoing connections.
