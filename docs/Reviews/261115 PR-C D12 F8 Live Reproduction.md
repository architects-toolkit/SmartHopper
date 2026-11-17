# PR-C: D12 and F8 live reproduction

Status: diagnosis complete; proposed fixes await approval. No production source changes.

## Conclusions

- **D12:** the lost-acknowledgement mechanism is reproduced. `gh_put` waits for downstream synchronous document solving after objects have been placed. A client timeout therefore does not mean placement failed. The original 300-second failure at 395 objects is not reproduced as a size threshold.
- **F8:** busy-UI head-of-line blocking is reproduced independently of providers. A canvas call waiting for the UI owns the same dispatcher gate needed by ping and discovery.
- **Negative control:** forty real provider-test workers, with eighty intercepted 30-second failures, do not reproduce the historical minutes-long MCP freeze on the current build. Do not attribute it to provider concurrency without further provider-specific evidence.
- These results justify separating mutation acknowledgement from solving and introducing bounded, cancellation-aware UI admission. They do not justify unconditional placement chunking, longer timeouts, or a provider-worker rewrite.

## Environment And Safety

Tests ran on 2026-11-15, approximately 20:42-21:02 UTC, using the already-running Rhino 8 / Grasshopper host. Production source remains at the PR-B baseline; the current branch adds only investigation documentation. There was no rebuild for this session.

An isolated scratch document hosted its own MCP component. The original `mcp-cert-rig.ghx` remained open and inactive with 36 objects. Actual tool execution used MCP `tools/call`, not direct tool delegates.

Provider requests were intercepted through the existing `ProviderSdkHost.HttpClientFactory` seam. An in-memory `HttpMessageHandler` returned delayed `TaskCanceledException` failures; no request reached a provider network, no settings/credentials were changed, and no paid calls were made. Events recorded timings, event names, and thread IDs, not request bodies or credentials.

Diagnostics used Grasshopper solution events, a 100 ms UI heartbeat, existing SmartHopper logs, concurrent raw MCP requests, and `dotnet-stack` snapshots. Stack collection can perturb timings slightly; these are diagnostic measurements, not a formal benchmark.

Teardown restored the original HTTP factory, disposed all fake timers, stopped the heartbeat, detached event handlers, and removed only the scratch document. The original document's MCP component was expired/re-solved to restart its listener. Final `gh_document_list` confirms the original document is active, still modified as before, still has 36 objects, and is the only open document.

## 1. Size And Execution Controls

All generated definitions omit pivots. The star graph has one source connected to every other node and one extra distinct connection, giving exactly N objects and N wires.

| Fixture | MCP elapsed | Additional evidence |
| --- | --- | --- |
| 10 inert panels / 10 wires | 3.415 s | First-use/cold overhead; not evidence of linear scaling |
| 100 inert panels / 100 wires | 0.469 s | Successful placement |
| 395 inert panels / 395 wires | 1.056 s | Placement/solve log interval only 0.206 s |
| 395 objects / 395 wires, including 40 provider-test workers | 15.830 s | Actual placement/solve interval 0.413 s; a 13.430 s UI heartbeat gap occurs during preparation |

The inert trials accumulated to 506 objects because MCP removal hit the separate serialization defect described below. They therefore did not all start from empty canvases. The scratch canvas was cleared before the mixed-worker trial, whose baseline was the host plus the locked diagnostic helper.

For the mixed trial, 40 `Test OpenAI Standard Call` components share one Run toggle and feed 354 panels. Each worker makes two actual provider calls through the SDK; both are intercepted. The first run used 5-second failures and produced 80 fake sends and 80 fake failures.

The mixed trial's roughly 15-second cost is mostly before the placement interval. We did not capture a preparation stack capable of attributing that cold cost to a particular constructor/layout/serialization method. Repeated off-document measurement is still a code-visible optimization opportunity, not a measured explanation of the historical 300 seconds.

## 2. D12: Applied Mutation, Lost Response

### Reproduction

1. Submit another 395-object/395-wire star graph, replacing one leaf with a Python script component.
2. Its first solve records `d12_slow_start`, performs a controlled 12-second synchronous sleep, records `d12_slow_end`, and disarms itself. Later ordinary solves do not repeat the delay.
3. Give the client a deliberately shortened 3-second socket timeout; concurrently issue `ping` and `tools/list`.
4. Capture a blocked stack, then query the placed component after the server recovers.

This deliberately shortens the client deadline rather than freezing the desktop for 300 seconds. It reproduces the failure mechanism, not the exact historical workload or timeout duration.

### Observed Evidence

