# SmartHopper Workflows

Canonical tool chains for common tasks. Each workflow lists the recommended tool calls in order.

## inspect_canvas

Read the current state of the Grasshopper canvas with the right level of detail.

1. Call `gh_get_selected` when the user refers to "selected", "this", or "these".
2. Call `gh_get_errors` to locate broken definitions.
3. Call `*_with_data` tools (for example `gh_get_start_with_data`, `gh_get_end_with_data`) when computed values are needed.
4. Call `gh_get_start` to find data sources (parameters, sliders, first components in the flow) without runtime data.
5. Call `gh_get_end` to find output sinks (panels, previews, last components in the flow) without runtime data.
6. Call `gh_get_by_guid` when GUIDs are already known.
7. Use `gh_get` (generic) as a fallback only when no specialized `gh_get_*` variant fits.

## edit_script

Modify an existing Grasshopper script component.

1. Identify the script component via `gh_get_selected` or `gh_get_by_guid`.
2. Call `script_edit_and_replace_on_canvas` with the `instanceGuid` and instructions.
3. Alternatively, call `script_edit` on the GhJSON, then `gh_put` with `editMode=true`.

## create_script

Create a new Grasshopper script component from natural language.

1. Call `script_generate` with the instructions and preferred language.
2. Call `smarthopper_ghjson_reference` with topic `specification` or `components` when reviewing or adjusting the generated GhJSON.
3. Call `gh_put` with the returned GhJSON and `editMode=false`.

## debug_script

Find and fix problems in a Grasshopper script component.

1. Call `gh_get_errors` or `gh_get` with `categoryFilter=['+Script']` to locate broken script components.
2. Call `script_review` on the broken component's GUID.
3. Call `script_edit_and_replace_on_canvas` to apply the fix.

## organize_canvas

Tidy up selected components on the Grasshopper canvas.

1. Call `gh_get_selected` if the user already selected the components; otherwise use `gh_get` (or a specialized `gh_get` variant) to obtain the component GUIDs.
2. Call `gh_tidy_up` with the GUIDs, or `gh_tidy_up_selected` if the components are already selected.
3. Optionally call `gh_group` with the GUIDs to visually highlight the changed area.

## place_components

Add new components to the canvas from a description or GhJSON.

1. To generate a new network from a description, call `gh_generate` with instructions. For complex networks, call `smarthopper_ghjson_reference` with topic `specification` or `components` first.
2. To generate and place in one step, use `gh_generate_and_place_on_canvas`.
3. Once the GhJSON is ready, call `gh_put` with `editMode=false` to place components on the canvas.
4. Use `gh_connect` to wire the new components to existing ones.

## search_knowledge

Search McNeel or Ladybug Discourse forums for answers.

1. Call `mcneel_forum_search` or `ladybug_forum_search` with the query.
2. Retrieve promising topics/posts with `mcneel_forum_topic_get` or `mcneel_forum_post_get`.
3. Summarize with `mcneel_forum_topic_summarize` or `mcneel_forum_post_summarize`.
4. For general web pages, use `web2md`.

## compare_definitions

Compare two Grasshopper definitions and describe the differences.

1. Get the two GhJSON documents with `gh_get` or `gh_get_by_guid`.
2. Call `gh_diff` with the two documents.
3. Review the returned `.ghpatch` or summary.

## apply_patch

Apply a structured `.ghpatch` change to a Grasshopper definition.

1. Obtain the base GhJSON document and a `.ghpatch` document (for example from `gh_diff`).
2. Call `smarthopper_ghjson_reference` with topic `ghpatch` or `validation` when inspecting or editing the patch by hand.
3. Call `gh_patch_validate` on the patch first.
4. Call `gh_patch_apply` with the base GhJSON and the patch.
5. Review any conflicts reported by `gh_patch_apply`.
6. Call `gh_put` with the resulting GhJSON and `editMode=true` to update the canvas.
