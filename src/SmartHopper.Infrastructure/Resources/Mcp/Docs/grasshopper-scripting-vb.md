# Grasshopper VB Scripting

Use VB only when the user requests it or an existing VB script component must be maintained.

- Inspect the existing component and preserve its parameter names, type hints, item/list/tree access, output contract, and language runtime.
- Use RhinoCommon types and document tolerances for geometry operations.
- Preserve branch paths unless the algorithm intentionally changes topology.
- Validate null, empty, invalid, and degenerate inputs.
- Keep computation deterministic and avoid hidden Rhino document, UI, file, or network side effects.
- Do not translate a working VB component to another language unless requested.
- Review compile/runtime messages and outputs after editing.

Use `script_review` before changes and `script_edit_and_replace_on_canvas` to preserve component identity and wiring.
