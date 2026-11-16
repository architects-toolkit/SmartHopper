<!-- docs-validation: ignore -->

# PR-C 4: Validation and delivery

Parent: [handoff](261115-pr-c-implementation-handoff.md). Read the three implementation packages before using this checklist. A test result is evidence only when actually run against the candidate binaries.

## Test Placement

| Concern | Location / rule |
| --- | --- |
| Protocol, request lifecycle, cache/reservation, diagnostics, admission | `src/SmartHopper.Infrastructure.Tests/Mcp`; fake clocks, tasks and UI-post delegates; no Rhino types |
| Pure host plan/ranking/projection logic | Existing Core/Grasshopper test project as appropriate, but do not add tests requiring Rhino/Grasshopper references or runtime activation |
| Pure GhJSON identity, endpoint-ranking and origin math | `../ghjson-dotnet/tests/GhJSON.Core.Tests` (currently net8.0) |
| Actual Grasshopper state, undo, IO, layout or UI | `src/SmartHopper.Components.Test`; GhJSON host tests belong in `tests/GhJSON.Grasshopper.TestComponents` |

Existing test projects referencing GH assemblies are not permission to add more Rhino-dependent xUnit tests. Keep new ownership/projection/ranking services separable from host objects. Use deterministic clocks/latches, not timing sleeps in unit tests.

## Minimum Automated Matrix

| ID | Required assertions |
| --- | --- |
| U01 D35 | Payload+Warning succeeds; payload+Error fails; every message origin; Info visibility; Debug policy; truncation/scrubbing |
| U02 D35 | Payload, images, order, metrics, scalar/array handling and schema details survive envelope serialization and replay cloning |
| U03 F2 | Numeric/string RPC IDs distinct; duplicate active IDs rejected; session isolation; unknown/finished cancellation silent; notification has no JSON-RPC response |
| U04 F2/F8 | Fake UI post cancelled before claim never runs; claim/cancel race has one winner; registration survives awaits; no disposed-resource access |
| U05 D12/F2 | Concurrent equal replay keys execute once; changed arguments conflict; no active-entry eviction; terminal expiry documented; document switch never retargets |
| U06 D12 | Deadline during Applying returns an observer/in-progress result, retains gate/task, and later replays actual outcome; no cached false final timeout |
| U07 F8 | Full tool/UI admission still leaves bounded control capacity; slow parsers/invalid auth do not create an unlimited bypass |
| U08 D37 | Closed operation union; unique IDs; local-ID resolution; complete graph used for preparation; conflicting writes rejected before apply |
| U09 D37 | Atomic group rejection applies nothing; ordinary gh_put independent selection still respects dependencies |
| U10 D62 | Free-block rendered-edge translation, not double offset; empty and negative-coordinate canvases; several islands; fixed/movable mix |
| U11 F4 | All ranking tiers; query fallback; categories; stable GUID ties; limit after rank; metadata-only makes no instance |
| U12 F5 | Identity/pivot removal is generation-only and idempotent; state/scripts/groups/wires retained; missing/duplicate references fail safely |
| U13 F5 | Exact/nickname/alias/index resolution, duplicate-name index fallback, fuzzy ambiguity, direction, conflicting name+index, unavailable/dynamic ports |
| U14 F16 | Fit/target/projection math at non-unit zoom, rounding tolerance, finite/range validation, center-preserving zoom, degenerate bounds |

Add host test components to exercise each unsafe-to-fake part below. Do not substitute tests that simply assert the implementation's own helper was called.

## Safe Live Setup

1. Confirm installed candidate build/version and actual loaded GhJSON assembly version/location. Source-mode compilation success does not prove Rhino loaded that DLL.
2. Inspect existing documents through `gh_document_list`; record their IDs, object counts, dirty status and MCP ownership. Never save, remove, close or mutate user content to make a test easier.
3. Create a dedicated scratch document and MCP host. Keep the original open/inactive. Bind every fixture to that scratch document; abort if it changes.
4. Use `SmartHopper.Providers.Test`/Test Batch Control where possible. For the forty real provider-test-worker control, intercept the existing `ProviderSdkHost.HttpClientFactory` with an in-memory failing handler. Verify interception before arming workers. All tests must remain cost-free.
5. Store the original factory and diagnostics registrations; restore them in a `finally`/explicit teardown path even after timeout. Never change stored credentials or production endpoints.
6. Use actual MCP JSON-RPC requests, initialize a session for replay/cancellation, preserve `Mcp-Session-Id`, and capture response bodies/metadata without secrets. Use concurrent HTTP clients for concurrency, not sequential tool calls.
7. Logs: `%APPDATA%\\Grasshopper\\SmartHopper\\Logs`. Optional `dotnet-stack report -p <verified-current-Rhino-PID>` captures managed blocking stacks; never hard-code the historical PID.
8. Use scratch-only Rhino Python/native UI automation when bootstrap/recovery is necessary. Do not mutate/isolate/remove objects inside `SolveInstance`; defer cleanup until a safe idle/scheduled boundary.

