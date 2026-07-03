# MCP server

> Status: **phase 1 implemented**. SmartHopper exposes its existing AI tools to external MCP clients (Claude Desktop, Cursor, VS Code, Claude Code, etc.) over a local loopback HTTP/JSON-RPC server. This document describes the resulting architecture; phases 2–5 (resources, prompts, LAN exposure, streaming) remain design-only. It is informed by `brookstalley/cordyceps` (MIT) but adapted to SmartHopper's existing architecture and GhJSON-first principle.

## 1. Goal and scope

Add an opt-in **Model Context Protocol** (MCP) surface to SmartHopper so that external AI agents can discover and invoke SmartHopper's existing AI tools over a local HTTP transport, **without** any duplication of the GhJSON schema layer that lives in `architects-toolkit/ghjson-dotnet`.

In scope:

- A new transport project that speaks MCP (JSON-RPC 2.0 over HTTP / Streamable HTTP) and is hosted in-process inside Grasshopper.
- An adapter that exposes existing `AITool` instances (registered via `IAIToolProvider` and `AIToolManager`) as MCP tools.
- A user-placed Grasshopper component (`SmartHopperMcpServerComponent`) that starts/stops the server, binds to `127.0.0.1` by default, and surfaces server status.
- Embedded knowledge resources (`gh://docs/*`) reusing existing `/docs/Tools/*` Markdown.

Explicitly **out of scope**:

- Re-implementing GhJSON serialization, validation, `Put`, or migration logic. All schema work continues to live in `ghjson-dotnet` and is reached only through existing `AITool` execution paths.
- Remote network exposure (non-loopback), authentication providers beyond a static bearer token, or multi-user session handling. Those are explicitly deferred.
- Replacing the existing in-process tool-calling loop (`AIInteraction*` + `AIToolCall.Exec()`); MCP is an additional client surface, not a replacement.

## 2. Why MCP, why now

- SmartHopper already owns a rich `IAIToolProvider` surface (canvas, script, document, provider/model orchestration). Today those tools are only reachable from SmartHopper's in-component chat UI. MCP unlocks the same tools for any MCP-aware editor / agent.
- The space already has a reference implementation (Cordyceps, MIT-licensed). Its transport choice (HTTP + JSON-RPC + reflection-discovered tools) is a sane baseline.
- SmartHopper provides additional value Cordyceps does not (provider abstraction, GhJSON-first orchestration, fan-out / batching). Exposing those tools via MCP brings unique value to non-SmartHopper agents.

## 3. Architecture overview

```
                                +-------------------------------------------+
                                |  External MCP client                      |
                                |  (Claude Desktop, Cursor, VS Code, …)     |
                                +----------------------+--------------------+
                                                       | HTTP / JSON-RPC 2.0
                                                       v
+----------------------------------------------------------------------------+
|  SmartHopper.Mcp  (new project, opt-in, .NET 7 net7.0/net7.0-windows)      |
|                                                                            |
|  +-------------------+   +------------------+   +----------------------+   |
|  | McpHttpListener   |-->| JsonRpcDispatcher|-->| AIToolMcpAdapter     |   |
|  | (HttpListener +   |   | (initialize,     |   | (maps MCP tool calls |   |
|  |  loopback bind,   |   |  tools/list,     |   |  to AIToolCall via   |   |
|  |  CORS, SSE)       |   |  tools/call,     |   |  AIToolManager)      |   |
|  +-------------------+   |  resources/list, |   +----------+-----------+   |
|                          |  resources/read) |              |               |
|                          +---------+--------+              |               |
|                                    |                       |               |
|                                    v                       v               |
|                          +------------------+   +----------------------+   |
|                          | ResourceRegistry |   | RhinoUiMarshaller    |   |
|                          | (gh://docs/*)    |   | (SemaphoreSlim +     |   |
|                          +------------------+   |  GH_DocumentEditor)  |   |
|                                                 +----------+-----------+   |
+------------------------------------------------------------|---------------+
                                                             v
+----------------------------------------------------------------------------+
|  Existing SmartHopper layers (unchanged)                                   |
|                                                                            |
|  SmartHopper.Infrastructure                                                |
|    - AIToolManager (static registry, ExecuteTool(AIToolCall))              |
|    - IAIToolProvider, AITool, AIToolCall, AIReturn                         |
|                                                                            |
|  SmartHopper.Core.Grasshopper.AITools                                      |
|    - gh_put / gh_get / gh_move / gh_group / ...                            |
|    - Uses GhJson.* from ghjson-dotnet for all schema work                  |
|                                                                            |
|  architects-toolkit/ghjson-dotnet (NuGet)                                  |
|    - Schema, Put, Validate, Fix, Sugiyama layout, migration                |
+----------------------------------------------------------------------------+
```

