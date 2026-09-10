# Tools

Tools are callable operations the AI can invoke (function/tool calling) and utilities exposed to Grasshopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

SmartHopper's tool system extends Grasshopper components with AI-powered capabilities such as text generation, image analysis, canvas manipulation, and document conversion. Understanding the tool ecosystem helps you compose powerful parametric + AI workflows.

**You should read this if you:**

- Want to know which AI operations are available in SmartHopper
- Are building a custom component that calls AI tools directly
- Need to understand the tool result envelope convention
- Want to add a new tool to the SmartHopper ecosystem

---

## End-User Guide

### What Are Tools?

| Tool | Description |
|------|-------------|
| text2text | Generates text based on a prompt with optional instructions |
| text2boolean | Evaluates text against criteria and returns boolean assessments |
| text2textlist | Generates a list of items based on a prompt, count, and type (text/number/integer/boolean) |
| text2json | Generates a JSON object from a prompt, conforming strictly to a provided JSON Schema |
| list_filter | Filters list items based on criteria |
| textlist2boolean | Evaluates list items against criteria and returns boolean results |
| canvas_screenshot | Captures the visible Grasshopper canvas as a bounded base64 PNG |
| viewport_screenshot | Captures the active or named Rhino viewport as a bounded base64 PNG |

### When to Use Them

- **Text tasks**: Use `text2text`, `text2boolean`, `text2textlist`, `text2json`
- **Image tasks**: Use `img2text` (vision), `text2img` (generation), or `canvas_screenshot` / `viewport_screenshot` (capture)
- **Document tasks**: Use `file2md` or `web2md` to convert documents and web pages to Markdown
- **Canvas tasks**: Use `gh_get`, `gh_put`, `gh_move`, `gh_group`, `gh_tidy_up`, `gh_connect`, `gh_disconnect`, `set_ai_provider_and_model` for Grasshopper automation
- **Provider/model tasks**: Use `get_available_providers` (includes a `configured` flag per provider), `get_available_models` to inspect registered AI providers and their supported models, and `set_ai_provider_and_model` to override provider/model on a component
- **Knowledge tasks**: Use `smarthopper_readme`, `smarthopper_tool_help`, `mcneel_forum_search` for contextual guidance

### Visual Guide

<!-- PLACEHOLDER: Screenshot showing the Tools panel or component category in Grasshopper -->
<!-- - Location: SmartHopper tab → Tools panel -->
<!-- - Typical wiring: AI component → tool call → result parsing -->

### Common Questions

**Q: How do I know which tools a model supports?**
A: Check the model's capabilities in the AI Models component. Tool calling requires `AICapability.ToolCalling`.

**Q: Can I use tools without the chat interface?**
A: Yes. Many tools are wrapped as standalone Grasshopper components (e.g., `AIImg2TextComponent`, `AIFile2MdComponent`).

---

## Developer Reference

### API Overview

SmartHopper tools are represented by `AITool` and executed through `AIToolCall`. Registration and dispatch are handled by `AIToolManager`.

```csharp
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AIModels;

var tool = new AITool(
    name: "my_custom_tool",
    description: "Does something useful.",
    category: "Custom",
    parametersSchema: "{ \"type\": \"object\" }",
    execute: async (toolCall) =>
    {
        // Read the pending tool call, do work, and return an AIReturn with a tool result.
        return new AIReturn();
    },
    requiredCapabilities: AICapability.Text2Text,
    mutatesCanvas: false);

AIToolManager.RegisterTool(tool);
```

### Key Types

| Type | Purpose |
| --- | --- |
| `AITool` | Immutable contract for all tools |
| `AIToolCall` | Request to execute one registered tool |
| `AIReturn` | Normalized result with body, metrics, and diagnostics |
| `ToolResultEnvelope` | Optional metadata attached to a tool result payload |
| `AIToolManager` | Registry and execution dispatcher |

### Code Examples

#### Creating a Simple Tool

For a complete, registered tool implementation, see `src/SmartHopper.Core.Grasshopper/AITools/smarthopper_readme.cs`.

