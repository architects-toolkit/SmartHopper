# SmartHopper Workflows

Canonical tool chains for common tasks. Each workflow lists recommended calls in order.

## inspect_canvas

Read the current state of the Grasshopper canvas with the right level of detail.

1. Call `gh_get_selected` when the user refers to selected objects, "this", or "these".
2. Call `gh_get_errors` for failures or `gh_report` for a broad canvas summary.
3. Call `gh_get_selected_with_data`, `gh_get_errors_with_data`, `gh_get_start_with_data`, `gh_get_end_with_data`, or `gh_get_by_guid_with_data` only when computed values are needed.
4. Call `gh_get_start` or `gh_get_end` for wide source/sink views without runtime data.
5. Call `gh_get_by_guid` when GUIDs are already known.
6. Use generic `gh_get` only when no specialized query fits.

## design_definition

Design a robust component workflow before placing it.

1. Establish expected outputs, inputs, units, tolerances, and item/list/tree contracts.
2. Call `smarthopper_readme` with `definition-design`, `data-trees`, or `geometry` when those constraints are unclear.
3. Call `gh_list_categories`, then `gh_list_components` with a category filter and bounded results.
4. Prefer a clear native component network and use scripts only when they materially improve the solution.
5. Generate GhJSON with `gh_generate`; for complex networks, consult `smarthopper_ghjson_reference` first.
6. Validate and inspect the generated structure before placing it.

## diagnose_data_tree

Diagnose incorrect item, list, branch, or path matching.

1. Call `smarthopper_readme` with topic `data-trees`.
2. Inspect the affected component and immediate upstream/downstream objects with runtime data.
3. Compare paths, branch counts, item counts, access modes, nulls, and empty branches.
4. State the intended branch/item correspondence before proposing flatten, graft, simplify, or path mapping.
5. Apply one targeted correction and verify output paths and values.

## edit_script

Modify an existing Grasshopper script component.

1. Identify the component via `gh_get_selected` or `gh_get_by_guid`.
2. Call `script_review` to inspect language, code, parameters, access modes, and diagnostics.
3. Call `smarthopper_readme` with `csharp`, `python`, or `vb` when language guidance is needed.
4. Call `script_edit_and_replace_on_canvas` with the instance GUID and focused instructions.
5. Verify compile/runtime messages and output topology.

## create_script

Create a Grasshopper script component from natural language.

1. Determine language, inputs, outputs, type hints, access modes, paths, units, tolerances, and side effects.
2. Call `smarthopper_readme` with `csharp`, `python`, or `vb`.
3. Call `script_generate` with explicit requirements.
4. Consult `smarthopper_ghjson_reference` when reviewing or adjusting generated GhJSON.
5. Call `gh_put` with `editMode=false`, then verify diagnostics and outputs.

## debug_script

Find and fix problems in a Grasshopper script component.

1. Call `gh_get_errors` or a script-filtered canvas query to locate broken scripts.
2. Call `script_review` on the broken component.
3. Inspect input types, access modes, paths, runtime version, references, and geometry assumptions.
4. Call `script_edit_and_replace_on_canvas` for one focused fix.
5. Verify compile/runtime messages and output topology.

## organize_canvas

Tidy a scoped part of the canvas without changing behavior.

1. Call `gh_get_selected` when the user selected the scope; otherwise query the intended objects.
2. Call `gh_tidy_up` with known GUIDs or `gh_tidy_up_selected`.
3. Optionally call `gh_group` with a descriptive title.
4. Verify that connections, component identity, and solution behavior are unchanged.

## place_components

Add components from a description or GhJSON.

1. Discover installed components when availability is uncertain.
2. Call `gh_generate` for a new network and consult `smarthopper_ghjson_reference` for complex structures.
3. Call `gh_put` with `editMode=false` to place validated GhJSON.
4. Use `gh_connect` to wire new objects to existing ones.
5. Inspect errors and relevant outputs.

## review_performance

Find the actual cause of a slow or unresponsive definition.

1. Call `smarthopper_readme` with topic `performance`.
2. Inspect the canvas, profiler evidence, and expensive component warnings.
3. Inspect branch/item counts around suspected stages and check cross-reference or accidental data multiplication.
4. Check dense previews, repeated geometry operations, duplicated subgraphs, scripts, and repeated expiration.
5. Propose the smallest measurable optimization and preserve output behavior.
6. Re-check runtime and output correctness after the change.

## validate_change

Verify a canvas mutation before reporting completion.

1. Re-read affected objects and their connections.
2. Call `gh_get_errors` and compare relevant runtime messages.
3. Inspect final output values or geometry when available.
4. Confirm expected item/list/tree paths and counts.
5. Summarize changed objects, preserved scope, verification evidence, and remaining uncertainty.

## search_knowledge

Search McNeel, Ladybug, another Discourse forum, or a web page.

1. Call `smarthopper_readme` with topic `knowledge` or `sources`.
2. Search with `mcneel_forum_search`, `ladybug_forum_search`, or `discourse_forum_search`.
3. Retrieve promising content with the matching McNeel, Ladybug, or generic Discourse topic/post tool.
4. Summarize long discussions with the matching summarization tool.
5. Use `web2md` for general pages and distinguish official documentation from community advice.

## compare_definitions

Compare two Grasshopper definitions.

1. Obtain the two GhJSON documents with canvas query tools.
2. Call `gh_diff` with the documents.
3. Review the returned `.ghpatch` or summary and explain behavioral and topology differences.

## apply_patch

Apply a structured `.ghpatch` change.

1. Obtain the base GhJSON and patch documents.
2. Consult `smarthopper_ghjson_reference` with topic `ghpatch` or `validation`.
3. Call `gh_patch_validate` before applying the patch.
4. Call `gh_patch_apply` and review conflicts.
5. Call `gh_put` with the result and `editMode=true`.
6. Re-read the affected objects, errors, outputs, and data topology before reporting completion.
