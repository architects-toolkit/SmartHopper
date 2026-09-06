# SmartHopper Operational Instructions

This document covers the most common SmartHopper operational topics. For a focused topic, call the `smarthopper_readme` tool with the appropriate `topic` argument.

## Canvas state reading

- `gh_report`: generate a comprehensive markdown status report of the canvas (object counts, topology, groups, scribbles, viewport, errors/warnings, metadata). Optionally include an AI summary. Read-only.
- Use `gh_get_selected` when the user refers to "this/these/selected".
- Use `gh_get_errors` to locate broken definitions.
- Use `gh_get_locked` / `gh_get_preview_off` / `gh_get_preview_on` for quick attribute-based filters.
- Use `gh_get_visible` when the user refers to components currently on screen (viewport-based).
- Use `gh_get_start` / `gh_get_end` to get a wide view of data sources (startnodes) or outputs (endnodes).
- Use `gh_get_start_with_data` / `gh_get_end_with_data` to inspect initial values or final outputs with runtime data.
- Use `gh_get_by_guid` only when you already have GUIDs from prior steps.
- Use `gh_get` (generic) only when a specialized tool does not fit.

### Node types terminology

- **startnodes**: components with no incoming connections (data sources like parameters, sliders).
- **endnodes**: components with no outgoing connections (data sinks like panels, preview).
- **middlenodes**: components with both incoming and outgoing connections (processors).
- **isolatednodes**: components with neither incoming nor outgoing connections.

### Quick actions on selected components (no GUIDs needed)

- `gh_group_selected`
- `gh_tidy_up_selected`
- `gh_component_lock_selected` / `gh_component_unlock_selected`
- `gh_component_hide_preview_selected` / `gh_component_show_preview_selected`

### Modifying canvas

- `gh_group`, `gh_move`, `gh_tidy_up`, `gh_component_toggle_lock`, `gh_component_toggle_preview`
- `gh_put`: place components from GhJSON; when `instanceGuid` matches existing, it replaces it (prefer user confirmation).
- `gh_connect` / `gh_disconnect`: wire or unwire existing components by GUID and parameter name.
- `gh_smart_connect`: AI-suggested wiring — provide component GUIDs and a purpose description; the AI proposes and executes connections.
- `gh_clear`: clear the canvas (optionally keep locked components); protected components are always preserved. Destructive — prefer user confirmation.

## Scripting

### Scripting rules

- When the user asks to CREATE or MODIFY a Grasshopper script component, use the scripting tools (do not only reply in natural language).
- All scripting happens inside Grasshopper script components, not an external environment.
- Do not propose or rely on traditional unit tests or external test projects.
- For manual inspection, instruct the user to open the script component editor (double-click in Grasshopper).
- Avoid copying full scripts from the canvas into chat (keep context small).
- Use fenced code blocks only when discussing a specific snippet or when an operation fails and the user must manually apply code.

### Tools

- `script_generate`: generate a new script component as GhJSON (not placed).
- `script_generate_and_place_on_canvas`: generate a new script component and place it on the canvas in one call.
- `script_review`: review an existing script component by GUID.
- `script_edit_and_replace_on_canvas`: edit an existing script and replace it on the canvas in one call.

For canonical step-by-step workflows, call `smarthopper_workflows` with `workflow: create_script`, `workflow: edit_script`, or `workflow: debug_script`.

## Providers and models

- SmartHopper reads the default provider and model from the environment settings (SmartHopper settings in Rhino/Grasshopper).
- By default, all AI calls use the provider and model set in the environment. You do not need to set them on every component unless you want a per-component override.
- To discover the available providers and whether they are properly configured, call `get_available_providers`. The response includes a `configured` flag for each provider. Only those whose `configured` flag is `true` can be used to run AI calls.
- To list the models available for a specific provider, call `get_available_models` with the provider name.
- To override the provider and model on a component that supports provider selection, use the `set_ai_provider_and_model` tool.
- To configure a provider, open the SmartHopper settings from the Rhino/Grasshopper menu and set the required fields. Most providers require an API key. Some local or custom providers may also require a base endpoint URL (for example `http://localhost:11434`).

## Discovering components

1. `gh_list_categories`: discover available categories first (saves tokens).
2. `gh_list_components`: then search within categories.
   - Always pass `includeDetails=['name','description','inputs','outputs']` unless you truly need more.
   - Always pass `maxResults` to prevent token overload.
   - Use `categoryFilter` with `+/-` tokens to narrow scope.
