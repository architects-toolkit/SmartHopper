# smarthopper_ghjson_reference

Returns reference documentation for the GhJSON and GhPatch formats so the AI can generate, edit, and validate Grasshopper definitions without internalizing the full specification.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/smarthopper_ghjson_reference.cs` |
| **Spec Loader** | `src/SmartHopper.Core.Grasshopper/GhJson/GhJsonSpecLoader.cs` |
| **Embedded Snapshot** | `src/SmartHopper.Core.Grasshopper/Resources/GhJsonSpec/v1.0/` |
| **Sync Script** | `tools/Sync-GhJsonSpecDocs.ps1` |
| **Since Version** | ? |
| **Last Updated** | 2026-07-12 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

`smarthopper_ghjson_reference` is a *tool-as-documentation* resource for the GhJSON format. It keeps the system prompt short while giving the model on-demand access to the authoritative format specification.

**You should read this if you:**

- Need to generate or edit GhJSON documents programmatically.
- Need to produce or apply `.ghpatch` files.
- Want to understand how the specification is embedded and kept up to date.

---

## End-User Guide

### When to Use

Call `smarthopper_ghjson_reference` when a task requires format-level knowledge:

- Creating a new Grasshopper definition from a description or partial JSON.
- Editing an existing definition via `gh_patch_apply` or `gh_put`.
- Validating that generated component, connection, or group objects follow the schema.

For canonical tool-call sequences (e.g., "create a script component"), use `smarthopper_workflows` instead.

### Parameters

- `topic` (string, required): which reference topic to return.

Supported topics:

| Topic | Source | Returns |
| --- | --- | --- |
| `overview` | Both specs | Combined introduction from GhJSON and GhPatch. |
| `specification` | `specification.md` | Full GhJSON format specification. |
| `ghpatch` | `ghpatch.md` | Full GhPatch format specification. |
| `document_structure` | `specification.md` | Top-level document structure. |
| `components` | `specification.md` | Component objects, identification, and settings. |
| `connections` | `specification.md` | Connection and endpoint format. |
| `groups` | `specification.md` | Group objects and membership. |
| `data_types` | `specification.md` | Internalized data trees and type hints. |
| `component_specific_formats` | `specification.md` | `componentState` and extension formats. |
| `validation` | `specification.md` | Validation rules and schema references. |
| `examples` | `specification.md` | Example GhJSON documents. |

### Output

The tool returns a tool-result payload containing:

- `topic` (string): the requested topic.
- `instructions` (string): markdown-formatted reference documentation.

---

## Developer Reference

### Embedded Snapshot

The specification markdown is embedded into `SmartHopper.Core.Grasshopper` as a resource under:

```text
src/SmartHopper.Core.Grasshopper/Resources/GhJsonSpec/v1.0/
```

Files:

- `specification.md` — GhJSON format specification.
- `ghpatch.md` — GhPatch format specification.

The `.csproj` includes them as embedded resources:

```xml
<EmbeddedResource Include="Resources\GhJsonSpec\**\*.*" />
```

### Sync Script

Refresh the local snapshot from the upstream `ghjson-spec` repository:

```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\Sync-GhJsonSpecDocs.ps1
```

Check for drift without modifying files:

```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\Sync-GhJsonSpecDocs.ps1 -Check
```

### Online Fallback

`GhJsonSpecLoader` reads embedded resources by default. When `preferOnline` is set, it fetches from:

```text
https://raw.githubusercontent.com/architects-toolkit/ghjson-spec/main/docs/
```

The `smarthopper_ghjson_reference` tool currently uses the embedded snapshot; online fallback is available in the loader for future scenarios.

### Loading the Spec Directly

To read a spec document without going through the tool layer, use `GhJsonSpecLoader`:

```csharp
// Load the full GhJSON specification from the embedded snapshot.
string specification = await GhJsonSpecLoader.LoadSpecificationAsync();

// Load a single topic, forcing an online fetch with embedded fallback.
string components = await GhJsonSpecLoader.LoadTopicAsync("components", preferOnline: true);
```

### Calling the Tool Programmatically

```csharp
var arguments = new JObject
{
    ["topic"] = "components"
};

var result = await AIToolManager.ExecuteAsync("smarthopper_ghjson_reference", arguments, context);

// result contains the topic and markdown-formatted instructions.
```

---

## Architecture & Design

`smarthopper_ghjson_reference` follows the same tool-as-documentation pattern as `smarthopper_readme` and `smarthopper_workflows`. The actual format documents live in the separate `ghjson-spec` repository; SmartHopper embeds a snapshot so the AI can reference them without a network dependency at runtime.

The loader discovers embedded resources by path fragment rather than by exact resource name, which avoids brittle dependencies on how MSBuild encodes dots in folder names (e.g., `v1.0` may become `v1._0`).
