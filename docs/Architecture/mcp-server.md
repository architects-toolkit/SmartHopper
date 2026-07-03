# MCP Server

Loopback-only MCP transport that exposes SmartHopper's existing `AITool` catalog to external MCP clients without reimplementing GhJSON or the in-process tool pipeline.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Mcp/` |
| **Since Version** | ? |
| **Last Updated** | 2026-07-03 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Read this if you want to understand how SmartHopper's existing tools are exposed over MCP, how the loopback server is started and stopped, and which tools are hidden by default because they mutate the Grasshopper canvas.

You should also read this if you are:

- wiring the `SmartHopperMcpServerComponent` into a Grasshopper definition
- adding or renaming `AITool` implementations that should appear in MCP
- changing the server's security posture, tool allow-list behavior, or mutating-tool policy
- debugging how `tools/list` and `tools/call` map to `AIToolManager` and `AIToolCall`

## End-User Guide

### Overview

SmartHopper's MCP surface is opt-in and local-only. A Grasshopper component starts a shared server on loopback, and MCP clients connect to that server to discover and invoke SmartHopper tools.

The user-facing component is `SmartHopperMcpServerComponent` in `src/SmartHopper.Components/Mcp/`. It exposes the server through these inputs and outputs:

- Inputs:
  - `Enable` — starts or stops the shared server.
  - `Port` — TCP port to bind on loopback.
  - `BearerToken` — optional static bearer token.
  - `ExposeMutatingTools` — whether tools marked as mutating may be exposed.
- Outputs:
  - `Url` — the MCP endpoint when the server is running.
  - `Status` — a short status string.
  - `LastCall` — the most recent successful tool call name.

### Default Configuration

| Setting | Default | Notes |
| --- | --- | --- |
| Port | `26929` | Matches Cordyceps' default for easier documentation parity. |
| Bearer token | empty | Empty means no bearer auth; loopback-only still applies. |
| Enabled tools | `null` | Null or empty means the full read-only surface is eligible. |
| Expose mutating tools | `false` | Mutating tools stay hidden unless explicitly allowed. |
| Server name | `smarthopper` | Reported during MCP `initialize`. |
| Server version | assembly informational version fallback | Can be overridden from options. |
| Bind address | loopback only | `127.0.0.1` and `[::1]` only in phase 1. |

### Security Posture

- **Loopback only.** The server binds to `127.0.0.1` and `[::1]` only.
- **Origin guard.** Requests from unexpected origins are rejected.
- **Bearer token optional.** If a token is configured, requests without `Authorization: Bearer ...` are rejected.
- **Read-only by default.** Tools that alter the canvas are hidden unless `ExposeMutatingTools` is enabled.
- **Allow-list overrides everything.** If `EnabledTools` is set, only those tools are exposed.
- **No file-system or shell access.** MCP only exposes existing `IAIToolProvider` tools.
- **No payload logging.** Requests are logged without GhJSON payload contents.

### What Mutating Tools Mean

A tool is considered mutating when its `AITool.MutatesCanvas` flag is `true`.

- `true` is the default for new tools.
- Read-only, query, validation, and transformation tools should set `mutatesCanvas: false`.
- MCP exposure uses the flag instead of name-prefix heuristics.

That means tools such as `gh_get`, `gh_list_components`, `gh_list_categories`, `gh_diff`, `gh_patch_validate`, `script_review`, `text2json`, `img2text`, `web2md`, and the Discourse readers stay visible by default, while canvas-changing tools remain hidden unless explicitly enabled.

## Developer Reference

### Tool Mapping

`JsonRpcDispatcher` exposes the following MCP methods:

| MCP method | SmartHopper behavior |
| --- | --- |
| `initialize` | Returns the server name, version, and supported capabilities. |
| `tools/list` | Uses `AIToolMcpAdapter.BuildDescriptors()` to project the `AIToolManager` catalog. |
| `tools/call` | Resolves the named tool, builds `AIToolCall`, and executes it on the UI thread. |
| `notifications/initialized` | Acknowledged as a notification. |
| `ping` | Lightweight health check. |
| `resources/*` and `prompts/*` | Reserved for later phases. |

### `tools/call` Payload Contract

The adapter expects MCP clients to send a tool name and a JSON object of arguments. That object is forwarded directly into `AIToolCall.Arguments`.

1. MCP sends `{"method":"tools/call","params":{"name":"gh_get","arguments":{...}}}`.
2. `JsonRpcDispatcher` resolves the tool by name.
3. `AIToolMcpAdapter` checks `EnabledTools` first, then `ExposeMutatingTools`, then `AITool.MutatesCanvas`.
4. `AIToolCall.Exec()` runs on the Grasshopper UI thread.
5. `AIReturn.Body` becomes the MCP response payload.

### Thread Safety and UI Marshalling

Canvas/document work must be serialized. The MCP layer does that with a shared lifecycle object and a UI-thread marshaller.

```csharp
var options = new McpServerOptions
{
    Port = 26929,
    BearerToken = null,
    ExposeMutatingTools = false,
    EnabledTools = null,
    ServerName = "smarthopper",
};
```

```csharp
var adapter = new AIToolMcpAdapter(options);
var server = McpServerLifecycle.Acquire(this, options);
try
{
    var descriptors = adapter.BuildDescriptors();
}
finally
{
    McpServerLifecycle.Release(this, options.Port);
}
```

`McpServerLifecycle` is ref-counted:

