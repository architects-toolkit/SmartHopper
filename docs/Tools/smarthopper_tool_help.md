# smarthopper_tool_help

Returns metadata, usage guidance, and relationship hints for any SmartHopper tool.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/smarthopper_tool_help.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-07-05 |
| **Documentation Maintainer** | Devin AI |
| **Category** | Instructions |
| **Tags** | `instructions`, `meta`, `read-only` |

---

## Why Read This?

`smarthopper_tool_help` is a self-documenting tool. It lets an MCP client look up a specific tool by name and learn its input schema, output schema, tags, annotations, and how it relates to other tools.

---

## End-User Guide

### When to Use

Call `smarthopper_tool_help` when you need to:

- Understand the parameters of a tool you have not used before.
- Check whether a tool mutates the Grasshopper canvas.
- See the output schema a tool returns.
- Find related tools for chaining.

### Parameters

- `tool_name` (string, required): name of the SmartHopper tool to look up.

### Output

Returns a JSON object containing:

- `tool_name` (string)
- `found` (boolean)
- `description` (string)
- `category` (string)
- `tags` (array of strings)
- `mutates_canvas` (boolean)
- `annotations` (object with MCP hints)
- `input_schema` (object)
- `output_schema` (object)
- `similar_tools` (array of related tools with name, description, category, and tags; when the requested tool is found, this is filtered to its category; when not found, this contains the full catalog)

---

## Developer Reference

### Example Request

```json
{
  "tool_name": "gh_get"
}
```

### Looking Up a Tool

```csharp
var arguments = new JObject
{
    ["tool_name"] = "gh_get"
};

var result = await AIToolManager.ExecuteAsync("smarthopper_tool_help", arguments, context);

// result is a tool result interaction containing the tool metadata,
// input schema, output schema, and related tools.
```

### Filtering the Tool Catalog

```csharp
var arguments = new JObject
{
    ["tool_name"] = "unknown_tool"
};

var result = await AIToolManager.ExecuteAsync("smarthopper_tool_help", arguments, context);

// Because the tool was not found, the response includes the full catalog
// in similar_tools so the caller can discover available tools.
```

---

## Architecture & Design

`smarthopper_tool_help` queries the `AIToolManager` catalog at runtime. It is read-only and does not require provider/model metrics. It is designed to help MCP clients discover the tool surface without hardcoding tool metadata.