- The placement client times out; measured total elapsed is 5.881 s, including local request work, rather than an exact 3.000 s wall-clock bound.
- Concurrent ping and discovery wait approximately 15.3 s.
- The blocking component is present afterward, despite the caller receiving no successful placement response.
- `gh_put` logs "Putting document on canvas" at `20:59:07.6081909Z` and "Placement complete" at `20:59:20.1317902Z`: **12.524 s inside the placement/solve interval**.
- The fixture's own events delimit its sleep at `20:59:08.1238050Z` and `20:59:20.1265675Z`.

Captured UI stack, with unrelated frames omitted:

```text
System.Threading.Thread.Sleep
Python script execution
Grasshopper.Kernel.GH_Component.ComputeData
Grasshopper.Kernel.GH_Document.SolveAllObjects
Grasshopper.Kernel.GH_Document.NewSolution
GhJSON.Grasshopper.PutOperations.CanvasPlacer.Put
SmartHopper.Core.Grasshopper.AITools.gh_put.<GhPutToolAsync>b__4
Rhino.InvokeHelper.InvokeCallback
```

### Root Cause

GhJSON's `CanvasPlacer.Put` unconditionally calls `NewSolution(true)` after adding objects/wires/groups. SmartHopper awaits that callback before returning placement identities, and the outer undo coordinator also completes only after the tool returns. Placement completion and arbitrary downstream computation therefore share one acknowledgement boundary.

The client disconnect does not cancel the in-progress request: the transport dispatch token is server-scoped, and the adapter does not propagate request cancellation into `AIToolCall.Exec`. The actual apply may finish after a reported client timeout, making a blind retry unsafe.

