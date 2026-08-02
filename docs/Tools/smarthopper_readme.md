# smarthopper_readme

AI tool that returns detailed operational guidance to the agent on demand.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/smarthopper_readme.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-07-04 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`smarthopper_readme` enables a *tool-as-documentation* pattern where the chat system prompt stays short and stable, while domain-specific workflows and edge-case constraints are pulled only when needed. This reduces token usage and makes system prompts easier to iterate on.

**You should read this if you:**

- Want safety/UX constraints and operational conventions without bloating the system prompt
- Want the assistant to follow consistent internal conventions
- Are working with canvas inspection, GhJSON workflows, scripting, or knowledge searches

For **canonical sequences of tool calls**, use `smarthopper_workflows` instead.

---

## End-User Guide

### When to Use

Call `smarthopper_readme` **before** executing domain-specific workflows, especially when:

- You need the operational rules, safety/UX constraints, or conventions for a domain.
- You want the assistant to follow consistent internal conventions.

For the canonical sequence of tool calls, call `smarthopper_workflows` after retrieving the relevant domain guidance.

This is particularly valuable for:

- Canvas inspection and modifications.
- GhJSON read/modify workflows.
- Scripting workflows (creation/editing/review, and constraints like avoiding huge pasted scripts).
- Knowledge searches (forum/web reading and summarization flow).

### Parameters

- `topic` (string, required): which instruction bundle to return.

Supported topics are declared in the tool schema. Examples include:

- `canvas`
- `discovery`
- `scripting`
- `knowledge`
- `ghjson` now redirects to `smarthopper_ghjson_reference` for authoritative GhJSON/GhPatch format documentation.
- (project-specific subtopics such as `selected`, `errors`, `locks`, `visibility` are routed to the `canvas` bundle.)

### Output

The tool returns a tool-result payload containing:

- `topic` (string)
- `instructions` (string): a markdown-formatted instruction block intended for the agent.

The response is returned as a tool result interaction (so it becomes part of the chat/tool trace).

---

## Developer Reference

### Tool Registration Pattern

When registering `smarthopper_readme` in the tool manager, it is typically exposed as a local tool that does not require provider or model metrics:

```csharp
public class SmarthopperReadmeTool : AIToolBase
{
    public override string Name => "smarthopper_readme";

    public override AIToolSchema GetSchema()
    {
        return new AIToolSchema
        {
            Description = "Returns detailed operational guidance for a given topic.",
            Parameters = new Dictionary<string, AIToolParameter>
            {
                ["topic"] = new AIToolParameter
                {
                    Type = "string",
                    Description = "Which instruction bundle to return (e.g., canvas, discovery, scripting, knowledge).",
                    Required = true
                }
            }
        };
    }

    public override Task<AIReturn> ExecuteAsync(JObject arguments, AIToolContext context)
    {
        var topic = arguments["topic"]?.ToString();
        var instructions = InstructionRepository.Get(topic);

        var result = new JObject
        {
            ["topic"] = topic,
            ["instructions"] = instructions
        };

        var output = new AIReturn();
        output.CreateSuccess(AIBodyBuilder.Create()
            .AddToolResult(result, id: context.ToolCallId, name: Name)
            .Build());

        return Task.FromResult(output);
    }
}

```

### Calling the Tool from a Conversation

```csharp
var body = new AIBody();
body.AddInteraction(AIAgent.System, "You are a helpful assistant.");
body.AddInteraction(AIAgent.User, "Help me inspect the Grasshopper canvas.");
body.ToolFilter = "smarthopper_readme";

var req = new AIRequestCall();
req.Initialize(provider: "OpenAI", model: "gpt-5-mini", body: body, endpoint: "/v1/chat/completions", capability: AICapability.Text2Text);

var session = new ConversationSession(req);
var result = await session.RunToStableResult(new SessionOptions { ProcessTools = true });

var last = result.Body.GetLastInteraction(AIAgent.Assistant);
// The assistant will have called smarthopper_readme with topic "canvas" and received guidance.

```

### Tool-as-Documentation Pattern

#### Why

- Keeps system prompts short and easier to iterate on.
- Reduces token usage on every turn.
- Centralizes "how we operate" knowledge into a single authoritative tool.

#### Recommended Usage Pattern

1. Identify which domain the user's request falls into.
2. Call `smarthopper_readme` with the relevant topic to retrieve operational rules and constraints.
3. If the task has a canonical tool sequence, call `smarthopper_workflows` for the named workflow (e.g., `create_script`, `edit_script`, `debug_script`).
4. Follow the returned steps as the authoritative workflow.

---

## Architecture & Design

`smarthopper_readme` is intentionally **local** and does not require provider/model metrics. Instruction text may include edge-case constraints (for example, scripting-specific "do not paste entire scripts"). The tool acts as a centralized knowledge repository for operational rules and conventions; canonical step-by-step workflows are owned by `smarthopper_workflows`.

The returned instructions are formatted as markdown so they can be directly consumed by the AI as context. Because the result is returned as a tool result interaction, it becomes part of the chat trace and can be referenced in subsequent turns if needed.