- `Acquire(object key, McpServerOptions options)` starts the shared server for a port when needed.
- `Release(object key, int port)` stops it when the last holder leaves.
- `Find(int port)` returns the currently running instance, if any.

### Code-Level Notes

- `McpServerOptions.Clone()` is used when the lifecycle creates the shared server, so later input edits do not mutate a running instance.
- `AIToolMcpAdapter.IsExposed(string toolName)` returns `false` for unknown tools.
- `BuildDescriptors()` sorts descriptors by name after filtering.
- `ExecuteAsync()` returns a tool error if the adapter cannot resolve or run the requested tool.
- `McpServerOptions.EnabledTools` is an allow-list; when non-empty, it overrides the mutating-tool policy.
- Mutating-tool gating now comes from `AITool.MutatesCanvas`, not from tool-name prefixes.

### Representative API Usage

```csharp
var options = new McpServerOptions
{
    Port = 26929,
    BearerToken = "",
    ExposeMutatingTools = false,
};

var adapter = new AIToolMcpAdapter(options, () => AIToolManager.GetTools(), call => call.Exec());
var descriptors = adapter.BuildDescriptors();
```

```csharp
var server = McpServerLifecycle.Acquire(this, options);
var running = McpServerLifecycle.Find(options.Port);
if (running != null)
{
    // server is shared across all holders of the same port
}
McpServerLifecycle.Release(this, options.Port);
```

## Architecture & Design

### Architecture Overview

MCP is an additional client surface, not a replacement for SmartHopper's existing tool pipeline. The server sits on top of the current `AIToolManager` and reuses the same `AITool` implementations that Grasshopper already uses.

The main design goals are:

1. keep GhJSON schema work inside existing tools and `ghjson-dotnet`
2. keep the transport loopback-only and opt-in
3. keep canvas mutations serialized on the UI thread
4. keep read-only tools visible by default while mutating tools stay hidden unless opted in

### Project Layout

Phase 1 is implemented under `src/SmartHopper.Infrastructure/Mcp/` rather than a separate project.

- `McpServer.cs` — loopback HTTP transport, origin guard, bearer-token auth, request limits
- `JsonRpcDispatcher.cs` — MCP method dispatch and result shaping
- `AIToolMcpAdapter.cs` — projects `AIToolManager` into MCP descriptors and calls
- `McpServerOptions.cs` — port, token, allow-list, and mutating-tool settings
- `McpServerLifecycle.cs` — ref-counted singleton server manager
- `McpToolDescriptor.cs` / `McpToolCallResult.cs` — protocol DTOs

The component entry point lives in `src/SmartHopper.Components/Mcp/SmartHopperMcpServerComponent.cs`.

### Phased Rollout

| Phase | Deliverable | Scope |
| --- | --- | --- |
| 0 | Design doc | No code. |
| 1 | Transport and tool bridge | Loopback server, `tools/list`, `tools/call`, read-only tools by default. |
| 2 | Resources | `resources/list`, `resources/read`, embedded docs. |
| 3 | Prompts | `prompts/list`, `prompts/get`. |
| 4 | LAN exposure and stronger auth | Opt-in networking and stricter security. |
| 5 | Streamable HTTP / SSE | Long-running streaming transport support. |

### Decision Points

- **Project placement.** Phase 1 ships under Infrastructure, not as a separate `SmartHopper.Mcp` project.
- **Default port.** `26929` is kept for parity with Cordyceps.
- **Mutating tools off by default.** `McpServerOptions.ExposeMutatingTools = false` and `AITool.MutatesCanvas` control exposure.
- **Component path.** The component lives at `SmartHopper.Components/Mcp/SmartHopperMcpServerComponent.cs`.
- **Attribution surface.** Attribution lives in `THIRD_PARTY_NOTICES.md` and per-file headers under `src/SmartHopper.Infrastructure/Mcp/`.
- **Component-name aliasing.** The orchestration layer already handles aliasing through `ComponentNameAliases` in `SmartHopper.Core.Grasshopper.Utils.Canvas`; no extra MCP-side alias layer is introduced.

### Relationship to GhJSON and Cordyceps

- `ghjson-dotnet` remains the single source of truth for GhJSON.
- SmartHopper does not import `GhJSON.Core` or `GhJSON.Grasshopper` from the MCP transport layer.
- The transport shape is structurally adapted from Cordyceps, but tool discovery is projected from `AIToolManager` instead of reflection-based attributes.
- Existing SmartHopper tools remain the source of behavior; MCP only exposes them.

### Open Questions

1. **Tool namespacing.** The current plan keeps tool names unchanged in phase 1.
2. **Tool schema source.** `AITool.ParametersSchema` is already a JSON Schema string and is parsed into a JSON object for MCP.
3. **GhJSON responses.** A future phase may add a `mimeType` for GhJSON payloads.
4. **Settings UI.** Port and token inputs are surfaced via the component first; a centralized settings UI can come later.
5. **CI coverage.** Infrastructure tests cover the adapter and dispatcher; the HTTP listener path remains an integration concern.

### References

- [Architecture overview](../Architecture.md)
- [Tool catalogue](../Tools/index.md)
- Cordyceps source: https://github.com/brookstalley/cordyceps
- MCP specification: https://modelcontextprotocol.io/
- GhJSON specification: https://github.com/architects-toolkit/ghjson-spec
- `ghjson-dotnet`: https://github.com/architects-toolkit/ghjson-dotnet
