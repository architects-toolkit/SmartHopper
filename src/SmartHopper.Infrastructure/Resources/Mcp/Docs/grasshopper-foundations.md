# Grasshopper Foundations

Grasshopper is Rhino's graphical algorithm editor. A definition is a reactive network whose objects exchange data and recompute when upstream inputs change.

## Core vocabulary

- **Document/definition**: the saved Grasshopper graph (`.gh` or `.ghx`).
- **Canvas**: the visual editor containing document objects.
- **Component**: an operation with typed input and output parameters. It reads inputs during a solution and emits outputs for downstream objects.
- **Parameter**: an input, output, or standalone data container. It has a data type and item/list/tree access behavior.
- **Wire**: a directed connection from an output parameter to a compatible input parameter.
- **Source**: an object with no incoming connection, such as a slider, panel, referenced geometry parameter, or other initial value.
- **Processor**: an object with both upstream inputs and downstream outputs.
- **Sink**: an object with no downstream recipients, such as a final parameter, panel, preview, or bake/export operation.

## Dependency and solution behavior

- Data normally flows upstream to downstream. A changed source expires dependent objects and causes affected downstream data to be recomputed.
- Ordinary feedback wiring is invalid because a value cannot depend on itself during the same solution. Specialized loop or iteration components may provide controlled multi-solution behavior; do not model those as ordinary wires.
- A component may run more than once when Grasshopper enumerates item or list inputs. Parameter access and data matching therefore affect both results and performance.
- Runtime data is transient and recomputed. Persistent data is stored in parameters or component settings. Referenced Rhino geometry depends on the active Rhino document and can become unavailable.

## Canvas state

- **Selected** identifies the objects the user is currently referring to.
- **Locked/disabled** objects do not solve normally. SmartHopper's MCP protection is a separate safeguard against remote updates.
- **Preview** controls viewport display, not whether data exists.
- Component warnings and errors describe runtime state; an object can also produce an empty but valid result without an error.
- Component names, nicknames, category placement, and availability vary by Rhino version and installed plug-ins. Discover the live catalogue before relying on a component.

## Sources

- [Rhino 8 Grasshopper command documentation](https://docs.mcneel.com/rhino/8/help/en-us/commands/grasshopper.htm)
- [Essential Algorithms and Data Structures for Grasshopper](https://developer.rhino3d.com/en/guides/grasshopper/gh-algorithms-and-data-structures/)
