---
trigger: model_decision
description: Information about AICall, AIRequest, AIToolCall, AIBody, AIAgent, AIInteraction*, AIReturn... Related with AI response generation logic
---

# AICall Infrastructure

- **Location**
  - `src/SmartHopper.Infrastructure/AICall/`

- **Purpose**
  - Provide a provider‑agnostic foundation to build, validate, execute, and capture results of AI calls.
  - Normalize diverse provider behaviors into a consistent request/response model with metrics and status.

- **Core Concepts**
  - **Agents & Status**
    - AIAgent: Who speaks (Context/System/User/Assistant/ToolCall/ToolResult).
    - AICallStatus: Call lifecycle (Idle/Processing/Streaming/CallingTools/Finished).
  - **Contracts**
    - IAIInteraction: Common metadata for any message (time/agent/metrics).
    - IAIRequest: What a request must expose (`Provider`, Model, `Capability`, Body, IsValid(), Exec()).
    - IAIReturn: What a result must expose (normalized body, raw payload, metrics, status, error).
  - **Interactions**
    - AIInteractionText: Text + optional reasoning.
    - AIInteractionImage: Image request/result (URL/data, size, quality, style).
    - AIInteractionToolCall / AIInteractionToolResult): Function/tool call and its result.
  - **Request Body**
    - AIBody: Conversation history + optional JSON schema + context/tool filters. Injects context messages dynamically when filters are set; validates body consistency.
  - **Execution**
    - AIRequestBase: Validates provider/model/capabilities; defines the shape of an executable request.
    - AIRequestCall: Adds HTTP specifics, computes effective capabilities (e.g., needs structured output or tools), encodes and executes async.
    - AIToolCall: Executes a specific tool via `AIToolManager` (used during tool-calling loops).
  - **Result**
    - `AIReturn`: Normalized result object with processed body, raw provider data, metrics, status, and error details.

- **Typical Flow**
  1. Build AIBody with interactions (text/image/tool), optional predefined output JSON schema, and filters.
  2. Create AIRequestCall (provider, model, capability, endpoint, body); IsValid() is automatically triggered on Exec().
  3. Exec() to get `AIReturn` with results + metrics + raw payload.
  4. If tool calls are returned, execute them (AIToolCall), append AIInteractionToolResult to Body.Interactions, and re‑invoke until no pending tools remain. // TODO: execute tools automatically in Exec().

- **Why This Design**
  - **Provider‑agnostic**: Centralizes capability checks and request formatting while allowing provider‑specific encoding.
  - **Reliability**: Normalized results with clear status, metrics, and raw fallbacks for debugging.
  - **Extensibility**: New providers and interaction types plug into existing contracts.
  - **Safety & Validation**: Capability validation prevents unsupported features at runtime (e.g., structured output or tool‑calling).

- **Where to Look**
  - Requests: AIRequestBase.cs, AIRequestCall.cs, AIToolCall.cs
  - Results: AIReturn.cs
  - Body & Interactions: AIBody.cs, AIInteractionText.cs, AIInteractionImage.cs, AIInteractionToolCall.cs, AIInteractionToolResult.cs
  - Enums & Interfaces: AIAgent.cs, AICallStatus.cs, IAIRequest.cs, IAIReturn.cs, IAIInteraction.cs