```csharp
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;

var tool = new AITool(
    name: "my_custom_tool",
    description: "Returns an uppercase greeting.",
    category: "Custom",
    parametersSchema: "{ \"type\": \"object\", \"properties\": { \"name\": { \"type\": \"string\" } }, \"required\": [\"name\"] }",
    execute: async (toolCall) =>
    {
        var call = toolCall.Body.PendingToolCallsList().First();
        var name = call.Arguments["name"]?.ToString() ?? "World";

        var body = AIBodyBuilder.Create()
            .AddToolResult(
                result: new JObject { ["greeting"] = $"HELLO, {name.ToUpperInvariant()}!" },
                id: call.Id,
                name: "my_custom_tool")
            .Build();

        var result = new AIReturn();
        result.CreateSuccess(body);
        return result;
    },
    requiredCapabilities: AICapability.None,
    mutatesCanvas: false);
```

**Output**: An `AIReturn` whose body contains an `AIInteractionToolResult`.

#### Executing a Tool Programmatically

```csharp
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

var body = AIBodyBuilder.Create()
    .Add(new AIInteractionToolCall
    {
        Id = "call_1",
        Name = "my_custom_tool",
        Arguments = new JObject { ["name"] = "Grasshopper" }
    })
    .Build();

var toolCall = new AIToolCall { Body = body };
var result = await AIToolManager.ExecuteTool(toolCall);
```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| Tool not found | Name mismatch or tool not registered | Verify tool name and that the assembly is loaded |
| Missing parameter | Required parameter not provided | Check tool schema and supply all required fields |
| Capability mismatch | Model does not support required capability | Select a model with the appropriate capability flags |
| Execution timeout | Tool operation took too long | Increase timeout or optimize the tool implementation |

---

## Architecture & Design

### Design Rationale

**Problem**: Grasshopper users need AI-powered operations (text generation, image analysis, canvas manipulation) but there is no standard way to expose them across providers and components.

**Approach**: Define a unified `AITool` contract with JSON-schema inputs and structured outputs. Tools are registered in a central `ToolManager`, formatted by providers into their native API shapes, and executed by components or the chat system.

**Trade-offs**:

- Unified tool interface (portable across providers) vs provider-specific feature limitations
- JSON-schema validation (robust) vs rigid parameter structures
- Central registry (discoverable) vs tight coupling to the tool manager

### Data Flow

```text
Component → AIToolRequest → ToolManager → Provider formatting → AI Model
                                              ↓
                                    ToolResult + ToolResultEnvelope

```

### Tool Categories

| Category | Tools |
| --- | --- |
| Instruction & Knowledge | `smarthopper_readme`, `smarthopper_workflows`, `smarthopper_tool_help`, `smarthopper_ghjson_reference`, `file2md`, `web2md`, `mcneel_forum_search`, `mcneel_forum_topic`, `mcneel_forum_post` |
| Text Generation | `text2text`, `text2boolean`, `text2textlist`, `text2json`, `list_filter`, `textlist2boolean` |
| Image Processing | `img2text`, `text2img`, `canvas_screenshot`, `viewport_screenshot` |
| Grasshopper Canvas | `gh_get`, `gh_put`, `gh_move`, `gh_merge`, `gh_group`, `gh_tidy_up`, `gh_list_categories`, `gh_list_components`, `gh_component_preview`, `gh_component_lock`, `gh_connect`, `gh_disconnect` |
| Scripting | `script_generate`, `script_edit`, `script_review` |

### Related Documentation

- [ToolResultEnvelope](./ToolResultEnvelope.md)
- [img2text Tool](./img2text.md)
- [Screenshot Tools](./screenshots.md)
- [smarthopper_readme Tool](./smarthopper_readme.md)
- [smarthopper_workflows Tool](./smarthopper_workflows.md)
- [smarthopper_tool_help Tool](./smarthopper_tool_help.md)
- [smarthopper_ghjson_reference Tool](./smarthopper_ghjson_reference.md)
