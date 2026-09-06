# Grasshopper Performance

Measure before optimizing. Use Grasshopper's Profiler and inspect data counts to identify the actual expensive stage.

- Track branch and item counts between stages. Cross-reference matching and accidental grafting can create combinatorial growth.
- Reduce repeated intersections, booleans, projections, meshing, and other expensive geometry operations.
- Disable preview for dense intermediate geometry and preview only useful outputs.
- Avoid recomputing identical geometry in duplicated branches or scripts.
- Avoid broad or repeated `ExpireSolution(true)` calls. Expiration clears caches and recursively affects downstream objects.
- Cache only when inputs, document state, tolerance, and invalidation rules are understood.
- Avoid unnecessary type conversions, serialization, logging, and script standard-output capture inside large loops.
- Support cancellation for long-running work and keep Rhino/Grasshopper UI work on the appropriate UI thread.
- Do not assume multithreading is safe or faster. Parallelize only independent, thread-safe computation; Rhino document and UI access generally require controlled document/UI context.
- Prefer native multithread-capable components when they fit. Validate results because parallel execution does not fix inefficient data topology.

## Sources

- [Multi-threaded Grasshopper components](https://developer.rhino3d.com/guides/grasshopper/multi-treaded-components/)
- [ExpireSolution API](https://developer.rhino3d.com/api/grasshopper/html/M_Grasshopper_Kernel_GH_DocumentObject_ExpireSolution.htm)
- [Grasshopper C# scripting](https://developer.rhino3d.com/guides/scripting/scripting-gh-csharp/)