### Key invariants

1. **No GhJSON re-implementation.** Every MCP `tools/call` ultimately invokes an existing `AITool` via `AIToolManager.ExecuteTool(AIToolCall)`. That tool — and only that tool — talks to `ghjson-dotnet`. The MCP layer treats `AIReturn.Body` as opaque JSON.
2. **Single UI thread.** All canvas / document mutations are serialized through `RhinoUiMarshaller.ExecuteOnUiThreadAsync(Func<Task<T>>)`, which is a `SemaphoreSlim`-protected wrapper around the existing UI-marshalling helpers in `SmartHopper.Core.Grasshopper.Utils.Canvas`.
3. **Opt-in.** The MCP server only runs when a `SmartHopperMcpServerComponent` is present on at least one open Grasshopper document. Removing the last instance stops the server.
4. **Loopback-only by default.** `HttpListener` is bound to `http://127.0.0.1:<port>/`. A future change can add a settings flag for LAN exposure, gated behind a bearer-token requirement.

## 4. Project layout

Phase 1 lands the MCP transport inside the existing `SmartHopper.Infrastructure` project instead of carving out a new standalone project. This keeps the dependency graph flat (only `SmartHopper.Components` already references `SmartHopper.Infrastructure`) and lets future phases peel out into a dedicated `SmartHopper.Mcp` assembly if and when a second host or out-of-process surface is introduced.

```
src/SmartHopper.Infrastructure/Mcp/
├── McpServer.cs                          # HttpListener (127.0.0.1 + [::1]), origin guard, bearer auth
├── JsonRpcDispatcher.cs                  # initialize / tools/list / tools/call / notifications / ping
├── AIToolMcpAdapter.cs                   # AITool <-> MCP tool descriptor; executes via AIToolCall
├── McpToolDescriptor.cs                  # name, description, inputSchema (from AITool.ParametersSchema)
├── McpToolCallResult.cs                  # success/error wrapper for tools/call
├── McpServerLifecycle.cs                 # ref-counted singleton per port
└── McpServerOptions.cs                   # port, bearer token, enabled-tools allow-list, mutating policy
```

Future phases (resources, prompts, optional Streamable HTTP) will add `Resources/`, `Prompts/`, and `Transport/` subfolders under the same root, or graduate to a dedicated `SmartHopper.Mcp` project if cross-host reuse becomes a requirement.

The user-facing component lives in `SmartHopper.Components`:

```
src/SmartHopper.Components/Mcp/
└── SmartHopperMcpServerComponent.cs      # opt-in; Inputs: Enable, Port, BearerToken, ExposeMutatingTools. Outputs: Url, Status
```

### Dependencies

| Reference                                              | From                       | To                          | Reason                                                       |
|--------------------------------------------------------|----------------------------|-----------------------------|--------------------------------------------------------------|
| `SmartHopper.Infrastructure/Mcp` → `Infrastructure.AITools` | existing project           | existing namespace          | Reach `AIToolManager`, `IAIToolProvider`, `AIToolCall`.      |
| `SmartHopper.Components/Mcp` → `SmartHopper.Infrastructure` | existing                   | existing                    | Component hosts `McpServerLifecycle`.                        |
| `SmartHopper.Infrastructure/Mcp` ↛ `ghjson-dotnet`     | **forbidden**              |                             | Schema work stays inside existing `AITool` implementations.  |

