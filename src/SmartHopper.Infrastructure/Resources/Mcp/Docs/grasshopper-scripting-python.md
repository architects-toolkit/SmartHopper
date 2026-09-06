# Grasshopper Python Scripting

Rhino environments may contain Rhino 8 Python 3 script components and legacy GhPython/IronPython components. Detect the existing component language and runtime instead of assuming compatibility.

- Python 3 and IronPython 2.7 differ in syntax, standard library, package support, and .NET interop behavior.
- Preserve input/output names, type hints, optionality, and item/list/tree access when editing.
- Inputs are exposed under their parameter names. Assign output values to the corresponding output names.
- Prefer RhinoCommon for explicit geometry behavior; use `rhinoscriptsyntax` only when its convenience and document behavior are appropriate.
- Preserve data-tree paths unless the requested algorithm requires a topology change.
- Validate null, empty, invalid, degenerate, and tolerance-sensitive geometry.
- Avoid global mutable state, unbounded caches, hidden file/network access, and Rhino document mutation during ordinary computation.
- Do not introduce third-party Python packages until their availability in the target runtime is established.
- Review the component after generation and verify runtime messages and output topology.

## Source

- [Rhino scripting guides](https://developer.rhino3d.com/guides/scripting/)
