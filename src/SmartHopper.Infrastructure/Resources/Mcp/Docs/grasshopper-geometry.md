# Grasshopper Geometry

Geometry operations are sensitive to type, coordinate system, parameterization, units, tolerance, validity, and orientation.

## Checks before operating

- Read the Rhino document units and use document absolute and angle tolerances where applicable; do not invent a fixed epsilon without justification.
- Distinguish world coordinates from construction planes and local object planes.
- Confirm whether the operation expects a curve, surface, trimmed Brep face, Brep, extrusion, mesh, or generic geometry.
- Validate geometry and handle null, empty, degenerate, very short, zero-area, non-manifold, or self-intersecting cases as relevant.

## Curves and surfaces

- Curve parameters belong to the curve's domain and are not necessarily normalized to `0..1`.
- Closed curves have a seam and direction; periodic and merely closed curves are not identical.
- Surface parameters belong to U/V domains. Surface direction, transpose state, and seam location can affect downstream logic.
- A trimmed Brep face is not equivalent to its underlying untrimmed surface.
- Reparameterization changes the parameter domain, not the physical geometry.

## Solids and meshes

- A Brep may be open or closed; do not assume it is a valid solid.
- Boolean and intersection operations are tolerance-sensitive and may return multiple or empty results.
- Mesh results depend on meshing settings and topology. Do not substitute meshes for precise Brep operations without explaining the trade-off.

## Transformations and references

- Transformation order matters. Build and apply transforms in the intended coordinate frame.
- Prefer non-mutating geometry operations or duplicate geometry before in-place changes when ownership is uncertain.
- Referenced Rhino objects depend on the active document and may be deleted, replaced, transformed, or unavailable.
- Baking modifies the Rhino document and should occur only when explicitly requested.

## Source

- [Rhino and Grasshopper developer guides](https://developer.rhino3d.com/guides/)
