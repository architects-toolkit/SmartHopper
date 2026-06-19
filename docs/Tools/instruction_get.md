# instruction_get

AI tool that returns detailed operational guidance to the agent on demand.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/instruction_get.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`instruction_get` enables a *tool-as-documentation* pattern where the chat system prompt stays short and stable, while domain-specific workflows and edge-case constraints are pulled only when needed. This reduces token usage and makes system prompts easier to iterate on.

**You should read this if you:**

- Need canonical sequences of tools for a given task
- Want safety/UX constraints without bloating the system prompt
- Want the assistant to follow consistent internal conventions
- Are working with canvas inspection, GhJSON workflows, scripting, or knowledge searches

---

## End-User Guide

### When to Use

Call `instruction_get` **before** executing domain-specific workflows, especially when:

- You need the canonical sequence of tools for a given task.
- You need safety/UX constraints (edge cases) without bloating the system prompt.
- You want the assistant to follow consistent internal conventions.

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
- (project-specific subtopics such as `ghjson`, `selected`, `errors`, `locks`, `visibility` may be routed to a common bundle; for GhJSON format details see [ghjson-dotnet](https://github.com/architects-toolkit/ghjson-dotnet))

### Output

The tool returns a tool-result payload containing:

- `topic` (string)
- `instructions` (string): a markdown-formatted instruction block intended for the agent.

The response is returned as a tool result interaction (so it becomes part of the chat/tool trace).

---

## Developer Reference

### Tool Registration Pattern

When registering `instruction_get` in the tool manager, it is typically exposed as a local tool that does not require provider or model metrics:

```csharp
public class InstructionGetTool : AIToolBase
{
    public override string Name => "instruction_get";

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
body.ToolFilter = "instruction_get";

var req = new AIRequestCall();
req.Initialize(provider: "OpenAI", model: "gpt-5-mini", body: body, endpoint: "/v1/chat/completions", capability: AICapability.Text2Text);

var session = new ConversationSession(req);
var result = await session.RunToStableResult(new SessionOptions { ProcessTools = true });

var last = result.Body.GetLastInteraction(AIAgent.Assistant);
// The assistant will have called instruction_get with topic "canvas" and received guidance.

```

### Tool-as-Documentation Pattern

#### Why

- Keeps system prompts short and easier to iterate on.
- Reduces token usage on every turn.
- Centralizes "how we operate" knowledge into a single authoritative tool.

#### Recommended Usage Pattern

1. Identify which domain the user's request falls into.
2. Call `instruction_get` with the relevant topic.
3. Follow the returned steps as the authoritative workflow.

---

## Architecture & Design

`instruction_get` is intentionally **local** and does not require provider/model metrics. Instruction text may include edge-case constraints (for example, scripting-specific "do not paste entire scripts"). The tool acts as a centralized knowledge repository that the AI can query dynamically, allowing the system prompt to remain generic while still enforcing domain-specific operational rules.

The returned instructions are formatted as markdown so they can be directly consumed by the AI as context. Because the result is returned as a tool result interaction, it becomes part of the chat trace and can be referenced in subsequent turns if needed.