The ignored `tmp/prc-*.py` files are historical diagnostic artifacts with machine-specific paths/IDs. They are not a supported runner and must not be blindly executed on a fresh checkout.

## Live Certification Matrix

### L01 D37: No Intermediate Paid Work

- Use a TestProvider component with known initial input/provider/model and Run held true, plus intercept/count submissions.
- Submit one `gh_apply_changes` containing provider, model/settings, input and wiring changes. Approve once. Assert exactly the final configuration is consumed, one undo group exists, and no submission observes an intermediate combination.
- Repeat with refusal, partial row rejection (whole atomic group rejects), protected target, invalid second operation, stale target during approval and document switch. Assert zero writes/submissions for pre-start failures.
- Repeat while an old asynchronous run is active. It retains the old run snapshot; final edits remain pending under PR-B semantics and cause at most one legitimate follow-up run. No forced resubmission/epoch manipulation.
- One Ctrl+Z/redo restores/reapplies values, provider, model-panel existence, settings wires, graph additions and positions coherently. Observe Run behavior: do not casually undo an active paid-capable run.

### L02 D12: Commit Is Not Solver Completion

- Warm the candidate, then run 10/100/395 inert objects with N wires; record initial canvas size, each phase and whether cleanup succeeded.
- Prepare a 395-object/N-wire fixture with one deliberately synchronous 12-second solver. It is a test-only component, initially disarmed and guaranteed to run once.
- With a short client deadline, assert placed identities/Applied outcome are available after commit/undo sealing rather than after the 12-second solve. Measure preparation separately; cold plugin work is not covered by the one-second queued-start budget.
- Drop an HTTP observer after commit begins, then retry concurrently with the same `requestId`. Assert one apply, one review, no duplicate identities/paid generation and eventual real outcome replay.
- Record solve-start after undo-sealed and no subsequent UI-dependent finalization. Test delayed solve callback on document close and forced schedule failure: graph stays honestly Applied, not falsely rolled back.

### L03 D62: Position Preservation

- Cover empty canvas; canvas entirely near X=5000; negative-coordinate canvas; several free islands; explicit pivots including zero; mixed anchored/unanchored components; non-center-pivot panels/sliders; in-place update and different-type replacement.
- Compare review projection to applied pivots/bounds. Free content aligns to existing rendered left/bottom policy, anchored content stays relative to anchors, authored/existing pivots do not move.
- Include a composed new-to-existing connection before layout. Ordinary later `gh_connect` changes only wiring.

### L04 D35/F4/F5/F16: Actual MCP Payloads

- Search `AI Settings`, canonical names, nicknames, keyword-only terms, and ranking ties with maxResults=1. Request metadata without IO and check no component instantiation.
- Add one benign unknown argument to a read-only tool. Assert success payload plus Warning in model-visible text and typed metadata; no paid call.
- Feed deterministic generated responses containing unwanted GUIDs/pivots, aliases, valid indices, ambiguous ports, dynamic scripts and invalid references. Inspect the final serialized GhJSON and placed graph; no dropped wires or default fixer side effects.
- Frame the same component twice at non-unit zoom; setCenter and setZoom; immediately request `canvas_screenshot`. Returned center/zoom/visible region match the applied projection within documented pixel tolerance. Partial/all missing targets and queued cancellation are covered.

### L05 F2/F8: Busy UI And Cancellation

- Schedule a controlled twelve-second solver after setup RPC completion, independently of placement.
- During that solve, ping/discovery should remain responsive (target <=250 ms in a quiet local rig). A canvas call unable to start returns busy/not-started within the selected one-second queue budget, with scheduling tolerance documented.
- Cancel that queued view/mutation via a different request in the same session. Assert no late effect after the solver wakes. Another session with the same numeric RPC ID must not be cancelled.
- Fill all admitted tool/UI slots and repeat ping/discovery/cancellation. Verify prompt overload rejection and retained HTTP size/auth/I/O protections.
- Interrupt the observer after a mutation has started. It cannot release ownership or falsely certify non-application. Final owned outcome remains recoverable.