Phase 1 only depends on `Newtonsoft.Json` (already in the solution) plus `System.Net.Http` and `System.Net.HttpListener` from the BCL. No additional NuGet dependencies are introduced.

## 5. MCP method mapping

| MCP method            | Maps to                                                                                 |
|-----------------------|------------------------------------------------------------------------------------------|
| `initialize`          | Returns server name (`smarthopper`), version, capabilities (`tools`, `resources`).      |
| `tools/list`          | `AIToolMcpAdapter.BuildDescriptors()` — wraps each `AITool` from `AIToolManager.GetTools()`. |
| `tools/call`          | Builds `AIToolCall(name, arguments)` and calls `AIToolCall.Exec()`. Returns `AIReturn.Body` verbatim. |
| `resources/list`      | `ResourceRegistry.List()` — enumerates embedded `/docs/Tools/*.md` and `/docs/Architecture/*.md`. |
| `resources/read`      | Returns the embedded Markdown body for the matching `gh://docs/<path>` URI.             |
| `prompts/list` / `prompts/get` | **Deferred** to phase 2 (see §9).                                              |
| `logging/setLevel`    | Sets a per-session `DebugLog` level forwarded to `Debug.WriteLine` (no new sink).       |
| `notifications/*`     | One-way notifications (tool list updates) when components are added/removed.            |

### `tools/call` payload contract

1. MCP client sends: `{ "method": "tools/call", "params": { "name": "gh_put", "arguments": { ... } } }`.
2. `JsonRpcDispatcher` resolves the `AITool` by name. If unknown → JSON-RPC error `-32601`.
3. The `arguments` object is forwarded as `AIToolCall.Arguments` (it is already JSON; no Grasshopper-specific shaping happens here).
4. `RhinoUiMarshaller.ExecuteOnUiThreadAsync(() => AIToolCall.Exec())` runs the tool serialized against the UI thread.
5. `AIReturn.Body` is returned to the client as the JSON-RPC `result.content[0].text` payload (the MCP "text content" envelope). Structured error messages on the `AIReturn` (origins `Tool`, `Provider`, `Network`) are surfaced as `isError: true` MCP responses; shape/metrics validation messages from the inner pipeline are intentionally suppressed.

This is the entire bridge. There is no GhJSON marshalling here; the existing `AITool.Execute` delegates handle that themselves.

## 6. Thread-safety and UI marshalling

```csharp
// SmartHopper.Mcp/Hosting/RhinoUiMarshaller.cs (sketch)
internal sealed class RhinoUiMarshaller
{
    private static readonly SemaphoreSlim _gate = new(1, 1);

    public static async Task<T> ExecuteOnUiThreadAsync<T>(Func<Task<T>> body)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await CanvasAccess.RunOnUiThreadAsync(body).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

- `CanvasAccess.RunOnUiThreadAsync` already exists in `SmartHopper.Core.Grasshopper`. The marshaller adds a single global mutex so that concurrent HTTP requests cannot race on the document.
- This mirrors Cordyceps' `GrasshopperContext.ExecuteOnUiThread` pattern but reuses SmartHopper's existing helpers.
- Long-running tool calls do **not** block the HttpListener thread — `HttpListener` callbacks are `async`, the marshaller is awaited, and other requests queue on `_gate`. This intentionally serializes canvas mutations, which matches the GH document's single-threaded model.

## 7. Component and lifecycle

```
SmartHopperMcpServerComponent (sealed, no GhJSON output)
├── Inputs:
│     - Enable (bool, default false) — toggles the server.
│     - Port (int, default 26929) — only consulted on Enable rising edge.
│     - BearerToken (string, optional) — empty disables auth (loopback only).
├── Outputs:
│     - Url (string) — "http://127.0.0.1:<port>/mcp" once running.
│     - Status (string) — "Stopped" | "Starting" | "Running on …" | "Error: …".
│     - LastCall (string) — name of the most recent successful tool call.
└── Behavior:
      - Solve-time toggle via McpServerLifecycle.AcquireOrRelease(this, Enable).
      - Lifecycle is ref-counted: multiple components on multiple documents
        share one server instance per (port, token) tuple.
      - Removing or disabling the last referencing component stops the server.
      - Lifecycle is owned by SmartHopper.Mcp, not Grasshopper, so document
        close / Rhino shutdown also triggers Stop via GH_RhinoScriptInterface.
