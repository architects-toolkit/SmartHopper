# Grasshopper C# Scripting

Target the Rhino 8 Grasshopper C# Script component unless the existing component proves otherwise.

## Component contract

- Inputs and outputs are the script's public contract. Give them meaningful names.
- Inspect and preserve parameter order, names, type hints, optionality, and item/list/tree access when editing.
- Use the narrowest useful RhinoCommon or .NET type hint instead of relying on `object` and implicit conversion.
- Match output values and data topology to the declared outputs. Preserve input paths unless the algorithm intentionally changes them.
- The `out` parameter captures standard output. Remove or disable it when unused and avoid high-volume logging.

## Coding guidance

- Keep `RunScript` deterministic for the same inputs and document state.
- Separate pure geometry/data computation from Grasshopper, Rhino document, file, network, or UI side effects.
- Validate null, empty, invalid, degenerate, and tolerance-sensitive inputs at the appropriate boundary.
- Use Rhino document units and tolerances when geometric correctness depends on them.
- Prefer non-mutating geometry operations or duplicate geometry before in-place changes when ownership is uncertain.
- Avoid static mutable state and unbounded caches across solutions.
- Do not access Rhino document or UI state from worker threads without the required marshalling.
- Prefer native components when they express the operation more clearly and portably.

## SmartHopper workflow

- Generate with `script_generate` or `script_generate_and_place_on_canvas`.
- Inspect existing code with `script_review` before editing.
- Apply edits with `script_edit_and_replace_on_canvas` so component identity and canvas wiring are preserved.
- Verify compile/runtime messages and output topology after placement or replacement.

## Sources

- [Grasshopper Scripting: C#](https://developer.rhino3d.com/guides/scripting/scripting-gh-csharp/)
- [Essential C# Scripting for Grasshopper](https://developer.rhino3d.com/en/guides/grasshopper/csharp-essentials/)
