# Grasshopper Debugging

Use evidence from the current definition and change one cause at a time.

## Diagnostic order

1. Restate the expected result and identify the smallest failing output.
2. Read component errors and warnings with `gh_get_errors` or inspect the canvas with `gh_report`.
3. Inspect selected or implicated objects and their immediate upstream inputs.
4. Check input data types, implicit conversions, and item/list/tree access.
5. Inspect branch paths, branch counts, item counts, nulls, empty branches, and matching behavior.
6. Check units, tolerance, curve/surface domains, directions, seams, planes, and geometry validity.
7. Check missing plug-ins, unavailable Rhino references, locked components, disabled solver state, and stale external data.
8. Isolate the smallest subgraph that reproduces the problem.
9. Apply one targeted correction.
10. Re-read errors and verify relevant final outputs and data topology.

## Failure categories

- **Compile error**: script syntax, missing reference, unsupported language feature, or incompatible API.
- **Runtime error**: the component or script threw while processing current inputs.
- **Warning**: processing continued but assumptions or outputs may be incomplete.
- **Empty valid result**: no error occurred, but geometry or filtering produced no values.
- **Data mismatch**: values are valid but item/list/tree matching is wrong.
- **Geometry mismatch**: type, domain, orientation, tolerance, or validity is wrong.
- **Environment failure**: plug-in, provider, file, network, or referenced Rhino object is unavailable.
- **Performance failure**: data growth, repeated recomputation, preview, or expensive geometry makes the solution impractical.

Do not hide an error by flattening data, swallowing exceptions, disabling components, or replacing unavailable functionality without confirming the intended behavior.
