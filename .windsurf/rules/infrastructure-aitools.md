---
trigger: model_decision
description: Information about the Tool Manager (AITools)
---

# AITools

- **Purpose**
  - Define AI-callable tools and a central manager to discover, register, and execute them.
  - Provide a safe, structured interface between model tool-calls and application functionality.

- **Core Types**
  - AITool (AITool.cs)
    - Metadata: `Name`, Description, `Category`.
    - Parameters: `ParametersSchema` (JSON schema string).
    - Execution: Execute (`Func<AIToolCall, Task<AIReturn>>`).
    - Compatibility: `RequiredCapabilities` (`AICapability`) for upstream checks.
  - IAIToolProvider (IAIToolProvider.cs)
    - Contract for tool sets discoverable at runtime.
    - GetTools() returns an `IEnumerable<AITool>`.
  - AIToolManager (ToolManager.cs)
    - Static registry and runtime manager.
    - RegisterTool(AITool), GetTools(), ExecuteTool(AIToolCall), DiscoverTools().

- **Discovery & Registration**
  - DiscoverTools() loads the `SmartHopper.Core.Grasshopper` assembly and scans the `SmartHopper.Core.Grasshopper.AITools` namespace.
  - Instantiates classes implementing IAIToolProvider, calls GetTools(), then RegisterTool() for each AITool.
  - Discovery runs once (guarded by `_toolsDiscovered`).

- **Execution Flow**
  1. ExecuteTool(AIToolCall) ensures discovery and fetches the target tool.
  2. Validates the AIToolCall via `toolCall.IsValid()`.
  3. Calls the tool’s Execute(toolCall) to get an AIReturn.
  4. Wraps the result in a manager-level AIReturn with `Request = toolCall` and SetBody(result.Body). Errors are captured in `ErrorMessage`.

- **Integration**
  - Works with AIToolCall and AIReturn from AICall/.
  - Providers may use GetFormattedTools() (in `AIProviders/`) to expose tool function definitions to models.
  - `RequiredCapabilities` allows providers/models to filter tools based on model features (e.g., function-calling, JSON output).

- **Best Practices & Security**
  - Keep Execute implementations side-effect aware; validate and sanitize parameters against `ParametersSchema`.
  - Prefer idempotent operations and clear error messages; log via `Debug.WriteLine` as shown in manager code.
  - Tool discovery is restricted to a trusted assembly/namespace to reduce supply-chain risk.
  - Avoid long-running or UI-thread operations in Execute unless explicitly marshaled to the Rhino UI thread when needed.

- **Typical Usage**
  1. Implement IAIToolProvider in `SmartHopper.Core.Grasshopper.AITools`, return AITool instances.
  2. Each AITool defines metadata, JSON schema, and an async Execute.
  3. The AI model issues a tool call; the system constructs an AIToolCall.
  4. `AIToolManager.ExecuteTool(toolCall)` runs the tool and returns an AIReturn to the tool-calling loop.
