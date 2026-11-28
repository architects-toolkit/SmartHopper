<!-- docs-validation: ignore -->

# PR-C Live Test Report — 261116

Branch: `fix/2.0.0-mcp-robustness` — candidate binary `2.0.0-dev.261116+166f9f16` (built via `tools/Build-Solution.ps1 -Testing`, signed with test PFX, loaded through the `SmartHopper.ghlink` dev link). GhJSON loaded from source (`feature/1.3.7-mcp-robustness`).

Host: Rhino 8 + Grasshopper on Windows, `net7.0-windows` debug output. Test document: `test-assets/e2e-canvas.gh` (180 objects, `hostsMcpServer: true`). All calls are real JSON-RPC over `http://127.0.0.1:26929/mcp` with `Mcp-Session-Id` session correlation. No provider keys were exercised; no paid calls.

## Results

| Check | Outcome |
| --- | --- |
| `initialize` / `tools/list` | 200, session id issued; 50 tools including `gh_apply_changes` with auto-injected optional `requestId` |
| `gh_document_list` baseline | 1 document `ae75cd7e…`, 180 objects, MCP host, dirty flag recorded |
| L01 `gh_apply_changes` single op | `status:applied`, `appliedCount:1`, `solutionScheduled:true`, per-op detail (`Set panel text on ''`) |
| L01 composite (put-first + 2×set_value) | `appliedCount:3`, per-op results, `idToGuid` fragment mapping (`"9"→GUID`), one scheduled solve |
| Plan validation | unsupported `target` on `disconnect` rejected; `put` not first rejected (`must be … first in the array`) — no canvas writes on rejected plans |
| L01 compensation | `disconnect` applied, then `set_value` failed → wire **restored** (verified via `gh_get connectionDepth:1`); honest failure, no partial-write residue |
| D12 deferred solve | every apply reports `solutionScheduled:true`; commit returns without a synchronous `NewSolution` |
| gh_put prepared-put | slider+panel+wire placed: `acceptedChanges:3`, `rejectedChanges:0`, both `instanceGuids` returned; wire confirmed via `gh_get` (strict `paramIndex` endpoints) |
| U05 idempotent replay | same `requestId`+same args → replayed `applied` without re-executing; same key+different args → `idempotency_conflict` (typed `code`) |
| U03 duplicate id | two concurrent calls with `id:60` → one OK, one `Duplicate request id` (-32600) |
| U03 cancellation | `notifications/cancelled` on finished id and unknown id → silent HTTP 204, no JSON-RPC body; session-B cancels did not affect session A (`ping` 200) |
| U07 control plane | 25-request burst: 24 `tools/call` OK, 1 transport-level `Server busy` (503), `ping` answered immediately mid-burst — control methods never gate behind tool work |
| D35 diagnostics | unknown-arg warning emitted as second text block + `_meta["smarthopper/diagnostics"]` with severity/origin; `isError` separation clean |

## Not covered live (unit-tested instead)

- In-flight `notifications/cancelled` mid-mutation and `operation_in_progress` observer timeouts (needs a >1 s mutation window; covered by dispatcher/adapter unit tests and the `BoundedUiPost` contract tests).
- 12-slot admission saturation (tool calls complete in <1 s on this doc; the admission lane never queued past budget — the transport 503 cap was the reachable bound).
- Per-document commit-gate concurrency (single open document).
- L06 40-worker stress, Ctrl+Z undo-group inspection, macOS.

## Anomalies

- `e2e-canvas.gh` reports 14 "Input parameter chunk is missing. Archive is corrupt." messages on load. It still loads 180 objects and hosts MCP correctly; pre-existing asset condition, unrelated to PR-C.
- `set_value` on a Number Slider accepts plain numerics only (range objects and `current<min~max>` text are rejected as non-numeric); slider bounds remain `componentState.extensions['gh.numberslider']` territory.

## Final pre-merge regression (same day, merge candidate `ab3d71b6`)

Re-run of the core contract battery on the live session (Rhino 8, MCP `127.0.0.1:26929`) against an `Untitled` scratch document hosting the MCP server (2 objects; restored to that state afterwards). Candidate binary reports `2.0.0-dev.261116+ce871cad`: built before the `ab3d71b6` diagnostics commit landed and additionally containing the short-lived `ParamSignatureReconciler` experiment — inert on drift-free documents and removed from source before merge. Every contract exercised here is byte-identical between the binary and merge HEAD.

| Check | Outcome |
| --- | --- |
| `gh_put` prepared put | slider+panel+wire, `acceptedChanges:3`, both `instanceGuids` returned; wire confirmed via `gh_get` |
| U05 idempotent replay | same `requestId`+args → identical replayed result (same GUIDs, `totalComponents` unchanged — no duplicates placed) |
| U05 idempotency conflict | same `requestId`+different args → typed `idempotency_conflict` error envelope |
| `gh_apply_changes` composite | put+connect+set_value: `appliedCount:3`, per-op results, `idToGuid` (`10`→GUID), `solutionScheduled:true` |
| Plan validation (2 plans) | nonexistent endpoint (`No canvas object matches …`) and capability mismatch (`target does not support AI provider selection`) → whole plan rejected at validation, **zero writes** — verified `totalComponents` unchanged |
| Op-level dedup/tolerance | exact duplicate disconnect → `noop` (`Exact duplicate of an earlier operation`); disconnect of absent wire → `noop` (`Wire does not exist`) |
| U03 duplicate in-flight id | two concurrent `tools/call` with `id:100` → one `Duplicate request id` error + one success |
| U03 cancellation | `notifications/cancelled` on finished and unknown ids → silent HTTP 204 |
| U07 control plane | 24-request `tools/list` burst → 24×200; `ping` answered mid-burst in 0.686 s |
| `gh_get_errors` | 0 error-producing components after the battery |
| `gh_remove` cleanup | all 3 test objects removed, doc back to 2 objects |

Notes: mid-apply compensation was already live-certified above (`disconnect` + apply-time `set_value` failure → wire restored). Today's hard-failure attempts reject at plan *validation* instead — a stricter, earlier failure boundary that still guarantees atomicity. Binary rebuild during this run was blocked by Rhino/VS file locks; the reconciler-containing binaries are superseded by the committed removal.