```

Settings persisted via the existing `SmartHopper.Infrastructure.Settings` surface (so the bearer token never lives in the GHX file).

## 8. Security model (phase 1)

- **Bind address.** `HttpListener` prefix is hard-coded to `http://127.0.0.1:<port>/` and `http://[::1]:<port>/`. No LAN exposure in phase 1.
- **Origin guard.** `Origin` header is checked against an allow-list (`http://127.0.0.1*`, `http://localhost*`, `vscode-webview://*`, `claude://*`). Other origins receive `403`.
- **Authentication.** Optional static bearer token (`Authorization: Bearer …`). When the user sets `BearerToken`, requests without it are rejected with `401`.
- **Allow-list of tools.** `McpSettings.EnabledTools` may pin the subset of `AIToolManager` tools to expose. Defaults to "all read-only tools enabled, all mutating tools disabled" — the user must opt mutating tools in via each tool's `AITool.MutatesCanvas` flag.
- **No file-system / shell access from MCP.** Phase 1 only exposes tools that already exist in `IAIToolProvider`. Adding new tools requires going through `IAIToolProvider` and its review.
- **Logging.** Every accepted request gets a structured `Debug.WriteLine` entry (`[Mcp] tool=<name> sessionId=<id> ok=<bool>`). No payload bodies are logged to avoid leaking GhJSON containing sensitive geometry.

### Threat surface summary

| Threat                                        | Mitigation                                                                 |
|-----------------------------------------------|-----------------------------------------------------------------------------|
| Drive-by web origin invoking tools            | Origin guard + bearer token + loopback-only bind.                          |
| Cross-document race / corrupted GH state      | `SemaphoreSlim`-protected UI marshalling.                                  |
| Unbounded resource use from a single client   | JSON-RPC `params` size cap (256 KiB), per-session concurrency = 1.         |
| Tool exposure beyond intended set             | `McpSettings.EnabledTools` allow-list; mutating tools off by default.      |
| Token leakage via GHX                          | Bearer token stored in `SmartHopper.Infrastructure.Settings`, not the GHX. |

## 9. Phased rollout

| Phase | Deliverable                                                                                                              | Scope                                                                                          |
|-------|---------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------|
| 0     | **This design doc.**                                                                                                      | No code. _Done._                                                                                |
| 1     | `SmartHopper.Infrastructure/Mcp/*`, `SmartHopperMcpServerComponent`, `tools/list`, `tools/call`, read-only tools by default. | Wires existing read-only `AITool`s. Mutating tools off until user opts in. _Done._             |
| 2     | `resources/list`, `resources/read`. Embedded `/docs/Tools/*` and `/docs/Architecture/*` as `gh://docs/*` URIs.            | Reuses existing Markdown; no new content authored.                                             |
| 3     | `prompts/list`, `prompts/get`. Workflow templates for "parametric geometry", "debug GhJSON", "switch provider".          | Templates author-controlled in `/docs/Prompts/*`.                                              |
| 4     | LAN exposure flag + mandatory bearer token, structured audit log, per-session rate limit.                                | Opt-in only.                                                                                   |
| 5     | Optional Streamable HTTP / SSE for long-running streaming tool calls (e.g. provider streaming).                          | Only if user demand exists; phase 1 returns full results synchronously.                        |

Each phase is intended to land as a separate PR.

## 10. Relationship to `ghjson-dotnet` and Cordyceps

- `ghjson-dotnet` remains the single source of truth for GhJSON. The MCP server never imports `GhJSON.Core` / `GhJSON.Grasshopper`.
- Cordyceps is the architectural reference. The following Cordyceps pieces are **structurally** reused, with attribution:
  - HTTP / JSON-RPC dispatch shape (`McpServer.cs`).
  - Reflection-based tool discovery via `[McpServerTool]` attributes — adapted to SmartHopper's `AITool` model so no reflection on user code is required.
  - Thread-safe UI marshalling via `SemaphoreSlim`.
  - Embedded Markdown resource serving (`gh://docs/*`).
