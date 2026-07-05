# smarthopper_workflows

Returns canonical SmartHopper tool workflows so an MCP client can discover how to chain tools.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/smarthopper_workflows.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-07-05 |
| **Documentation Maintainer** | Devin AI |
| **Category** | Instructions |
| **Tags** | `instructions`, `workflow`, `read-only` |

---

## Why Read This?

`smarthopper_workflows` is a self-documenting tool. It helps an MCP client (or the AI using the tools) understand the recommended sequence of tool calls for common tasks without reading source code or documentation.

---

## End-User Guide

### When to Use

Call `smarthopper_workflows` when you need to know:

- The canonical order of tool calls for a task.
- Which specialized `gh_get_*` variant to use.
- How to create, edit, or debug a script component.
- How to search forums or compare Grasshopper definitions.

### Parameters

- `workflow` (string, optional): name of the workflow to retrieve.

Available workflows include:

- `inspect_canvas`
- `edit_script`
- `create_script`
- `debug_script`
- `organize_canvas`
- `place_components`
- `search_knowledge`
- `compare_definitions`
- `apply_patch`

Omit `workflow` to list all workflows.

### Output

Returns an array of workflows, each with:

- `name` (string)
- `description` (string)
- `steps` (array of strings)

---

## Developer Reference

### Example

```json
{
  "workflow": "apply_patch"
}
```

```json
{
  "workflows": [
    {
      "name": "apply_patch",
      "description": "Apply a structured .ghpatch change to a Grasshopper definition.",
      "steps": [
        "Obtain the base GhJSON document and a .ghpatch document (e.g., from gh_diff).",
        "Call gh_patch_validate on the patch first.",
        "Call gh_patch_apply with the base GhJSON and the patch.",
        "Review any conflicts reported by gh_patch_apply.",
        "Call gh_put with the resulting GhJSON and editMode=true to update the canvas."
      ]
    }
  ]
}
```

Every step is a concrete tool call. Use `smarthopper_tool_help` to look up the exact parameters of any tool.

### Calling the Tool

```csharp
var arguments = new JObject
{
    ["workflow"] = "apply_patch"
};

var result = await AIToolManager.ExecuteAsync("smarthopper_workflows", arguments, context);

// result contains the named workflow with its description and ordered steps.
```

### Listing All Workflows

```csharp
var arguments = new JObject(); // no workflow filter

var result = await AIToolManager.ExecuteAsync("smarthopper_workflows", arguments, context);

// result contains every available workflow so the caller can present a menu
// or pick the most relevant one for the current task.
```

---

## Architecture & Design

`smarthopper_workflows` is intentionally read-only and does not require provider/model metrics. It centralizes workflow knowledge that was previously embedded in the `smarthopper_readme` topic instructions, making it queryable by name for MCP clients.