Evidence locations: GhJSON 1.3.6 `src/GhJSON.Grasshopper/PutOperations/CanvasPlacer.cs:344-350`; [SmartHopper placement](../../src/SmartHopper.Core.Grasshopper/AITools/gh_put.cs#L270), especially 330,409,416,427-428; [whole-tool undo scope](../../src/SmartHopper.Infrastructure/AITools/ToolManager.cs#L247), especially 355-357.

## 3. F8: Provider Storm Versus Busy UI

### A. Controlled Provider-Time-Out Storm

Rearm the same 40 provider-test workers with the in-memory handler delayed to 30 seconds. Run ping and a small `gh_get` concurrently once per second for 72 samples, spanning both waves of failed requests and worker completion.

Results:

- 80 intercepted requests in this run; 160 total across the 5-second and 30-second runs.
- Median ping/read-pair completion: **7.96 ms**.
- Maximum measured ping completion: **96.5 ms**.
- Maximum pair completion: **164.2 ms**.
- Largest heartbeat gap in the 30-second-storm window: **0.714 s**, not minutes.

Standard provider work is genuinely off the UI thread. This does not reproduce real TLS/socket/DNS behavior or rule out provider-specific stalls; it does falsify the assumption that forty delayed provider failures necessarily freeze this build. No provider concurrency cap, retry rewrite, or worker refactor is justified by this test.

### B. Busy UI Without An Outstanding Placement RPC

1. Through a completed setup call, schedule the same slow component to recompute two seconds later.
2. While it is sleeping in a Grasshopper-scheduled solution, call ping before making any canvas request.
3. Start `canvas_view(frameGuids)` with a 3-second client timeout, then call ping and discovery again.

Observed:

- Setup RPC completes in 0.635 s; the slow solve starts later, at `20:59:22.8708577Z`.
- With the UI already blocked but no canvas request admitted, ping completes in approximately **2.6 ms**.
- The canvas request times out after **3.009 s**.
- Ping/discovery issued behind that canvas request wait approximately **11.5 s**.
- The solver wakes at `20:59:34.8723581Z`; `canvas_view` completes on the server at `20:59:35.1056470Z`, well after its client has abandoned the call.

The UI stack is inside the scheduled Grasshopper solve, not `gh_put`. A second captured thread is:

```text
TaskAwaiter.GetResult
CanvasAccess.RunOnUiThread
canvas_view.CanvasViewAsync
AIToolManager.ExecuteTool
AIToolCall.Exec
AIToolMcpAdapter.ExecuteAsync
JsonRpcDispatcher.HandleToolsCallAsync
JsonRpcDispatcher.DispatchAsync
```

### Root Cause

UI unavailability is legitimate while a synchronous Grasshopper component computes. The MCP defect is allowing an unbounded UI waiter to hold the protocol-wide gate, so even operations needing no UI are blocked. An abandoned queued call also remains executable because it has no request-scoped cancellation/start-ownership guard.

Evidence: [blocking UI bridge](../../src/SmartHopper.Core.Grasshopper/Utils/Canvas/CanvasAccess.cs#L46), especially 53-66; [viewport call](../../src/SmartHopper.Core.Grasshopper/AITools/canvas_view.cs#L141); [dispatcher gate](../../src/SmartHopper.Infrastructure/Mcp/JsonRpcDispatcher.cs#L113); [missing execution token](../../src/SmartHopper.Infrastructure/Mcp/AIToolMcpAdapter.cs#L55).

## Specific Proposed Fixes

### GhJSON 1.3.7: Host-Composable Apply

- Extract an apply-without-solving path, exposed through a compatible composition hook (for example, an optional `PutOptions.RequestSolution=false`). Existing standalone defaults remain unchanged.
- Host-composed Put/connect/data edits mark affected objects dirty without independently calling `NewSolution`; one host-owned commit boundary requests the final solve.
- Share prepared geometry/endpoints between layout/review/apply where possible. Keep phase timings so the separate cold-preparation cost can be measured before choosing further optimization.

### SmartHopper: Commit Before Scheduling Solve

- Bind the intended document and revalidate the approved plan.
- Apply the graph and finalize its undo snapshot in the **same bounded UI commit scope**, without solving.
- Materialize stable placed identities and an additive execution status such as applied/solve-scheduled, preserving existing payload keys.
- Schedule one document solution without awaiting downstream completion. Do not leave an undo-finalization UI callback behind the scheduled slow solve: that would recreate the same block through another path.
- Reuse/strengthen idempotency reservation and outcome replay so a disconnected client can recover the actual result with the same key instead of blindly placing again. An already-started mutation must not be reported as safely cancelled or have its lock released while still running.

This is the D37 finite-change-plan boundary as well; it is not a `gh_put`-only workaround.

### SmartHopper: Independent Control Plane And Bounded UI Admission

- Route ping, cancellation, and immutable discovery independently from document work. Do not infer UI independence merely from `readOnlyHint`.
- Give canvas reads/writes a bounded host UI executor: queued -> started -> completed, with an atomic cancellation/deadline claim before execution. An expired queued callback becomes a no-op.
- Propagate per-request cancellation from the MCP request registry through the adapter into `Exec(token)`. Preserve document serialization and started-operation ownership.
- Return a structured busy/not-started outcome within a short UI-start budget rather than waiting indefinitely. Never inspect live Grasshopper state from a background thread as a fallback.
- Bound UI-work admission separately from HTTP transport admission, retaining control-plane capacity: sixteen canvas waiters must not consume every active-request slot and turn ping into 503.

No change can make a synchronously blocked UI answer fresh canvas reads immediately. The correct behavior is responsive protocol handling and an honest bounded busy result.

## Acceptance Tests For The Fix

1. Repeat the warmed inert and mixed 395/395 fixtures, logging prepare/measure/layout/review/apply/undo/solve-request phases separately.
2. Repeat the 12-second slow-solver placement with a short client deadline: acknowledge applied identities after commit, without waiting those 12 seconds. Assert undo is sealed before the solver starts.
3. Repeat the independently scheduled busy-UI test: ping/discovery remain responsive; canvas admission returns a documented busy/not-started outcome within the chosen budget (proposed 1 s), and no abandoned callback runs later.
4. Repeat concurrent same-key mutation requests and lost-response retry: exactly one apply and recoverable outcome; no false cancellation of an already-started write.
5. Repeat the 40-worker/80-timeout negative control to guard against regressions. Diagnose additional provider-specific failures separately if they recur.

## Test-Setup Caveats And Local Artifacts

- `gh_remove` could not clean up the inert panels: GhJSON rejected serialized `gh.panel.bounds` strings against its regex. This is a separate confirmed round-trip failure, not a D12/F8 timing cause. Its numeric-format cause still needs isolation before proposing a 1.3.7 fix.
- An initial diagnostic helper incorrectly tried to isolate objects during `SolveInstance`, causing Grasshopper's safety modal. That artificial stall is excluded from all conclusions. The temporary in-memory assertion suppression used to recover the helper was removed before the measured worker/slow-solver tests. Future fixture cleanup must run outside a solution.
- Local raw evidence remains under ignored `tmp/`: `prc-events.jsonl`, `prc-storm-latencies.json`, `prc-blocking-summary.json`, `prc-d12-blocked-stacks.txt`, `prc-f8-blocked-stacks.txt`, and the `prc-*.py` scratch harnesses. These artifacts contain machine/document-specific setup and are not committed as a supported test runner.
- Historical exact 300-second placement and real-provider socket storm are not newly certified reproductions. The shortened-deadline and independently scheduled busy-UI mechanisms above are.