- Cordyceps' tool implementations are **not** ported. SmartHopper already has equivalent tools and reuses them through `AIToolManager`.
- Component-name aliasing (Cordyceps' `Core/ComponentRegistry.cs`) is already addressed by [`ComponentNameAliases`](../../src/SmartHopper.Core.Grasshopper/Utils/Canvas/ComponentNameAliases.cs) in the orchestration layer (`feature/2.0.0-text2json`).

Attribution is recorded in `THIRD_PARTY_NOTICES.md` and in per-file headers under `src/SmartHopper.Infrastructure/Mcp/`.

## 11. Open questions

1. **Tool name namespacing.** SmartHopper tools are `gh_put`, `script_edit`, etc. — same shape as Cordyceps. Do we prefix (`smarthopper.gh_put`) to avoid collisions when both servers run in the same Rhino session? Current proposal: **no prefix in phase 1**, but reserve the right to add one if collisions become a real complaint.
2. **Tool schema source.** `AITool.ParametersSchema` is already a JSON Schema string. MCP wants a JSON object. Phase 1 will `JObject.Parse` it. If any tool ships a non-object schema, validation will fail loudly — that's a tool-side bug worth surfacing.
3. **GhJSON returned to MCP.** Should tools that already return GhJSON (e.g. `gh_get`) tag their response with a `mimeType` so MCP clients can render it? Proposed `mimeType: application/vnd.ghjson+json`. Decision deferred until phase 2.
4. **Settings UI.** Phase 1 surfaces port / token via component inputs. A central Settings dialog entry is deferred.
5. **CI coverage.** Phase 1 will add `SmartHopper.Mcp.Tests` (xUnit, no Rhino refs) covering `JsonRpcDispatcher` and `AIToolMcpAdapter`. The HttpListener path stays untested in CI; integration testing happens via a manual Claude Desktop session.

## 12. Decision points (resolved in phase 1)

- [x] **Project placement.** Phase 1 ships under `SmartHopper.Infrastructure/Mcp/` rather than a standalone `SmartHopper.Mcp` project, to avoid adding a new project until cross-host reuse is needed. Revisit if/when out-of-process hosting (CLI, sidecar) is required.
- [x] **Default port `26929`** (Cordyceps default) — kept to ease documentation parity and reduce confusion for users running both servers.
- [x] **Mutating tools off by default.** `McpServerOptions.ExposeMutatingTools = false`; per-tool `AITool.MutatesCanvas` flags suppress mutating tools from `tools/list` and `tools/call`.
- [x] **Component path** `SmartHopper.Components/Mcp/SmartHopperMcpServerComponent.cs`.
- [x] **Attribution surface** is `THIRD_PARTY_NOTICES.md` plus per-file headers under `src/SmartHopper.Infrastructure/Mcp/`.

## 13. References

- Cordyceps source: <https://github.com/brookstalley/cordyceps> (MIT, copyright © 2026 Brooks Talley).
- Model Context Protocol spec: <https://modelcontextprotocol.io/>.
- GhJSON spec: <https://github.com/architects-toolkit/ghjson-spec>.
- `ghjson-dotnet` (.NET implementation, NuGet `GhJSON.Core` / `GhJSON.Grasshopper`): <https://github.com/architects-toolkit/ghjson-dotnet>.
- Local prior art:
  - [`/docs/Architecture.md`](../Architecture.md) — overall SmartHopper architecture.
  - [`/docs/Tools/index.md`](../Tools/index.md) — tool catalogue exposed via `IAIToolProvider`.
  - [`SmartHopper.Core.Grasshopper.Utils.Canvas.ComponentNameAliases`](../../src/SmartHopper.Core.Grasshopper/Utils/Canvas/ComponentNameAliases.cs) — orchestration-layer alias resolver landed alongside this doc.