### L06 F8 Negative Control And Recovery

- Run forty real test workers, each making two intercepted 30-second failing requests; sample 72 ping/read pairs over both waves.
- Record max/median latency, UI heartbeat gaps, submissions and exceptions. Previous baseline: median 7.96 ms, maximum pair 164.2 ms; no minutes-long freeze. Do not require exact benchmark numbers on different hardware.
- Restore the factory, dispose timers, detach diagnostics, remove only the scratch document at idle, and reactivate the original document. Its MCP host may need explicit expiration plus `NewSolution(false)` to restart after document-context unload.
- Confirm original document IDs/counts/dirty flags and MCP availability match the pre-test record.

## Build, Package And Documentation Gates

Use Developer PowerShell for Visual Studio on Windows. Run the official build/signing script; do not work around missing signing tools by changing committed signing settings.

```powershell
.\tools\Build-Solution.ps1 -Configuration Debug
dotnet test src/SmartHopper.Infrastructure.Tests/SmartHopper.Infrastructure.Tests.csproj -f net7.0-windows --no-restore
dotnet test src/SmartHopper.Core.Tests/SmartHopper.Core.Tests.csproj -f net7.0-windows --no-restore
dotnet test src/SmartHopper.Core.Grasshopper.Tests/SmartHopper.Core.Grasshopper.Tests.csproj -f net7.0-windows --no-restore
dotnet test src/SmartHopper.ProviderSdk.Tests/SmartHopper.ProviderSdk.Tests.csproj -p:SignAssembly=false
```

- `-Testing` on the build script is for a disposable test certificate with the known test password; do not assume it unlocks an existing real PFX. If a signing password is required, ask the user to run the secure flow. Never print it.
- Do not kill Rhino holding DLLs or unsaved work. Arrange a safe save/restart with the user. Rebuild is not complete until the candidate actually loads for live tests.
- Run `dotnet test tests/GhJSON.Core.Tests/GhJSON.Core.Tests.csproj` from the sibling and build its test-component project for host checks. It targets a different TFM; do not force every project to net7 merely to unify commands.
- Verify source references with the correct `GhJsonSourceRoot`, then pack candidate 1.3.7 to a local test feed and verify SmartHopper with `UseLocalGhJsonSource=false` and both package references set to 1.3.7. Set/restore the MSBuild property in the official build environment; do not silently change global NuGet sources.
- Only after approval publish/release GhJSON 1.3.7, then pin both consumer packages and repeat package-mode verification. Public Git pushes, NuGet publishing and deployment require explicit permission; a local package is not a released dependency.
- Run Release build, applicable tests, existing style/analyzer checks and `dotnet list SmartHopper.sln package --vulnerable --include-transitive`. Record inaccessible feeds/unsupported platform checks honestly. Do not invent an npm lint command for this solution.
- On macOS validate the existing net7.0 build/Infrastructure-test path and new host bridge/viewport behavior with native Rhino UI APIs; Windows-only automation is not macOS certification.
- Update `CHANGELOG.md` under Unreleased, `docs/Architecture/mcp-server.md`, affected tool/undo/layout/generation docs and the live report. Separate any unrelated feature-train changes into dedicated commits.

## Stop / Failure Criteria

- No API masquerading as an existing method, no missing package release hidden by sibling source mode.
- No claim of atomic rollback backed only by undo-record counts; no dependence on full GhJSON serialization for native undo snapshots. The known `gh.panel.bounds` round-trip failure can break that serialization.
- That panel-bounds failure is an ancillary confirmed issue with an unisolated numeric-format cause, not a proven D12/F8 root. Do not weaken schema validation or guess a culture fix. If it blocks required APIs/tests, isolate and document a focused GhJSON 1.3.7 fix; otherwise retain it as an explicit follow-up and use scratch-document teardown.
- No late queued writes after busy/cancel, control-plane stalls behind UI waits, duplicate same-key commits, undo sealed before the last graph mutation, lost warnings/images/metrics, moved anchors, removed valid wires, or real network provider calls.
- If compensation is unsupported/fails, authorization changes, a target disappears, or the plan no longer matches its approved preview, stop/reject truthfully rather than silently proceeding.
- Before commit scan staged changes for secrets, inspect the actual staged file list, and exclude generated keys/settings/artifacts. Commit completed work on the current PR branch and push only to the authorized remote.
