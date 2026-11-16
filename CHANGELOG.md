# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

### Security

- Chat WebView bridge isolation: every `sh://`/`clipboard://` event must echo a per-document session token minted by the host, so a foreign or stale document can no longer drive privileged host actions. All non-bridge navigation is cancelled — external links open in the system browser (including `window.open`/`target=_blank`), and the bridge only activates for the trusted embedded document.
- Provider Authenticode verification now uses `WinVerifyTrust` on Windows so the signature must actually cover the file bytes before any provider code loads; merely carrying the signing certificate no longer passes. Signature-valid chain failures for the non-CA pinned signer (untrusted root, expired, unchainable) still pass to the host-thumbprint check; unsigned, tampered, malformed or distrusted files are rejected. `ProviderClassifier` uses the same verification, so a tampered file cannot report a "signature present" trust signal.
- Remote model-catalogue documents are now freshness-bound: a signed `published_at` older than the configured limit (default 30 days, settable on the Security tab; 0 disables) or more than a day in the future is rejected before merge, so a stale-but-validly-signed release cannot be replayed as an update. The verified cache and persisted version floor already covered offline replay; freshness closes the remote-acceptance gap.

- Arbitrary external-source retrieval now requires exact-origin approval: Allow once, Always allow this origin, or Deny. The new Security tab manages persistent permissions, provider trust controls and a configurable 16 MiB download cap. Downloads validate resolved destinations and redirects, isolate credentials and use direct connections; proxy-only source networks are unsupported. Chat images and provider image URLs use the same boundary, and image/audio downloads use async-only APIs so consent cannot block Rhino's UI thread.

- AI tool dispatch now rechecks enabled status, execution surface and the originating host-owned request filter, including include/exclude categories. A registered but unadvertised/out-of-scope tool cannot execute just because a model returns its name; progressive discovery remains scoped to the eligible catalogue.

- Provider trust previews no longer load or execute candidate DLLs. Human-readable names and declared model counts come from a bounded embedded JSON manifest, covered by the DLL's existing pre-consent SHA-256 verification. Missing/invalid metadata falls back to the filename and an unknown count.

- Provider trust is now verification-conditional: enabling a provider persists a `ProviderTrustRecords` consent snapshot (the bytes consented to and whether consent followed a catalogue match) alongside the `TrustedProviders` flag, and a trusted file is re-checked on every load instead of riding its stored flag. Official provider updates verify against the published hash manifest and re-mint the snapshot silently — no re-prompting on legitimate updates. A trusted file that fails hash verification under an official provider name is treated as potential tampering: it is disabled, blocked with a severe error, and the user is advised to re-download it from official sources before re-enabling it. A custom/third-party provider whose file changed since consent — or whose stored consent record is missing, as with a forged trust entry — shows a re-confirmation dialog in `Soft` mode and stays blocked in `Hard`/`Strict`. Changed bytes are never silently accepted, even while the hash manifest is unreachable. DEBUG builds keep trust silently on changed bytes so local provider rebuilds never re-prompt.
- Disabled providers no longer execute at all: `TrustedProviders[name] = false` now skips the file before signature checks and `Assembly.LoadFrom`, so provider code cannot run while the user believes it disabled.
- The MCP server transport now runs on a raw `TcpListener` bound to `127.0.0.1` instead of `HttpListener`, and rejects requests with a non-loopback `Host` header (DNS-rebinding guard) or a non-loopback `Origin`/`Referer` header — `fetch()` POSTs always carry `Origin`, so websites can no longer drive the tool surface even when no bearer token is configured. `POST /mcp` now requires `Content-Length` within the 256 KiB cap (411/413), request headers are capped at 16 KiB, every socket read/write is bounded by a 15 s timeout, at most 16 requests run concurrently (503 beyond), and the component's `Status` output warns explicitly while the server runs without a bearer token. Direct LAN support is removed: the listener is unconditionally loopback-only and the legacy external option is gone entirely — no programmatic path can request a non-loopback bind.
- OpenAI, MistralAI and DeepSeek provider settings no longer write the configured API key to the debug log during validation; the log line now reports `<set>`/`<null>` like the other providers.
- Vendored DOMPurify updated from 2.5.4 to 2.5.8 (`js/purify.min.js`), picking up upstream mXSS bypass fixes (nesting/depth, template-content and `form`/`math`/`svg` cases) while staying on the 2.x line the chat bridge already consumes. The npm tarball sha512 is recorded in `THIRD_PARTY_NOTICES.md` for verification.
- PDF page rendering loads through a dedicated trust gate: `RendererIntegrityGate` accepts the isolated `SmartHopper.DocumentRendering` assembly only when `ProviderClassifier` reports `Official` classification AND its strong name matches the host AND (on Windows) its Authenticode signature matches the host — a stricter predicate than ordinary provider loading, because renderer code executes native image pipelines on document-controlled content. The renderer's dedicated `AssemblyLoadContext` is dependency isolation (it keeps PDFtoImage/SkiaSharp away from Rhino's default-context SkiaSharp), **not** a security sandbox — the entire trust decision comes from the classifier gate, and a rejected or missing renderer degrades to text-only extraction with explicit warnings rather than a fallback load.
- External web images referenced by converted pages are materialized only through the approved-source boundary (`ExternalImageMaterializer` → `ExternalDataHttpClient`): per-origin consent, redirect and credential isolation, a 32-source cap, a 64 MiB aggregate encoded budget and magic-byte validation that rejects SVG/unknown/executable content — no raw `HttpClient` bypass exists. MCP image-block transport never implies the remote model accepts images.
- `data:` image URLs materialize to bounded inline bytes and are re-linked as stable `smarthopper-asset:img-N` references, so base64 never leaks into source provenance, tool JSON, `structuredContent` or Markdown projections.
- Rendered PDF pages capture a page's visible composition (vector plots, image placement, scans, annotations, rotation and crop) as visual evidence — this is not semantic understanding: extraction preserves what is visible without inventing hidden structure, and model comprehension remains a separate, unverified claim.
- Log scrubbing now also redacts LAN hostnames before any log leaves the process: single-label machine names in URL host position (`http://nas-box:8080` → `http://<hostname>:8080`) and private-use DNS suffixes (`.local`, `.lan`, `.internal`, `.intranet`, `.corp`, `.home`, `.localdomain`, `.private`, `.arpa`) in any context are masked, while `localhost` and public hostnames stay readable. URL userinfo on a scrubbed LAN host is dropped entirely so a lone `user@` cannot survive the credential rule.

### Added

- `gh_edit_datatree` AI tool — surgical replacement of stored data-tree content without rewriting the whole value. `edits[]` entries address an existing branch (`{a;b;c}`) or a single item inside it (`{a;b;c}(i)` or `(i)` shorthand); values use GhJSON prefixed encodings (`text:`, `number:`, `pointXYZ:`, …), so item- and branch-level edits support the same native types as document placement. Targets panels (line items; `item`-mode panels expose the single `{0}(0)` slot) and `GH_PersistentParam<T>` stores (standalone params and component input/output parameters via `param`). Edits are staged and validated before mutation — unknown encodings, missing paths, out-of-range indices, incompatible types, overlapping addresses, and wired inputs all fail without touching the tree — then apply as one undoable step with one downstream re-solve. Position parsing, staged editing, and goo conversion are delegated to `GhJSON.Grasshopper`'s `EditParamData`/`EditPanelData` APIs.
- **Incremental per-unit result reuse** — stateful components that opt in now keep a persistent cache of processing-unit results keyed by the semantic fingerprint of each unit's *effective* inputs plus the component's execution context (type, provider, model, request parameters). Unchanged items/branches replay their stored fragment instead of re-executing — Entwine path re-indexing follows the content, re-saves reopen with the cache intact, and batch sentinels resolve inside stored fragments so in-flight batches complete cached entries after save/reopen. Reused units produce a "Reused N of M" remark instead of provider metrics. Side-effectful components (chat, canvas selecting/reporting, file/web fetchers, forum/web summarizers) are opted out. See `docs/Components/ComponentBase/UnitResultReuse.md`.
- **Clear output cache** — new component menu action on every stateful component: cancels in-flight work, wipes cached outputs and unit results, transitions to `NeedsRun` and reprocesses immediately when `Run` remains true.
- **AI Settings → Share** — new boolean input (default `true`) controlling whether identical processing units inside a run share one produced result. Wired through `AIRequestParameters.ShareIdenticalResults` as local execution policy — never sent to providers; when off, identical occurrences get deterministic slots and generate independently.
- `gh_set_value` AI tool — new optional `panelMode` enum for panels (`item`|`list`): `item` emits the whole text as one item, `list` emits each line as a separate item. Supply it alone to switch an existing panel's mode without touching its text, or together with `value` to set both. `value` is now optional only when `panelMode` is supplied.
- **Open a new chat** — new first item in the SmartHopper menu that opens an independent assistant web chat dialog on every click, so users can keep multiple parallel conversations (each gets its own dialog ID, history, and provider session). The canvas assistant button keeps its stable dialog ID and still reuses/focuses the same chat. Shared provider/model resolution moved to `WebChatUtils.OpenAssistantChatDialogAsync`.
- Components now surface a SmartHopper-green triangular badge while an external-source consent prompt is open and the component is still processing, so a run blocked on approval is visible on the canvas; hover explains where to answer. The awaiting state is broadcast from the serialized consent gate and repaints the active canvas on transitions.
- Components now show a persistent remark when the `Run?` input is unwired, guiding first-time users to wire a boolean toggle or button; the hint clears automatically once the input is wired or set true. `Run?` still defaults to false.
- **Metrics provenance for cached results** — every processing unit now stores its execution metrics alongside the output fragment (`UnitResultStore` provenance records, persisted with the unit cache), so the `Metrics` output always describes the complete current output instead of only the latest run's calls. Reused units replay their original provider/model/usage/cost with `reused: true` and zeroed `data_count`/`iterations_count`, and `Combine Metrics` deduplicates replays sharing one `execution_id`.
- **Metrics completion timestamps** — entries now carry `completed_at` (UTC, ISO 8601) stamped when a call finishes (`completed_at` stays distinct from the `completion_time` duration; legacy metrics simply omit it), plus a stable `execution_id` per execution — batch calls use their `customId` so identity survives save/reopen. `Deconstruct Metrics` exposes the new `Completed At`, `Execution Id` and `Reused` outputs, and serialization is shared through the new `AIMetricsJson` codec.
- **Rhino geometry fingerprinting** — `SemanticFingerprint` now hashes `GeometryBase.ToJSON()` for goo-wrapped and raw geometry, so edits to control points, trims or vertex data invalidate unit results even when the display text stays identical. Custom data types can opt into deterministic reuse via `SemanticFingerprint.RegisterValueFingerprinter`.
- **Unit placements for snapshot assembly** — scheduled units record `(unitKey, inputPath, itemIndex, targetPaths)` and `UnitResultStore.BuildRunTrees` rebuilds every output channel from typed fragments at their targets. Batch completion now reconstructs heterogeneous channels for both executed and reused units — including secondary outputs (e.g. *Used Fallback*) that never carried sentinels — with placements persisted for in-flight batches across save/reopen.

- `show_ghjson` AI tool (`GhJSON.Tools`): hands a GhJSON v1.0 document to the user as a dedicated artifact card in WebChat instead of pasting JSON into reply text. The document is validated with `GhJsonValidator` first — invalid documents return structured schema errors to the model (so it can self-correct and retry) and render a compact red card listing the errors, with no placement action. Valid cards show a collapsed source preview, a copy button and a "Place on canvas" action (`sh://event?type=ghjson_place`) that places the definition on the active document through `CanvasGhJsonPlacer` → `GhJsonGrasshopper.Put` with undo recorded — an explicit user action, never automatic tool execution. The artifact rides `MutationInvocationContext.GhJsonArtifactPresenter` (new `IGhJsonArtifactPresenter` contract), implemented by `WebChatGhJsonArtifactPresenter` through the new `showGhJsonArtifact`/`resolveGhJsonArtifact` bridge globals.
- Autonomy-continue consent in WebChat: when a run stops on the autonomous time/token budgets, an approval card ("Autonomous run limit reached", naming the reached limit) offers Continue working / Stop instead of leaving the run silently ended. Approving appends a persisted user-role authorization ("The agent reached its autonomous run limits and stopped. The user approved continuing — resume the task.") and re-runs the conversation as a normal turn, so the model sees both the stop and the authorization in history; each approval starts a fresh run with the full configured budgets. A pending card resolves as dismissed when the user sends a new message, and stays actionable through subagent wake turns. The card rides the shared `type=consent` bridge route with `autonomy-N` ids via new `showAutonomyContinue`/`resolveAutonomyContinue` globals.
- Progressive tool loading: when more than 12 tools are eligible for a request, providers only send the core categories (`Canvas.Query`, `Canvas.Modifications`, `GhJSON.Generation`) plus the new `search_tools` tool; the model discovers the rest via `search_tools(query)` and invokes them by name — execution is name-dispatched so undiscovered-but-eligible tools still run. The request's `AIBody.ToolFilter` travels to the tool on `MutationInvocationContext.ToolScopeFilter`, so discovery never leaks tools outside the request's include/exclude scope or surface. Shared constants live in `ProviderSdk.Utils.ProgressiveToolLoading` (threshold 12, result limit 6).
- Structured logging architecture (salvaged from the retired `feature/2.0.0-structured-logging` spike): `ILogger`/`ILogger<T>` contracts plus `Logger.For`/`Logger.For<T>` facade in `SmartHopper.ProviderSdk.Logging`, async-flowing `LogScope`/`LogScopeContext` scopes, `ProviderLogLevel`, `SmartHopperLogScrubber` (PII scrubbing before any log leaves the process), `SmartHopperLogExceptionFormatter`, and `DebugLogger` fallback.
- Logging sinks in `SmartHopper.Infrastructure.AICall.Logging`: `ILogSink`/`ILogFormatter` contracts, `SmartHopperLogEntry`, `SmartHopperLogFormatter`, `SmartHopperLiveSink` (Rhino command-line output) and `SmartHopperLogFileSink` (rolling per-day file under the diagnostics log folder with retention pruning). `SmartHopperLogger` (Infrastructure.Hosting) routes entries to every enabled sink at or above its per-sink level and refreshes routing whenever settings are saved.
- `SmartHopperDiagnostics` helper (log folder resolution, zip-the-logs support for bug reports) and `RuntimeMessageLogger` (ProviderSdk.Diagnostics) capturing runtime messages as structured log entries.
- Diagnostics settings: `LiveOutputEnabled`/`LiveOutputLevel`, `LogFileEnabled`/`LogFileLevel`, and `LogRetentionDays` (default 3).
- Diagnostic zips now include `diagnostics.json` (SmartHopper/OS versions) so a shared zip carries the version context a maintainer needs.
- `AIProvider` exposes a `protected ILogger Log` typed logger (`AIProvider<T>` exposes `ILogger<T>`) so provider code logs with the concrete type as category; the singleton-instance catch blocks in `AIProvider<T>` now report through `Logger.For<T>()`.
- Diagnostics menu (`SmartHopper → Diagnostics`): Open Log Folder (Debug builds), Copy Diagnostics for GitHub (scrubbed markdown report to clipboard), and Generate Diagnostic Zip (scrubbed logs + `diagnostics.json`).
- Migrated all `Debug.WriteLine` calls in the `SmartHopper.Providers.*` projects to `Logger.For<T>()` (or the `Logger` static facade in static classes), so provider diagnostics flow through the local logging sinks instead of debug-only output.
- Migrated all `Debug.WriteLine` calls in `SmartHopper.Core` (291 calls, 27 files) to `Logger.For<T>()` / the `Logger` static facade.
- Migrated all `Debug.WriteLine` calls in `SmartHopper.Core.Grasshopper` (250 calls, 50 files) to `Logger.For<T>()` / the `Logger` static facade.
- Migrated all `Debug.WriteLine` calls in `SmartHopper.Components` (188 calls, 46 files) to `Logger.For<T>()` / the `Logger` static facade.
- Migrated all remaining `Debug.WriteLine` calls (400 calls, 61 files: `SmartHopper.Infrastructure`, `SmartHopper.Menu`, `SmartHopper.ProviderSdk`, `SmartHopper.Components.Test`, test projects) to `Logger.For<T>()` / the `Logger` static facade — no `Debug.WriteLine` remains in `src/`.

- Provider **settings surface** model (`SmartHopper.ProviderSdk.Settings.ProviderSurfaceElement`): `IAIProviderSettings`/`AIProviderSettings`/`IAIProvider`/`AIProvider` gain `GetSurface()`, the ordered list the settings dialog renders — `BoundSettingElement` rows wrap persisted descriptors with live view-state delegates (options, enabled, companion actions), while `ChromeElement` rows (`StatusChrome` status labels, `ActionChrome` buttons invoking provider actions directly with the current form, `LinkChrome` hyperlinks) are evaluated through delegates at render and repaint time and never touch the settings store. The default implementation wraps every non-hidden descriptor, so existing providers need no changes. The generic provider settings page renders the surface and repaints delegate-driven live values periodically without storage traffic.
- Added `SettingDescriptor.IsHidden` for settings that remain stored and encrypted according to `IsSecret` but are never shown in provider settings UI and are preserved when visible settings are saved.
- Added `IAIProvider.AuthenticationErrorMessage` and the default `AIProvider.AuthenticationErrorMessage` property, plus `ClassifyHttpError(..., IAIProvider)`. A trimmed plain-text message of 1–300 characters with no control or Unicode format characters, URL markers, email addresses, or domain names replaces the server body on HTTP 401; providers implementing `IAIProvider` directly must add the property.


- Added a remote model catalogue layer (`smarthopper-models/1`): `ModelCatalogueService` in `SmartHopper.ProviderSdk.Catalogue` lazily refreshes model capabilities at startup from `models.smarthopper.xyz/api`, verifies detached ES256 signatures against an embedded public key before parsing, strictly re-validates the wire schema (closed property sets, bounded identifiers, decimal-string pricing, no URL/HTML-capable fields), applies entries over the embedded seed lists without deletions, and persists the verified artefact under `%APPDATA%/Grasshopper/SmartHopper/models-catalogue.*` for offline/long-outage reuse. All failures degrade silently to the embedded seed lists.
- Added `AIModelPricing.Currency` (`USD`/`EUR`) so catalogue-provided EUR pricing is not mislabeled as USD.
- Catalogue refresh now verifies the discovery manifest too: when the server provides `manifest.sig`, the detached ES256 signature is checked against trusted keys before `latest_version`/`key_id`/artefact paths are trusted — an invalid signature fails closed; a missing one is tolerated during rollout (the manifest then acts as an unauthenticated hint and the catalogue signature still gates what is applied).
- Added a persisted anti-downgrade floor (`models-catalogue.floor`): the highest accepted `catalogue_version` is ratcheted on disk, so a replayed older-but-genuine release is rejected even after the cached artefact is deleted or corrupted. The floor never moves down and is restored from the verified cache when its file is lost.
- Added a "Refresh Model Catalogue" item to the SmartHopper Grasshopper menu (between "Refresh Providers" and "Verify Providers Hash") that opens a dedicated dialog: it streams the catalogue service's progress log (`RefreshLog` event), then reports the applied source — remote, verified local cache, or built-in seed tables — plus descriptive errors.
- Added chat document tracking to WebChat. The chat now records an "Active document" card when it opens, a "Switched to `<document>`" card when the active document changes, and a deterministic "Canvas changed since the last AI response" summary (components added/removed/modified, input modifiers changed, connections added/removed) with the GhPatch inlined when small or a `gh_get` hint when large. Pivots, runtime messages, selection and metadata are ignored. The cards are persisted in history, replayed in the UI, and sent to the model as user-side text. Baselines are stored under `%APPDATA%/Grasshopper/SmartHopper/ChatCanvasBaselines`.
- Added `AIInteractionDocumentEvent`, `IAIModelProjectedInteraction`, and `InteractionModelProjection` to the Provider SDK so special interactions can keep their own type in history while providers encode them as user text.
- Added Grasshopper document tools: `gh_document_list` (open documents with `documentId`, name, path, active/modified flags and object count), `gh_document_switch` (ViewControl), `gh_document_new` (blank document) and `gh_document_open` (open a `.gh`/`.ghx` from an absolute path, or activate it if already open). `gh_get` and `gh_document_save` accept an optional `documentId` to read or save a document that is not active, without switching the canvas.
- Added a "Visit Website" item to the SmartHopper Grasshopper menu that opens <https://smarthopper.xyz>, placed just above "About".
- Added an agentic WebChat planning flow through the Chat-only `plan_propose` tool. Proposed multi-step plans pause in an interactive chat card until approved or rejected; plan approval never pre-approves later canvas edits.
- Added a Chat-only `plan_tasks` tool that renders a live task plan card in WebChat. The model sends the complete task list on every call, tasks show as pending, in progress, or completed, and a progress indicator updates in place per plan ID through the new `ITaskPlanPresenter` invocation seam.
- Added invocation-scoped `ConsentGate`, explicit `AIToolSurface` exposure controls, and `AIMutatingTool` metadata so WebChat, MCP, direct AITool calls, and Grasshopper components share one-call mutation consent without requiring a `ConversationSession`.
- Added automatic Grasshopper undo coordination around every `AIMutatingTool`: operation-specific undo actions are verified and multiple records from one tool call are merged into one Ctrl+Z step.
- Added document-staged visual review for structural AI canvas changes. `gh_put`, `gh_remove`, `gh_clear`, `gh_connect`, `gh_disconnect`, `gh_move`, `gh_group`, and `gh_group_selected` now paint proposed additions, modifications, removals, wires, target positions, and groups over the actual Grasshopper canvas and apply only user-accepted changes.
- Added reusable `CanvasChangeReviewSession`, `CanvasChangeReviewService`, `CanvasChangePreviewOverlay`, and Eto review dialog infrastructure in `SmartHopper.Core.Grasshopper`.
- Pull request descriptions and titles are now drafted automatically, with a deterministic fallback when AI is unavailable and no overwriting of descriptions written by a person.
- Added `AIBody.WithReplaced` extension for replacing a specific interaction by reference in an immutable body.
- Added `AITool.GetRequiredParameters()` helper to parse required parameter names from a tool's JSON schema.
- Added `ProviderTestComponentBase` to `SmartHopper.Components.Test/Providers` and converted all ~43 `Test{Provider}{Feature}Component` classes to inherit from it. The base centralizes common setup/teardown (provider selection, `RunOnlyOnInputChanges`, category/exposure) while each component remains an independent per-provider test runner.
- Added nested mutable `Builder` classes to `AIInteractionText` and `AIBody` for streaming aggregation and body construction. Builders accumulate local state and emit immutable snapshots via `Build()`.
- Added `ProviderTrustPolicy` in `SmartHopper.ProviderSdk.AICall.Validation` to centralize provider integrity/trust decisions. `AIRequestCall.IsValid()` and `AIRequestCall.Exec()` now use this policy to block, warn, or allow provider calls based on the configured `ProviderIntegrityCheckMode` (Soft/Hard/Strict) instead of only emitting validation messages.
- Added automatic `version:` labeling for pull requests and issues. `pr-milestone.yml` now delegates to the `milestone/assign-pr` action; `milestone/assign-pr`, `github-issue-label-version-from-template.yml`, and `create-version-label` all use the shared `versioning/normalize-version` action to produce a milestone-aligned `version:` label (e.g. `version: 2.0.0-alpha` for `2.0.0-dev.260901`), creating the label if it does not exist.
- Added `TestProviderHttpClientFactory`, `FakeAIProvider`, `FakeProviderModels`, and `FakeProviderRegistryHost` to `SmartHopper.ProviderSdk.Tests/TestHelpers` to support deterministic, network-free provider contract/round-trip tests.
- Added `AIProviderCallTests` in `SmartHopper.ProviderSdk.Tests/AIProviders` covering text and tool-call round-trips, authorization header propagation, request encoding, and provider error handling using an in-memory HTTP fake.
- Added `SelectionContextProvider` and `ViewportContextProvider` in `SmartHopper.Core/AIContext`, registered by default through `AIContextBootstrapper`. WebChat now includes `selection` and `viewport` in its default context filter.
- Added MCP `resources/list`, `resources/read`, `prompts/list`, and `prompts/get` support in `SmartHopper.Infrastructure/Mcp/JsonRpcDispatcher`. Added `McpResource`, `McpPrompt`, `IMcpResourceProvider`, `IMcpPromptProvider`, `EmbeddedMcpResourceLoader`, `StaticMcpResourceProvider`, and `StaticMcpPromptProvider`. Resource and prompt content is now compiled into the assembly from embedded `.md` files under `src/SmartHopper.Infrastructure/Resources/Mcp/` and linked from the existing GhJSON spec snapshot; `docs:///tool-help/{toolName}` still delegates to the `smarthopper_tool_help` AITool.
- Added a shared `AgentKnowledgeCatalog` with embedded Grasshopper foundations, data-tree, definition-design, geometry, debugging, performance, scripting, tool-strategy, provider, research, and source-policy references. Added MCP `definition-builder` and `performance-reviewer` prompts.
- Added read-only `canvas_screenshot` and `viewport_screenshot` AITools with bounded base64 PNG output, UI-thread capture, MCP exposure, and `Canvas To Image` / `Viewport To Image` stateful Grasshopper components that preserve `VersatileImage` outputs between trigger runs.
- Added `OutputAdapterTreeGatheringTestComponent` to `SmartHopper.Components.Test/OutputAdapters`, a canvas test that validates `AIOutputAdapterBase` typed-tree gathering, per-branch option alignment, and verbatim non-tree input injection without issuing a provider request.
- Added `OpenAICompatibleAudioCodec` in `SmartHopper.ProviderSdk.AICall.Core.Interactions`, a shared codec for OpenAI-compatible chat audio: `input_audio` content parts from `AIInteractionAudio` (in-memory bytes or file paths, with MIME/extension-to-format mapping), `modalities`/`audio` output parameters with `voice` and `format` extras, and `message.audio` response decoding with transcript fallback. OpenAI now routes `AudioInput`/`AudioOutput` requests to Chat Completions (the only endpoint that carries audio payloads) and serializes audio interactions; OpenRouter does the same on its `/chat/completions` path, so `Audio2Text`/`Text2Audio` requests actually transport audio instead of silently dropping it.
- Added an opt-in `Auto-approve changes` (`AA`) input to the `SmartHopper MCP Server` component. When enabled on the component that owns the running server, canvas mutations requested through that MCP server are approved without showing the review dialog; undo recording and canvas protection are unaffected. Defaults to `false`.
- Added an opt-in `Allow View Control` (`AV`) input to the `SmartHopper MCP Server` component, backed by `McpServerOptions.AllowViewControl`. When false (default), tools in the `ViewControl` category (`canvas_view`) are omitted from `tools/list` and rejected by `tools/call`; an explicit `EnabledTools` allow-list still overrides the filter.
- Added the `gh_set_value` AI tool to set values on existing canvas objects in place without recreating them: panel and scribble text, boolean toggles, number sliders (range-clamped), value list selections (by index, name, or item value), standalone persistent parameters, and component input parameters via the `param` argument. Mutations run on the Grasshopper UI thread, are refused while a solution is in progress, record undo, and re-schedule the downstream solution. Added `TestGhSetValueComponent` to `SmartHopper.Components.Test`.
- Added the `gh_run` AI tool to re-run components through the normal lock transition: each target is disabled then re-enabled on the Grasshopper UI thread so the solution is scheduled by Grasshopper itself, avoiding direct expiration during an active solve. Calls are refused while a solution is in progress and locked objects are skipped. Added `TestGhRunComponent` to `SmartHopper.Components.Test`.
- Added session-scoped mutation reservations for `tools/call` idempotency. Mutating tools advertise an optional `requestId` transport field in their input schema; a retry of the same tool + requestId inside one MCP session joins the in-flight operation or replays its recorded terminal result — the mutation can never apply twice because of a dropped connection. `requestId` requires the `Mcp-Session-Id` header (`session_required` otherwise) and is stripped from business arguments before schema validation; replays preserve payload, images, helper metrics and diagnostics as defensive clones.
- Added read-only `canvas_view` AITool that repositions the Grasshopper canvas viewport without mutating the document. Supports `zoomExtents`, `frameGuids`, `setZoom`, and `setCenter` actions and returns the resulting zoom, center, and visible region.
- Added read-only `canvas_hi-res_screenshot` AITool (`Vision`) that renders a canvas region to a high-resolution PNG independently of the visible viewport, using Grasshopper's hi-res tile renderer. Supports `scope` (`document`/`selection`/`guids`/`bounds`), `padding`, `scale` (default 2.0), `background` (`transparent`/`white`/`canvas`/hex), and `savePath`. Output size is bounded by a 2 GiB hard cap on the composited bitmap that tightens automatically under memory pressure. Intended for publishing/printing rather than agent vision.
- Added mutating `gh_select` AITool that updates the Grasshopper canvas selection (`set`, `add`, `remove`, `clear`) through the standard consent review. Protected components are never added to the selection.
- `canvas_screenshot` and `viewport_screenshot` now accept an optional `savePath` argument that also writes the PNG to disk, and their MCP responses are emitted as a native `image` content block plus a `text` metadata block.
- `gh_get` and its wrapper variants now accept an optional `detail` argument (`summary` | `full`, default `full`). `summary` omits `ghjson` and `serializationQuality` and returns a compact per-component projection with `instanceGuid`, `name`, `nickName`, `pivot`, and live `bounds`.
- MCP `tools/call` argument-validation errors now include the called tool's `expectedSchema` in the error payload instead of leaving clients to refetch `tools/list`.
- Added image support to tool results. `AIInteractionToolResult` now carries a `Images` list (`ToolResultImage` records) populated by `ToolResultMediaExtractor` at session-persist time: the `imageBase64` payload is removed from the result JSON (replaced by an `imageAttached` marker plus size metadata) so providers never receive base64 as text, and WebChat renders the image inside the tool-result bubble. Tools declare intent with `"imageAudience": "model" | "display"`; provider codecs emit model-bound images natively where possible (Anthropic `tool_result` image blocks, OpenAI Responses `input_image` items, Gemini `inline_data` parts) or as a trailing user-role image message on OpenAI-compatible chat APIs (OpenAI, MistralAI, OpenRouter). `canvas_screenshot` and `viewport_screenshot` declare `model` so vision models actually see the screenshot; `canvas_hi-res_screenshot` declares `display`.
- Added `OpenAICompatibleImageCodec` in `SmartHopper.ProviderSdk.AICall.Core.Interactions`, a shared codec that encodes `ToolResultImage` payloads as OpenAI-compatible `image_url` content parts and user messages.
- Added image attachments to WebChat. A new attach button opens a native file picker; selected images (PNG/JPEG/GIF/WebP/BMP, magic-byte validated, ≤15 MB, ≤4 per message) show as removable thumbnail chips and are sent as user-role `AIInteractionImage` interactions alongside the text.
- Added `AIReturn.AttachUsageMetrics` in `SmartHopper.ProviderSdk`, a shared helper that attaches a call's `AIMetrics` to the last interaction of a return body. It is a no-op when the body already reports usage, so streaming adapters can call it unconditionally at finalization without double counting.
- Added `AutonomyUsage.TokensEstimated`, which flags when the reported token total includes heuristic estimates for calls where the provider did not report usage; WebChat renders the counter with a `~` prefix in that case.
- `AIBodyExtensions.EstimateTokensFromInteractions` is now public so session-level accounting can reuse the heuristic token estimate.
- Added a read-only `canvas_point` AITool (`ViewControl`) that pans and zooms the Grasshopper viewport to component instance GUIDs and/or a world-space region and draws a transient border highlight for 8 seconds via the new `CanvasPointerService` post-paint overlay. WebChat renders a card with the tool's message and a "Show on canvas" button that replays the pan/zoom/highlight through the `CanvasPointerBridge` Core→Core.Grasshopper handler; requests are kept in a bounded registry keyed by pointer id.
- Added a Chat-only `ask_user` AITool (`Planning`) that lets the model ask a blocking question mid-run with 2–4 predefined answer options; the WebChat card always adds a free-text field and only resolves on an answer or run cancellation, through the new `IUserQuestionPresenter` invocation seam.
- Added suggested follow-up prompt chips to WebChat. After each completed turn a `SuggestedPromptsSpecialTurn` runs on the session's current model against a bounded digest of the last ~8 meaningful interactions, gated on `AICapability.Text2Json` and a `{"suggestions": [...], "maxItems": 3}` JSON output schema (new `SpecialTurnConfig.OverrideJsonOutputSchema`). The turn is non-streaming, tool-less, and `Ephemeral` so suggestions never enter history; a tolerant parser yields 0–3 chips that fill the input field without sending. Controlled by the new `EnableSuggestedPrompts` Canvas Assistant setting (default on).
- Added a persistent task-plan panel to WebChat docked under the autonomy overlay in a shared `#hud` column. The active `plan_tasks` plan renders there with goal, completed/total count, progress bar, and per-task pending/in-progress/completed status, keeping its final state until replaced or the chat is reset.
- Added a live "Extend limits" row to the WebChat autonomy overlay. Clicking it navigates `sh://event?type=extend_autonomy`, which calls the new `ConversationSession.ExtendAutonomyLimits()`: each call adds half of the configured `MaxAutonomousTime` and `MaxAutonomousTokens` on top of the effective budgets (accumulated via `Interlocked`-backed counters so the turn loop sees them at the next budget check). The row is hidden for unbounded runs and the button is disabled once the run ends or a budget is already exhausted.
- Added opt-in LAN access to the MCP server via a new `Allow External Connections` (`AX`) component input (`McpServerOptions.AllowExternalConnections`, default off). When enabled the server binds all IPv4 interfaces (`0.0.0.0`) and the component's `Url` output reports the LAN-reachable `http://<machine-ip>:<port>/mcp` — the OS may prompt for firewall permission on first use. The server refuses to start without a bearer token, so unauthenticated remote access is impossible, and the `Host`-header guard then requires the request to name this machine (loopback, `localhost`, hostname, or one of its own interface IPs).
- Added local file path support to the `img2text` AI tool. When `imageUrl` resolves to an existing file on disk (absolute path or `file://` URI), the image is loaded and sent as base64 instead of passing an unfetchable `file://` URI to remote providers; MIME type is inferred from the file extension.
- `gh_get` and its wrapper variants now accept a `nameFilter` parameter (case-insensitive substring match against `Name` or `NickName`), an `includeRuntimeMessages` flag, and a `fields` projection list for `detail: 'summary'`. Supported projection fields: `instanceGuid` (always emitted), `name`, `nickName`, `pivot`, `bounds`, `selected`, `locked`, `previewOn`, `category`, `subcategory`, `messages`, `runtimeData`, and `internalizedData`. The data projections reuse the GhJSON serializer's compact per-parameter form (only parameters that actually carry data, keyed by nickname) instead of dumping raw data trees.
- Added in-session subagent orchestration to WebChat (the "conductor" pattern): four Chat-surface tools — `spawn_subagent`, `send_to_subagent`, `wait_subagents`, `list_subagents` — let the assistant delegate focused work to background workers while the main turn ends. `SubagentHost` (`SmartHopper.Infrastructure.AICall.Subagents`) owns per-session subagents, runs each as a child `ConversationSession` cloned from the parent request (provider/model/endpoint/tool filter minus conductor tools — depth cap 1), and receives no presenters or subagent host, so consent-gated canvas mutations fail closed and workers cannot spawn recursively. When a run finishes the host raises `RunFinished`; WebChat appends a visible evidence line (`subagent 'x' reported back`), queues the report while a turn is in flight, and — when the conversation is idle — automatically drives a wake turn whose persisted transcript keeps a terse user marker while the full worker report rides as a system-level interaction the model sees. `wait_subagents` marks the run's outcome delivered inline so no wake is needed. Wake chains are capped (5 consecutive wake turns; any real user message resets), a turn gate serializes user sends against wake turns, and a "still working" hint shows while workers remain active. Closing the chat cancels all runs.
- `ConversationSession.ExecProviderAsync` now passes the linked cancellation token into `AIRequestCall.Exec(stream: false, ct)`, so `SessionOptions.CancellationToken` aborts an in-flight provider call instead of only applying between turns — this is what makes subagent cancellation (and WebChat stop) take effect promptly.
- Added an outgoing session queue to WebChat: messages sent while a turn runs (or while another send is staged) wait FIFO in `WebChatDialog._messageQueue` — staged image attachments travel with the entry — and the head is promoted automatically when the turn settles through the same `_pendingUserMessage`/`_attachmentsForNextSend` staging path, so an idle send is unchanged. The toolkit queue panel (`features.queue`, `setQueue(items)`) mirrors the merged queue — user sends plus agent-injected entries — with per-row edit/send-now/remove actions that round-trip through `sh://event?type=queue&action=edit|submit-now|remove&id=…`. While a turn runs the composer stays editable (type-ahead) and the send button relabels to "Queue" next to Stop. Queued user sends always win over subagent wake turns.
- `send_to_subagent` gains a `mode` argument: `"queue"` (default) parks the instruction in the worker's new `PendingInstructions` FIFO and `SubagentHost.KickNext` delivers it as the next run when the current run settles — busy sends are never rejected or lost; `"on-the-fly"` steers the live run by appending the instruction to the child session when no tool call is pending (falls back to queue otherwise), with a guaranteed continuation run at settle when a steer lands inside the last provider call's window. Tool results report `delivery` (`started`/`queued`/`steered`) and `pending_count`. Worker pending instructions surface in the chat queue panel via `ISubagentHost.PendingItems`/`PendingChanged` (`meta.agent` bylines) and accept the shared row actions through `TryApplyPendingAction` (remove/edit/promote). `SubagentItem` gains `PendingCount` and `Delivery`.
- MCP clients are now warned that the server lives inside a Grasshopper document: the `initialize` result carries an `instructions` field explaining that activating a different document unloads the host document and stops the server, document descriptions from `GhDocumentAccess.Describe` (`gh_document_list`, `gh_document_new`, `gh_document_open`) report `hostsMcpServer` per document, and the descriptions of `gh_document_new`, `gh_document_switch` and `gh_document_open` state the disconnect consequence. The shared component-type GUID moved to `WellKnownComponentGuids.McpServerComponent` so lower layers can identify the hosting component without referencing `SmartHopper.Components`.
- Hybrid PDF document evidence: the new `SmartHopper.DocumentProcessing` project (Rhino-free, references ProviderSdk + PdfPig) owns the `IDocumentReader` contract plus the `DocumentReaders` extension registry — `Readers/PdfDocumentReader` is the built-in implementation producing typed `DocumentEvidence` (text/layout extraction plus embedded assets); future formats register new readers without touching call sites — while the new `SmartHopper.DocumentRendering` project (PDFtoImage 5.4.0/PDFium) supplies page renders inside one long-lived dedicated `AssemblyLoadContext` whose public reflection boundary exchanges only BCL values via the format-keyed `DocumentRenderer` facade (format id, source bytes, page index, DPI, max side, normalized crop in; PNG bytes and page dimensions out — new renderers plug in without changing the host contract). Selected pages render at 144 DPI bounded to 2048 px on the longest side (detail regions up to 4096 px), with a 128 MiB source cap, at most 32 rendered pages per call and a 64 MiB aggregate encoded budget; conversion and rendering are cancellable and run off the UI thread.
- Neutral multimodal document contracts in `SmartHopper.ProviderSdk`: `Documents/DocumentEvidence.cs` (`DocumentEvidence`, `DocumentPageEvidence`, `DocumentCoverage`, `DocumentEvidenceJsonCodec`, `DocumentEvidenceInteractionBuilder`), `AICall/Core/Interactions/SourceContentReference.cs` (`SourceContentReference`, `SourceContentKind`, `SourceContentRegion`, `SourceContentReferenceJsonCodec`), `IAIInteraction.SourceReference` + `WithSourceReference`, and `ToolResultImage.SourceReference` — provenance (document, page, asset, figure, crop) rides end-to-end so visual evidence can convert to text without losing attribution.
- `AIInputPayloadType.Multimodal` plus `AIInputPayload.FromInteractions(IEnumerable<IAIInteraction>)`: one payload wraps a whole document's ordered interactions — shared `TurnId`, sourced text and model-visible images in document order. The type label is a presentation classification only; required input capabilities always derive from the interactions, and `AICapabilityExtensions.RequiredInputCapabilities` now also requires `ImageInput` when a tool result carries `SendToModel` images (display-only tool images add no requirement).
- Central model-bound request preparation: `IPolicyPipelineHost.PrepareExecutionAsync` returns an `AIRequestPreparation` (a detached execution copy plus diagnostics and helper metrics); `SmartHopper.Infrastructure` implements it in `RequestModalityPreparationService` on the reworked `FallbackChain`/`ModalityFallbackResolver`/`ImageToTextFallback`. Direct calls, streaming, special turns and chat tool continuations all prepare through this one path — the resolved primary model is never upgraded merely because the source carries images, `SendToModel` media is capability-validated (unknown capability is not confirmed support), and a configured vision helper converts only the image evidence while preserving order, `TurnId`, tool-call/result IDs and source references. Helper calls are metered once per evidence conversion (charged even when the primary call later fails), keep their own provider/model, and cannot recursively fall back. `ModalityFallbackContext` carries the per-run evidence cache.
- Explicit visual-evidence enrichment: `VisualEvidenceEnrichmentService` + `VisualEvidencePrompts` reuse the same helper machinery for the `file2md`/`web2md` describe paths. Unlike silent fallback it runs only on explicit request (`describeImages=true` or `consumerSupportsImages=false`), preserves `sourceMarkdown` and appends a labeled `## Visual Evidence (AI-Generated)` section — original media and provenance are retained and adapted images are marked `SendToModel=false`.
- Optional `Pages` and `Region` inputs on `File To Markdown`, `AI File To Markdown` and `File to AI`: `Pages` is a JSON array of unique 1-based PDF page numbers (empty selects the whole document) and `Region` is `{page, x, y, width, height}` in normalized top-left coordinates inside a selected page; invalid selections are explicit errors, parsed by the shared `DocumentSelectionJsonParser`.
- `File To Markdown` and `AI File To Markdown` now surface rendered evidence on a dedicated `Rendered Pages` output: `Img` keeps only embedded assets (original figures/logos) while `Pgs` carries the page renders and any requested region crops in document order, both as `VersatileImage` trees with page/asset provenance.
- `file2md`/`web2md` tool schemas replace `imageMode` with evidence controls: `extractImages` (default `true` — set `false` for local text-only extraction without rendering), `describeImages` (default `false`), `consumerSupportsImages` (nullable tri-state — see MCP notes), `pages`, `region`, and `imageDescriptionPrompt` (custom helper guidance; the shared evidence-preservation instruction remains active).
- `File to AI`/`Web to AI` now emit one ordered multimodal `Input >` payload — document identity/coverage header, then each selected page's text with its model-visible renders and crops via `DocumentEvidenceInteractionBuilder` — alongside `Markdown`/`Format` outputs. Packaging performs no provider call and no image pre-description, and scanned pages with no extractable text still yield image-bearing payloads.
- Web image materialization in `web2md`/`Web2AI`: approved HTTP(S) article images become typed `ToolResultImage`s through the source-access boundary (deduplicated, at most 32 sources, 64 MiB aggregate encoded budget), and `data:` image URLs inline as bounded bytes re-linked by stable `smarthopper-asset:img-N` references — Markdown and JSON never carry base64.
- MCP `tools/call` now emits typed model-visible `ToolResultImage`s as ordered native `image` content blocks before the text block; `structuredContent` carries compact metadata plus `nativeImageOrder` (the asset IDs of the model-visible blocks, in order), image bytes never appear in JSON or structured content, and `_meta["smarthopper/helperUsage"]` forwards enrichment/fallback metrics on both success and error results. Legacy `{imageBase64, mimeType, ...}` screenshot payloads still map to a single image block with the binary stripped from metadata. `file2md`/`web2md` results report a `consumerCapability` tri-state: `supported` (native blocks emitted), `unsupported` (page-linked textual evidence via the configured helper, no native blocks), or `unknown` + `clientAdaptationRequired: true` when `consumerSupportsImages` is omitted — never an unconsented provider call.
- PDF and image attachments in WebChat: the picker now accepts PDF documents alongside images and stages each file through the shared `file2md` tool pipeline — conversion runs off the UI thread on a bounded snapshot, PDF bytes and local paths never reach the WebView, the composer locks only while staging (the send button still relabels to Queue while a turn runs), and queued entries keep their own attachment snapshots.
- `SmartHopper.DocumentProcessing.Tests` — new Rhino-free xUnit project covering page/region selection parsing, evidence preservation and the real parser/renderer against a checked-in synthetic fixture (`TestAssets/`, ground truth plus expected page renders); real-world PDFs are exercised only through the `SH_TEST_REAL_PDF` opt-in. `TestFile2MdSelectionComponent` joins `SmartHopper.Components.Test` for canvas-side `Pages`/`Region` wiring validation.
- Shared `size`/`aspect_ratio` image-generation concepts: the `AI Text To Image` component, `AI to Image` component, and `text2img` tool now carry a provider-agnostic size ("1024x1024" or tier "1K"/"2K"/"4K") plus an optional "W:H" aspect ratio, parsed by `ImageGenerationSpec` and normalized per provider — OpenAI snaps to its supported `size` literals by orientation (gpt-image `1536x1024`/`1024x1536`, dall-e-3 `1792x1024`/`1024x1792`, `auto` passes through), Gemini maps to `imageConfig.imageSize` tiers and snaps `aspectRatio` to the closest supported ratio, OpenRouter forwards `modalities`/`image_config`, and LocalAI resolves literal `WxH` sizes.
- Image-generation extras replace the removed shared inputs (keys name the API concept, no provider prefix): `quality`, `background`, `output_format`, `output_compression` on OpenAI; `negative_prompt` (folded into the request text — the generateContent contract has no dedicated field) on Gemini; `negative_prompt`, `guidance_scale` on OpenRouter; `negative_prompt`, `seed`, `step` on LocalAI.
- LocalAI image generation: `ImageOutput` requests now route to `/images/generations` with a dedicated encode (prompt/size/`b64_json`) and `data[]` decode, instead of being dropped onto `/chat/completions`.
- OpenAI image decode now reads `b64_json` payloads (gpt-image family) in addition to hosted `url` results (dall-e).
- Gemini gains a `ThinkingLevel` provider setting (default empty = API default): Gemini 3 accepts `minimal`/`low`/`medium`/`high`, Gemini 2.5 accepts an integer token budget as string (`"0"` disables). A per-request `thinking_level` extra overrides the setting, matching the `service_tier` precedence pattern.
- Non-image provider extras: `reasoning_effort`, `reasoning_max_tokens` and `reasoning_exclude` assemble OpenRouter's unified `reasoning` object; `provider_order`, `provider_only` and `provider_ignore` set `provider` routing preferences per request (also configurable as provider settings), accepting comma-separated slugs or JSON arrays; `cache_ttl` emits `cache_control.ttl` (`"5m"`/`"1h"`) alongside the existing `enable_caching` extra on Anthropic; `keep_alive` maps to `keep_alive` on Ollama; `media_resolution` sets `generationConfig.mediaResolution` on Gemini 3+ models (the 2.x family rejects the field with HTTP 400). All new keys are registered as `AIExtraDescriptor`s, so the **Extra Settings** component renders them per provider: OpenAI (`quality`/`background`/`output_format`/`output_compression`), Gemini (`negative_prompt`/`media_resolution`), OpenRouter (`reasoning_*`/`provider_*`/`negative_prompt`/`guidance_scale`), Anthropic (`cache_ttl`), Ollama (`keep_alive`) and LocalAI (`negative_prompt`/`seed`/`step` image options).
- `SmartHopper.Providers.Test` — deterministic local test provider (Debug-only, never shipped) exposing a `test-batch` model that implements `IAIBatchProvider` with controllable `SubmitDelay`/`CompleteAfter`/`OmitIndices`/`ThrowOnSubmit`/`FailBatch` knobs and a `SUBMIT`/`POLL`/`CANCEL` event log with counters. The new **Test Batch Control** testing component drives the knobs at runtime, enabling live lifecycle verification inside Grasshopper — mid-flight cancel/re-request, cancel-during-submit, shared-sentinel dedup, partial-result retry and double save/reopen — without provider costs.
- `SmartHopper.Components.Test` gains a null-item `DataTreeProcessor` regression fixture (`Test DataTreeProcessor (Null Items)` + `Null Item Emitter` driven by `NullTreeBuilder`) so a null branch item cannot regress into an NRE without a component-level test catching it.
- **Cache outputs** — new per-instance checkable menu item on every stateful component that supports result reuse (persisted, default on). Unchecking forces fresh execution: the committed-result lookup is bypassed while run-scoped bookkeeping — fragments, sentinel registration/resolution, within-run deduplication — keeps working, and the run skips `CommitRun` so the previously committed snapshot survives. Re-checking restores reuse without re-executing; *Clear output cache* remains the only discard path.
- `gh_apply_changes` AI tool (`Canvas.Modifications`) — finite, closed change sets applied as one review, one undo group and one scheduled solve. `operations` is a non-empty array (max 256): at most one `put` (first; GhJSON fragment like `gh_put`) followed by `set_value` (`target` + `value`/`panelMode`/`param`), `set_provider` (`target` + `provider`/`model`) and `connect`/`disconnect` (`from`/`to` endpoints with optional `paramName`/`paramIndex`). Targets are exactly `{instanceGuid}` for live objects or `{id}` for components inside the same request's put fragment — never a canvas index. The request is validated before any canvas access: unknown kinds/fields, duplicate `opId`s, malformed targets, `id` targets without a put, contradictory connect/disconnect pairs and conflicting writes on one logical field are all rejected up front, and a `set_provider(model)` plus an explicit wire into the same Settings input is refused as ambiguous. One all-or-nothing review covers the set (deselecting any row rejects everything); the commit runs in a single bounded UI turn with strict endpoint resolution, per-operation status (`applied`/`noop`/`failed`), reverse-order compensation on failure (`failed_compensated`/`failed_partial`), and exactly one scheduled solve at the end — a valid no-op, rejection, pre-start cancellation or compensated commit requests none. Per-phase timings (parse/prepare/review/commit) are traced without payloads.

### Changed

- Embedded chat now follows the Canopy visual language: soft sage surfaces, organic asymmetric radii (role-shifted bubbles, rounded cards, pill progress bars), Plus Jakarta Sans UI with JetBrains Mono kept for code and technical labels, and warm soft shadows. The chat theme was rewritten onto the shared Canopy tokens, and plugin-specific markup adaptations were re-translated accordingly.
- **Breaking (Provider SDK):** `IAIProviderModels` gains `SupportsApiModels`, declaring whether the provider exposes a live API model listing. The base `AIProviderModels` returns `false` and keeps `RetrieveApiModels` as an empty list; providers with a real listing opt in with `=> true` (OpenAI, Anthropic, Gemini, MistralAI, OpenRouter). Callers can now distinguish "no live listing by design" from "listing failed" instead of treating an empty result as an API failure.
- Chat instructions now follow an evidence-first inspect/act/verify workflow: retrieve focused local guidance when needed, discover missing capabilities before use, research uncertain technical claims with citations, respect mutation reviews, and report only tool-confirmed execution or worker status. Retrieved content is explicitly untrusted, and external research must not disclose private canvas data without authorization. Large-catalogue defaults are now 16 explicit tool names rather than entire categories, adding `smarthopper_readme`, `smarthopper_workflows`, `mcneel_forum_search` and `mcneel_forum_post_get` while moving duplicate `gh_get` wrappers and document switching behind discovery. Small scoped catalogues and execution authorization are unchanged.
- Settings dialog reorganized: the General tab now opens with a clickable provider tile grid (icon + name) that navigates to each provider's settings view; the separate AI Providers tab is gone. The Security tab now groups Trusted Providers, Integrity Check Mode and Network Settings (HTTP timeout plus the external download limit) above the external-source allowlist, which moved to the bottom and is edited inline — type an origin and press Enter or leave the field to add it, `−` removes a row, and the old add/remove buttons are gone. Per-row Configure buttons were removed from the trusted providers list since configuration launches from the tiles. Log sinks moved to a new Developer tab. The catalogue freshness limit (`CatalogueMaxAgeDays`) is no longer exposed in the UI and remains a hidden, still-applied setting.
- The About dialog now cites all shipped third-party packages: PDFtoImage (plus its bundled PDFium and SkiaSharp natives), AngleSharp, ExcelDataReader, the vendored DOMPurify and chat-ui chat resources, and the missing `System.Formats.Asn1`/`System.Text.Encoding.CodePages` runtime packages.
- Provider-model maintenance tooling is entries-first only: the new `tools/ProviderModelPricing.psm1` parses, merges and emits `AIModelPricing` `Entries` (`AIPriceEntry` kind/unit/amount) for `Pricing`/`BatchPricing` with exact property-boundary extraction, exact decimal-literal handling (scientific notation expands to plain invariant text, explicit zero is kept), and the full OpenRouter snake_case rate surface mapped to `(kind, unit)` entries (including `image`→`image_input`/`item` vs `image_token`→`image_input`/`token`, `video_input` winning over `video`, and `web_search`→`tool:web_search`/`invocation`). `Update-ProviderModels.ps1` delegates to it and now fails loudly on malformed or flat-field pricing instead of silently dropping rates; deterministic tests live in `tools/tests/ProviderModelPricing.Tests.ps1` and run in the `check-provider-models` workflow.
- **Breaking (metrics & pricing, `smarthopper-models/2`):** usage metrics and model pricing moved to the entries-first v2 contract. `AIMetrics` replaces flat token fields with `Quantities` (`"<kind>/<measured-unit>" → value`, vocabulary in `AIQuantityKey`) — decoders record what providers report (tokens, audio seconds, tool invocations) without unit inference; `EstimatedCost` is replaced by `EstimatedCosts` (currency → amount, never mixed) plus `ReportedCosts` for provider-billed amounts (e.g. OpenRouter `usage.cost`). `AIModelPricing` replaces flat price fields with `Entries` (`AIPriceEntry`: `kind` + explicit `unit` + per-unit `Amount`) supporting token/minute/item/invocation/character/megapixel billing; `AICostCalculator` joins entries against measured quantities, converts units, applies `Discount`, and accumulates per currency. Token-priced convenience accessors (`TextInput`, `InputTokensPrompt`, …) remain as sugar over the same contract. Serialized metrics (`Metrics` output, Combine Metrics) now emit `{ai_provider, ai_model, quantities, costs:{estimated, reported}, …}` — panels carrying the old flat shape degrade to empty dictionaries. Deconstruct SmartHopper Metrics keeps the stable fields as direct outputs (provider, model, finish reason, timing, counts, role) and emits `Quantities` and `Costs` as JSON objects for the JSON tools. The remote catalogue client only accepts `smarthopper-models/2` documents (`min_client_protocol` 2); v1 artefacts are refused.
- Anthropic model endorsements moved to the 5.x generation: `claude-haiku-5-5`, `claude-sonnet-5-5` and `claude-opus-5-5` are now `Verified`, while `claude-haiku-4-5-20251001` and `claude-sonnet-4-5-20250929` are no longer endorsed — matching the models catalogue overlay. The three 5-5 models are also the provider defaults (`Default = Text2Text | Text2Json | ReasoningChat | ToolReasoningChat | ToolChat | Image2Text`), taking over from `claude-haiku-4-5-20251001`.
- Seeded model defaults now mirror the models catalogue for OpenAI and MistralAI: `gpt-4o-mini-tts-2025-03-20`, `tts-1`, `tts-1-1106`, `tts-1-hd`, `tts-1-hd-1106` (`Default = Text2Speech`), `gpt-image-1`, `gpt-image-1-mini`, `gpt-image-1.5`, `gpt-image-2.5-flare-2026-09-08`, `gpt-image-2.5-sunburst-2026-09-08` (`Text2Image | Image2Image`), `sora-2`, `sora-2-pro` (`VideoOutput`), `text-embedding-3-large`, `text-embedding-3-small`, `codestral-embed`, `mistral-embed-2312` (`EmbedOutput`) — aligning default model selection before the first catalogue refresh.
- Deprecated models no longer carry `Default` flags in the seeded tables (`deepseek-reasoner`, `gemini-2.5-flash-preview-tts`, `gemini-2.5-pro-preview-tts`, `gemini-3.1-flash-preview`, `lyria-3-clip`, `lyria-3-pro`, `voxtral-mini-transcribe-2602`, `voxtral-mini-tts-2603`, `voxtral-mini-tts-mellon-greek-2606-solutions`, `dall-e-3`) — a deprecated model cannot be an endorsed default, matching the catalogue protocol invariant.
- Model seed metadata aligned with the catalogue: `gpt-6.1-sol` is now `Verified`, and stale context limits were corrected (`deepseek-chat` 60,000→163,840; `inclusionai/ling-3.1-flash` 262,144→1,000,000; `meta-llama/llama-4-scout` 1,310,720→327,680; `nvidia/nemotron-3-super-120b-a12b` 262,144→1,048,576; `qwen/qwen3.8-27b` 1,000,000→1,024,000).
- Seeded model defaults refreshed to the cheapest model of each provider's newest generation: OpenAI chat/JSON/reasoning/vision defaults move to `gpt-6-luna` (from `gpt-5-mini-2025-08-07`/`gpt-5.4-mini-2026-03-17`), image generation to `gpt-image-2.6-flare`/`gpt-image-2.6-sunburst`, text-to-speech to `gpt-4o-mini-tts-2025-12-15` only, and speech-to-text to `gpt-4o-transcribe` (from `whisper-1`); Gemini text/JSON/reasoning defaults move to `gemini-3.8-flash` (from `gemini-2.5-flash`) and all image capabilities to `gemini-3.1-flash-lite-image`; OpenRouter chat defaults consolidate on `openai/gpt-5.6-luna` (all five chat capabilities, replacing the delisted `openai/gpt-5-mini`). `gemini-2.5-pro`, `gemini-2.5-flash-lite` and `openai/gpt-5-mini` are now seeded `Deprecated` (withdrawn upstream — Google returns 404 and OpenRouter delisted it), `claude-sonnet-4-6` loses its stale `Text2Json` flag, and `claude-sonnet-5-5`/`claude-opus-5-5` are no longer endorsed defaults (high-cost tiers never hold defaults — `claude-haiku-5-5` remains the sole Anthropic default); MistralAI splits defaults by task weight: `mistral-large-4` takes `Text2Json | Image2Text` (complex/structured-output tasks — `mistral-small-2603` is `DiscouragedForTools` for `script_generate`/`script_edit`, so script components previously resolved an explicitly-not-recommended default) while `mistral-small-2603` keeps `Text2Text | ToolChat | Image2Text`, and dash-spelling aliases were registered across dotted model ids (`gpt-6-1-*`, `gpt-5-4-*`, `gpt-5-5-*`, `gpt-image-2-*`, `gemini-3-*`, `gemini-2-5-*`, `deepseek-v4-1-*`, `mistral-large-4.0`, plus OpenRouter-prefixed mirrors).
- Model selection is now strictly three-tiered: a manually chosen model (per-call override or provider settings) when capable, then the capability's endorsed default (`Default` matching the required capability), then the best-capable model — ordered `Verified` → `Rank` → not-deprecated → name. The intermediate "any other defaulted model" tier is removed: a model endorsed for an unrelated capability no longer outranks a higher-ranked non-defaulted one.
- Newly discovered providers are now presented in a single unified dialog instead of one confirmation prompt per provider: each row shows the provider icon, hash-verification status, last update date and non-deprecated model count, with a per-provider checkbox and a Select all toggle; verified providers are pre-checked and "Enable selected"/"Skip all" persist the decisions to `TrustedProviders` in one save.
- Provider integrity findings are now reported in a single dialog instead of one modal per failing provider: every Authenticode failure, tampered-trusted block, consent-mismatch block, strict-mode hash mismatch, unavailable-manifest block and unknown-provider block — plus soft-mode mismatch warnings — is collected while provider assemblies load and shown afterwards as one card per provider (severity glyph, file name, reason and full detail) in the shared trust dialog that already hosts the new-provider consent flow.
- The Metrics components moved from subgroup `L. Metrics` to `M. Metrics` to free the `L.` prefix.
- Compact placement and Tidy Up now use GhJSON's final constraint-group alignment: single-connection input/output leaves and compatible chains align to measured grips without independently breaking accepted alignments during collision handling. Authored pivots remain authoritative.
- Local GhJSON source builds accept `GhJsonSourceRoot` as an MSBuild property/environment override, retaining the adjacent-checkout default. Runtime layout regressions now include named/indexed input-port and output-leaf alignment around a fixed central component.

- GhJSON placement now preserves supplied pivots exactly and automatically lays out only pivotless new components, using the same compact spacing as Tidy Up (28-unit horizontal/vertical gaps, 48-unit island gaps). Mixed documents position missing components relative to connected fixed anchors; only disconnected, unanchored parts use `autoOffset`, which never shifts supplied pivots.
- AI definition-generation prompts, tool descriptions, and assistant/MCP guidance now require omitting `pivot` fields. `gh_generate` also removes model-emitted pivots so generated definitions automatically use compact placement.

- Redrew every SmartHopper component icon (105 components) in the Grasshopper icon style following the icon-set brief: semantic palette, darker-shade outlines, upper-left lighting, and the standard Grasshopper drop shadow. The set follows a compositional grammar — Grasshopper 2 cyan for data flowing into AI (`type >`), amber for data flowing out (`> type`), `A > B` conversions with a small green AI spark when the call uses AI, repeated glyphs for lists, square cards for forum posts and round frames for forum topics, and a single simple badge at most — and each resource is now named after its component class instead of the legacy abbreviations. Thirteen components that rendered no icon (forum adapters, GhJSON file components, `AIGhConnect`, `AIGhReport`, `CombineMetrics`, `GhPatchApplyToCanvas`, `GhValidate`, `SmartHopperMcpServer`) and the ones sharing an icon now each get a distinct one.
- The "Copy Diagnostics for GitHub" report now keeps only Info-level-and-above log entries instead of the raw tail: Trace-level settings-reload and catalogue-polling chatter no longer drowns the entries a maintainer needs, continuation lines (exception stack traces, multi-line messages) stay attached to their entry, and the read window grew to 200 KB (400 KB in diagnostic zips) to compensate. When nothing at that level was captured the report says so explicitly. The zip's full `log.txt` is unchanged.

- **Breaking (catalogue wire + Provider SDK):** `AIModelPricing` properties were renamed to the shared `<modality>_<direction>` convention — `Prompt`→`TextInput`, `Completion`→`TextOutput`, `Image`→`ImageInput`, `Audio`→`AudioInput`, `InputCacheRead`→`CacheRead`, `InputCacheWrite`→`CacheWrite`, `InputAudioCache`→`AudioCacheRead` — and `VideoInput`/`VideoOutput` (per input/generated video unit) were added. All generated `*ProviderModels.cs` tables, `Update-ProviderModels.ps1` (field map + `OutputPrice` now also sums `video_output`), `AICostCalculator` and the catalogue wire parser follow the new names. The catalogue `pricing` object now accepts `text_input`, `text_output`, `request`, `image_input`, `image_output`, `image_token`, `audio_input`, `audio_output`, `video_input`, `video_output`, `audio_cache_read`, `cache_read`, `cache_write`, `internal_reasoning` and `web_search` — including `cache_write`, which the previous parser did not read even though the server schema already allowed it.
- The catalogue wire parser no longer rejects documents carrying unknown properties: unknown fields are ignored (blocked from entering parsed state) instead of throwing, so catalogues emitting newer fields keep working on older clients. Known fields keep their full validation — protocol id, regex-bound identifiers, safe-text, decimal-string money and required-member checks are unchanged.
- Context editing now runs before every provider call: older `AIInteractionToolResult` bodies superseded by a newer result of the same snapshot tool with the same arguments (`gh_get` family, canvas/document reads, forum search, registry reads) are replaced with a short re-fetch pointer. The newest occurrence always survives; stale dumps stop consuming window without a summarization call.

- Per-provider settings tabs in the Settings dialog are replaced by in-place navigation: each row in the Providers tab's trusted-provider list gains a Configure button that swaps the dialog content to that provider's settings page with a "< Back" button at the top left (the dialog title shows the provider name while inside). The tab bar no longer grows with the provider count, and trusted-provider rows are now a single column (`icon | checkbox | Configure`).
- Collapsed tool bubbles in WebChat now show a short activity sentence in their header instead of `Tool Call: <name>`: each tool can declare a deterministic `AITool.ActivityLabel` (e.g. "retrieving components on the canvas" on `gh_get`, "surfing McNeel forum" on `*_forum_search`, "planning what to do next" on `plan_propose`, "starting some subagents" on `spawn_subagent`), and every tool's parameter schema gains an optional `aim` string the model can set per call to override it. Only sentences shorter than seven words are honored; labels are resolved in `AIInteractionToolCall.GetDisplayNameForRender`, so no chat-ui change was needed. Labels are now seeded across the built-in tool catalog (~60 user-facing tools), and `AIMutatingTool` forwards `ActivityLabel`/`ResultLabel` to `AITool` so mutating tools carry them too.
- Tool call and tool result now share a single collapsed chat card: `AIInteractionToolResult` reports the originating call's stream/dedup key (`tool.call:{id}` + args hash — the result record inherits the call's `Id`/`Arguments`), so every render path (live upserts, history replay, final flush) upserts the result into the pending call's card instead of appending a second bubble. The card header leads with a status glyph driven by `data-state` — `pending` hollow circle while the call is queued, `running` spinner after the executor dispatches it (flipped in place via a `data-callid` DOM update, no re-render), `done` ✓ or `error` ✕ once the result upserts — matching the shared `tool_call` status union in chat-ui; failed results are detected from the result's `error` field or `success: false`. When the result arrives the header flips to a completed-state sentence — the tool-written `summary` field in the result JSON (e.g. "found 12 components"), the model-written `aim_done` argument added to every tool's schema alongside `aim`, or the tool's deterministic `AITool.ResultLabel` — falling back to `Tool Result: {name}`; the body shows a compact `args → result` line above the pretty-printed JSON. `aim`/`ActivityLabel` text is additionally sanitized against HTML injection (`<`, `>`, `&`, `"`) since the header is injected raw.
- Every new component placed through `gh_put` or `CanvasGhJsonPlacer` (chat "Place on canvas" cards) now gets its canvas position from the shared GhJSON tidy-up layout pipeline (`GhJsonGrasshopper.ComputeLayout` — dependency-graph layout plus bounds-aware spacing, port alignment, wire corridors and collision resolution) instead of raw AI-supplied pivots or `(0,0)` fallbacks. The layout runs before the staged review, so the in-canvas preview ghosts the exact final placement. `PutOptions.AutoOffset` is applied when the pivots are baked and `Put` applies them verbatim; `autoOffset` still controls whether the block lands below existing canvas content.

- **Breaking (Provider SDK):** `IAIProvider.PreCall`/`AIProvider.PreCall` are replaced by `Task<AIRequestCall> PreCallAsync(AIRequestCall request, CancellationToken cancellationToken = default)`, awaited by `Call`, the streaming adapters and the batch paths. Out-of-tree providers must override `PreCallAsync` and await `base.PreCallAsync` instead of overriding `PreCall`.
- The WebChat shell now mounts the vendored **chat-ui toolkit** (`vendor/chat-ui.iife.js`, MIT — attribution in `vendor/THIRD_PARTY_NOTICES.md`): the status line, scrollable message list with pin/jump handling, staged-attachment strip and composer are toolkit-native, while a new `js/chat-bridge.js` preserves the full `ExecuteScript` contract (all C#-facing globals, `sh://event`/`clipboard://copy` schemes, keyed upsert/patch pipeline, autonomy overlay, consent/question/task-plan/canvas-pointer/suggestion cards). Legacy `.message` HTML renders through a custom `html` part renderer without extra bubble chrome; `js/chat-script.js` is retained on disk but no longer injected. Native part migration and the toolkit message queue are deferred to a later PR.
- WebChat rendering migrates from legacy plugin-card DOM paths to native chat-ui parts/cards: plan consent → `card:approval`, task plan → `card:task_plan` (replaces the `#hud` docked panel), `ask_user` question → `card:question`, suggested prompts → `suggestions` part at the end of the transcript, canvas pointer → `evidence` + `meta.actions` row, and short notice-role bubbles (`system`/`summary`/`info`/`warning`/`error`/`debug` — special turns, background/subagent lines) → `evidence` parts with level mapping; message metrics footers now render as `usage` parts. Card/button reports travel through the toolkit `transport.messageAction` and are re-mapped to byte-identical `sh://event` URLs (consent decisions, question choice indices, canvas-pointer ids), and every `.cui-msg` is stamped `data-iid`/`data-key` so the keyed upsert/patch pipeline and `getAllMessageKeys()` keep addressing keyed native messages. The composer gains the toolkit model picker (`features.modelPicker` + new `setModels` global → `sh://event?type=model`, opt-in host route), and attachments keep using `requestAttachments` → `type=attach`. C# `ExecuteScript` signatures, `sh://`/`clipboard://` schemes and the `queue: false` flag are unchanged; the legacy DOM paths remain as the no-toolkit fallback.
- `AIProvider.GetApiKey` is now `protected virtual`, so a provider can supply its request credential from its own secret setting.
- `gh_document_save` now only accepts absolute `.gh`/`.ghx` target paths.
- `gh_document_save` is now marked as a mutating tool (`MutatesCanvas`), so it is hidden from MCP servers unless `Allow Editing` is enabled and is presented to models with the mutating-tool prefix and destructive annotation.
- Provider secrets stored on disk now use AES-256-GCM (`SH04:` envelope `[nonce][tag][ciphertext]`) instead of AES-256-CBC, so tampered or truncated encrypted settings fail decryption instead of being accepted. Where `AesGcm` is unsupported (net7.0 on macOS) secrets keep using the `SH03:` CBC envelope so they remain writable and readable locally. At startup, secrets still stored with the legacy `SH02:`/`SH03:` CBC prefixes are transparently re-encrypted to `SH04:` on GCM-capable platforms; unrecoverable `SH02:` values on macOS are still cleared by the existing key-upgrade warning flow.
- MCP servers gain an `Allow External Tools` (`ET`) input (`McpServerOptions.AllowExternalTools`, default `false`) that gates tools annotated `openWorldHint`. The annotation now honestly marks every tool that reaches outside the Grasshopper document — local file reads/writes (`file2md`, `rhino_read_3dm`, `rhino_get_geometry_3dm`, `gh_document_open`, `gh_document_save`), arbitrary URL fetches (`web2md`, Discourse tools), and AI provider calls (`gh_generate`, `script_generate`, `script_edit`, `script_review`, `img2text`, `providers_models`) — so an unauthenticated loopback client can no longer read arbitrary files, fetch arbitrary URLs, or spend provider credits unless the serving component opts in. Canvas-scoped tools (`gh_get`, screenshots, lists) are unaffected, and an explicit `EnabledTools` allow-list still overrides the filter.
- OpenAI-compatible providers now share request tool-choice formatting, JSON-schema wrapping, and token-usage decoding in `OpenAICompatibleProvider<T>` in `SmartHopper.ProviderSdk.AIProviders`, keeping cached and reasoning-token accounting consistent across streaming and non-streaming responses.
- The model catalogue is now the source of truth for the providers it covers: after applying entries verbatim, registry models keyed under a catalogue-declared alias are folded into the canonical model and remaining registry models absent from the document are removed outright — a model that left the API is treated as unknown rather than deprecated-and-callable. Providers the document does not cover (Ollama, LocalAI, …) keep their own tables.
- `SmartHopperInitializer.Reinitialize` (the "Refresh Providers" menu action) re-runs the catalogue refresh with `force: true` after provider re-registration so the verified document keeps authority over entries providers repopulate from seed tables.
- Autonomous assistant runs are now bounded by time and token budgets instead of interaction counts. `SessionOptions.MaxTurns`/`MaxToolPasses` are replaced by `MaxAutonomousTime` (`TimeSpan`, default 10 minutes) and `MaxAutonomousTokens` (default 300 000 provider-reported input+output tokens); a value of `0` or less disables that limit. `ConversationSession` checks the budgets before each provider turn, each tool pass, and each tool execution, closing pending tool calls with an explicit budget-exhausted result. `ConversationSession.GetAutonomyUsage()` exposes a live `AutonomyUsage` snapshot (elapsed/limit seconds, consumed/limit tokens, running/exhausted flags).
- Canvas Assistant settings gain "Max autonomous time (minutes)" and "Max autonomous tokens" steppers (`SmartHopperAssistantSettings.MaxAutonomousTimeMinutes`/`MaxAutonomousTokens`). WebChat shows a floating autonomy overlay while an autonomous run is in progress — elapsed `m:ss / limit` and `tokens / limit` with mini progress bars, ticking client-side between host pushes, highlighting when a limit is reached, and auto-hiding shortly after the run ends (briefly lingering in a red state when a budget was exhausted). When both limits are disabled the overlay shows an "unbounded run" warning instead. The overlay only paints once the run has produced 10 history interactions (`AutonomyUsage.Interactions`, reported as the history-count delta since run start) or 15 s elapsed — unless a budget was exhausted — so short-lived runs never flash metrics.
- `gh_tidy_up` now feeds live Grasshopper component bounds into the layout engine via `LayoutOptions.NodeSizeProvider` (`CanvasAccess.GetNodeSize`) and uses tighter default spacing (`SpacingX` 200→80, `SpacingY` 100→28, `IslandSpacingY` 150→100), producing denser, bounds-aware layouts. `GhJSON.Core`/`GhJSON.Grasshopper` package references are bumped to `1.2.0`.
- `gh_tidy_up` and `gh_put` now share one layout pipeline through `GhJsonGrasshopper.ComputeLayout`: the same dependency-graph pass plus bounds-aware spacing, port alignment, wire-corridor clearance, and collision resolution, so freshly placed and tidied canvases follow identical rules. Wires that skip columns keep a 20 px corridor clear of unrelated component bounds (obstacle rows are nudged and skip-edge targets lifted, matching manual tidy-up corrections), positions round to whole pixels to stop ±1 px oscillation between runs, and connected panels reserve a streamed-data row during placement so path + value entries are not clipped.
- Added a `Canvas.ViewControl` AITool category for canvas-viewport tools; `canvas_view` now uses it. WebChat tool filters (`CanvasButton`, `AIChatComponent`) include the `Canvas.ViewControl` category so the tool is reachable from chat. On MCP, `Canvas.ViewControl` tools are hidden unless the serving component enables `Allow View Control`.
- **Tool taxonomy:** `AITool.Category` is now a hierarchical `Group.Subgroup` path (`Canvas.*`, `GhJSON.*`, `Knowledge.*`, `AgentPlanning`, `Scripting.*`, `AIProviders`, `Data.*`, `Rhino.*`), replacing the flat labels (`Components`, `Vision`, `DataProcessing`, `Instructions`, `Planning`, `Providers`, `NotTested`, …). Experimental tools keep their real subgroup and stay gated by `enabled: false`; internal helpers keep `Hidden`. `DiscourseToolsBase` gains a virtual `ToolCategory` (`Knowledge.Discourse`) overridden by the McNeel (`Knowledge.McNeelForum`) and Ladybug (`Knowledge.LadybugForum`) subclasses. Tool filters now match category paths hierarchically: a group token covers the whole branch (`Canvas` ⇒ all `Canvas.*` tools), a leaf matches one subgroup, `-Group` excludes a branch, and leading `+`/trailing `.*` are tolerated.
- `Filter` gains `ShouldIncludePath` for dot-hierarchy matching (used by `AIProvider.GetFormattedTools` for `ToolFilter`); flat `ShouldInclude` is unchanged for non-hierarchical keys (context/provider filters).
- Renamed the `SmartHopper MCP Server` component inputs: `Expose Mutating Tools` (`M`) is now `Allow Editing` (`AE`).
- Canvas mutation reviews now run through centralized consent lifecycle and cancellation handling. Direct `GhPut` and patch-to-canvas components propagate component invocation context, and compound generation/script tools propagate consent into their nested `gh_put` calls.
- Chat-only control tools are excluded from MCP discovery/calls and provider batch execution through explicit surface checks rather than descriptive tags.
- WebChat requests now prepend mandatory shared Grasshopper operating knowledge before component or user instructions. MCP resources and the in-process `smarthopper_readme` and `smarthopper_workflows` tools consume the same embedded source of truth.
- Renamed provider-related AI tools for a consistent surface: `get_available_providers` → `providers_list`, `get_available_models` → `providers_models`, and `set_ai_provider_and_model` → `gh_component_set_provider` (the tool is component-scoped, so it uses the `gh_` prefix). `providers_list` now lists all registered providers (not only enabled ones) and reports per-provider `enabled`, `configured`, and `isDefault` flags plus a top-level `defaultProvider`. `AIModelsComponent` and the MCP/provider documentation were updated to the new names. No aliases are kept — callers must use the new names.
- `FileContextProvider` no longer reports selected object metadata. Use the new `SelectionContextProvider` for `selected-count`, `selected-objects`, `selected-topology`, and `selected-runtime-values`.
- Removed redundant `new` modifiers on `provider` fields in first-party `*ProviderSettings` classes; the base `AIProviderSettings` does not expose a conflicting accessible member, so the modifier only produced compiler warnings.
- Releases now come from a single `main` branch with on-demand stabilization lines: a release is prepared on demand, tagged when its pull request merges, and published as a draft release. Version tags are the source of truth for released versions.
- Hotfixes now branch from the release tag they patch and are backported automatically to active release lines.
- `hotfix-2-release.yml` now deletes the `hotfix/<version>-<slug>` branch once the release-prep and backport branches carry its commits; on a backport conflict the branch is kept for manual resolution.
- Maintenance workflows that open automatic pull requests now target `main`.
- Development releases are supported as dated prereleases for testers, and promotion checks cover every version on a stabilization line.
- Same-day development releases use a sequence suffix so `main` always advances beyond an existing tag.
- Stabilization promotion no longer blocks on open PRs targeting the release branch or recently closed issues.
- Release notes generation now uses per-release-type prompt templates under `.github/prompts/`, with distinct stable, pre-release, patch, and development release formats.
- Consolidated OpenAI-compatible JSON schema wrapping into `SmartHopper.ProviderSdk.AICall.JsonSchemas.OpenAICompatibleJsonSchemaAdapter`. `OpenAI`, `MistralAI`, `Ollama`, and `LocalAI` now register the shared adapter directly; `DeepSeek` inherits it and only overrides `Unwrap`.
- Consolidated OpenAI-compatible role mapping into `SmartHopper.ProviderSdk.AICall.Core.Interactions.OpenAICompatibleRoleMapper`. `OpenAI`, `MistralAI`, `LocalAI`, `Ollama`, `OpenRouter`, and `DeepSeek` now use the shared mapper, removing the duplicated per-provider `switch (interaction.Agent)` blocks.
- Added `AIProvider.GetApiKey()` and `AIProvider.LoadIconFromResources(...)` helpers in `SmartHopper.ProviderSdk.AIProviders`. All built-in providers now use these shared helpers, removing the duplicated per-provider `GetApiKey()` methods and icon-loading code.
- Added `AIProviderSettings.ValidateMaxTokens(...)` and `AIProviderSettings.ValidateTemperature(...)` helpers. All built-in provider settings classes now delegate `MaxTokens` and `Temperature` validation to the shared helpers, using stricter parsing that fails on unparseable values.
- Converted `AIInteractionBase`, `IAIInteraction`, concrete interaction types, `AIBody`, `AIReturn`, and `AIMetrics` to immutable record contracts with `init`-only properties and copy-returning `With...` helpers.
- `AIMetrics.Combine(...)` replaced by `AIMetrics.WithCombined(...)`, returning a new metrics record instead of mutating in place.
- Streaming and session code now uses mutable local builders and `with` expressions instead of mutating interactions or metrics after construction.
- `SmartHopper.Infrastructure` project file now conditionally emits `InternalsVisibleTo` public keys only when `SignAssembly` is not disabled, allowing unsigned `dotnet build -p:SignAssembly=false` runs to compile test projects.
- Autonomy token accounting now uses a per-run usage ledger in `ConversationSession` instead of summing metrics over persisted history. Each completed provider call is metered exactly once at the two call choke points (`ExecProviderAsync`, `ProcessStreamingDeltasAsync`), counted in effective tokens (`AIMetrics.EffectiveTotalTokens`: provider-reported, or estimated when unreported), and greeting/context-summarization special turns no longer count toward the main autonomy budget.
- `AIToolManager.ExecuteTool` now normalizes a `null` `AIInteractionToolCall.Arguments` value to an empty `JObject` at execution time when the tool schema has no required parameters, replacing the interaction in the immutable `AIBody` so downstream tool delegates receive the normalized value.
- `ToolJsonSchemaValidator` now uses `AITool.GetRequiredParameters()` to determine required parameters instead of parsing the schema inline.
- Bumped all `actions/github-script` references from `v7` to `v9` to run on Node 24 and remove Node 20 deprecation warnings.
- Updated all GitHub Actions and reusable action references to Node 24-compatible versions, replacing deprecated Node 20 actions.
- ProviderTrustPolicy unknown-provider diagnostic now only suggests switching to `Hard` or `Strict` integrity mode when the current mode is `Soft`, avoiding misleading guidance when the provider is already blocked.
- `pr-notes.yml` now updates the PR title automatically and posts description suggestions as PR comments (first suggestion in code blocks, later updates inside a `<details>` block with a reason), instead of editing the PR description directly.
- `pr-validation.yml` now runs on `pull_request` again instead of being dispatched by `pr-notes.yml`, since title validation is handled by `pr-notes.yml`.
- Centralized the macOS/WinForms `net48` reference-assembly workaround in `Directory.Build.props` and removed the duplicated workaround blocks from all affected `.csproj` files. `SmartHopper.Components` now conditions its `System.Drawing.Common` reference on the Windows TFM, and `SmartHopper.Core.Grasshopper` now uses a simple `ProjectReference` without explicit GUID metadata.
- `AIProvider.CallApi`, `AIProvider.CreateBatchHttpClient`, and `AIProviderStreamingAdapter.CreateHttpClient` now use `IProviderHttpClientFactory` instead of creating `new HttpClient()` directly. The host-side factory caches one `HttpMessageHandler` per provider while returning a fresh `HttpClient` on each call, applies the provider `User-Agent`, and honors per-request timeouts. Gemini streaming and batch paths were also updated to use the shared factory.
- `tools/Update-ProviderModels.ps1` now distinguishes dedicated speech from general chat audio when mapping OpenRouter metadata: provider-aware inference assigns `SpeechInput`/`SpeechOutput` (OpenAI whisper/transcribe/tts identifiers, MistralAI `voxtral`, Gemini audio input), preserves hand-curated speech flags on refresh, and reports inferred flags for review in generated PRs. Validation now also rejects `Default` markers whose model lacks the composite's constituent capabilities, and `Audio2Text`/`Text2Audio` are validated as independent composites.
- `tools/Update-ProviderModels.ps1` now honors maintainer-declared aliases: a provider-API id already listed in a model's `Aliases` (e.g. `claude-opus-5-5` under canonical `claude-opus-5.5`) refreshes that entry and keeps the declared canonical name instead of being re-added as a new model or re-canonicalized to the API id, and counts as a live listing in the deprecation check.
- `check-provider-models.yml` now triggers on `SmartHopper.ProviderSdk/AIModels/AICapability.cs` (the enum moved out of `SmartHopper.Infrastructure`) and validates Gemini model declarations on PRs.
- `tools/Validate-Documentation.ps1` no longer requires a minimum number of code examples in the `Developer Reference` section, and now honors a `<!-- docs-validation: ignore -->` opt-out marker for non-template docs such as design briefs. `pr-documentation-validation.yml` scopes validation to docs changed in the pull request and falls back to the full `docs/` directory when the validation script itself changes.
- Canvas change review ghosts now draw with real component bounds: at review time, proposed components without a live canvas instance are instantiated off-document (`GhJsonGrasshopper.Deserialize`) and measured onto the `CanvasChangeReviewSession`, while existing objects reuse their live bounds translated to the proposed pivot. Ghost rectangles are also anchored on the pivot as the bounds center instead of its top-left, matching Grasshopper pivot semantics; the fixed 140×52 estimate remains only as a fallback.
- The canvas change review dialog is now a floating non-modal topmost window instead of a modal dialog, so the Grasshopper canvas stays fully interactive for panning and zooming while reviewing staged changes. A "Zoom to changes" button frames the union of all staged change bounds (auto-framing on open when they are off-screen), and double-clicking a change row frames that single change.
- Renamed the `plan_tasks`/`plan_propose` AITool category from `Control` to `Planning` (shared with the new `ask_user`), and added `Planning` to the WebChat tool filters in `CanvasButton` and `AIChatComponent`, fixing plan tools being unreachable from chat.
- The WebChat task plan no longer renders as an inline message card; it is shown in the persistent HUD panel below the autonomy overlay instead.
- Settings dialog tabs are now ordered General → Canvas Assistant → AI Providers (the former Providers tab is renamed AI Providers).
- Renamed the `guidFilter` parameter to `instanceGuids` in `gh_get`, `gh_get_by_guid`, and `gh_get_by_guid_with_data` to match the `instanceGuids` convention used by `gh_remove`, `button_click`, and the `gh_put` result payload, and to disambiguate document-object instance GUIDs from component-type (`componentGuid`) GUIDs. The old `guidFilter` key is still accepted as a silent alias on `gh_get` (where the parameter is optional); `gh_get_by_guid*` callers must use `instanceGuids` since it is a required parameter there.
- `includeRuntimeMessages` on `gh_get` is now implicit: any `attrFilters` include token for a runtime-message level (`error`, `warning`, `remark`, or a synonym) automatically enables runtime messages, so `gh_get({ attrFilters: ['+error'] })` returns a complete errors report without extra flags.
- The four remaining `gh_get*` tools (`gh_get`, `gh_get_selected`, `gh_get_by_guid`, `gh_get_errors`) now share one parameter surface; each wrapper only injects a predefined filter that merges with user-supplied filters instead of replacing them. User filter tokens that directly contradict an injected filter (e.g. `-selected` under `gh_get_selected`) are dropped with a warning so the wrapper's guarantee holds.
- `docs/UI/icon-set-brief.md` was re-audited against the current component set (14 missing + 8 shared/mis-assigned icons, all unchanged) and extended with a small-format design review (§11: no multi-character text, 2 px strokes, badge-dominance rules, stock-GH style decision) and a production-workflow comparison (§12: code-generated icons via `System.Drawing`, the GH2 alpha plugin's `G2IconSetup`/`G2IconSymbol`/`G2ExportIcons` workflow, official/recovered vector sources, AI generators).
- `gh_put` results now include a `summary` line ("applied X of Y changes"), rendered as the completed-state sentence on the merged tool card.
- Reorganized the component ribbon into a fully letter-prefixed panel layout (Grasshopper sorts panels alphabetically, so the prefixes control the order): `A. AI` now also hosts the SmartHopper MCP Server components; `B. Input` and `C. Output` are unchanged; the former `Grasshopper` panel splits into `D. Canvas` (canvas operations) and `E. GhJSON Files` (open/save GhJSON and GhPatch); `Knowledge` splits into `F. Forums` (Discourse/Ladybug/McNeel browsing and AI summarizers) and `G. Documents` (file/web to markdown); `Img` and `Data Visualization` merge into `H. Media` (image/audio ops, viewers, canvas and viewport captures); `JSON` becomes `I. JSON`; `Text` and `List` merge into `J. Text & Lists`; `Script` becomes `K. Script`; and `Utils` is renamed `L. Metrics`. Placement is subcategory-only — component GUIDs and saved definitions are unaffected.
- Component visibility groups (`GH_Exposure` shelves) now follow one convention inside every panel — core action first, then variants/lists, structured/advanced forms, external sources and utilities: `A. AI` orders chat → connectivity (MCP) → settings → catalog/context; `B. Input` orders scalar adapters → list adapters → media/data sources → forum adapters (replacing the old septenary shelf); `C. Output` orders scalar → list → structured → media; `D. Canvas` groups read (Get/Retrieve/Report) → write (Put/Connect/PatchApplyToCanvas) → document diffs → maintenance (TidyUp/Validate); `E. GhJSON Files` splits open vs save; `F. Forums` gives each provider its own five-component shelf (Discourse → McNeel → Ladybug) with `DiscoursePostDeconstruct` alone on a fourth; `G. Documents` splits direct vs AI; `H. Media` orders viewers → AI ops → captures; `I. JSON` puts schemas and constructors up front, then accessors, then merge/sanitize; `J. Text & Lists` splits text transforms from JSON/list ops. `AIInputAdapterBase`/`AIOutputAdapterBase` gain an optional `subcategory` ctor parameter so adapter test components can live under a `Testing Adapters` subcategory instead of leaking into `B. Input`/`C. Output` — `OutputAdapterTreeGatheringTestComponent` moved there.
- Rewrote and standardized component, input, and output descriptions across all 94 Grasshopper components and shared component base classes (`StatefulComponentBase`, `AIStatefulAsyncComponentBase`, `AIInputAdapterBase`, `AIOutputAdapterBase`, `AIInputPayloadParameter`, `VersatileImageParameter`, `VersatileAudioParameter`), plus `README.md` and the in-repo component/user docs under `docs/`: replaced internal `AIInputPayload` / `JSON scalar` jargon and shouted `REQUIRED`/`OPTIONAL` prefixes with clear end-user tooltips, and fixed inaccurate parameter descriptions on `DiscoursePostOpen`, `McNeelForumPostOpen`, `LadybugForumPostOpen`, `AI2NumberList`, `AI2IntegerList`, `AI2BooleanList`, `GhPut`, `GhGet`, and `GhJSON2AI`.
- Updated `README.md` with a stable-release notice linking to `smarthopper.xyz`, a lead section centered on user control, and a three-part workflow overview covering native parametric components, the customizable agentic canvas assistant, and the built-in MCP server. The workflow sections and component gallery now show real SmartHopper 2 canvas captures instead of v1 screenshots.
- Rebalanced the component ribbon into 12 panels with dropdown visibility (`GH_Exposure.obscure`) for specialized components: `D. Canvas` and `E. GhJSON Files` merge into `D. GhJSON`; `M. Metrics` merges into `A. AI` as a quarternary block; `F. Forums`→`E. Forums`, `G. Documents`→`F. Documents`, `H. Media`→`G. Media`, `I. JSON`→`H. JSON`, `J. Text & Lists`→`I. Text & Lists`, `K. Script`→`J. Script`. Settings and AI Models are now `primary` (with Extra Settings, Context Providers and File Metadata in the `secondary` block); Get/Place GhJSON pair in `primary` alongside Apply GhPatch to Canvas; McNeel Forum components lead `E. Forums` (then Ladybug, then generic Discourse); JSON value builders, accessors and schema builders get their own blocks. Component GUIDs and saved definitions are unaffected.
- ~50 specialized components now carry `GH_Exposure.obscure`, keeping the unexpanded ribbon compact while staying one click away in their panel dropdown and fully searchable on canvas. `AIInputPayloadParameter` is now `GH_Exposure.hidden` (was a `primary` parameter leaking a null icon into `A. AI`), matching `VersatileImageParameter`/`VersatileAudioParameter`.
- Output adapter nicknames standardized from `AI→*` to `AI2*` (e.g. `AI→Text` → `AI2Text`), matching the `Text2AI` input convention and making nicknames typeable in the component search box. `AIContext` is now `AI Context`; `McNeelForum`/`LadybugForum` display names gain a space (`McNeel Forum`, `Ladybug Forum`).
- `File to AI`/`Web to AI` no longer expose a converter `Image Mode` choice (`skip`/`link`/`embed`/`caption`/`describe`): the payload builder now always preserves source evidence — original image references and assets, never base64 inside Markdown — and produces one ordered multimodal payload locally with no provider call, leaving modality compatibility to the central request preparation at the consuming output component.
- `AI File To Markdown`/`AI Web To Markdown` no longer offer per-mode image handling (`embed`/`caption`/`describe`): a single explicit visual-enrichment behavior runs the configured vision helper through the shared `VisualEvidenceEnrichmentService` and appends generated observations under `## Visual Evidence (AI-Generated)` headings (`### Page {n}, Asset {id}`), preserving source Markdown, original images and provenance. Explicit enrichment is synchronous and cannot be queued as a provider batch — the components fail explicitly when `Run` is combined with batch submission.
- Batch submission now prepares every queued request before upload: each item runs request policies and approved-source materialization, then the same central `PrepareExecutionAsync` outbound preparation (capability validation plus permitted modality conversion) used by `Exec`, streaming and session paths; helper costs are recorded per item even when another item aborts the submission, and blocking preparation errors fail the whole submission explicitly instead of uploading malformed payloads.
- `AIRequestCall.Exec` (and the streaming/special-turn paths) submits the prepared execution copy rather than the canonical request: preparation replaces only the outbound view — canonical request and session evidence keep their original media — and a strict final `ValidateOutboundRequest` gate runs regardless of the host's own validation, failing closed on a null preparation.
- WebChat no longer disables the attach button for models lacking `ImageInput`: capability compatibility is decided at submission by the central modality-fallback preparation, which converts model-bound images through the configured vision helper when permitted.
- WebChat DEBUG builds no longer suppress the WebView2 context menu: the host defines `window.__SH_DEBUG__` ahead of `chat-bridge.js`, so right-click → Inspect stays reachable for troubleshooting while release builds keep blocking the menu to prevent a reload wiping the rendered chat.
- `show_ghjson` is now classified under `GhJSON.Generation`, keeping generation and artifact presentation together. Surgical edits, scripting and agent orchestration are discovery-first in large catalogues; the chat prompt and `search_tools` description explain that path. Discovery controls schema presentation, not execution authorization.
- The SmartHopper menu now groups provider maintenance separately from catalogue and diagnostics: `Refresh Providers` and `Verify Providers Hash` share a section, `Refresh Model Catalogue` sits alone below them, and `Diagnostics` is its own section ahead of `Visit Website`/`About`.
- WebChat tool cards are now human-readable instead of raw JSON dumps: the collapsed card peeks a one-line digest (`name(params)` on calls, `name(params) → done|failed` on results), structured or numerous arguments expand into a `<details>` parameter table, failures surface as an error callout, and the result renders as colorized pretty JSON in an open details block with a "Copy raw" button (compact wire JSON via `data-copy` → `clipboard://copy`). Calls without arguments emit no body — the header sentence suffices. Copying a whole tool message keeps returning readable `args → result` text through the new `GetCopyTextForRender` channel on `AIInteractionToolCall`/`AIInteractionToolResult`.
- **Breaking (Provider SDK):** request `Endpoint` is now wire-only — `AIRequestBase` gains `Origin`, the semantic provenance label (tool/component/fallback/chat labels like `gh_put`, `fallback:stt`, `canvas-chat`) that callers set instead of overloading `Endpoint`; `Endpoint` is reserved for the provider-resolved wire path (or an explicit path on backoffice requests such as `/models`). All `Initialize` overloads rename `endpoint:` to `origin:`; `AIRequestCall.IsValid` no longer requires `Endpoint` at the logical layer (the provider resolves it during preparation and `AIProvider.Call` still guards the resolved route); `SpecialTurnConfig.OverrideEndpoint` is now `OverrideOrigin`; modality-fallback labels move from `Endpoint` to `Origin`.
- **Breaking (Provider SDK):** provider preparation no longer mutates the caller's request. `IAIProvider.PreCallAsync` is now a non-overridable-shaped pipeline: the public entry on `AIProvider` creates a detached `CreateExecutionCopy()` and delegates to the new `protected virtual PreCallCoreAsync(outbound)` extension point, which providers override to resolve wire fields (`Endpoint`, `HttpMethod`, `Authentication`, `Headers`) on the copy. `Call`, `EncodeBatchItemAsync` and the streaming adapters consume the returned copy directly. Execution copies stamp `Source` lineage to the canonical root request so per-logical-request state (e.g. idempotency keys) survives both preparation copies and retries.
- Modality fallbacks and special turns label requests through `Origin` instead of `Endpoint`, so the semantic label reaches the wire copy intact (e.g. a request-origin attribution header) instead of being destroyed by provider endpoint resolution.
- **Breaking (components):** every stateful component now shares uniform `Run?` edge semantics — a false→true edge on `Completed`/`Waiting`/`Cancelled`/`Error` transitions to `Processing` (unchanged units replay through unit-result reuse, so a re-trigger does not re-charge), a held-true re-solve with identical inputs stays idle, `Run=false` disarms to `NeedsRun`, and explicit *Cancel* owns aborting in-flight work. Restoration initializes the previous-`Run?` baseline so a saved `Run=true` document cannot mint a phantom trigger on reopen, and the mid-batch input diff excludes `Run?` so a momentary button releasing is not mistaken for changed work.
- Stateful completion now commits a **run-scoped input snapshot** instead of the latest pending hashes: `ComponentStateManager.CaptureRunSnapshot` copies the pending input hashes/branch counts when `Processing` starts and `CommitRunSnapshot` acknowledges only the inputs that run actually consumed — edits landing during async work stay pending and retrigger exactly once after completion. Batch submission already worked this way; the contract is now shared by ordinary async runs. The run's effective `AIRequestParameters` also freeze at run start, so mid-run Settings edits cannot shift a multi-unit run partway through.
- Selecting components (`SelectingStatefulComponentBase`, `AISelectingStatefulAsyncComponentBase`) treat captured-selection changes as a synthetic `Selection` input (`WellKnownInputs.Selection`): `SelectingSupport` fingerprints the selection as a deterministic sorted set of object instance GUIDs, `InputsChanged()` surfaces pending selection edits alongside real parameter diffs, and the fingerprint captures/commits per run like `AIProvider` — the first capture establishes a baseline (no phantom trigger) and a re-select after completion retriggers processing.
- **CI:** the Mistral model used by AI-assisted workflows (PR notes, changelog review, release notes) is now configurable through the `MISTRAL_MODEL` repository variable, defaulting to `mistral-large-4-0` (was `mistral-medium-latest`); the `pr-notes` diff budget is sized to its 1M-token context window.

### Removed

- The `SmartHopper MCP Server` component no longer carries the reserved `LAN Access Removed` (`AX`) input, and the dead `McpServerOptions.AllowExternalConnections` legacy option is gone with its fail-closed startup guard — nothing can request a non-loopback bind. `Allow External Tools` moves up one slot (index 6): documents saved with the retired `AX` input now feed `Allow External Tools`, and wiring into the old index 7 drops on load.
- **Breaking (Provider SDK):** `IProviderLogger`/`DebugProviderLogger` (`SmartHopper.ProviderSdk.Hosting`) are removed and `ProviderSdk.Logger` is now `SmartHopper.ProviderSdk.Logging.ILogger` (default `DebugLogger`). `ProviderLogLevel` moved to `SmartHopper.ProviderSdk.Logging`. Provider hosts should register an `ILogger` implementation — `SmartHopperLogger` routes to the live and file sinks.
- Removed the unused `SmartHopper.Infrastructure.Settings.TrustedProviderRecord` class. `SmartHopperSettings.TrustedProviders` remains the authoritative `Dictionary<string, bool>` trust list; no migration is required.
- Removed the `dev` branch and its dev↔main synchronization workflows.
- Removed the patch-propagation documentation and other documentation references to retired CI automation and the former `dev` branch.
- Removed the redundant `SmartHopper.Infrastructure.AIModels.ModelManager` singleton. All model capabilities, defaults, selection, and streaming validation now flow through `SmartHopper.ProviderSdk.AIModels.AIModelCapabilityRegistry.Instance`, making it the single source of truth for model selection.
- Removed the duplicated per-provider JSON schema adapters: `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `OllamaJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, and the fallback `DefaultJsonSchemaAdapter`.
- Removed the duplicated `AIFileContextComponent` Grasshopper component. Its free-form file context was redundant with `AIFileMetadataComponent`'s `description`; use `AIFileMetadataComponent` for title, description, version, author, and tags.
- Removed the `pr-linear-history.yml` workflow. Pull requests are no longer required to be rebased onto their base branch or free of merge commits; history-shape enforcement, if desired, remains a repository ruleset setting.
- Removed the specialized `gh_get_*` AI tool variants `gh_get_selected_with_data`, `gh_get_by_guid_with_data`, `gh_get_errors_with_data`, `gh_get_start`, `gh_get_start_with_data`, `gh_get_end`, `gh_get_end_with_data`, `gh_get_locked`, `gh_get_preview_off`, `gh_get_preview_on`, and `gh_get_visible`. Their behavior is covered by parameters on the remaining tools: `includeRuntimeData: true` together with `includeInternalizedData: true` for the `*_with_data` variants (those tools enabled both), `typeFilter: ['+startnodes']`/`['+endnodes']` for start/end, `attrFilters: ['+disabled']` for locked, `attrFilters: ['+previewoff']`/`['+previewon']` for preview state, and `viewportOnly: true` for visible.
- Removed the post-release development bump from the `Release 2 - Tag on Release Preparation Merge` workflow: the job no longer creates `chore/bump-*` branches/PRs or enables auto-merge, since it relied on repository auto-merge that is unavailable under the merge queue. The release-prep PR stays the only validated place version files change; the next development version comes from the `version-bump` workflow.
- Removed the catalogue provider-name mapping for a provider the plugin does not ship; unmapped wire provider ids were already ignored when applying catalogue entries.
- **Breaking (components/tools):** the OpenAI-specific `Quality` and `Style` inputs are gone from `AI Text To Image` and `AI to Image`, and `quality`/`style` args from `text2img` — image quality moves to the `quality` provider extra and style is dropped entirely (dall-e is being deprecated). `AIInteractionImage.ImageQuality`/`ImageStyle`, the matching `WithRequest`/`AddImageRequest` parameters, and their `imageQuality`/`imageStyle` payload-codec keys are removed; request bodies keep `ImageSize` plus the new `AspectRatio`. Documents or scripts feeding the removed inputs shift remaining inputs up one slot.
- Removed the Gemini image extras `person_generation`, `add_watermark` and `guidance_scale`: live certification showed `generateContent` rejects all three with HTTP 400 `Unknown name ... at 'generation_config.image_config'` on both Gemini 2.5 and 3.x image models — they are Imagen `predict`-API concepts, not `generateContent` fields.
- Removed dead provider extras that the APIs silently discard: `num_ctx` and `top_k` on Ollama (the OpenAI-compatible endpoint ignores `options.num_ctx`/`top_k` — only the native `/api` surface honors them), and `cfg_scale`/`width`/`height` on LocalAI image generation (not `OpenAIRequest` fields; dimensions already ride the shared `size` parsing). The LocalAI `steps` extra was renamed `step` to match the real diffusion field.
- Removed the converter `Image Mode` options (`skip`, `link`, `embed`, `caption`, `describe`) and the `imageMode` tool argument: the file/web converters and `File to AI`/`Web to AI`/`AI File To Markdown`/`AI Web To Markdown` components no longer branch on image-formatting modes — evidence preservation is unconditional and the single shared enrichment path covers AI visual description. The retired input parameter is removed outright: on `File to AI`/`AI File To Markdown` a saved definition that still wires the old slot feeds it into `Pages`, which rejects non-JSON values with an explicit selection error; on `Web to AI`/`AI Web To Markdown` the trailing wire drops on load.
- **Breaking (components):** `RunOnlyOnInputChanges` is removed — the per-component flag is gone and all stateful components share the uniform `Run?` edge contract described under Changed.
- MCP JSON-RPC dispatch no longer serializes every method behind one lock. Control-plane methods (`initialize`, `ping`, notifications, `tools/list`) answer immediately; long-running tool/resource/prompt execution is admitted into a bounded lane of 12 slots and excess work is rejected promptly with a `server_busy` error instead of queueing behind running calls — keeping the control plane responsive under tool saturation within the 16-request transport cap.
- Canvas-commit serialization moved out of the MCP dispatcher and into the shared mutation coordinator: `GrasshopperMutationUndoCoordinator` now serializes commits per bound document identity (resolved on the UI thread before the gate is taken), so MCP, chat, and direct tool mutations on the same document never overlap while mutations on different documents proceed concurrently. `AIMutatingTool` carries a `MutationBoundary` classification (`Legacy`/`PreparedCommit`/`Composite`) describing how each tool owns its commit.

### Fixed

- `AI to Image` output adapter no longer throws `NullReferenceException` when the generated image arrives as base64 `ImageData` without an `ImageUrl` (the shape Gemini decoders produce). Media result extraction is now centralized behind content contracts instead of per-consumer field inspection: `AIInteractionImage.HasContent`/`AIInteractionAudio.HasContent` mark consumable payloads, `GetLastAssistantImage`/`GetLastAssistantAudio` skip contentless media interactions, `VersatileImage.FromInteraction` resolves URL-or-inline-data into a consumer-facing image (URL preferred, inline data as a data URI with declared MIME), and `ToolResultImage.IsModelVisible` replaces the duplicated `SendToModel && ImageData` predicate across modality checks, codecs and fallback passes. `AICapability.RequiredInputCapabilities` now counts only content-carrying media interactions.
- Gemini tool calling no longer fails with HTTP 400 on canonical JSON Schema shapes (e.g. `type: ["integer","string"]`): declarations are emitted under Gemini's full-JSON-Schema `parametersJsonSchema` field instead of the restricted `parameters` proto, keeping shared tool schemas unmodified. Gemini `thoughtSignature` values are preserved as opaque provider state on `IAIInteraction.ProviderData` — captured at decode and re-emitted on the exact model part for multi-turn tool continuations, never surfaced as reasoning text or component output.
- Modality fallback now separates candidate selection from execution: `FallbackStep` carries an ordered candidate list and `FallbackChain` retries a failed conversion once on the next candidate (bounded to two attempts per step), discarding failed-attempt output while preserving its metrics and demoting its errors to warnings. Input-attributable failures (`InputMissing`/`InputInvalid`/`BodyInvalid`) are never retried. A used fallback emits one surfaceable `ModalityFallbackApplied` remark naming the provider/model that ran, plus informational logging.
- Automatic model selection (`GetDefaultModel`/`SelectBestModel`) now excludes deprecated models outright instead of merely ordering them last, so stale catalogue tombstones can no longer win auto-selection. Explicit provider/model pins remain authoritative single-target resolutions.
- `AI Text To Image` keeps the canonical `VersatileImage` representation (URL/data-URI/base64) instead of materializing a GDI+ bitmap bound to a disposed stream — persisted outputs now round-trip as usable image content. `VersatileImageCodec` decodes saved bitmaps through the shared clone path (`VersatileImage.DecodeBitmap`), and `UnitResultStore` drops a whole unit when any of its items fails serialization rather than persisting a silently-truncated cache entry (recorded as a `w` warnings array).
- The DeepSeek provider seed table and model catalogue tombstone the withdrawn `deepseek-v4.1-flash` / `deepseek-v4.1-flash-1017` model ids as deprecated — they are retained for stale-cache compatibility but excluded from selection, matching the live API surface (`deepseek-flash`, `deepseek-v4-pro`).
- Routine restore and initialization work no longer logs at error severity: restored persistent outputs, selection-state restore (including the expected no-active-document deferral), canvas-button initialization, persistence version checks, batch-cancellation notices and rollback skips now log at trace/informational levels so real errors stand out.
- Provider hash-manifest fetches no longer retry once per loaded provider when the manifest is missing (e.g. unpublished development builds): failures are negatively cached for five minutes and concurrent fetches for the same version share a single in-flight request.
- OpenRouter live model lists no longer offer `:batch` catalog variants as selectable model names. Per the OpenRouter Batch API, `:batch` entries are pricing/capability descriptors resolved by the batch endpoint from the base model slug — submitting `model:batch` is rejected with `model_not_found`. The seeded registry already folds `:batch` variants into the base entry's `Aliases` + `SupportsBatch`/`BatchPricing`; the live `/models` list now matches that contract. (`:free` variants stay — they are valid request model ids.)
- Metrics `role` attribution is now emitted on the tree output instead of being dropped: the label is stamped once before an entry is recorded to the metrics list and tree, so `"main"`/`"fallback"`/`"passthrough:{name}"` labels survive to `Deconstruct Metrics`. Direct tool calls from components (`CallAIToolAsync`) are now attributed `tool:{name}` instead of `"main"` — matching the documented `tool:img2text` convention — and the role follows batch items through queueing, decoding, and save/reopen via the persisted `BatchSubmittedRoles` map.
- Usage metrics now carry the model the provider actually ran instead of the request model: `AIReturn` keeps a decoder-reported `Metrics.Model` (e.g. OpenRouter `response.model`) when present instead of always restamping it with the request's model.
- `gh_batch_import_results` AI tool (mutating + `openWorldHint`, category `Canvas.ComponentControl`): imports locally saved provider batch-result files (*.jsonl/*.json) into a stateful AI component by instance GUID — the same pipeline as the component's *Load results from file* menu entry (`ImportBatchResultContents`). Results match by `custom_id`: a matching import finalizes the run immediately; otherwise a collect-only recollection run rebuilds the sentinel mapping. Returns mode/provider/loaded/matched/expected statistics instead of result bodies, goes through the standard per-component mutation review, and caps each file at 64 MiB.
- Changing Settings Extras (e.g. `reasoning_effort`) on a wired AI Settings component now reliably triggers change detection and marks affected AI components `NeedsRun` — input hashes now fingerprint `AIRequestParameters` by canonical content (sorted keys, typed values) instead of the opaque `Extras(...)` display token.
- Processing-plan grouping no longer crashes on trees containing null items: `DataTreeProcessor`'s branch-content keys go through the null-safe central fingerprint instead of calling `item.ToString()` unconditionally.
- `providers_models` no longer mislabels providers without a live models API: the API call is gated on the new `SupportsApiModels` contract, and `source` is now `api` or `registry` (was `static` with a misleading "Provider API models unavailable" warning even when the registered list was the designed source). The warning survives only for a supported API call that genuinely fails or returns empty.
- Reused processing units no longer drop their metrics: provenance is retained per unit and replayed with `reused: true`, so partial recalculations show the full output's metrics (with original `completed_at`) instead of only the calls that ran this cycle.
- Anthropic reasoning models now actually think: request encoding emits the `thinking` field the API requires — `{"type":"adaptive","display":"summarized"}` for the adaptive family (all 5.x/fable/mythos lines and every 4.6+ model) and `{"type":"enabled","budget_tokens"}` mapped from the configured effort for the extended-thinking family (4.5-era and 3.7, with `temperature` forced to 1 and `top_p`/`top_k` dropped as the API requires). Previously only `output_config.effort` was sent, so 4.6–4.8 models never thought at all and 5.x models thought invisibly (`display` defaults to `omitted`: billed thinking tokens, empty thinking blocks). Thinking blocks decode onto `AIInteractionText.Reasoning`, their signatures are preserved verbatim in `ProviderData` and re-emitted ahead of text on assistant-history replay (required for multi-turn/tool-loop continuation), streamed `thinking_delta`/`signature_delta`/`redacted_thinking` events are handled, and `usage.output_tokens_details.thinking_tokens` maps to `internal_reasoning` metrics without double-counting output tokens.
- Strict fingerprinting no longer trusts arbitrary `ToString()` overrides as content identity — two distinct objects printing the same description could previously share a unit key and return each other's results. Unknown values now resolve to opaque identity in strict mode (conservative recompute), and Rhino geometry is fingerprinted by serialized content instead.
- `Share identical results = false` now also disables planner-level `GroupIdenticalBranches` — identical branches were collapsed before the result store saw them, silently re-sharing despite the setting.
- `AI List to Boolean` batch completion no longer runs `ProcessBatchResults` twice (duplicate metrics, double decode, overwritten provenance) and its synchronous path finalizes `Result` + `Used Fallback` in one atomic `FinishResults` call; batch extra outputs now land at the sentinel's own branch instead of `{0}`.
- `ExtractTypedTree` no longer silently skips nulls and type mismatches — parallel output channels stay position-aligned via null placeholders (with a logged warning), so a malformed item can no longer shift every subsequent item's provenance.
- Mixed reused/new batch results no longer lose the reused units' outputs or secondary channels: completion rebuilds every output channel from stored fragments at their recorded placements instead of walking only sentinel strings (reused booleans were previously stringified into sentinels and dropped).
- Modal dialogs can no longer bury themselves on Rhino's hidden desktop: `DialogForeground.ShowModalInFront` now falls back to an unowned, taskbar-visible, topmost window whenever the Rhino process is not the foreground app, so consent and review prompts triggered by background tool calls (MCP sessions, component workers) become reachable instead of silently stalling the caller. Owned-modal behavior is unchanged while Rhino is in the foreground.
- OpenRouter `image_config.image_size` now snaps `WxH` sizes to the tier vocabulary the API accepts (`0.5K`/`1K`/`2K`/`4K` by longest-side pixels) — raw `1024x1024` was rejected with HTTP 400.
- OpenRouter `reasoning.max_tokens` is now emitted only when the `reasoning_effort` extra is absent: the API rejects requests carrying both (`Only one of "reasoning.effort" and "reasoning.max_tokens" can be specified`).
- `speech_generate` now forwards the full per-request `AIRequestParameters` (temperature, max tokens, timeout, seed, batch tier, extras) instead of rebuilding an extras-only parameter set that silently dropped typed overrides.
- Ollama and LocalAI now honor typed `AIRequestParameters.TopP`/`Seed` (wired by the AI Parameters component) with the same typed-then-extras precedence as DeepSeek — previously only `Extras` keys were read, so typed overrides were ignored.
- Changing the provider integrity check mode now invalidates cached `ProviderTrustPolicy` verdicts, so the next evaluation observes the new mode instead of reusing stale per-provider verdicts.
- Batch lifecycle ownership hardening: `SubmitBatchQueueAsync` itself now claims the pending-submission marker for direct worker submissions (`TrySubmitBatchAsync`), so `BatchInFlight` covers the entire prepare+submit window on both submission paths — no phantom worker can spawn mid-submit. Polling no longer starts inside submission: every path calls `CompleteSubmissionSetup()` only after the sentinel trees are installed, and a staged manual import finalizes through the same boundary instead of racing ahead of the reconstruction map.
- Restored batches now install every piece of reconstruction state (submission clock, decode context, pending fragments, customId map, sentinel trees) before the resumed poll timer is scheduled — a save/reopened batch can no longer complete against partially restored state. The persisted submission timestamp is the authoritative restored clock, so successive save/reopen cycles no longer lose the real elapsed time or re-extend the 24 h polling deadline.
- Stale batch callbacks can no longer mutate a newer run: poll progress/timeout/terminal handling and remote-cancel cleanup now verify they still own the installed submission before touching shared state (timer teardown, submission clear, finalization), and installing a new submission bumps `FinalizeEpoch` so finalizations queued by the previous run are discarded instead of finalizing the new run with old results.
- Manual batch-result imports now take over completion ownership from any in-flight local poll (detached timer + invalidated callbacks, remote job left untouched), decode with the submitted provider instead of the live selection, and pass the known expected sentinel ids to the completion accounting — a partially matching import correctly counts its missing items for NeedsRun instead of silently completing, and the collect-only fallback now reports the collected ids as its expectation as well.
- Importing batch results onto a completed component no longer resubmits paid provider work: the direct path now requires every matched custom id to be freshly resolvable (`UnitResultStore.CanResolveSentinel` → GH-free `SentinelResolution` — live id map, pending fragment, not already resolved), so a sentinel id surviving from a finished run is treated as stale bookkeeping and routed to the collect-only fallback instead of classifying unresolved → `NeedsRun` → resubmission. The collect-only recollection also bypasses the committed result cache (`BypassCommittedResultReuse`), so a fully-cached component can no longer leave the batch queue empty and silently drop the staged import while reporting success.
- `text2img` no longer passes a provider-specific `/images/generations` endpoint hint; providers route by capability as with every other tool (`endpoint: <tool name>`).
- `AITool` parses its parameters schema once per instance instead of on every `GetDeclaredParameters`/`GetRequiredParameters` call (the unrecognized-argument check runs per tool execution).
- Gemini `thinking_level` now maps to `thinkingLevel` on the whole Gemini 3+ model family instead of only `gemini-3*` names, and the duplicated model-family regex helpers were consolidated.
- Gemini cached-input tokens are now billed at the cache rate: `usageMetadata.cachedContentTokenCount` (populated by implicit and explicit context caching) decodes into the `cache_read` quantity and is subtracted from `text_input` so the same tokens are not billed twice.
- Gemini batch submission, polling, and result decoding now follow the actual `batchGenerateContent` REST contract — the create payload wraps `displayName`/`inputConfig`/`priority` in a top-level `batch` field (top-level fields were rejected `HTTP 400 Unknown name`), inlined request correlation uses `metadata.key` (echoed back on results) instead of `custom_id`, lifecycle state is read from `metadata.state` (`BATCH_STATE_*` values map to Failed/Cancelled/Expired), and inline results parse from `response.inlinedResponses.inlinedResponses[]` with `metadata.key` back-mapping and per-item error siblings. Batch result download fetches the operation through the batch HTTP pipeline directly instead of `provider.Call`, which rewrote the status GET into a `generateContent` POST that could never succeed.
- Gemini responses now propagate the first candidate's `finishReason` into decoded interaction metrics, so aggregated call metrics carry the terminal reason instead of relying on the "defaulted to 'stop'" fallback.
- Unanswered external-source consent prompts no longer stall the caller until the generic tool timeout: `ExternalSourcePrompt` now applies its own deadline and resolves to a distinct `NoResponse` decision — treated as a refusal for that invocation and reported as "approval was not granted" instead of a misleading execution-timeout error.
- Denying external-source access now surfaces as an explicit component runtime error instead of a green run: `UnauthorizedAccessException` propagates through the converter and tool layers (`UrlConverter`, `FileConverterRegistry`, `ImageProcessingService`, `web2md`, `file2md`, Discourse tools) to the tool manager's error channel — the component enters Error state with the refusal message rather than completing with the denial buried in output text.
- Tool execution errors now reach component runtime messages: `RuntimeMessageUtility.ExtractMessages(toolResult)` bound the `JObject` overload via the implicit conversion, silently dropping `ToolCallResult.Messages` — the envelope carrying `CreateToolError` diagnostics. `Web2Md`, `AIWeb2Md`, `Web2AI`, `AIScriptReview`, and `AIScriptGenerate` now use the `ToolCallResult`-aware extension so error-severity tool diagnostics transition the component to Error state.
- Caller-initiated cancellation is no longer swallowed as an HTTP timeout: `AIProvider.CallApi` rethrows `OperationCanceledException` when the caller's token fired and only builds the `HTTP Request Timeout` error return for genuine request timeouts, so callers can distinguish aborted calls from slow servers.
- Provider trust evaluations are now cached per session: `ProviderTrustPolicy.Evaluate` no longer re-runs signature/hash trust checks on every component badge refresh, and the cache is invalidated on provider refresh and after all-provider hash verification — eliminating the repeated trust warnings that flooded the trace log while results stay fresh whenever the provider set changes.
- OpenAI Responses API replies no longer render the request's text-format config as the assistant message: the response decoder's audio-transcription sniff (`text` present, no `choices`) also matched Responses API payloads, which echo the request `text` object (`{"format":{"type":"text"},"verbosity":…}`) at top level, so `ProcessAudioTranscriptionResponseData` returned that JSON instead of the generated answer. `output` is now decoded before the `text` sniff, and transcription `text` must be a JSON string.
- Web chat no longer opens in the system browser instead of the Eto dialog: `WebChatNavigationPolicy` now recognizes the loopback `HttpListener` document (`http://localhost|127.0.0.1|[::1]:<port>`) that Eto's Windows `LoadHtml(html, baseUri)` actually navigates to — the `https://smarthopper.local` base URI only supplies the virtual directory, so the trusted-document allowlist never matched the real document URI. `WebView_DocumentLoading` allows a trusted-document URI only while a document load is pending (`!_webViewInitialized`), preserving browser routing for every later external link including loopback ones; `about:blank`/`file:` loads on macOS are unblocked by the same branch.
- Canvas mutation review no longer hangs or leaves a stale window after a timed-out or cancelled tool call: the floating change-review dialog is now created on the Eto dispatcher (instead of relying solely on `RhinoApp.InvokeOnUiThread`, which can run the action on a thread that never pumps the window), its cancellation path always closes it — with a `RhinoApp` fallback when no Eto application is attached — and the review task still completes if closing fails. Post-decision session updates (`SetAccepted`/`SetAllAccepted`) can no longer throw `Eto.Forms.UIThreadAccess` from background threads: the dialog's `SelectionChanged` handler marshals its summary refresh onto the Eto thread, and the preview overlay tolerates canvas-refresh failures from any thread. `gh_tidy_up` also skips review for move sessions that produced no reviewable items, matching `gh_move`.
- Anthropic requests no longer fail with HTTP 400 "This model does not support the effort parameter.": the provider now drops `output_config.effort` for models older than the 4.6 generation (e.g. `claude-haiku-4-5`, `claude-sonnet-4-5`, `claude-opus-4-5`, and the 4.x/3.x lines) instead of forwarding the configured `ReasoningEffort` — a warning is logged when the parameter is dropped. The existing `xhigh`/`max` downgrades still apply on the models that support effort.
- `AI List Filter` (`list_filter`), `AI Smart Connect` (`gh_smart_connect`), and `AI Canvas Report` (`gh_report`) now emit the `Metrics` output on successful runs: their workers appended provider metrics to the internal metrics tree but never invoked `SetMetricsOutput`, so `M` stayed empty even though the AI call produced usage, finish reason, and completion time.
- `viewport_screenshot` (and the `Viewport To Image` component) no longer fails when the Rhino window is minimized or the view has no on-screen pixels to grab: the screen-capture path now falls back to `DisplayPipeline.DrawToBitmap`, which renders the viewport to an off-screen bitmap.
- `gh_list_components` no longer instantiates every component proxy on metadata-only listings: `CreateInstance` now runs only when `inputs`/`outputs` details are requested, skipping hundreds of unnecessary object constructions (including user-object deserialization) per call — faster responses and fewer first-chance exceptions from proxies whose embedded objects cannot load.
- Canvas measurement of freshly deserialized objects now runs `Attributes.PerformLayout()` before reading bounds: Grasshopper components keep stock (0,0,150,20) attribute geometry until a layout pass, which previously produced near-invisible wires, ~5px spacing, and misaligned ports on placed documents. Real bounds also feed the center-vs-corner pivot conversion, so panels and other corner-pivoted parameters land correctly.
- Batch submission is now owned by the component lifecycle, not by each worker remembering a handoff call: `AIStatefulAsyncComponentBase` captures every `RunProcessingAsync` output tree and, when a run ends with queued-but-unsubmitted batch work, derives the sentinel trees and submits itself. `AI List Filter` could previously mark itself `Done` while publishing raw `##SH_BATCH:` sentinels because it collected deferred requests but never submitted them — the defect class is now unreachable: a populated queue with no sentinel-bearing output transitions to `Error`, and `FinishResults` strips any leftover sentinels so placeholders can never reach published output.
- Batch completion now proves output resolution instead of counting provider bodies: decode failures are recorded as per-item errors (`batch_item_error`) instead of silently re-invoking the failing decoder, an `OnBatchCompleted` crash forces `Error` instead of a logged-and-forgotten `Completed`, and undecodable items count as unresolved so affected units re-execute instead of producing phantom empty output.
- In-flight batch context is now durable across save/reopen: components persist a `BatchDecodeContext` (list-filter source items, Boolean fallback value) and the original `BatchSubmittedAt` clock is restored so a resumed poll keeps its real elapsed-time deadline and `completion_time` basis instead of restarting from the reopen moment.
- Batch result decoding now uses the provider that submitted the batch, not the provider currently selected in Settings: switching provider mid-flight previously decoded `text2text`, `text2json`, `img2text`, `list_filter`, `text2boolean` and `list2boolean` results through the wrong codec.
- A batch submission in flight toward the provider is now invisible no more to the lifecycle: `BatchRunState` marks `PendingSubmission` synchronously at the worker-completion handoff and every guard (worker spawn, per-run reset, mid-batch prompt, cancel, removal, cache clear) keys off a single `BatchInFlight` predicate. Previously the ~1-second window between queue consumption and `Submission` assignment was unguarded — the processing watchdog re-expired the solution, spawned a phantom worker that replayed pending fragments as "reused" units, recorded a duplicate placement, and transitioned the component to `Completed` while the provider job did not even exist yet (live-observed as duplicated `AI List Filter` output: 2× items plus a phantom reused metrics twin).
- Late batch callbacks can no longer revive invalidated runs: `BatchRunState` carries a submit `Epoch` (bumped on every run reset or invalidation) and a `FinalizeEpoch` (bumped only by destructive events — cache clear, cancel, component removal). A provider submit returning after its run moved on is remotely cancelled and dropped instead of installing a zombie `Submission`; a poll completion marshalled to the UI thread re-checks `FinalizeEpoch` before mutating outputs, so clearing results or cancelling mid-marshal can no longer resurrect the old run's outputs.
- Batch completion now requires per-item resolution, not delivered response bodies: `UnitResultStore` tracks which submitted customIds actually resolved inside their fragments, and `CompleteBatchAndTransition` counts expected ids that resolved (successful), failed provider-side (terminal — never auto-resubmitted, so a definitive provider error is not re-charged), or stayed retryable (missing result, local decode failure → `NeedsRun` re-execution). A batch whose items all failed now ends in `Error`; partial failures complete with per-item warnings.
- Shared batch results are decoded once and placed at every sentinel seat: a customId appearing at multiple output positions previously re-ran provider decode, metrics recording and fragment resolution per occurrence, double-appending decoded items inside the unit fragment. Both the generic pipeline and the output-adapter variant now decode unique customIds once; `ResolveSentinel` is idempotent, and per-id metrics record once.
- `SetPersistentOutput` is the last-resort sentinel publication boundary: any path that bypasses `FinishResults`' own strip (e.g. a worker forwarding its raw processing tree) publishes a sentinel-free copy preserving branch layout — `##SH_BATCH:` placeholders are transport metadata inside the pending fragment and can never reach component outputs, while the stored fragment keeps its placeholders for batch-completion reconstruction.
- Batch result parsing no longer crashes on non-object `error` fields: `AIBatchResultParser` gains a shared scalar/null-safe extraction used by the shared parser and the OpenAI, MistralAI, Anthropic and Gemini item parsers — an `"error": null` or scalar error payload previously threw `InvalidOperationException` inside `AddResultItem`, which the poll loop then re-parsed and re-logged every ~20 seconds on an already-completed OpenRouter batch (live-observed).
- Submitted per-item model identity now survives save/reopen: `BatchSubmittedModels` joins the persisted submitted-run snapshot (provider, roles, decode context, submission clock), so a resumed batch stamps decoded metrics with the model that actually ran even when the live model selection changed mid-flight.
- Data-tree matching now ignores inputs that carry no data: an unwired optional input or a tree of empty branches neither contributes paths to the processing obligation nor can broadcast, and produces no mismatch remarks — previously its `{0}` empty branch could suppress matching work and emit false unmatched-branch warnings.
- `Combine Metrics` dedup now zeroes execution counters for replayed seats too: a shared execution placed at several outputs still counts usage/cost once, and now also counts `data_count`/`iterations_count` once instead of summing them per seat.
- Identical-branch grouping now fingerprints branches in strict mode: the display-oriented fingerprint could group branches whose strict unit identities differ, silently sharing a result the grouped units never produced — grouping now agrees with unit-key identity.
- `SentinelTransformOutputs` can now replace the primary output: a transform returning the primary parameter name previously had its primary-channel output ignored (only extra channels landed), which made transforms unable to reshape a unit's main result.
- `AI List Filter` no longer logs raw list JSON, criteria text, or full tool parameters, and routine skip/progress lines no longer use error severity.
- `UnitResultStore` is now serialized behind a private gate: a batch-finalization poll on one thread could previously enumerate `_runPlacements`/`_runMetrics` while a worker's reuse replay appended on another, crashing `RunMetricsProjection.Build` with `Collection was modified` and failing the component during save/reopen finalization (live-observed on a restored OpenRouter batch). Snapshot-returning readers (`RunPlacements`, `TryGetMetrics`, `TryGetFragment`) let callers enumerate without holding the lock while `ResolveSentinel` rewrites fragments.
- Persisted batch sentinels orphaned by a save mid-flight no longer leave a component `Completed` with empty output: on document restore, `SetPersistentOutput` first resolves each placeholder against the committed unit snapshot (republishing last-known-good results), and when no resumable batch exists and a sentinel still cannot resolve, the component is forced to `NeedsRun` so the unfinished work re-executes — previously the placeholder was stripped at publication and the component sat complete while publishing zero items.
- Cancelling a component during the batch poll phase now actually tears the batch down: the task-cancel continuation only runs when worker tasks exist, so a menu cancel mid-poll previously left the poll timer running — a late completion could resurrect the component and the remote job kept running. `OnEnteringCancelled` now invalidates the run epochs and cancels the remote batch on any `Cancelled` entry, and `CancelRemoteBatchAsync` claims the submission synchronously so duplicate cancel paths cannot double-cancel.
- Remote batch cancel failures are no longer reported as success: every provider's `CancelBatchAsync` used to swallow non-success responses, so an API rejection (e.g. "cannot cancel a batch with status 'completed'") still produced a "cancelled successfully" message. Providers now propagate the failure and the component surfaces the real provider reason.
- A batch completing with retryable-unresolved units no longer reports `Completed` with missing results: the partial-results demotion requested `Processing → NeedsRun`, which the state machine rejected as an invalid transition and the component queued `Completed` anyway (live-observed as a 3-unit batch finishing with 2 results). `NeedsRun` is now a legal destination from `Processing` and the demotion carries the dedicated `TransitionReason.ResultsIncomplete`; the retry re-collects only the missing units — verified live as a follow-up submission of exactly 1 item.
- A save/reopened batch's provider-side clock is restored alongside the component clock: `AIBatchSubmission` accepts an optional `submittedAt` (defaulting to `UtcNow` for fresh submissions) fed from the persisted `BatchSubmittedAt`, so a provider deriving elapsed time, deadlines or completion metrics from the submission handle sees the real submission time instead of the reopen moment — live-observed as `completionTime=731.81s` on a batch persisted mid-flight and restored across two reopens.

- New-component layout measurement now mirrors declared wiring before reading bounds, so connected floating parameters are measured at their rendered size: a wired panel renders taller than an unwired one, and previously its post-connection growth left the input wire visibly misaligned from the output port it was aligned to.

- New-component canvas review no longer replaces explicit GhJSON pivots with an automatic grid; preview, accepted placement, and direct GhJSON placement follow the same pivot-preservation rule.

- Git hooks now scope Git operations to the repository root even when helper scripts change directory, preventing accidental staged source-file deletions. Clean linked worktrees skip post-commit signing-metadata refresh when no local signing key exists.
- SmartHopper dialogs can no longer hide behind other windows while blocking a workflow: a new shared `DialogForeground` helper parents modal dialogs to the Rhino main window (falling back to a taskbar-visible, topmost window when no owner is available so a blocked prompt is always findable) and brings modeless windows to the front and focuses them. Applied to the canvas change-review dialog, styled message dialogs, provider connection/external-source/new-providers/verification/catalogue-refresh prompts, and the About/Settings menu dialogs — matching the AI chat dialog's bring-to-front behavior.
- The computed `EffectiveProviderIntegrityCheckMode` property no longer serializes into `SmartHopper.json` — it is a derived view over a real setting and was missing `[JsonIgnore]`, so the settings file carried a stale copy that could diverge from the configured `ProviderIntegrityCheckMode`.
- The **Refresh Model Catalogue** dialog no longer prints `OFFLINE OK` when the catalogue service was actually reached: `ModelCatalogueService` now records `RemoteStatus` for the last refresh (`Updated` / `UpToDate` / `Unreachable` / `Failed`), and the conclusion line distinguishes a confirmed up-to-date catalogue, offline-on-verified-cache, and a rejected/failed update — previously all three collapse-rendered as `OFFLINE OK — unreachable or had no newer release`, which also hid signature/protocol verification failures behind a green checkmark.
- TTS voice defaults are now provider-scoped: `AI→Speech` and `speech_generate` no longer inject OpenAI's `nova` voice into every request — the Voice input/tool arg defaults to empty so each provider applies its own default (`alloy` on OpenAI, `Kore` on Gemini, server-side default on MistralAI). Previously MistralAI (and any OpenAI-compatible provider validating voices) failed with `invalid_voice` errors.
- `modality_fallback: "any_provider"` now resolves cross-provider conversion chains: `IModalityFallback.IsAvailable` required `SelectBestModel` to yield a concrete model before validating it, because `ValidateCapabilities` returns `true` for unknown models — previously the component's own provider always passed the check with an empty model, so the cross-provider candidate loop was unreachable and `any_provider` behaved exactly like `configured_provider` (hard error whenever the configured provider couldn't convert).
- `gh_disconnect` result entries now report `status: "noop"` when no wire existed between the resolved endpoints, instead of indistinguishable `"disconnected"` successes for never-existing wires.
- Restored the provider tool rename lost in a refactor: `get_available_providers`, `get_available_models` and `set_ai_provider_and_model` are again `providers_list`, `providers_models` and `gh_component_set_provider` as documented; `AIModelsComponent`, the context-editing tool list, embedded MCP provider docs and `docs/Tools/index.md` were updated to match.
- GhJSON generation guidance now keeps agents from emitting GUIDs: the embedded GhJSON spec (`GhJsonSpec/v1.0/specification.md`) no longer shows `componentGuid`/`instanceGuid` in its generation examples and explicitly instructs omitting them for new components (a wrong-but-plausible `componentGuid` silently instantiates a different component than `name` suggests — e.g. a requested "Slider" placed as "Addition"). `gh_generate`/`gh_generate_and_place_on_canvas`/`gh_put` tool descriptions and the generation system prompt carry the same instruction; `gh_put` additionally validates the document after `GhJson.Fix` normalization and fails back to the caller on invalid GhJSON instead of silently dropping data (e.g. connections referencing undeclared component ids) during placement.
- Canvas change review dialog: the "Accept all"/"Reject all" buttons are renamed to "Select all"/"Select none" with tooltips clarifying they only change the selection (nothing applies until "Apply selected"), the summary now reports how many selected changes will not apply because their required changes are unchecked, and component rows warn `Resolves to "<type>"` when a proposed component resolves to a different Grasshopper type than its `name` (e.g. a copied `componentGuid` winning over the name).
- Tool-call accounting in `ConversationSession`: `AIInteractionToolResult` derives from `AIInteractionToolCall`, so every place that matched calls by `OfType<AIInteractionToolCall>`/`is AIInteractionToolCall` also matched results. `DebugLogToolCallIds` and the merge duplicate-warning now exclude results (debug logs no longer report every call+result pair as a false duplicate), the streamed and pre-execution persist checks exclude results, streamed tool results are persisted through their own branch instead of the updatable-call branch, `UpdateToolCallInHistory` can no longer overwrite a result entry, and `AppendToSessionHistory` deduplicates tool calls/results by Id at the single history-append choke point (a repeated call merges arguments via `UpdateToolCallInHistory` instead of double-storing).
- Catalogue fetches are no longer single-shot: network errors, timeouts, and 408/429/5xx responses are retried up to 3 attempts with exponential backoff, so a transient blip at Grasshopper startup no longer leaves model metadata stale for the whole session.
- Provider settings dialogs no longer overflow horizontally: drop-downs sized to their longest item pushed every scaled row — including action buttons — past the dialog edge and forced a horizontal scrollbar. Each setting control's preferred width is now bounded, matching the fixed label/description widths already used by the generic settings page.
- The four provider test components that still registered under the `Test/Providers` ribbon (`Test DeepSeek Batch Call`, `Test DeepSeek Vision`, `Test Gemini Function Calling`, `Test OpenRouter Batch Call`) now use the `SmartHopper Tests` category / `Testing Providers` subcategory like the rest of `SmartHopper.Components.Test`.
- The `gh_report` tool / AI Canvas Report component no longer embeds a serialized component dump inside the report's `Metadata` field — it now emits only `{"schema","metadata":{...}}`, removing the duplication with the in-view GhJSON extract. The metadata is also generated from the full document rather than a single object, so `componentCount` and `dependencies` now reflect the whole canvas instead of just the first object.
- The canvas change review dialog no longer stretches short rows: `DynamicLayout` rows scale vertically in Eto, so wire entries with one or two lines of text were expanded to fill the scrollable list. The list now uses non-scaling `TableLayout` rows at their natural height, and explicit `ScaleHeight` spacer rows absorb the leftover viewport space — Eto auto-scales the last row when no row opts into height scaling, which had kept stretching the final wire row and pushing its details ~200px below the title. Each change row also carries a subtle hover background so the row's actual bounds stay readable against the empty scroll space.
- `gh_connect` no longer prompts for wires that already exist: proposals whose resolved wire is already on the canvas are reported as `status: "noop"` successes instead of being staged for review — previously a `gh_connect` call after `gh_put` created doc connections re-prompted the user for the same wire. `gh_disconnect` gets the mirrored treatment: proposals for wires that do not exist are no-ops reported before the review rather than being staged and detected only at apply time.
- Multi-line suggestion chips in WebChat no longer center their wrapped text: `.cui-suggestion`/`.suggestion-chip` now pin `text-align: left`, so a long suggested prompt reads like the others instead of centering its continuation line.
- Multi-line context notices in WebChat no longer center every line: only the first block (the title shown when collapsed) stays centered, while bullet lists and follow-on paragraphs now render left-aligned.
- Collapsed tool cards in WebChat now show only the muted byline — status glyph, activity/result sentence and disclosure chevron — with the JSON body hidden entirely until expanded (`max-height: 0` keeps `scrollHeight` measurable so the chevron still detects overflow). The per-tool metrics footer is suppressed: tool interactions inherit the turn's provider/model/token data, so every card repeated an identical `provider · model · ctx` usage line — that usage now shows once on the assistant message where it belongs.
- WebChat no longer shows the browser's right-click context menu (e.g. WebView2's "Refresh"), which reloaded the page and emptied the chat UI. Messages are rendered into the DOM by the host and are not part of the document, so a reload could not restore them.
- Anthropic model list: `claude-haiku-5-5`, `claude-sonnet-5-5`, `claude-opus-5-5`, `claude-fable-5-1`, `claude-sonnet-4-6`, and `claude-opus-4-6`/`4-7`/`4-8` now declare their OpenRouter dot-version spellings (`claude-*-X.Y`) and `-latest` forms as aliases, and `claude-opus-5.5` was re-canonicalized to the provider-API id `claude-opus-5-5`. `tools/Update-ProviderModels.ps1` now treats version-separator variants (`X-Y` vs `X.Y`) of the same model as one logical model — the provider-API spelling is always canonical and the other spellings are recorded as aliases instead of being added as duplicate entries.
- MistralAI model list: `tools/Update-ProviderModels.ps1` no longer uses the provider API's human-readable `.name` (e.g. `Voxtral Mini TTS`) as the canonical `Model` id — `.name` now only groups related API ids and is kept as an alias, while the canonical is chosen from the group's slug ids (newest `-YYMM` dated id, then the longest non-rolling id, then `-latest`). The five affected deprecated entries (`voxtral-mini-tts-2603`, `voxtral-mini-transcribe-2602`, `voxtral-small-2507`, `mistral-ocr-2512`, `mistral-ocr-4-1`) are back to slug canonicals, matching what the next generated run will produce.
- `tools/Change-SolutionVersion.ps1` version parsing now accepts the same-day dev sequence format (`X.Y.Z-dev.YYMMDD.N`, produced by the `next-version` dev sequence) — previously release preparation failed with "Failed to parse current version" on versions like `1.3.0-dev.261010.1`
- `Extra Settings` no longer emits provider defaults such as `{"reasoning_effort":"none"}` when the user has not configured any extra. Descriptor defaults are no longer seeded as persistent input data — they remain visible in each generated input's tooltip — so only user-set or wired values are serialized into the extras JSON.
- `Web to AI`, `File to AI` and `Web to Markdown` no longer emit an all-zero Metrics output on runs that never reached an AI provider (plain web retrieval, file conversion, image `skip` mode). Metrics recording is now driven by evidence of a provider call on the returned `AIReturn` — a finish reason, reported or estimated token usage, or a measured completion time — instead of a per-call flag, so any result that never received a provider response emits nothing while inner provider calls (e.g. `img2text` image descriptions) are still metered. Metrics persisted by an earlier AI-enabled run are cleared when a run produces none.
- `gh_put` now reports `acceptedChanges` as the number of staged changes actually applied to the canvas (placed components plus created connections and groups) instead of the number accepted at review. Staged changes the user rejected or that failed to apply are counted under `rejectedChanges` and surfaced in `analysis` — previously a put whose components all failed name resolution still reported them as accepted.
- Provider settings pages no longer overflow the dialog horizontally: the widest bound rows (150px label + 370px control, plus companion buttons) exceeded the viewport once the page's vertical scrollbar appeared, so every row clipped and a horizontal scrollbar appeared. The control/companion width bounds are tightened so the widest row fits with slack, the live status label gets the same preferred-width bound (a wrapping label otherwise reports its full unwrapped text), and chrome rows no longer stretch their last cell to the full row width — action buttons and links now render at natural size instead of a ~390px-wide button.
- The Security tab's section titles ("Network Settings", "External data sources") now get the same spacer rhythm as the other settings pages — 15px above and 5px below the bold heading — instead of sitting on the default 10px row gap, which left them crowded against the controls above.
- `AI List to Boolean` and `AI List Filter` no longer fall back to synchronous execution when `Batch` is enabled: both tools advertised the `Batch` surface but registered no `buildRequest`, so `CallAIToolAsync` could not form a queueable request and silently ran a direct call. Each now supplies a shared `AIRequestCall` builder (same prompt construction as the direct path), and the base component surfaces a `processing in regular mode` remark for any remaining tool that declares batch without a request builder instead of hiding the degradation.
- `gh_put` with an `instanceGuid` matching an existing canvas object now updates that object in place instead of placing a duplicate: matching components are always detected (not only with `editMode`), staged as modifications in the review, and re-placed preserving the instance GUID, the existing position when no `pivot` is supplied, and external wires. `autoOffset` now defaults to off when the document updates existing objects so updates keep their position, and `editMode` remains only as a compatibility hint. A true in-place update (without remove/re-place) is implemented upstream in GhJSON.NET (`PutOptions.UpdateExistingByInstanceGuid`) and takes effect once the GhJSON package reference is bumped.
- `VersatileAudio.ToByteArray()` no longer freezes the Grasshopper UI thread when the audio source is a URL: downloads now run through `ToByteArrayAsync` on a shared `HttpClient` with a 30-second timeout, `AudioViewerComponent` loads URL audio on a background task (redrawing via `RhinoApp.InvokeOnUiThread`), and saving awaits the download instead of blocking. The synchronous `ToByteArray()` remains available and is now deadlock-safe and time-bounded.
- OpenAI-compatible providers no longer double-count reasoning tokens: `OutputTokensGeneration` now excludes `reasoning_tokens`, so `AIMetrics.OutputTokens` and estimated costs match the provider's reported output usage (streaming and non-streaming, including the OpenAI Responses API).
- WebChat's autonomy overlay no longer shows the previous run's elapsed time, token count, and interaction totals during the first seconds of a new run. The run-start usage push reads session autonomy state before `BeginAutonomyRun` resets it, so a stale last-run snapshot could pass the overlay's first-paint threshold and stay visible until the new run became meaningful (~15 s in). The overlay now resets on `setProcessing(true)` and drops any not-running usage snapshot received before the new run's first live snapshot.
- Successful local tool executions no longer report spurious metrics-validation failures (`Finish reason must be set`, `Provider and model fields are required`). `AIToolCall` now sets `SkipMetricsValidation` by default — tool results are local-only and never carry provider completion metadata — covering the session tool-execution path and composite inner calls (`gh_generate` inside `gh_generate_and_place_on_canvas`, `script_generate`, `script_edit`) that previously produced false `success: false` results. `gh_generate` now also propagates the inner provider request's real `AIMetrics` into its tool result so generation token usage is metered.
- `gh_tidy_up` now converts layout positions (canonical bounds centers) into each object's own pivot semantics before applying them, via `PivotSemantics.CenterToPivot`/`BoundsCenter` from `GhJSON.Grasshopper.Shared`. Previously center positions were written straight to `Attributes.Pivot`, which is top-left for sliders, panels and floating parameters — shifting them down-right relative to components, misaligning islands, and producing inconsistent gaps. Islands also stack vertically with a shared left edge in the GhJSON layout engine.
- `CanvasAccess.MoveInstance` now treats sub-pixel (<0.5 px) moves as no-ops and records the undo event only when an instance actually moves; repeated `gh_tidy_up` runs no longer report phantom moves or pollute the undo stack.
- `canvas_view` no longer throws `Cross-thread operation not valid`: viewport access (`zoomExtents`, `frameGuids`, `setZoom`, `setCenter`) is marshaled to the Rhino UI thread through the new `CanvasAccess.RunOnUiThread` helper.
- Batch-capable components no longer resubmit their batch in an endless loop after completion: the input-hash and provider-selection baselines are now committed at submission time, because the state machine stays in `Processing` until poll completion and never reaches `OnWorkerCompleted` — the only path that committed them. Without the commit, every post-completion solve reported phantom input changes and triggered a new submission while `Run` stayed true. Batch completion finalization (`OnBatchCompleted`, output writes) is also marshalled to the UI thread like the collect-only path instead of running on the poll timer.
- Batch outputs now populate on completion for output-adapter components (`AI to Text` and similar): the worker stored the sentinel tree as `GH_Structure<IGH_Goo>` but `GetSentinelTree` cast it to `GH_Structure<GH_String>` — an invariant generic cast that always failed — so reconstruction returned early with empty outputs. Sentinel trees are normalized to `GH_String` items at storage and the lookup tolerates the `IGH_Goo` form.
- `gh_component_toggle_lock` no longer throws cross-thread errors or stops after the first target: a new `ComponentManipulation.SetComponentLocks` applies all accepted GUIDs in one UI-thread pass with a single undo record (`[SH] Lock/Unlock Components`) and a single canvas redraw, and the per-object `SetComponentLock` is UI-marshalled for all callers.
- Debug builds now roll the log file at 1 GB instead of 100 MB so long development sessions (provider storms, Trace-level batch polling) no longer lose diagnostic history to size rollovers; the `LogRetentionDays` time-based cutoff is unchanged.
- `tools/Update-GitHubLabels.ps1` no longer appends a stray blank line before `# --- Component labels ---` in `.github/labeler.yml` on every run: the section regexes now operate on LF-normalized content (CRLF working trees left an orphaned `\r` that grew the gap by one line per commit), and a single trailing newline is restored at EOF. The update is now idempotent.
- Cross-platform `SmartHopper.ProviderSdk.Tests` now include the `System.Drawing.Common` runtime asset required by the standalone test host, so the `net7.0` test assembly is discovered and run instead of being skipped. macOS CI now runs these Provider SDK tests alongside Infrastructure tests; production builds continue using Rhino's host-provided drawing runtime.
- `AICapability.AudioInput`/`AudioOutput` no longer imply `SpeechInput`/`SpeechOutput`. Chat-audio models (e.g. OpenAI `gpt-audio-mini`) were being selected for `Text2Speech`/`Speech2Text` tasks and routed to the dedicated `/audio/speech` and `/audio/transcriptions` endpoints they do not support, producing a misleading `404 Invalid URL (POST /v1/audio/speech)` from the provider. The OpenAI `Text2Speech` default is now `gpt-4o-mini-tts-2025-12-15` and dedicated transcription models (`whisper-1`, `gpt-4o-mini-transcribe-*`, `gpt-4o-transcribe*`, `gpt-transcribe`, `gpt-live-transcribe`) carry explicit `SpeechInput`; MistralAI `voxtral` models and Gemini audio-capable chat models gained explicit `SpeechInput`/`SpeechOutput` where their speech paths work through chat; stale `Default` markers on OpenRouter (`google/lyria-3-pro-preview`, `google/gemini-3.1-flash-lite`) were corrected to `Text2Audio`/`Audio2Text`.
- AI output adapters now gather typed Grasshopper tree inputs without `GetDataTree<T>` type-mismatch breakpoints, align additional controls through `DataTreeProcessor`, and apply branch-specific speech, image, and script options to direct provider requests.
- System-prompt composition no longer writes full prompt and context contents to debug output; diagnostics retain only structural metadata and content lengths.
- **Streaming / turn grouping**: Every streaming delta (and every interaction of a non-streaming provider response) was rendered as a separate assistant message with its own random `TurnId`, and per-turn metrics aggregation was broken. `InteractionUtility.EnsureTurnId` now unconditionally stamps the session's turn identifier onto provider-produced interactions, restoring the contract lost in the immutable `AIBody` refactor (`AIBodyBuilder.Build()` always assigns a random `TurnId`, so an "assign only if missing" policy was a no-op). Special-turn persistence (`PersistResult`, `PersistAll`, `ReplaceAbove`) applies the same rule to greeting and summary results.
- **Tool-call history integrity** (`invalid_request_error: An assistant message with 'tool_calls' must be followed by tool messages…` on DeepSeek and other OpenAI-compatible providers): a run aborted by cancellation, timeout, error, or an exhausted tool-pass/turn budget could leave a tool call in the conversation history without a result, and every subsequent request was then rejected. `ConversationSession` now closes such calls with a synthetic failed tool result (`success: false, cancelled: true`) when the turn ends abnormally and, as a safety net, at the start of every turn. `SessionOptions.ProcessTools = false` now also hides tools from the provider for the run (restoring the original tool filter afterwards), so single-shot runs such as the chat greeting cannot emit tool calls that would never be answered.
- `SmartHopper.sln` no longer maps `SmartHopper.Components.Test` to `Release` build configurations; the test-only project is now built only in Debug, aligning with `.devin/rules/solution-structure.md`.
- Fixed a nullability mismatch in `AIRequestBase.Provider` by updating `IAIRequest.Provider` to `string?`, matching the fact that a provider may be unset until the request is initialized.
- `tools/Sign-Authenticode.ps1` now Authenticode-signs `SmartHopper.Providers.LocalAI.dll` and `SmartHopper.Providers.Ollama.dll`. They were missing from the signing allowlist, so both providers shipped unsigned and Grasshopper refused to enable them as untrusted providers.
- `FileContextProvider` is now the single `file` context provider (previously `current-file`). It merges user-defined file metadata (`title`, `description`, `version`, `author`, `tags`) from `AIFileMetadataComponent` directly into the file context, so context filters such as `file` produce `file_title`, `file_description`, etc. The legacy `metadata-{key}` entries are removed.
- WebChat default context filter now uses `file` instead of `current-file`.
- AI-call error reporting no longer duplicates failures: `AIReturn.CreateError`/`CreateProviderError`/`CreateNetworkError`/`CreateToolError` now mirror the structured message text and origin into the body diagnostic so the `[Provider]`/`[Return]` (or `[Network]`/`[Tool]`) pair for one failure deduplicates into a single surfaced message.
- Failed AI calls no longer report `finish_reason: "stop"` in the Metrics output: `FinishReasonNormalizeResponsePolicy` now skips normalization when the return carries an error diagnostic or surfaceable error message **and** reports no finish reason, and no longer emits a spurious "Finish reason missing; defaulted to 'stop'" warning on failures. A failed call that still reports a finish reason (e.g. `length`) is normalized normally so the "response was cut off by the maximum token limit" error is not lost. Successful calls keep their real provider finish reason and still default to `stop` (with warning) when the provider omits it.
- Persistent component runtime messages are now cleared whenever a new run begins (entry into `NeedsRun` or `Processing`), so stale errors and warnings from a previous solve no longer linger on the component; messages produced by the new run are stored and re-applied as usual.
- Provider integrity (trust) warnings now surface at most once per provider per session instead of once per component/solve: a new process-wide `ProviderTrustWarningDeduplicator` in `SmartHopper.ProviderSdk.AICall.Validation` is consulted by `AIStatefulAsyncComponentBase.SurfaceMessagesFromReturn` and the batch pre-validation warning path. Deduplication keys on provider name plus warning text, so distinct trust conditions for the same provider (e.g. "hash check unavailable" and "community provider") each still surface once; error-severity trust blocks are unaffected.
- Hash-manifest validation now permits only bot-authored additions from release automation and continues to block manual edits.
- Released-version validation now permits ordinary pull requests to inherit the target branch's already-released version while rejecting newly introduced reuse.
- Milestone management now stays on the same pre-release stage for sequence releases (`alpha.1` → `alpha.2`) and only promotes when a new stage is released (`alpha.1` → `beta.1`). A stable release closes every milestone belonging to that core and opens `X.(Y+1).0-alpha`.
- Post-release version bump now stays on the same pre-release stage and increments its sequence for alpha/beta/rc, and bumps to the next minor `dev` version only after a stable release.
- `gh_select` no longer throws `Cross-thread operation not valid`: every canvas, document, and selection-attribute access is now marshaled to the Rhino UI thread through `CanvasAccess.RunOnUiThread`, matching the `canvas_view` fix. The async consent review still runs off the UI thread between the prepare and apply phases.
- `gh_list_components` no longer fails wholesale when a single component proxy cannot be instantiated: `CreateInstance()`/`CreateAttributes()` introspection now runs per proxy inside a guarded block that emits empty input/output lists on failure, and `Desc` access is null-safe in the category, query, and detail-mapping paths.
- Composite action `next-version` now uses the repository-root relative path for the `calculate-version` dependency and validates `bump` and `stage` inputs before processing.
- Fixed documentation validation failures by adding the required sections, metadata, and valid cross-references to `docs/Providers/AICall/JsonSchemaAdapters.md` and `docs/Testing/index.md`.
- Fixed a JavaScript `SyntaxError` in the `pr-milestone.yml` milestone-assignment script caused by redeclaring the `core` object injected by `actions/github-script`.
- Generation requests whose body carries no conversation content — only system, context, summary or diagnostic interactions — are now rejected at `AIRequestCall.IsValid()` with a clear `BodyInvalid` error instead of dispatching to the provider. Every provider routes those agents to its instruction channel (`system` messages, Gemini `system_instruction`, Anthropic top-level `system`), so such bodies produced guaranteed HTTP 400s on Gemini (`contents is not specified`) and Anthropic (`messages` empty), and meaningless continuations elsewhere. The check lives in the shared outbound gate (`AIRequestPreparation.ValidateOutboundRequest`), so `Exec`, streaming and batch submissions all enforce it; `AIBodyExtensions.HasConversationContent()` counts user/assistant/tool turns and non-empty `IAIModelProjectedInteraction`s.
- Batch submissions now encode every item through one shared pipeline: `AIProvider.EncodeBatchItemAsync` runs `PreCallAsync`, the `ValidateOutboundRequest` outbound gate, and `Encode`, returning the prepared request and body. Gemini previously parsed `AIRequestCall.EncodedRequestBody` — empty for requests that never passed validation — so uninitialized `SubmitBatchAsync` items crashed with `JsonReaderException` before reaching the wire; they now fail with the item's validation errors. Anthropic, MistralAI, OpenAI and OpenRouter were deduplicated onto the same helper, so the Exec-path gate is enforced for every batch queue as documented.
- `TestGemini*` components: corrected fixtures and assertions so the suite exercises the real Gemini paths — image interactions now declare `AIAgent.User` (uninitialized agents default to `Context` and were silently dropped from `contents`), the standard-call test asserts a non-empty text response instead of counting the returned `Error` interaction as success, decode asserts on the intended text turn, encode/function-calling assertions match Gemini's `functionCall`/`functionResponse` wire shape (function responses ride inside a `user` turn, not a `function` role), and batch legs `Initialize` provider/model/endpoint like production requests instead of sending an invalid `service_tier:"batch"` extra.
- Fixed `mistral-chat` composite action to write prompts and request payloads to files instead of passing them as command-line arguments, added `user-prompt-file` and `system-prompt-file` inputs, and updated `pr-notes.yml` to pass the generated prompt via a file path, preventing `Argument list too long` failures for large diffs.
- Hardened `pr-notes.yml` context step by writing the diff, commit messages, changed files, and unreleased changelog to files before the Python script reads them, instead of passing them as environment strings. The diff budget is now computed from the `mistral-medium-latest` 256k-token context window, allowing the largest diff that still leaves room for the prompt, output, and overhead.
- `pr-notes.yml` now hashes the diff and skips the main LLM when the diff is unchanged. The diff hash uses `git patch-id --stable`, which ignores whitespace, line numbers and context-only formatting noise. On subsequent iterations it asks a severity LLM whether new changes are substantial enough to update the title/description; if not, no new suggestion is generated.
- `pr-notes.yml` no longer posts fallback suggestions on subsequent runs, so only the first run uses the branch-based fallback.
- The deterministic `## Testing Done` and `## Checklist` body sections have been moved from `pr-notes.yml` to `.github/prompts/pr-notes-static.md` so they can be edited independently.
- Hardened `ComponentStateManager` debounce logic so stale timer callbacks are ignored after `StartDebounce`/`CancelDebounce`, preventing a race where an old or cancelled debounce could still trigger a state transition.
- Fixed `pr-notes.yml` crash when the previous-suggestion lookup fails or returns unparsable output by always writing `previous-title.txt` and `previous-body.txt`, and by making the post-processing `_read_file_or_empty` helper tolerate missing files.
- `AIProvider.CallApi` now disposes the `HttpResponseMessage` after reading the response body, avoiding connection-pool stalls.
- `GeminiProvider` streaming now clamps the per-request HTTP timeout to the same `[MinTimeoutSeconds, MaxTimeoutSeconds]` bounds used by other provider HTTP paths.
- The WebChat autonomy overlay token counter stayed at 0 during agent runs. Streaming providers only carried usage on the final assistant text interaction, so tool-call-only turns — the majority of an agentic run — silently dropped their reported usage, and persisted history never saw it. Streaming adapters (OpenAI, Anthropic, MistralAI, DeepSeek, OpenRouter, Ollama, LocalAI) now attach the call's usage to the last interaction of the final delta via `AIReturn.AttachUsageMetrics`; Anthropic additionally reads `message_start` usage so input and cache tokens are captured, and the session falls back to a heuristic estimate when a call reports no usage. The token budget now enforces against the same effective totals, so autonomous spend can no longer silently exceed `MaxAutonomousTokens`.
- Provider integrity verification no longer falls back to the `latest.json` hash manifest when the version-specific manifest (`hashes/<version>.json`) is missing. A 404 or fetch failure now reports `Unavailable` instead of comparing local provider DLLs against a different version's hashes, which produced false-positive mismatch warnings after each release until the new manifest was published to GitHub Pages.
- Ctrl+Z after `gh_remove`/`gh_clear` no longer throws "Undo failed: Object reference not set to an instance of an object". `CanvasDeleter` recorded removals with generic object-state undo events (`RecordUndoEvent`/`CreateGenericObjectEvent`), which look the object up in the document on undo and null-reference once it is gone. Deletion now records `GH_UndoUtil.RecordRemoveObjectEvent`, which serializes each removed object into the undo record and re-adds it on undo — the same record Grasshopper's own delete uses.
- `McpServerLifecycle` now warns when a second component acquires an already-running port with different options, making the shared-server configuration behavior visible instead of silently ignored.
- "Copy Diagnostics for GitHub" no longer fails outright when another application (e.g. a clipboard manager) holds the Windows clipboard open. `SmartHopperDiagnostics.CopyToClipboard` now retries `Clipboard.SetDataObject` up to 10 times (~2 s) on `CLIPBRD_E_CANT_OPEN`, and the menu shows a dedicated "clipboard is in use, try again or generate a diagnostic zip" message instead of the raw `ExternalException`.
- Provider settings dictionaries are now thread-safe. Background actions call `SetSetting` while the settings dialog's 2 s refresh timer calls `GetProviderSettings`, and both `AIProvider._injectedSettings` and `SmartHopperSettings.ProviderSettings` were plain `Dictionary` instances — concurrent mutation during enumeration/lookup can corrupt bucket chains and hang a thread indefinitely. All reads, writes, removes, snapshots, integrity-check and migration accesses are now serialized under dedicated leaf-level locks.
- The provider settings page's periodic reload now runs through `SafeReloadProviderSettings`, which logs failures via `Logger.For<GenericProviderSettingsPage>()` instead of letting a single throwing tick permanently kill the refresh timer — previously one exception during descriptor regeneration, dropdown sync or status-label update left the page frozen on stale values.
- The MCP component's `Url` output could read `<empty>` while the status line already showed `Running on http://127.0.0.1:<port>/mcp`: `McpServer.Start()` only set `IsRunning` inside the asynchronous accept-loop task, so the synchronous `SolveInstance` right after `Start()` read `false` and output an empty URL. `Start()` now marks the server running once the listener is up, before spawning the accept loop (whose `finally` still clears the flag when the loop exits, so a dead accept loop reports `Stopped`), and the component derives its status text from the live server state — a stopped or failed server can no longer display a stale "Running on …" message.
- Internal-only AI tools (`list_filter`, `speech_generate`, `text2boolean`, `text2img`, `text2json`, `text2text`, `textlist2boolean`) are no longer exposed to MCP clients or WebChat. They now declare `AIToolSurface.Direct | AIToolSurface.Batch` instead of `All`, so `tools/list`/`tools/call` and the chat tool catalog skip them while Grasshopper components keep calling them through the component (`Direct`) and batch (`Batch`) surfaces.
- AI tools invoked through MCP `tools/call` or chat without an explicit provider/model no longer fail with `Provider=''` validation errors. `AIToolManager.ExecuteTool` now resolves a missing or `"Default"` provider through `ProviderManager.GetDefaultAIProvider()` (SmartHopper settings default, falling back to the first registered provider) and a missing model through the provider's capability-aware `SelectModel` for the tool's required capabilities — matching how Grasshopper components resolve the "(Default)" selection.
- MistralAI provider settings now cap `Temperature` at 1.5, matching the provider API that rejects higher values with `Input should be less than or equal to 1.5` (the setting was previously described and validated up to 3.0). The `AI Settings` component's Temperature input no longer claims a universal `0.0–2.0` range since accepted limits vary by provider.
- Per-request `Temperature` overrides can no longer send out-of-range values to the MistralAI API: `MistralAIProvider.Encode` now replaces `NaN` or out-of-range (< 0.0 or > 1.5) values with the provider default 0.5 at encode time, matching Anthropic's existing guard.
- Boolean AI calls now emit a stricter output contract: the `text2boolean` and `textlist2boolean` prompts use the shared `BooleanResultResolver.TrueFalseGuard` sentence ("Return exclusively TRUE or FALSE."), so responses are far more likely to parse through `AIResponseParser.ParseBooleanFromResponse` instead of falling back or returning null.
- Capability validation in `AIOutputAdapterBase.PrepareInputs` now derives required capabilities from both the component's `RequiredCapability` and the merged `AIInputPayload` interaction modalities, so media payloads can no longer bypass the capability gate (e.g. an `Img to AI` payload merged into `AI to Text` on a model without `ImageInput` was previously sent to the API ungated). `AIInteractionImage` requires `ImageInput` and `AIInteractionAudio` requires `AudioInput` on the resolved model — derived via the new `AICapabilityExtensions.RequiredInputCapabilities` and `AIInputPayloadMerger.GetRequiredInputCapabilities` — so a payload-modality gap resolves a modality fallback chain when fallback is enabled, and otherwise fails with a clear `[Capability] ... does not support` error that names the missing input capability.
- Fixed mixed-modality fallback chains running every conversion step on the provider and model resolved for the last step only. `FallbackChain` steps are now `FallbackStep` records carrying their own resolved provider/model, so e.g. a payload with both image and audio can convert each modality on whichever configured provider supports it; the fallback info message now reports each step's provider/model and a chain fails cleanly when a step resolves no model.
- **End-to-end audio pipelines (text-to-speech and speech-to-text)**: dedicated speech endpoints no longer fail or silently drop audio. `AIProvider.CallApi` gained a `BuildRequestContent` hook so providers can send non-JSON payloads, and binary `audio/*` responses (e.g. OpenAI `/audio/speech`) are normalized into an `{"audio_data","mime_type"}` JSON envelope instead of failing JSON parsing. `OpenAICompatibleProvider` converts `/audio/transcriptions` requests into `multipart/form-data` uploads carrying the resolved `AIInteractionAudio` bytes plus the encoded metadata fields (model, language, prompt, …); a `file_url` metadata field acts as the audio source instead of the upload (endpoints accept exactly one source), numeric form fields are serialized culture-invariantly, and a request with no usable audio source fails locally with an actionable error rather than posting a JSON body the endpoint rejects. `OpenAICompatibleAudioCodec` gained `TryResolveAudioBytes`, `DetectAudioMimeType` (magic-byte detection), and `TryDecodeAudioDataEnvelope`. MistralAI now encodes audio input on chat requests (`input_audio` parts), sends proper `/audio/speech` (`input`, `voice_id`, `ref_audio`, `response_format`) and `/audio/transcriptions` (multipart) payloads, and decodes its `audio_data` speech responses and transcription `text` into interactions. Gemini decodes `inlineData`/`inline_data` audio parts into `AIInteractionAudio` — wrapping raw `audio/L16` PCM in a WAV container — instead of emitting a base64 JSON blob as assistant text. Dedicated audio decode paths across OpenAI, MistralAI, and Gemini now attach a `FinishReason` so call metrics validate. Streaming requests mark `WantsStreaming` before provider preparation and reset it when no streaming adapter exists, and the Gemini streaming adapter runs `PreCall` (endpoint/auth/content-type selection) and strips SSE `data:` prefixes before parsing chunks.
- `speech_generate` and the `AI→Speech` component now consume the typed `AIInteractionAudio` returned by TTS providers (via the new `VersatileAudio.FromInteraction`, which converts in-memory bytes to a data URI and file references to local-file sources) instead of parsing the last assistant message's `ToString()` output, which never contained real audio.
- `tools/Update-ProviderModels.ps1` no longer rewrites `*ProviderModels.cs` when the only differences are regenerated `// Released between …` section comments or other comment-only/blank-line churn, which produced automated PRs whose entire diff was comment edits (e.g. #918). Before writing, the script now compares the regenerated content with the on-disk file after stripping comment-only and blank lines; when nothing else changed it leaves the file untouched and reports `fileUpdated: false`, so the scheduled `chore-update-provider-models` workflow sees a clean working tree and skips PR creation. Inline trailing comments (e.g. `// TODO: retrieve capabilities`) stay attached to their code line and still count as real changes.
- Metrics outputs no longer accumulate one entry per run: with `Run` held `true`, every trigger transitioned straight to `Processing` and never entered `NeedsRun`, so the per-run reset in `OnEnteringNeedsRun` never fired and `_metricsTree`/`PersistedMetricsList` grew one item per run. `AIStatefulAsyncComponentBase.OnEnteringProcessing` now re-enters the same reset (via the virtual `OnEnteringNeedsRun` chain, so derived components' per-run caches like `File2AI`/`Web2AI` file/URL contexts reset too) whenever a new run starts without a remote batch in flight — batch continuations re-entering `Processing` keep their state for sentinel reconstruction.
- `gh_document_new`, `gh_document_switch` and `gh_document_open` can no longer silently kill the MCP session that invoked them: activating a different document unloads the document hosting the enabled SmartHopper MCP Server component, which released its hold and stopped the server mid-request — the call's result could never reach the client and every subsequent call failed with a dead connection. On the MCP surface these tools now refuse with an actionable error whenever the document being left hosts the last enabled server component; pass `allowDisconnect: true` to proceed anyway. Chat/Direct surfaces and activations that keep the current document are unaffected, and the refusal never applies while another open document also hosts an enabled server component (the shared server survives through the other holder). `JsonRpcDispatcher.Dispose` also no longer disposes the request gate while a dispatch is in flight, which turned the in-flight `finally` release into an `ObjectDisposedException` whenever the server stopped mid-request.
- Editing inputs or switching provider while a remote batch is in flight now takes effect deliberately instead of silently corrupting the run: the batch was previously acknowledged against whatever the pending values happened to be once submission returned, so mid-flight edits were silently treated as processed and stale results could persist. Each run now snapshots its input hashes, branch counts and provider selection when entering `Processing`, and batch submission/finalization commits that snapshot instead of the mutable pending values. When a solve observes a diff against the snapshot while the batch polls, the user is asked whether to cancel the in-flight batch and re-request it with the new inputs, or keep the batch and omit the changes (the omitted edits are then acknowledged so they neither re-prompt nor retrigger a run).
- A provider or model that fails the batch-capability check now stays rejected for the whole run: `IsBatchRequest` previously only cached that the check had run, so the first call correctly returned `false` but every subsequent call skipped validation and returned `true`, letting batch sentinels be queued for a provider that would later reject them. The check now caches the outcome (`BatchSupported`) alongside the `UnsupportedChecked` flag.
- UI-thread mutations queued by canvas tools can no longer apply after the caller already reported a timeout: `InvokeOnUiThreadAndWait` now claims the queued action with an ownership flag, so when the 30 s wait elapses before the UI thread starts the action, the callback becomes a no-op and the lock/preview tool's failure is final; an action that already started keeps ownership and the caller waits for it rather than abandoning a half-applied canvas mutation.
- Batch metrics outputs now carry the provider and model that actually executed the batch: the batch decode path bypassed the `AIReturn.CreateSuccess` stamping the synchronous path applies, so emitted metrics reported `ai_provider`/`ai_model` as `null`, `iterations_count` as `0`, and left `context_usage_percent`/`estimated_cost` unresolvable. The submitted provider and per-item model are now captured with the batch submission (and recovered from `AIBatchSubmission.ProviderName` for batches restored from file), result bodies are decoded by the provider that ran the batch instead of the live selection — which could have changed mid-flight — and decoded metrics are stamped with provider, model and a default `iterations_count` of 1 per provider call, matching `AIBody.Metrics`' convention.
- MistralAI `AI→Speech` no longer fails with `HTTP 400: Either ref_audio or voice must be provided` when no voice is configured: MistralAI's `/audio/speech` endpoint requires one of `voice_id`/`ref_audio` and has no server-side default, so the provider now supplies its native `gb_jane_neutral` Voxtral voice when the request resolves neither — the same provider-local default pattern `OpenAIProvider` uses for `alloy`. Explicit `voice`/`voice_id` extras and `ref_audio` still take precedence over the default.
- `provider`/`model` arguments on AI tool calls now actually reach the executing request: `AIToolCall.FromToolCallInteraction` only accepted them as explicit parameters, which MCP call sites never passed, so overrides such as `img2text(provider: "OpenAI")` silently ran on the default provider. The arguments now fill the call context when the caller did not specify them explicitly, applying uniformly across the MCP executor, MCP adapter, chat session, and component tool surfaces.
- `script_generate` (and `script_generate_and_place_on_canvas`) no longer mask provider failures behind the bare `AI script generation failed` text: MCP callers surface only the first error message, so the underlying provider/runtime cause is now folded into that text (`AI script generation failed: <cause>`) while the full message list is still appended for component surfaces.
- Modality fallbacks no longer select unconfigured providers: `AudioToTextFallback` and `ImageToTextFallback` only checked the capability registry, so `any_provider` could pick a provider with catalogued models but no credentials (e.g. Gemini in a fresh install), which then failed instantly. Both now require `IAIProvider.IsConfigured` before a provider counts as a fallback candidate — applying uniformly to pinned providers, the component's own provider, and `any_provider` alternates. The shared fallback pipeline (availability check, per-item conversion call, metrics collection, body reconstruction) is consolidated in `ModalityFallbackBase<TModality>` so future fallbacks inherit the contract instead of re-deriving it.
- Pure-modality inputs now run as strict passthrough conversions instead of being answered by a general assistant: `AI to Text` with an all-`AIInteractionAudio` branch (e.g. `Audio to AI` alone) returns the verbatim transcript per item — previously the fallback's `[Audio transcript: …]` text was re-sent to a "helpful assistant" model that reformatted it (e.g. into a markdown heading) instead of emitting it; `AI to Speech` with an all-user-text branch synthesizes each text directly — previously `AI to Speech` on a non-TTS provider failed capability validation outright. The conversion resolves its provider/model through `ModalityFallbackResolver.ResolveStep`, honoring pins, the `modality_fallback` mode, the component's own model when already capable, and unconfigured-provider gating — and runs via the shared fallback pipeline, so voice/speed extras and per-call metrics carry over. Any mixed payload (text, prompt, or a System instruction alongside) keeps normal assistant behavior. The passthrough contract lives on `AIOutputAdapterBase` (`GetPassthroughConversion`/`ConvertPassthroughItem`) with raw per-item results surfaced through the new `ModalityFallbackResult.ConvertedItems`; `TextToSpeechFallback` is the new TTS conversion.
- `Audio Viewer` no longer floods its component face with raw audio payloads (a `DataUri`/`Base64` source printed the entire encoded blob as its info label — the label now shows the source kind, file/URL name, or byte size instead), and playback is a proper on-canvas control: the `Play?` input is removed in favor of a native Grasshopper capsule **Play** button on the component body (same style as the `Select` button on selecting components), while `Save?` stays a boolean input with stateful semantics — held `True` re-saves on every audio input change and a momentary `True` saves once. The face button now actually plays the audio, which the previous custom-drawn buttons never did.
- Provider tiles on the General settings tab are only created for trusted providers: configuring an untrusted provider could never work and only produced failures later, so its entry point is removed entirely, with an explanatory note under the grid pointing to the Security tab.
- Context usage no longer inflates toward the limit after turns whose last interaction carries no provider metrics: `AIMetrics.WithCombined` reset `LastEffectiveTotalTokens` to zero whenever the merged interaction contributed no usage, so `ContextUsagePercent` fell back to the cumulative token sum (each request's resent input counted again) and conversation summarization fired far below real occupancy. The combined metrics now keep the previous last-call footprint in that case.
- The SmartHopper canvas button is clickable at every zoom level and across documents: its hit-test bounds were stored in a single static field stamped on every paint, so any hooked canvas repainting (a hidden document, a zoom change reflowing `ClientSize`) stomped the entry the visible canvas's mouse handlers tested against — the icon drew correctly while clicks missed. Bounds are now stored per canvas and each mouse handler tests the bounds of the canvas that raised the event.
- WebChat tool cards now tick to their finished glyph when the result arrives: the in-place message patch only swapped `.message-content` and the metrics footer, but the `.cui-tool-title[data-state]` span lives in `.message-header` (`.message-sender`), so the same-key result upsert updated the card body while the header kept `pending` forever. `patchExistingMessage` now syncs the header too; the jsdom smoke test covers the pending → done transition.
- Anthropic requests no longer fail with HTTP 400 "`temperature` is deprecated for this model." on the effort-capable family (5.x plus `opus-4-6`/`4-7`/`4-8` and `sonnet-4-6`): the provider sent the configured `Temperature` unconditionally. The whole sampling set (`temperature`, `top_p`, `top_k`) is now emitted only for models that accept it, using the same family detection that already gated `output_config.effort` — caller-supplied `top_p`/`top_k` extras are dropped for the family too instead of re-triggering the 400.
- OpenAI requests to the reasoning family (o-series and `gpt-5*`) no longer fail with HTTP 400 when sampling parameters arrive through request extras: the main path already omitted `temperature` for those models, but extras could re-inject `temperature`, `top_p`, `presence_penalty`, `frequency_penalty`, `logprobs` and `top_logprobs` on both the Chat Completions and Responses API bodies. All sampling extras are now gated by the same o-series/`gpt-5` family predicate used by the main path.
- Anthropic structured-output requests (JSON schema responses such as chat suggestions) no longer fail with HTTP 400 "`output_config.format.schema`: property 'maxItems' is not supported": Anthropic's constrained decoding accepts only a subset of JSON Schema and rejected `minimum`/`maximum`, `minLength`/`maxLength`/`pattern`, `maxItems`/`uniqueItems`/`minProperties`/`maxProperties`, `multipleOf`, `not` and non-allowlisted `format` values outright. A new `AnthropicJsonSchemaAdapter` (registered like DeepSeek's) normalizes the schema in `Wrap`: it keeps `minItems` 0/1 and the supported string formats, strips the rest, and preserves each stripped constraint as a `Constraints: …` hint appended to the node's `description`, mirroring the official Anthropic SDK. The provider's `additionalProperties: false` injection moved into the same normalization pass.
- Provider settings pages no longer keep polling settings after the settings dialog is closed: `SettingsDialog` eagerly constructs a `GenericProviderSettingsPage` for every provider, and each page started its 2-second `UITimer` in the constructor but only stopped it on `UnLoad` — pages that were never navigated to are never loaded in Eto, so their timers leaked and kept sweeping every provider's descriptors for the lifetime of the process (hundreds of thousands of trace lines, plus a full settings decrypt per tick). The timer now starts on `Load` and stops on `UnLoad`, so only the visible page polls and polling stops with the dialog.
- `AIProvider.GetDefaultModel` no longer trips the recursion guard on every unconfigured-provider read: it fetched the configured model via `GetSetting<string>("Model")`, which evaluates the `Model` descriptor's lazy default — and providers such as Ollama and LocalAI define that lazy default as `GetDefaultModel()` itself, so each call re-entered the method through the settings path and logged `Recursion detected for <Provider>.Model`. The method now reads the injected settings cache directly (under the existing settings lock) and only falls back to the capability registry, leaving descriptor evaluation to the outer `GetSetting` call sites that own it.
- Secret settings no longer hit the OS secure store on every read: `GetOrCreateEncryptionKey` performed a DPAPI/registry (Windows) or Keychain (macOS) round-trip for every secret `GetSetting`, which the settings sweep multiplied into constant secure-store churn. The 32-byte key — stable for the process lifetime — is now fetched once and cached under a lock; only successful retrievals/generations are cached, and plaintext secrets are still never cached.
- Collapsed tool cards in WebChat no longer dump unbounded JSON: the single-line `args → result` preview embedded the full compact payloads, so a large `gh_report` result rendered as a wall of raw JSON on the collapsed card. The preview line is now capped at 240 characters with an ellipsis while the complete pretty-printed JSON remains available in the expanded card.
- OpenAI-compatible metrics decode no longer aborts on a single explicit JSON `null` usage counter (observed in batch result items): `Value<int>()` on a null token threw mid-decode, dropping every valid quantity plus the finish reason into the catch path. Token, duration, and cost reads in `DecodeOpenAICompatibleMetrics` now go through `IntOrNull`/`DoubleOrNull`/`DecimalOrNull` helpers — a null field decodes to zero while genuinely malformed types still surface through the decode error path.
- Gemini image generation no longer fails with HTTP 404: `text2img` passes the OpenAI-style `/images/generations` endpoint hint, which `GeminiProvider.PreCallAsync` preserved verbatim because it merely checked for a leading `/`, POSTing to a route the Gemini API does not expose. Only native Gemini API paths (`/models/…`, `/tunedModels/…`) are now preserved; foreign hints fall through to capability-based routing, so `ImageOutput` requests resolve to `/models/{model}:generateContent` (which already encodes `responseModalities`/`imageConfig` and decodes `inlineData` parts).
- OpenRouter image-generation responses no longer report "No image result received": image-capable chat models return generated images in `choices[].message.images[]` (`image_url` entries carrying data URIs), which the response decoder never read. A shared `OpenAICompatibleImageCodec.ExtractImages` now decodes those entries into `AIInteractionImage` — data URIs into `ImageData`/`MimeType`, remote links into `ImageUrl`, all tagged `AIAgent.Assistant` so image-result tools recognize them — and `OpenRouterProvider.Decode` surfaces them alongside the assistant text.
- Gemini `service_tier` overrides now serialize to the API contract: the request body carried the field with a bare upper-cased value (e.g. `FLEX`), which the API rejects. `serviceTier` now carries the lowercase v1beta enum spelling (`flex`, `priority`) — inputs are normalized from any casing and from prefixed enum forms (`SERVICE_TIER_*`, `TIER_*`); `standard`/`unspecified` stay omitted as the API default.
- Gemini image requests no longer fail with HTTP 400 "GenerateContentRequest.contents: contents is not specified": a request-mode `AIInteractionImage` (prompt set, no image data yet) encoded to zero parts, producing an empty `contents` array. `EncodeToJToken` now emits the request's `OriginalPrompt` as a text part for that shape, while image-bearing interactions still encode as `inline_data`.
- Per-request Extra Settings (`service_tier`, `thinking_level`, `voice`, sampling overrides, timeouts) now reach provider calls from every AI tool: tool wrappers built inner `AIRequestCall`s copying provider/model only, so `toolCall.Parameters` never propagated. The inner request now inherits the tool call's `Parameters` (`request.Parameters ??= toolCall.Parameters`, `speech_generate`'s explicit merge unchanged) across `text2img`, `img2text`, `text2text`, `text2json`, `text2boolean`, `text2textlist`, `textlist2boolean`, `list_filter`, `gh_generate`, `gh_report`, `gh_smart_connect`, `script_generate`/`script_edit` (including correction retries), `script_review`, and Discourse summary calls.
- Metrics `completed_at` no longer loses its UTC offset and sub-second precision on round-trips through `JObject.Parse`: the default `DateParseHandling.DateTime` converts the ISO string into a `JTokenType.Date` token, whose `ToString()` yields a culture string that the parser then re-interpreted as local time — replayed metrics after a Grasshopper undo/serialize cycle or `Combine Metrics` showed a shifted, millisecond-truncated timestamp. `AIMetricsJson.FromJson` now reads `JTokenType.Date` tokens natively via `ReadCompletedAt`, and `Deconstruct Metrics` emits the canonical ISO 8601 UTC text instead of the token's culture-formatted string.
- `AI List Filter` now completes batch executions correctly: the component never guarded the `##SH_BATCH:…##` sentinel in its worker nor overrode `OnBatchCompleted`, so a queued call left the raw sentinel in the `Result` channel and conversion to the output type failed. The worker now forwards sentinels and caches the unit's source items, and `OnBatchCompleted` decodes the index response back onto those items through the new `ProcessBatchResultsMany`/`ReconstructOutputTreeMany` overloads — one sentinel may expand to zero or more output items, replacing it positionally inside the stored fragment.
- `Metrics` output item order now follows result order on mixed reused/fresh runs: provenance used to append in completion order, so reused units replayed during processing landed ahead of fresh batch results that only arrived at poll completion — a `PAPA,SIERRA,ROMEO` result emitted `PAPA,ROMEO,SIERRA` metrics. Metrics are now rebuilt from recorded unit placements (`RunMetricsProjection`, the same mechanism `BuildRunTrees` uses for outputs) with per-seat `reused` flags, zeroed `data_count`/`iterations_count` on replays and sibling copies, and unplaced/orphan provenance preserved at the end. Placement state is stored as raw path indices (decoupled from Grasshopper types), serializes the `reused` flag into the pending `pl` node for in-flight batch persistence, and the batch elapsed `completion_time` is stamped into stored provenance before commit so persisted and emitted values agree.
- `AI Text to Boolean` no longer duplicates batch provenance in the `Metrics` output: `OnBatchCompleted` ran `ProcessBatchResults` twice — once for `Result`, once for `Used Fallback` — and each pass re-decoded every item and re-recorded its metrics, so a single execution emitted two entries (the second unstamped, `completion_time: 0`). The component now finalizes in a single pass like `AI List to Boolean` (`Used Fallback` flows through `SentinelTransformOutputs`), and `UnitResultStore.RecordMetricsForSentinel` became a bulk once-per-sentinel-per-run call so repeated decode passes cannot duplicate recorded provenance regardless of the caller.
- `AI Text to Boolean` and `AI List to Boolean` batch runs now actually reuse committed results: both workers stored the `##SH_BATCH:…##` sentinel in the `UsedFallback` channel as a length placeholder, but no decode pass ever resolved that channel, so `CommitRun` dropped every fragment as unfinished work and the committed cache stayed permanently empty — every identical-input run re-executed and re-charged all units. Both components now leave `Used Fallback` empty while queued (the decoded parse supplies it), `ResolveSentinel` sweeps leftover placeholder sentinels bearing the resolved custom id so the resolution contract cannot be silently violated again, and fragment channels now carry canonical output types (`GH_Boolean` under the parameter name `Used Fallback`) in both modes so committed results replay verbatim in sync, batch, and mixed runs.
- `AI Text to Boolean` synchronous finalization now emits `Result` + `Used Fallback` through one atomic `FinishResults` call (matching `AI List to Boolean`), and the string→boolean conversion handles typed replayed items per slot instead of assuming raw strings — reused batch-committed fragments no longer emit nulls when a run finalizes without a new submission.
- Batch-unit metrics now report per-call values instead of batch totals: `data_count` used to fall back to the whole-component item count (`3` on every unit of a 3-item run) because `StampBatchMetrics` never stamped it — each call now carries `data_count: 1` like the synchronous path, so aggregated `Combine Metrics` sums to the real call count. `finish_reason` also arrived `null` (aggregating to `""`) when the model answered via the Responses API, which reports `status` instead of `choices[].finish_reason`; finish reasons are now normalized through a single canonical `AIFinishReason` codec covering both wire formats — `completed`→`stop`, `incomplete`+`max_output_tokens`→`length`, other statuses verbatim — shared by the metrics decoder and both batch result parsers (which also stop warning about successful `completed` items).
- Branch-matching no longer treats unwired/empty inputs as matchable: an input with zero items (an unconnected optional parameter, or only empty branches) neither requires matching nor can broadcast, so it is excluded from the intersection used for `OnlyMatchingPaths` (where it previously could delete every input's branches) and from the mismatch-remark check (where it produced phantom "will receive broadcast values" remarks — e.g. two permanent remarks on `AI Text to Boolean` whenever `Fallback` was left unwired). Mirrored per-input remarks are now consolidated into a single message naming every affected input.
- Batch metrics no longer duplicate per provider: collection emitted one `Metrics` entry per decoded interaction, but `IAIInteraction.Metrics` is auto-initialized on every interaction, so providers that split content and usage into separate interactions (Gemini emits a metrics-only sidecar) produced a phantom empty entry (`quantities:{}`, `finish_reason:null`) beside each real one — six entries for a three-unit Gemini batch. Metrics are now folded into one combined entry per unit via `WithCombined`, mirroring `AIBody.Metrics` on the synchronous path.
- `Canvas to AI` (`GhJSON2AIComponent`) no longer sticks at `Processing…` after producing output: it overrode `OnStateProcessing` to run `gh_get` synchronously without delegating to the worker pipeline, so no worker ever spawned and `OnWorkerCompleted` — the only path that commits input hashes and requests `Completed` — never fired. It now runs through the standard `AsyncWorkerBase` pipeline (GatherInput → `DoWorkAsync` → `SetOutput`) like every sibling.
- Edits landing during a non-batch async run were silently acknowledged without ever running: completion committed the *current* pending input hashes, swallowing mid-flight changes — a Settings/Extras edit during a 46 s run vanished from change detection entirely. The run-scoped input snapshot described under Changed resolves it.
- Disabling result caching starved sentinel resolution and looped resubmissions: the disabled path bypassed the entire reuse wrapper including `ConfigureRun`/`StoreFragment`, so a completed batch left every sentinel unresolved → `ResultsIncomplete` demotion → `RunEnabled` → resubmit (repeating indefinitely). Only the committed-result lookup is bypassed now; run-scoped bookkeeping always runs.
- MCP `tools/call` responses now forward tool runtime diagnostics and report errors correctly: `AIToolMcpAdapter` used to serialize only the first Error-severity message (warnings and informational messages died at the adapter boundary, so tools like `gh_get`/`gh_put` silently dropped actionable warnings) and a present payload masked a simultaneous error. Every non-Debug `SHRuntimeMessage` is now projected as a bounded typed `McpDiagnostic` — exposed to models as an extra `diagnostics` text block and to clients under `_meta["smarthopper/diagnostics"]` — `isError` is derived from Error-severity execution messages (request-validation diagnostics can't turn a successful call into an error, while a partial payload is preserved under `result` in the error details instead of hiding the failure), and `requestId` cache replays now preserve images, helper metrics, and diagnostics instead of only the payload.
- `gh_list_components` now actually answers name searches: the `query` filter only matched `Name`/`NickName` while the catalogue's keyword metadata was serialized but never searched, so queries like `AI Settings` returned zero results even though Settings exposes that exact keyword. Proxy metadata is now snapshotted into immutable DTOs on the UI thread and matched deterministically off-thread — exact/prefix/substring tiers across name, nickname and keywords with stable tie-breaks, deduplicated by type GUID — and components are only instantiated for the returned results when `inputs`/`outputs` details are requested, so metadata-only listings never run `CreateInstance`.
- `gh_generate` no longer emits the identities its prompt forbids: generated documents went through the generic `GhJson.Fix` repair path, whose defaults inject `instanceGuid`s and silently drop connections the model wired to missing components. Generation now normalizes through a dedicated policy — strips model-supplied `componentGuid`/`instanceGuid`/group GUIDs and pivots (a hallucinated `componentGuid` would silently outrank the component name during placement), validates that every connection and group member references a supplied integer id instead of dropping dangling wires, and runs a bounded fixer that assigns missing ids and repairs metadata without ever generating GUIDs or deleting content. Unresolvable GUID-only components surface as validation errors that feed the generation retry loop.
- `canvas_view` actions now return the applied viewport instead of the stale one: `setCenter`/`setZoom`/`frameGuids`/`zoomExtents` used to read `MidPoint`/`VisibleRegion` immediately after requesting the change, answering with the pre-move projection. Actions now resolve the complete view first, apply zoom and target coherently in one UI turn, and recompute the projection (`ComputeProjection`) before the response is built — so a follow-up screenshot observes the requested view. `setZoom` preserves the world-space center and clamps to the same limits; framing fits the union of valid rendered bounds with padding; malformed GUID tokens are reported in `invalidGuids` instead of being silently skipped, and unresolved frame targets fail without moving the view.
- MCP mutation calls can no longer double-apply on connection-loss retries: the old global check-then-store result cache (any client could replay another's `requestId`, and two concurrent same-key calls could both execute) is replaced by `McpMutationReservations` — an atomic reserve/join/reject registry keyed by (session, tool, requestId, document binding) with a canonical argument fingerprint. Concurrent same-key calls join a single owner, a same-key call with different arguments fails as `idempotency_conflict`, a retry bound to a different document fails as `document_changed`, and an unresolved binding never blocks the join (the recorded binding wins). When an owned mutation outlives the caller's bounded wait the response is an honest `operation_in_progress` envelope (operation id + state) instead of a fabricated failure, and the real terminal outcome stays fetchable under the same key.
- MCP `initialize` now allocates a logical session returned in the `Mcp-Session-Id` response header (`McpSessionRegistry`: bounded at 128, idle-expiring at 30 minutes, never evicting sessions that still own in-flight requests or mutation reservations). Requests carrying an unknown/expired session id get HTTP 404 instead of silently degrading to stateless, and session state never crosses server generations.
- `notifications/cancelled` is now honored: in-flight JSON-RPC requests register per session under their typed id (string `"1"` and numeric `1` are distinct, and one session can never cancel another's request), a cancel fires the request's linked cancellation token through to tool execution, and a concurrent duplicate in-flight id is rejected as `InvalidRequest` instead of overwriting the original's cancellation slot.
- `MutationInvocationContext` now carries a `ToolExecutionLifecycle` (`Preparing`/`Reviewing`/`Queued`/`Applying` → `Applied`/`FailedCompensated`/`FailedPartial`/`CancelledNotStarted`/`Rejected`/`Invalid`) so the MCP adapter, consent review and the commit path share one ownership truth: cancellation before the commit claim is provably side-effect-free, and work that claimed the commit owns its outcome and is never reported as cancelled.
- Canvas-mutating AI tools can no longer execute after their caller's wait ended: every direct `RhinoApp.InvokeOnUiThread` + task-completion-source pattern (`gh_put`, `gh_connect`, `gh_disconnect`, `gh_group`, `gh_component_set_provider`, both parameter modifiers, `CanvasAccess.RunOnUiThread`, and the `CanvasNewComponentLayout` post) now goes through the shared bounded-post contract — a 30-second queued-start budget bounds only admission, a queued callback whose slot expired never runs, cancellation reaches the post while still queued, and a started callback owns its outcome so the caller always waits for the real result instead of reporting a fabricated timeout while a mutation is still applying. `gh_component_set_provider` additionally now awaits the mutation it previously fired and forgot, so its reported success reflects the actual canvas change.
- `gh_put` no longer blocks its acknowledgement on a full document solve: placement now runs through the new GhJSON `PreparePut`/`ApplyPreparedPut` split (`PutOptions.RequestSolution = false`), and exactly one `ScheduleSolution` is queued at the end of the UI commit — covering placed objects, document connections, and restored external wires — instead of up to three synchronous `NewSolution` calls that could hang the MCP response for the solver's duration.
- Disconnected, pivotless document parts now land with their bounds' top-left at the shared target (leftmost existing X, below the lowest existing content) in both the `gh_put` layout pass and the placement preview, replacing the additive per-part offset that made the drop position depend on each part's original coordinates; `CanvasNewComponentLayout` consumes the same `CanvasBoundsCalculator` target the placer uses, so preview and apply agree.
- The remaining canvas-mutation tools now respect the same solve boundary as `gh_put`: `gh_connect`, `gh_disconnect`, `gh_component_set_provider`, `gh_set_value` (via `ComponentManipulation.SetObjectValue`) and `gh_edit_datatree` (via `DataTreeManipulation`) queue one `ScheduleSolution` after the mutation instead of re-entering `NewSolution` inside the mutation turn, so the acknowledgement no longer waits for arbitrary downstream computation.
- Provider/model changes share one non-solving core (`ComponentProviderOps`): both `gh_component_set_provider` and `gh_apply_changes` call `ApplySetProviderOnUiThread`, which captures a compensation inverse (previous provider selection, previous Settings-input sources, added model panel) so a composite commit can rewind a completed provider edit when a later operation fails. `gh_set_value`'s value application is likewise extracted as `SetObjectValueOnUiThread` for the composite.

## [2.0.0-dev.260821] - 2026-08-21

### Added

- **LocalAI provider** (`SmartHopper.Providers.LocalAI`): new built-in provider for self-hosted [LocalAI](https://localai.io/) instances. Talks to the standard OpenAI-compatible `/v1/chat/completions` endpoint, with full support for tools/function calling, streaming, JSON Schema structured outputs, and an `Authorization: Bearer` API key when one is configured. Settings include a required **Base URL** (default `http://localhost:8080/v1`) plus optional API Key, Model, Streaming, Max Tokens, and Temperature. The provider does not auto-discover models — users list whatever models they have installed locally.
- **Ollama provider** (`SmartHopper.Providers.Ollama`): new built-in provider for [Ollama](https://ollama.com/) running locally via its [OpenAI-compatible endpoint](https://ollama.com/blog/openai-compatibility). Mirrors the LocalAI capabilities (tools, streaming, JSON Schema, optional bearer token for reverse-proxy setups) plus an extra `seed` extra-descriptor for reproducible sampling. Settings include a required **Base URL** (default `http://localhost:11434/v1`) plus optional API Key, Model (e.g. `llama3.1`, `qwen2.5:14b`, `mistral:7b-instruct` — install with `ollama pull <model>`), Streaming, Max Tokens, and Temperature.
- Added reusable model-level batch metadata: `AIModelCapabilities.SupportsBatch` and `AIModelCapabilities.BatchPricing` for provider-agnostic batch compatibility and discounted batch pricing.
- Implemented full `IAIBatchProvider` contract on `OpenRouterProvider`, including batch submission, status polling, cancellation, result downloading, and JSONL result parsing.
- Added `OpenRouterBatchProviderTests` to `SmartHopper.ProviderSdk.Tests` covering `:batch` alias canonicalization, generated model metadata, registry alias resolution, and batch result parsing.
- Added batch support gating in `AIStatefulAsyncComponentBase` so a request is only routed to batch mode when the selected model declares `SupportsBatch`.
- Added centralized `AIMetrics.EstimatedCost` (USD) and `AICostCalculator` that derive estimated call cost from provider/model pricing and token buckets (input prompt, cached, cache-write, output generation, reasoning). Falls back to `EstimatedInputTokens`/`EstimatedOutputTokens` when actual token counts are zero; free, negative, or missing prices contribute `0`.
- Added `Estimated Cost` output to `Deconstruct SmartHopper Metrics` and `estimated_cost` field to metrics JSON.
- Added `SmartHopper.ProviderSdk.Tests` xUnit project with coverage for `AICostCalculator`, `AIMetrics`, `AIModelCapabilityRegistry`, `AIBody`/`AIBodyBuilder`, `AIRequestBase`/`AIRequestCall`, `AIReturn`, all `AIInteraction*` types, `AICallStatus`/`AIAgent`/`AIRequestKind`, `AICapability`/`AIModelCapabilities`, and `SHRuntimeMessage`.
- Added `.github/workflows/pr-linear-history.yml` to enforce that pull requests targeting `main` (and release/hotfix branches) are rebased and contain no merge commits.
- Added `.github/workflows/sync-dev-from-main.yml` to automatically rebase `dev` onto `main` and force-push; if the rebase fails, the workflow run fails so it surfaces in GitHub Actions.
- Added `.github/actions/utils/rebase-onto-base` composite action to centralize rebase/base-management operations for GitHub Actions workflows.
- Added `AIBody.WithReplaced` extension for replacing a specific interaction by reference in an immutable body.
- Added `AITool.GetRequiredParameters()` helper to parse required parameter names from a tool's JSON schema.
- Added `ProviderTestComponentBase` to `SmartHopper.Components.Test/Providers` and converted all ~43 `Test{Provider}{Feature}Component` classes to inherit from it. The base centralizes common setup/teardown (provider selection, `RunOnlyOnInputChanges`, category/exposure) while each component remains an independent per-provider test runner.
- Added nested mutable `Builder` classes to `AIInteractionText` and `AIBody` for streaming aggregation and body construction. Builders accumulate local state and emit immutable snapshots via `Build()`.

### Changed

- `tools/Update-ProviderModels.ps1` now detects OpenRouter `:batch` model variants, canonicalizes them to the base slug, and merges `SupportsBatch`/`BatchPricing`/`Aliases` into the base model entry instead of generating separate `:batch` models.
- OpenRouter `:batch` model aliases are normalized to the base slug in `OpenRouterProvider.PreCall` so both chat and batch endpoints use the same model identifier.
- `AIStatefulAsyncComponentBase` now verifies `AIModelCapabilities.SupportsBatch` before enabling batch execution; unsupported model requests surface a runtime diagnostic.
- `release-2-pr-to-dev-closed.yml` now checks `git merge-base --is-ancestor origin/main HEAD` before creating the `dev → main` release PR and rebases/force-pushes `dev` onto `main` when it is behind.
- `chore-update-model-verification-template.yml` now targets the triggering branch (typically `dev`) instead of `main`, so updates flow through the regular release PR.
- `chore-update-contributors.yml` and `pr-anonymize-public-key.yml` no longer delete/recreate branches and PRs; they use `peter-evans/create-pull-request` updates.
- `safe-commit` and `code-style` now rebase (`git pull --rebase --autostash`) before pushing, falling back to a PR if the branch is protected.
- Automated workflows that open PRs to `main`/`main-*`/`release/**`/`hotfix/**` now fetch full history and rebase onto the target base before calling `create-pull-request`.
- `pr-block-dev-to-main.yml` is now `Warn Dev Release to Main`: it emits a warning and a PR comment instead of exiting with an error when a `-dev` version is merged into a protected branch.
- Updated `SmartHopper.Providers.Ollama` and `SmartHopper.Providers.LocalAI` resource icons to their official brand marks (Ollama from `@lobehub/icons`, LocalAI from the official `mudler/LocalAI` logo).
- Consolidated OpenAI-compatible JSON schema wrapping into `SmartHopper.ProviderSdk.AICall.JsonSchemas.OpenAICompatibleJsonSchemaAdapter`. `OpenAI`, `MistralAI`, `Ollama`, and `LocalAI` now register the shared adapter directly; `DeepSeek` inherits it and only overrides `Unwrap`.
- Added `AIProvider.GetApiKey()` and `AIProvider.LoadIconFromResources(...)` helpers in `SmartHopper.ProviderSdk.AIProviders`. All built-in providers now use these shared helpers, removing the duplicated per-provider `GetApiKey()` methods and icon-loading code.
- Added `AIProviderSettings.ValidateMaxTokens(...)` and `AIProviderSettings.ValidateTemperature(...)` helpers. All built-in provider settings classes now delegate `MaxTokens` and `Temperature` validation to the shared helpers, using stricter parsing that fails on unparseable values.

### Removed

- Removed the redundant `SmartHopper.Infrastructure.AIModels.ModelManager` singleton. All model capabilities, defaults, selection, and streaming validation now flow through `SmartHopper.ProviderSdk.AIModels.AIModelCapabilityRegistry.Instance`, making it the single source of truth for model selection.
- Removed the duplicated per-provider JSON schema adapters: `OpenAIJsonSchemaAdapter`, `MistralAIJsonSchemaAdapter`, `OllamaJsonSchemaAdapter`, `LocalAIJsonSchemaAdapter`, and the fallback `DefaultJsonSchemaAdapter`.

## [2.0.0-dev.260802] - 2026-08-02

### Added

- Backported DeepSeek provider settings from hotfix 1.4.4: `ReasoningEffort` and `TopP` settings, plus `reasoning_effort` per-request extra support.
- Backported OpenRouter provider model default capability assignments from hotfix 1.4.4.
- Added `smarthopper_ghjson_reference` AI tool that returns GhJSON/GhPatch format reference docs from an embedded snapshot of `ghjson-spec`, with a `tools/Sync-GhJsonSpecDocs.ps1` sync script to keep the snapshot current.
- Added `.github/workflows/chore-update-ghjson-spec-docs.yml` to automate syncing the embedded GhJSON/GhPatch spec snapshot from `ghjson-spec` and to validate the snapshot on related pull requests.
- Enabled `gh_generate` AI tool for production: renamed from `_gh_generate`, switched to instructions-driven GhJSON generation via an AI subagent, embedded the GhJSON specification into the system prompt, added GhJSON validation and retry logic, and introduced `gh_generate_and_place_on_canvas` wrapper that calls `gh_put`.
- Added `gh_report` AI tool: read-only markdown canvas status report with object counts by type/topology, unique component names, group titles, scribble texts, viewport contents, file metadata, and all errors/warnings. Optionally includes an AI-generated summary of the file purpose.
- Added `gh_clear` AI tool: clears all components from the canvas with optional `keepLocked` support. Protected components (and their direct neighbors) are always preserved via `CanvasProtection`. Destructive — supports undo.
- Added `gh_smart_connect` AI tool: AI-suggested wiring. Retrieves component structure directly via the ghjson-dotnet facade, asks an AI model to propose connections based on a purpose description, and executes them via `GhJsonGrasshopper.Connect`. Returns connection results and AI reasoning.
- Added `set_ai_provider_and_model` AI tool: configures an `IProviderComponent` by setting its selected AI provider and wiring a new Panel with the model name into its Settings input. Supports undo and respects `CanvasProtection`.
- Added `get_available_providers` AI tool: returns the list of enabled AI providers registered in SmartHopper, including a `configured` flag for each provider.
- Added `get_available_models` AI tool: returns the list of available models for a provider, preferring live provider APIs and falling back to the static model list.
- Refactored `AIModelsComponent` to execute `get_available_models` through `AIToolManager`, reusing the same AI tool infrastructure as other AI-powered components.
- Added `IAIProvider.IsConfigured` and `AIProvider.IsConfigured` to expose a provider's configuration state, computed from persisted settings at access time (not stored in settings).
- Implemented `IsConfigured` in all providers (OpenAI, Anthropic, MistralAI, DeepSeek, Gemini, OpenRouter) using non-empty API key as the configured check.
- Added pre-validation in `AIProvider.Call` that returns a structured error when the provider is not configured before attempting an API call.
- Added `providers` topic to `smarthopper_readme` explaining how default provider/model settings are managed in the environment and how to configure or override them.
- Added `AIGhReportComponent` Grasshopper component: wraps `gh_report` with an `Include Summary` toggle and `Report`/`Summary` outputs.
- Added `AIGhConnectComponent` Grasshopper component: wraps `gh_smart_connect` for canvas-selected components with a `Purpose` input and `Success`/`Reasoning`/`Connections` outputs.
- OpenDocument Format (ODF) support for `.odt`, `.ods`, and `.odp` files.
- Legacy Excel (`.xls`) file-to-Markdown conversion via `XlsConverter`.
- Single-item input adapters: `Number to AI`, `Boolean to AI`, and `Integer to AI`.
- Raw text fallback and improved encoding detection for file converters.
- `GhJsonSpecLoader` to load embedded GhJSON/GhPatch spec snapshots.
- MCP lock support for SmartHopper components: right-clicking an `AsyncComponentBase`, `SelectingComponentBase`, `ProviderComponentBase`, or derived component adds a "Lock from MCP updates" menu item, and locked components are highlighted with an orange border and lock icon overlay.

### Changed

- Stabilization backport (`stabilization-2-complete.yml`) now cherry-picks only the commits unique to `main-X.Y.Z` onto a fresh `backport/X.Y.Z` branch cut from `main`, instead of merging the whole stabilization branch. It skips creation when there is nothing to backport and lists the cherry-picked (and any conflicting) commits in the PR body.
- Standardized the git commit author across GitHub Actions workflows and the code-style action to use the `SMARTHOPPER_BOT_NAME`/`SMARTHOPPER_BOT_EMAIL` repository variables instead of hardcoded `github-actions[bot]` identities.
- `get_available_providers` now returns only enabled providers and omits the redundant `enabled` field from the output; use `get_available_providers` to discover configured providers and `get_available_models` to inspect their models.
- Scheduled provider-model updates (`.github/workflows/chore-update-provider-models.yml`) now open PRs against `dev` and `dev-*` branches only; `main` is excluded from automatic model-update PRs.
- DEV.md sync workflows (`chore-update-dev-aitools.yml` and `chore-update-dev-provider-models.yml`) no longer trigger on pull requests; the automated sync PR is created only after the source changes are pushed, so the workflows do not block incoming PRs.

### Fixed

- Backported DeepSeek provider tool-call fixes from hotfix 1.4.4: consecutive assistant messages are now merged, `reasoning_content` is preserved on assistant messages with tool calls and stripped from text-only assistant messages, and tool result `name` is no longer sent.
- Backported OpenRouter provider tool result fix from hotfix 1.4.4: removed the invalid `name` field from `role=tool` messages.
- `tools/Update-DevAiTools.ps1` no longer appends extra blank lines to the end of `DEV.md`; it now leaves exactly one trailing blank line.
- CI merge-queue support: required PR checks now run on `merge_group` events so queued PRs don't stall waiting for status reports.
- [#647](https://github.com/architects-toolkit/SmartHopper/issues/647): MistralAI reasoning_effort validation now gates the field and restricts values to "none" or "high".
- Issue-labeler configuration syntax: regex patterns now use JavaScript-compatible `/pattern/i` syntax instead of `(?i)`.
- PDF text extraction order: switched `PdfConverter` from `DefaultReadingOrderDetector` to `UnsupervisedReadingOrderDetector` configured as `RowWise` without rendering order, so PDF pages are emitted in correct top-to-bottom, left-to-right reading order instead of following the PDF content stream order.
- Metrics tree topology for batch image descriptions in File2Md, Web2Md, and Web2AI: per-image slot metrics now land at the same output branch path as the Markdown output instead of collapsing to `{0}`.
- Knowledge components to ensure the `Run` parameter triggers execution by disabling `RunOnlyOnInputChanges`.
- DEV.md provider-model sync workflow (`chore-update-dev-provider-models.yml`) now uses a per-base branch name (`chore/update-dev-provider-models-<base>`) instead of a single shared branch, preventing its auto PRs from listing unrelated commits carried over from another base branch.
- `gh_diff` now strips `instanceGuid` from added components and groups, producing valid `.ghpatch` documents that pass `gh_patch_validate` and `gh_patch_apply`.
- Fixed MCP lock overlay position: the canvas graphics transform is now reset before drawing so the orange border and lock icon align with the protected component instead of being double-projected.
- Fixed the placeholder component GUID in `AI Web To Markdown` so it no longer uses the non-unique `ABCDEFAB-CDEF-ABCD-EFAB-CDEFABCDEFAB` value.
- `VersatileImage` now rejects unsupported or missing image file paths instead of defaulting to `LocalFile` and producing malformed payloads.
- `Discourse Post Get` post JSON now includes `topic_title` and `url`, matching the fields emitted by `Discourse Search`.
- Added the missing `JSON Set Value` component (`JsonSetValueComponent`) for non-AI JSON editing.
- Replaced placeholder/low-entropy component GUIDs in `AI Text To Text List` and `Apply GhPatch` with random GUIDs to avoid future collisions.
- Provider model update PR summaries now count only newly deprecated models in the `Deprecated` metric; previously-deprecated models are reported as unchanged and no longer show a `Deprecated` decision in the details table.

### Security

- Replaced the non-Windows file-based XOR obfuscation for the encryption master key with macOS Keychain storage (DPAPI unchanged on Windows). This removes the predictable `System.Random` seed derived from the user and machine names. (VULN-002)
- Fixed `SmartHopperSettings.Decrypt` so it returns `null` instead of the original (possibly sensitive) ciphertext when the encryption key is missing or decryption fails. (VULN-003)
- Sanitised HTML rendered in the WebView chat before it is inserted into the DOM by embedding DOMPurify and applying a Content-Security-Policy, closing stored/reflected XSS vectors through `innerHTML`. (VULN-009)
- Pinned transitive package versions to patched releases: `AngleSharp` 1.5.0, `System.Formats.Asn1` 8.0.1, and `System.Text.RegularExpressions` 4.3.1, removing the NuGet vulnerabilities flagged in VULN-012.
- Removed the hardcoded `LegacyKey`/`LegacyIv` and all legacy migration code from `SmartHopperSettings`. The secure-store encryption prefix is now `SH03:` on both Windows and macOS. (VULN-001, VULN-003)
- Added a Windows-only automatic migration that renames existing `SH02:` prefixed secrets to `SH03:` using the same DPAPI key, so Windows users do not need to re-enter API keys. (VULN-001, VULN-003)
- Added a one-time macOS warning that informs users their saved provider API keys cannot be recovered and must be re-entered, then clears the stale `SH02:` values. The message is not shown again once the settings are updated. (VULN-001, VULN-003)

## [2.0.0-dev.260705] - 2026-07-05

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

### Added

- **PDF-to-Markdown improvements**: PDF conversion now preserves hyperlinks, detects lists, interleaves inline images in reading order, and renumbers/cleans Markdown output.
- **Web-to-Markdown image handling**: `web2md` supports `link`, `embed`, `describe`, and `caption` image modes; `File2AI` and `Web2AI` expose an `Image Mode` input.
- **New web component**: Added `AIWeb2MdComponent` for AI-powered URL-to-Markdown conversion; the standard `Web2MdComponent` is now non-AI.
- **Image generation**: `AIText2ImgComponent` now supports an `Aspect Ratio` input for Gemini image generation.
- **MCP server**: Added a local HTTP/JSON-RPC MCP server and an opt-in `SmartHopperMcpServerComponent` to expose SmartHopper tools to external MCP clients.
- **Canvas protection**: Components implementing `ICanvasProtectedComponent` (starting with the MCP server component) are protected from mutating AI tools.
- **New canvas tools**: Added `gh_remove`, `gh_disconnect`, `gh_document_save`, `button_click`, and `gh_connect` tools.
- **Canvas queries**: `gh_get` and `GhGetComponents` support pagination and `Count`/`pageSize` limits; responses can include live runtime data.
- **Runtime data**: Live Grasshopper parameter values can be serialized as runtime data via the shared GhJSON library.
- **Self-documenting tools**: Added `smarthopper_workflows` and `smarthopper_tool_help` to help MCP clients discover workflows and tool usage.
- **Tool discoverability**: Every AI tool now exposes MCP metadata, JSON schemas, and read-only/mutating hints; experimental tools can be disabled with the `Enabled` flag.
- **About dialog**: Added open-source package attribution.

### Changed

- **Provider settings**: Anthropic adds `ServiceTier` and `ReasoningEffort`; MistralAI and Gemini add `ReasoningEffort`; Gemini restricts `SafetyLevel` to allowed values.
- **Provider models**: Refreshed model registries across Anthropic, DeepSeek, Gemini, MistralAI, OpenAI, and OpenRouter to match current API lineups.
- **Tool descriptions**: Clarified `gh_get`, `gh_put`, `button_click`, and `web2md` descriptions.
- **Readme tool**: `smarthopper_readme` now points to `smarthopper_workflows` instead of embedding numbered scripts.
- **Tool help**: `smarthopper_tool_help` output field `all_tools` renamed to `similar_tools`.
- **MCP server**: The component restarts automatically when port, bearer token, or mutating-tools inputs change.
- **File conversion**: Simplified `file2md` AI tool parameters (`preserveFormatting` replaces several options; some are always enabled).
- **List/query tool**: `gh_list_components` parameter `nameFilter` renamed to `query` (backward compatible).
- **Patch workflow**: `gh_patch_apply` and `gh_patch_validate` reject `instanceGuid` in add operations and validate before applying.
- **Canvas wiring**: Canvas connection and replacement logic is centralized, preserving external wires during component replacement.
- **Canvas reads**: Canvas read helpers are centralized for consistent tool behavior.
- **Output schema**: `gh_put` output schema updated to match the actual response shape.

### Fixed

- **Canvas operations**: `gh_put` reliably restores external connections, `button_click` forces downstream recalculation, and `gh_group` returns a correct response.
- **Web-to-Markdown**: Fixed Wikipedia, Discourse, and generic HTML conversion edge cases.
- **File conversion**: Fixed XLSX/PPTX formatting issues and Markdown list blank-line edge cases.
- **Patch results**: `GhPatchApplyToCanvasComponents` outputs persist after the trigger button is released.
- **Empty results**: `gh_get` and `gh_put` now surface clear warnings for empty results.
- **Input handling**: `gh_put` accepts JSON strings and structured objects.
- **CI**: Fixed provider-model alias classification, documentation labeler rule, and `AIToolManager` test parallelism.

### Removed

- Removed the obsolete `pr-dependency-validation` workflow.

## [2.0.0-dev.260619] - 2026-06-19

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

> This release reimagines SmartHopper with a unified `*2*` naming convention, typed AI I/O components, GhJSON diff/patch, speech/music support, and breaking changes to tools, components, and settings.

### ⚠️ BREAKING CHANGES

- **Renamed AI Tools** (old → new): `text_generate` → `text2text`, `text_evaluate` → `text2boolean`, `list_generate` → `text2textlist`, `list_evaluate` → `textlist2boolean`, `img_generate` → `text2img`, `img_to_text` → `img2text`, `file_to_md` → `file2md`, `web_to_md` → `web2md`; `web_generic_page_read` removed.
- **Renamed Components**: `AITextGenerate` → `AIText2TextComponent`, `AITextEvaluate` → `AIText2BooleanComponent`, `AITextListGenerate` → `AIText2TextListComponent`, `AIListEvaluate` → `AIList2BooleanComponent`, `AIImgGenerateComponent` → `AIText2ImgComponent`, `AIImgToTextComponent` → `AIImg2TextComponent`, `AIFileToMdComponent` → `AIFile2MdComponent`, `WebPageReadComponent` → removed, `AIInstructionsComponent` → `AIPromptComponent`.
- **Batch API**: Custom `IAIBatchProvider` implementations must update to multi-item submission, `Results` dictionary, and `OnBatchCompleted` signature changes.
- **Settings input**: `AIStatefulAsyncComponentBase` now uses `Settings (S)` instead of `Model (M)`; accepts `AIRequestParameters` or a plain model name.
- **Removed `service_tier` descriptor** from OpenAI, Anthropic, and MistralAI providers; use the `Batch` input on `AISettingsComponent` instead.
- **Removed legacy interaction classes**: `AIInteractionError`, `AIInteractionInfo`, `AIInteractionWarning`, `AIInteractionDebug`, `AIInteractionDiagnosticBase`. Use `AIInteractionRuntimeMessage` with `Severity`.
- **`ComponentBase` refactor**: `IProviderComponent.HasProviderChanged()` removed; use `ProviderSelectionCore` events and idempotent methods. `SetAIReturnSnapshot` and `_persistedMetrics` visibility changed; use `SetPersistedMetrics` or `CombineIntoPersistedMetrics`. Legacy pre-V2 alpha file persistence removed; V2 files restore unchanged.

### Added

- **GhJSON diff and patch**: New `gh_diff`, `gh_patch_apply`, and `gh_patch_validate` AI tools plus `GhDiffComponents` and `GhPatchApplyComponents` Grasshopper components for versioning Grasshopper workflows.
- **Provider model sync automation**: Added tooling and workflow to validate and sync provider model documentation.
- **Trademark policy**: README now clarifies SmartHopper name/logo usage under LGPL.
- **MCP server**: Opt-in local HTTP/JSON-RPC server exposing SmartHopper tools to external MCP clients, with `SmartHopperMcpServerComponent` and security controls.
- **List I/O components**: 8 new typed list adapters (`TextList2AI`, `NumberList2AI`, `IntegerList2AI`, `BooleanList2AI`, `AI2TextList`, `AI2NumberList`, `AI2IntegerList`, `AI2BooleanList`) with fallback handling.
- **Output mapping**: Unified scalar and list output extraction for consistent batch and non-batch behavior.
- **AI model refresh**: Updated registries across Anthropic, OpenAI, DeepSeek, MistralAI, and OpenRouter.
- **JSON utilities**: `JsonFormatHelper` and `JsonSanitizerComponent` for robust JSON formatting and recovery from malformed AI output.
- **Google Gemini provider**: Full integration with text, image, structured output, tool calling, reasoning, batch, and service tier support.
- **Audio support**: Speech-to-text and text-to-speech across OpenAI, MistralAI, and Gemini, plus music generation with Gemini.
- **Model verification**: Community issue template and workflow to certify new models.
- **Main-to-dev sync**: Workflow to keep dev and stabilization branches in sync with main.
- **Automated provider model discovery**: Weekly CI sync against OpenRouter to add new models and deprecate vanished ones.
- **OpenAI Responses API**: Default API with streaming support.
- **Additional GhJSON components**: `SaveGhPatch`, `OpenGhPatch`, `GhValidateComponents`, and `GhPatchApplyToCanvasComponents`.
- **AI input payload codec**: Stable serialization for `GH_AIInputPayload`.
- **OpenRouter vision**: Image input support via OpenAI-compatible format.
- **New tools and components**: `text2json` + `AIText2JsonComponent`, `img2text` + `AIImg2TextComponent`, `GH_ExtractedImage` type, improved Python script validation.
- **File and web processing**: `file2md` and `web2md` tools with PDF layout intelligence, image extraction, and site-specific handlers.
- **Settings components**: `AISettingsComponent`, `AIExtraSettingsComponent`, and immutable `AIRequestParameters`.
- **JSON components**: Schema builders, object/array builders, parsers, and merge component.
- **Batch processing**: Multi-item batch support with progress, persistence, and per-item custom IDs in OpenAI, Anthropic, and MistralAI.
- **Infrastructure**: Centralized JSON recovery, structured HTTP errors, mixed-type data tree support, and unified diagnostic messages.
- **Reasoning effort**: OpenAI now consistently sends `reasoning_effort` for o-series and gpt-5 models, even with tool calling.
- **HTTP error classification**: 5xx, 408, and 429 are now surfaced as network errors; 4xx remain provider errors across streaming and non-streaming paths.
- **Fallback outputs**: `AI2Boolean`, `AI2Integer`, `AI2Number`, and `AI2Json` components now expose `Fallback` and `Used Fallback` outputs.

### Changed

- **Prompt caching**: Improved Anthropic and OpenRouter cache behavior and routing.
- **Context keys**: Renamed `current-*` to `local-*` for clarity.
- **AI file context**: Renamed `AIFileContext` to `AIFileMetadata` and integrated with GhJSON.
- **Codec registry**: Refactored into `IGooCodec` pattern with documentation.
- **GhGetComponents**: Migrated to worker pattern.
- **Provider parameters**: Removed `n` completions count from OpenAI and MistralAI.
- **AI rules**: Migrated from `.windsurf` to `.devin`.
- **Rhino compatibility**: Downgraded minimum requirement from Rhino 8.24 to Rhino 8.0.
- **AI model rebalancing**: Adjusted default and rank assignments across OpenAI, Anthropic, DeepSeek, and OpenRouter.
- **Timeout configuration**: Consolidated into a single `Timeout` setting with component input override; default 300s. **Breaking for saved files**: `AISettingsComponent` `Extras` input index shifted due to new `Timeout` parameter; reconnect `Extras` after upgrade.
- **Mixed-type trees**: Components can now mix `IGH_Goo` types (e.g., `GH_String` and `GH_Boolean`).
- **`AIFile2MdComponent`**: Reworked batch wiring so only image descriptions are batched; file conversion runs locally.
- **`img2text` tool**: Unified request building and decoding for execute and batch paths.
- **`file2md` tool**: Simplified image description handling.
- **`DataTreeProcessor`**: Added heterogeneous output support for different `IGH_Goo` types in one processing call.
- **`File2MdComponent` / `AIFile2MdComponent`**: Migrated to `RunProcessingAsync` for consistent tree handling.
- **Batch UX**: Cancellation feedback, progress counters, and queuing vs. execution messaging.
- **All providers**: Parameters now read from `request.Parameters` first, falling back to global settings.
- **`AIStatefulAsyncComponentBase`**: `Model` input replaced by `Settings` input; `GetModel()` reads from `AIRequestParameters`.
- **`AIRequestBase`**: Added `AIRequestParameters Parameters` property.

### Fixed

- **DeepSeek provider**: Fixed tool-call message ordering errors with parallel tool calls.
- **Batch processing**: Sentinel trees now survive file close/reopen; item errors surface as runtime messages; order-based fallback for result loading.
- **Progress messages**: Live batch progress counter and proper terminal-state message handling.
- **`AIImgToTextComponent`**: Fixed placeholder string being sent instead of base64 for extracted images.
- **`text2json` tool**: Robust JSON extraction from markdown fences and prefatory text.
- **Runtime messages**: Fixed messages disappearing after state transitions.
- **`gh_put`**: No longer fails when replacing components in Grasshopper edit mode.
- **General**: Added defensive null checks and error-handling improvements.

### Removed

- **`boolean_classify` AI tool**: Removed unused tool; boolean classification now handled locally by `AI2BooleanComponent`.
- **`integer_extract`, `number_extract`, `markdown_format` AI tools**: Removed orphaned tools; functionality covered by new output components.
- **`img_generate` AI tool**: Removed legacy tool superseded by `text2img`.

## [1.4.2-beta] - 2026-04-15

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

### Added

- ci(main-sync-to-dev): new workflow `.github/workflows/main-sync-to-dev.yml` that, on pushes to `main` (or manual dispatch), auto-opens/reuses a PR from `main` into `dev` and into every `dev-*` stabilization branch. For `dev` the PR is a plain `main → dev`. For each `dev-*`, the workflow maintains a `sync/main-to-<dev-*>` branch onto which it **cherry-picks only allow-listed files** from `main` (any change under `.github/`, `.windsurf/`, `.githooks/`, `hashes/`, plus *modifications* — not add/rename/delete — to existing `src/SmartHopper.Providers.*/*ProviderModels.cs`). Non-allow-listed files (feature source, docs, `CHANGELOG.md`, etc.) stay on `main` only, so a mixed commit on `main` still propagates its infra/model parts to stabilization lines; use `patch-propagate.yml` for targeted backports of the rest. Reuses an existing open PR per target instead of creating duplicates, and skips entirely when there is no effective allow-listed diff.
- Community model verification flow:
  - New issue template `.github/ISSUE_TEMPLATE/model-verification.yml` with tests grouped by location ("Components on the Grasshopper canvas" — `AITextGenerate`, `AITextListGenerate`, `AIImgToText`, `AIImgGenerate`, audio — and "Chat interface" — streaming, ToolChat/FunctionCalling, Reasoning, multi-turn `ConversationSession`), each test specifying the **exact prompt** to use and the expected behavior. The template also embeds a copy-paste codeblock (with a `/verify-confirm` header and a hidden `<!-- model-verification-confirm -->` marker) for additional verifiers to use as their certification comment.
  - New workflow `.github/workflows/model-verification.yml` that triggers only when an issue comment starts with `/verify-confirm` (and contains the template marker) or `/verify-force`, tallies distinct GitHub users (issue author + valid `/verify-confirm` commenters), and opens a PR promoting the model to `Verified = true` once two distinct users have certified it. `/verify-force` is restricted to `OWNER`/`MEMBER`/`COLLABORATOR`.
  - New helper `tools/Update-ModelVerified.ps1` that locates the matching `new AIModelCapabilities { Model = "..." }` block in `src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs` and flips `Verified = false` to `Verified = true` (or inserts the flag when missing).

- Automated provider model discovery CI (OpenRouter as single source of truth):
  - New workflow `.github/workflows/chore-update-provider-models.yml` that runs weekly (Sundays 05:00 UTC) and on `workflow_dispatch`. It queries OpenRouter's unified `/models` endpoint for each supported provider, compares the returned metadata with the static declarations in `*ProviderModels.cs`, and opens a PR that both auto-inserts new models and marks disappeared/expiring models as `Deprecated = true`.
  - New composite action `.github/actions/ai/fetch-models/action.yml` that invokes `tools/Update-ProviderModels.ps1` with an OpenRouter API key, passing the provider name and `update-file` flag.
  - New PowerShell tool `tools/Update-ProviderModels.ps1` invoked by the workflow.  Accepts `-Provider`, `-OpenRouterApiKey` (OpenRouter key), and an optional `-TargetFile`.  It queries OpenRouter, filters by provider prefix, maps `architecture.input_modalities`/`output_modalities` and `supported_parameters` to `AICapability` flags, auto-generates full `AIModelCapabilities` blocks for new models (with `ContextLimit`, `Verified=false`), and marks models with `expiration_date` < 1 year or absent from OpenRouter as `Deprecated = true`.  `Rank` values are auto-computed from OpenRouter `created` timestamp (newer models rank higher) and output pricing (cheapest first, considering `pricing.completion`, `pricing.image`, and `pricing.audio_output`).  Existing model capabilities, context limits, and ranks are refreshed on every run.  Emits a structured JSON report containing `newModels`, `deprecatedModels`, and `unchangedModels`.

### Changed

- **Infrastructure**: Migrated critical fixes including provider stability improvements, timeout policy refinements, and streaming adapter fixes
- **Thread Safety**: `ProviderManager` now uses `ConcurrentDictionary` for all provider collections to improve concurrent access safety
- **Code Quality**: Applied consistent code style with `this.` qualifiers and `ConfigureAwait()` patterns across Infrastructure and Providers

### Fixed

- `ProviderManager` now exposes `IsInfrastructureReady` flag to signal when provider infrastructure initialization completes
- All AI providers (Anthropic, DeepSeek, MistralAI, OpenAI, OpenRouter) received stability improvements and extended known list of models

### Deprecated

- **Anthropic**: marked deprecated `claude-opus-4-5`, `claude-sonnet-4-5`, `claude-sonnet-4-5-20250929`, `claude-haiku-4-5`, `claude-haiku-4-5-20251001` (superseded by 4-6 / 4-7 series).
- **DeepSeek**: `deepseek-chat` and `deepseek-reasoner` flagged `Deprecated = true` (DeepSeek docs state both will be deprecated; they alias `deepseek-v4-flash` non-thinking/thinking modes).
- **OpenAI**: `gpt-4o-mini-tts` marked deprecated per OpenAI docs.

## [1.4.2-alpha] - 2026-03-14

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

This release successfully passes macOS verification of provider hashes! This is the first macOS compatible release available through Yak/Rhino package manager. Thank you [nofcfy-fanqi](https://github.com/nofcfy-fanqi) for testing!

### Fixed

- Fixed `Id` missing GhJSON validation error in script tools.
- (automatically added) Fixes "Compatibility with Mac" ([#263](https://github.com/architects-toolkit/SmartHopper/issues/263)).

## [1.4.1-alpha] - 2026-03-09

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)
- [nofcfy-fanqi](https://github.com/nofcfy-fanqi)

----

### Added

- **Contributors Workflow**: Added automated GitHub workflow (`chore-update-contributors.yml`) to maintain the contributors section in CHANGELOG.md

### Changed

- **Provider Security & Verification**:
  - Replaced boolean "hard integrity check" with a three-tier mode (Soft/Hard/Strict) selectable in Providers settings; installs migrate automatically to the new modes.
  - SHA-256 hash verification now covers Windows and macOS with dual-runner CI hashes and DEBUG auto-switch to Soft Check for smoother local development.

- **UX & Components**:
  - Renamed `AIScriptGenerator` component to `AIScriptGenerate` for consistent AI script naming.
  - Tuned dialog sizing, text wrapping, and integrity-check descriptions for clearer messaging.

- **Tooling & Automation**:
  - Enhanced `Change-SolutionVersion.ps1` with explicit version parsing and help output for reliable version bumps.
  - Expanded `.gitignore` to exclude all local libraries (beyond Rhino).
  - Improved pre-commit hook with selective staging and safer password handling, added post-commit hook to auto-update `InternalsVisibleTo`, and added a GitHub workflow to anonymize the public key on protected branches.

### Fixed

- fix(ci): skip `Update-InternalsVisibleTo` step on macOS runners since `sn.exe` (Strong Name tool) is Windows-only; assemblies still strong-name signed with SNK file
- fix(infrastructure): reduce provider hash verification timeout from 10s to 5s for faster offline detection and improved Settings dialog responsiveness
- fix(infrastructure): add network availability check in `ProviderHashVerifier` to skip hash fetch attempts when offline, preventing unnecessary delays
- fix(infrastructure): implement 15-minute manifest caching in `ProviderHashVerifier` using `ConcurrentDictionary` for thread safety, and centralize cache operations in `ReadHashManifest` method
- fix(core): eliminate race condition in `ComponentStateManager.ProcessTransitionQueue()` where `isTransitioning` flag was cleared before event firing, potentially allowing concurrent queue processing on macOS. The flag now remains true until after all events are fired, preventing out-of-order event processing and concurrent event handler execution.
- fix(tools): set GhJSON component `Id = 1` when `InstanceGuid` is null in `script_generate` and `script_edit` to satisfy GhJSON.Core validation requiring at least one identifier
- fix(tools): add `ParseJsonObjectFromResponse` to handle AI responses wrapped in markdown code blocks or non-JSON formatting in `script_generate` and `script_edit`
- fix(infrastructure): improve `AIProvider.CallApi()` error messages for non-JSON API responses (e.g., HTML error pages from proxies)
- fix(infrastructure): GitHub Pages deployment now correctly places `latest.json` and `versions.json` in the `hashes/` subdirectory instead of site root, fixing 404 errors when ProviderHashVerifier and the web UI attempt to fetch manifest files
- fix(macOS): address mac compatibility issues (deadlock risk, GhJSON validation, and JSON parsing edge cases) tracked in [#389](https://github.com/architects-toolkit/SmartHopper/issues/389)
- fix: additional stability and compatibility fixes tracked in [#395](https://github.com/architects-toolkit/SmartHopper/issues/395) and [#393](https://github.com/architects-toolkit/SmartHopper/issues/393)

## [1.4.0-alpha] - 2026-02-15

Many thanks to the following contributors to this release:

- [nofcfy-fanqi](https://github.com/nofcfy-fanqi) **First contribution!**
- [nof2504](https://github.com/nof2504) **First contribution!**
- [marc-romu](https://github.com/marc-romu)

### Added

- **Provider Hash Verification**: Added SHA-256 hash verification system for AI provider DLLs to enhance security on all platforms
  - New "Verify Providers Hash" menu item in SmartHopper menu to manually verify provider integrity
  - Comprehensive verification dialog showing verification status, local vs expected hashes, and detailed results
  - Automatic hash generation during release workflow with public hash repository for verification
  - Multi-tier verification with graceful degradation when hashes are unavailable
- **Enhanced About Dialog**: Improved About dialog to automatically display current SmartHopper version and platform information using the new VersionHelper class

### Fixed

- **macOS Compatibility**: Improved cross-platform compatibility for macOS users
  - Provider loading now works on non-Windows platforms with appropriate security warnings (skip Authenticode signature verification where `X509Certificate.CreateFromSignedFile` is not supported)
  - URL handling fixed to prevent incorrect file:// URI generation by restricting `BuildFullUrl` absolute URI detection to HTTP/HTTPS schemes
  - Component state management updated to fire `ComponentStateManager` transition events outside `stateLock` to prevent deadlocks caused by re-entrant lock acquisition in event handlers
- **Settings**:
  - Fixed first initialization is created using EncryptationVersion 2 by default which stores a local hash for secrets encryptation

### Security

- Enhanced provider security with SHA-256 hash verification system to protect against tampered provider DLLs
- Platform-appropriate security measures: Authenticode + hash verification on Windows, only hash verification on macOS

### Known Issues

- bug(ui): `WebChatDialog` (CanvasButton chat window) crashes on macOS with `NSInvalidArgumentException` because Eto.Forms' `WKWebViewHandler.LoadHtml()` calls `WKWebView.LoadFileUrl()` with an `https://` base URI (`https://smarthopper.local/`), which only accepts `file://` URLs

## [1.3.0-alpha] - 2024-02-08

### Changed

- Added annual automation to update copyright years in `src/**/*.csproj` and normalize C# license headers (workflow: `chore-update-copyright-year.yml`, script: `tools/Update-CopyrightYear.ps1`).
- GhJSON API Simplification:
  - Refactored all AI tools to use organized ghjson-dotnet façade classes exclusively, removing deep namespace dependencies.
  - All SmartHopper code now imports only `GhJSON.Core` and `GhJSON.Grasshopper` (no `GhJSON.Core.Models.*`, `GhJSON.Grasshopper.Serialization.*`, etc.).
  - Removed legacy `ScriptParameterSettingsParser.cs` from SmartHopper (now in ghjson-dotnet façade).
  - **Serialization options** now use `GhJsonGrasshopper.Options.Standard()`, `.Optimized()`, and `.Lite()` factory methods.
  - **Script components** now use `GhJsonGrasshopper.Script.CreateGhJson()`, `.GetComponentInfo()`, `.DetectLanguageFromGuid()`, `.NormalizeLanguageKeyOrDefault()`.
  - **Document operations** now use `GhJson.CreateDocument()`, `GhJson.Merge()`, `GhJson.Parse()`, `GhJson.Fix()`, `GhJson.IsValid()`, `GhJson.Serialize()`.
  - **Runtime data** extraction now uses `GhJsonGrasshopper.ExtractRuntimeData()` instead of deep serializer access.
  - Tool-specific changes:
    - `gh_get`: Delegates connection depth expansion and connection trimming to `GhJsonGrasshopper.GetWithOptions()`; uses `GhJsonGrasshopper.Options.*()` factories and `GhJsonGrasshopper.ExtractRuntimeData()`.
    - `gh_put`: Delegates GhJSON placement to `GhJsonGrasshopper.Put()` and uses `PutOptions.PreserveExternalConnections` for edit-mode external wiring preservation; uses `GhJson.Parse()`, `GhJson.Fix()`, `GhJson.IsValid()`.
    - `gh_merge`: Uses `GhJson.Merge()` façade instead of direct `GhJsonMerger` access.
    - `gh_tidy_up`: Uses `GhJsonGrasshopper.Options.Standard()`.
    - `script_edit`, `script_generate`: Use `GhJsonGrasshopper.Script.*` façade methods.
    - `gh_connect`: Delegates canvas wiring to `GhJsonGrasshopper.ConnectComponents()`.
- Changed `ISelectingComponent` to use `IGH_DocumentObject` instead of `IGH_ActiveObject` to support scribble selection.

## [1.2.4] - 2024-02-08

### Changed

- Bump to stable version

## [1.2.4-alpha] - 2026-01-11

### Fixed

- AIStatefulAsyncComponentBase: Fixed issue where `context_usage_percent` was not being set correctly.

## [1.2.3-alpha] - 2026-01-11

### Added

- Context Management:
  - Added `ContextLimit` property to `AIModelCapabilities` for storing model context window sizes across all providers (Anthropic, DeepSeek, OpenAI, OpenRouter, MistralAI).
  - Added `SummarizeSpecialTurn` factory for creating conversation summarization special turns.
  - Added automatic context tracking in `ConversationSession` with percentage calculation.
  - Added pre-emptive summarization when context usage exceeds 80% of model limit.
  - Added context exceeded error detection and automatic summarization with retry.
  - Added graceful error handling when summarization fails to reduce context size.
  - Added context limits for all MistralAI models (128K for most, 40K for Magistral, 32K for Voxtral).
- Metrics:
  - Added `LastEffectiveTotalTokens` field to `AIMetrics` for accurate context usage percentage calculation.
- WebChat Debug:
  - Added debug Update button to refresh chat view from conversation history.
  - Added DOM synchronization functionality to compare cached HTML hashes and update only changed messages.
- GhJSON canvas tools:
  - Added `gh_get_start` / `gh_get_start_with_data` tools to retrieve start nodes (components with no incoming connections) with optional runtime data.
  - Added `gh_get_end` / `gh_get_end_with_data` tools to retrieve end nodes (components with no outgoing connections) with optional runtime data, providing a wide view of definition outputs.

### Changed

- Context Management:
  - Conversation summaries now use `AIAgent.Summary` instead of `AIAgent.Assistant`, preventing extra assistant messages in chat UI.
  - Providers automatically merge `Summary` interactions with the system prompt using format: `System prompt\n---\nThis is a summary of the previous conversation:\n\nSummary`.
  - WebChat UI renders `Summary` interactions as collapsible elements with distinct blue styling, similar to tool/system messages.
- GhJSON Serialization:
  - `PersistentData` and `VolatileData` properties are now excluded from serialization at the `GhJsonSerializer` level to prevent encrypted/binary strings from being included in GhJSON output, ensuring LLM-safe and token-efficient results by default.
  - Runtime data remains available via the separate `ExtractRuntimeData` method for `_with_data` tools (e.g., `gh_get_selected_with_data`).
- Debug Logging:
  - Updated `ConversationSession` debug history file to preserve previous conversations when summarization occurs.
  - Added `SUMMARIZED` marker in debug logs to clearly separate pre-summary and post-summary history.

### Fixed

- Context Management:
  - Fixed context usage percentage not appearing consistently in debug logs and WebChat UI metrics by caching it at the `ConversationSession` level and applying it to aggregated metrics.
- GhJSON canvas helpers and chat:
  - Standardized node classification terminology from `input/output/processing/isolated` to `startnodes/endnodes/middlenodes/isolatednodes` across `gh_get` filters and the `GhGetComponents` Grasshopper component UI.
  - Updated `instruction_get` canvas guidance to recommend `gh_get_start`/`gh_get_end` (and their `_with_data` variants) for obtaining a wide view of data sources and outputs.
  - Strengthened the `CanvasButton` default system prompt to always call `instruction_get` for canvas queries, improving tool selection for providers like `mistral-small`.
- GhJSON Serialization:
  - Fixed `gh_get` tool incorrectly using `Optimized` serialization context (which excludes `PersistentData`) instead of `Standard` context, breaking `gh_put` restoration of internalized parameter values. Now uses `Standard` format by default to preserve all data needed for restoration, only switching to `Optimized` when runtime data is explicitly included (where persistent values are redundant).

## [1.2.2-alpha] - 2025-12-27

### Added

- Chat:
  - Added `instruction_get` tool (category: `Instructions`) to provide detailed operational guidance to chat agents on demand.
  - Simplified the `CanvasButton` default assistant system prompt to reference instruction tools instead of embedding long tool usage guidelines.

### Changed

- Infrastructure:
  - Extracted duplicated streaming processing logic into shared `ProcessStreamingDeltasAsync` helper method in `ConversationSession`, reducing code duplication by ~80 lines.
  - Added `GetStreamingAdapter()` to `IAIProvider` interface with caching in `AIProvider` base class, replacing reflection-based adapter discovery.
  - Added `CreateStreamingAdapter()` virtual method for providers to override; updated OpenAI, DeepSeek, MistralAI, Anthropic, and OpenRouter providers.
  - Added `NormalizeDelta()` method to `IStreamingAdapter` interface for provider-agnostic delta normalization.
  - Simplified streaming validation flow in `WebChatDialog.ProcessAIInteraction()` - now always attempts streaming first, letting `ConversationSession` handle validation internally.
  - Added `TurnRenderState` and `SegmentState` classes to `WebChatObserver` for encapsulated per-turn state management.
  - Reduced idempotency cache size from 1000 to 100 entries to reduce memory footprint.
  - Promoted `StatefulComponentBaseV2` to the default stateful base by renaming it to `StatefulComponentBase`.
- Chat UI:
  - Optimized DOM updates with a keyed queue, conditional debug logging, and template-cached message rendering with LRU diffing to cut redundant work on large chats.
  - Refined streaming visuals by removing unused animations and switching to lighter wipe-in effects, improving responsiveness while messages stream.

### Fixed

- DeepSeek provider:
  - Fixed `deepseek-reasoner` model failing with HTTP 400 "Missing reasoning_content field" error during tool calling. The streaming adapter was not propagating `reasoning_content` to `AIInteractionToolCall` objects, causing the field to be missing when the conversation history was re-sent to the API.
  - Fixed duplicated reasoning display in UI when tool calls are present. Reasoning now only appears on tool call interactions (where it's needed for the API), not on empty assistant text interactions.

- Chat UI:
  - Fixed user messages not appearing in the chat UI. The `ConversationSession.AddInteraction(string)` method was not notifying the observer when user messages were added to the session history.

- Tool calling:
  - Improved `instruction_get` tool description to explicitly mention required `topic` argument. Some models (MistralAI, OpenAI) don't always respect JSON Schema `required` fields but do follow description text.

- Chat UI:
  - Reduced WebChat dialog UI freezes while dragging/resizing during streaming responses by throttling DOM upserts more aggressively and processing DOM updates in smaller batches.
  - Mitigated issue [#261](https://github.com/architects-toolkit/SmartHopper/issues/261) by batching WebView DOM operations (JS rAF/timer queue) and debouncing host-side script injection/drain scheduling.
  - Reduced redundant DOM work using idempotency caching and sampled diff checks; added lightweight JS render perf counters and slow-render logging.
  - Improved rendering performance using template cloning, capped message HTML length, and a transform/opacity wipe-in animation for streaming updates.
  - Further reduced freezes while dragging/resizing by shrinking update batches and eliminating heavy animation paths during active user interaction.

- Context providers:
  - Fixed `current-file_selected-count` sometimes returning `0` even when parameters were selected by reading selection on the Rhino UI thread and adding a robust `Attributes.Selected` fallback.
  - Added selection breakdown keys: `current-file_selected-component-count`, `current-file_selected-param-count`, and `current-file_selected-objects`.

## [1.2.1-alpha] - 2025-12-07

### Added

- Script tools:
  - Added `script_generate_and_place_on_canvas` wrapper tool that combines `script_generate` and `gh_put` in a single call, reducing token consumption by eliminating the need for the AI to call both tools separately.
  - Moved `script_generate` tool to `Hidden` category (only `script_generate_and_place_on_canvas` is now visible to chat agents).
- `gh_get` tools:
  - Added `gh_get_selected_with_data` tool that returns selected components with their runtime/volatile data (actual values flowing through outputs).
  - Added `gh_get_by_guid_with_data` tool that returns specific components by GUID with their runtime/volatile data.
  - Added `includeRuntimeData` parameter to the base `gh_get` tool for optional runtime data extraction.
  - Runtime data includes total item count, branch structure, and sample values for each parameter output.
  - Added `gh_get_errors_with_data` tool that returns only errored components with their runtime/volatile data, useful for debugging broken definitions.
- `gh_put` tool:
  - Added `instanceGuids` array to the tool result containing the actual GUIDs of placed components (useful for subsequent queries).

### Fixed

- Script tools:
  - Fixed `script_generate_and_place_on_canvas` returning incorrect `instanceGuid`. The tool was returning the in-memory GUID from `script_generate` instead of the actual GUID assigned by Grasshopper when the component was placed on canvas. Now returns the real `instanceGuid` from `gh_put` result.
- GhJSON validation:
  - Fixed `GHJsonAnalyzer.Validate` to treat missing `connections` property as an empty array instead of an error. Components without connections are now valid and won't trigger "'connections' property is missing or not an array" errors.
- Chat UI:
  - Fixed critical bug where two identical user messages were collapsed into a single message in the UI. The root cause was that user messages didn't have a unique `TurnId`, causing identical messages to generate the same dedup key and replace each other. Now each user message receives a unique `TurnId` via `InteractionUtility.GenerateTurnId()`.
- Conversation session:
  - Fixed TurnId inconsistency where `ToolResult` interactions were getting new TurnIds instead of inheriting from their originating `ToolCall`. The conditional check `if (string.IsNullOrWhiteSpace(toolInteraction.TurnId))` was never true because `AIBodyBuilder.EnsureTurnId()` had already assigned a new TurnId during tool execution. Changed to unconditional assignment to ensure correct turn-based metrics aggregation.

## [1.2.0-alpha] - 2025-12-06

### Added

- Dialog canvas link visualization:
  - Added `DialogCanvasLink` utility class that draws a visual connection line from a dialog to a linked Grasshopper component, similar to the script editor anchor.
  - `StyledMessageDialog` methods (`ShowInfo`, `ShowWarning`, `ShowError`, `ShowConfirmation`) now accept optional `linkedInstanceGuid` and `linkLineColor` parameters to enable canvas linking.
  - When a dialog is linked to a component, a bezier curve with an anchor dot is drawn from the component to the dialog window.
- Component replacement mode:
  - Added "Edit Mode" input parameter to `GhPutComponents` for component replacement functionality.
  - When Edit Mode is enabled and GhJSON contains valid instanceGuids that exist on canvas, users are prompted via `StyledMessageDialog` to choose between replacing existing components or creating new ones.
  - The `gh_put` AI tool now accepts an optional `editMode` parameter to support component replacement.
  - Replacement components preserve original `InstanceGuid` and exact canvas position.
  - Undo support included for component replacement operations.
- GhJSON helpers:
  - Added `GhJsonHelpers` utility class with methods for applying pivots and restoring InstanceGuids on deserialized components.
- GhJSON merge:
  - Added `GhJsonMerger` utility to merge two GhJSON `GrasshopperDocument` instances, with the target document taking priority on component GUID conflicts and automatic ID remapping for connections and groups.
  - Introduced `gh_merge` AI tool to merge two arbitrary GhJSON strings using `GhJsonMerger`, returning the merged GhJSON together with merge statistics (components/connections/groups added and deduplicated).
  - Added `GhMergeComponents` Grasshopper component ("Merge GhJSON") to merge two GhJSON documents directly on the canvas, exposing merged GhJSON and basic merge counters as outputs.
- Script tools:
  - Introduced GhJSON-based AI tools `script_generate` and `script_edit` for Grasshopper script components.
  - All script tools now validate GhJSON input/output via `GHJsonAnalyzer.Validate` and use `ScriptComponentFactory` for component construction.
  - Added `script_edit_and_replace_on_canvas` wrapper tool that combines `script_edit` and `gh_put` in a single call, reducing token consumption by eliminating the need for the AI to call both tools separately.
  - Enhanced `script_generate` and `script_edit` tools to support all parameter modifiers: `dataMapping` (Flatten/Graft), `reverse`, `simplify`, `invert`, `isPrincipal`, `required`, and `expression` for inputs; `dataMapping`, `reverse`, `simplify`, `invert` for outputs.
  - Added `ScriptCodeValidator` utility for detecting non-RhinoCommon geometry libraries in AI-generated scripts (e.g., System.Numerics, UnityEngine, numpy, shapely) with automatic self-correction prompts.
  - Enhanced `script_generate` and `script_edit` system prompts with explicit RhinoCommon geometry requirements and language-specific guidance (Python/IronPython/C#/VB.NET) including import templates and type mappings.
  - Added validation retry loop to `script_generate` and `script_edit` that detects invalid geometry patterns and re-prompts the AI with correction instructions (up to 2 retries).
  - Clarified that output parameters do NOT have 'access' settings and documented proper list output patterns per language (Python 3 requires .NET `List[T]`, IronPython can use Python lists, C# uses `List<T>`, VB.NET uses `List(Of T)`).
- `gh_get` tool:
  - Added `categoryFilter` parameter and extended category-based filtering from components to all document objects.
- Model compatibility badges:
  - Added "Not Recommended" badge (orange octagon with exclamation mark) displayed when the selected model is discouraged for the AI tools used by a component.
  - Added `DiscouragedForTools` property to `AIModelCapabilities` to specify tool names for which a model is not recommended.
  - Added `UsingAiTools` property to `AIStatefulAsyncComponentBase` allowing components to declare which AI tools they use.
  - Added `EffectiveRequiredCapability` property that merges component's required capability with capabilities required by its AI tools.
  - The "Not Recommended" badge suppresses the "Verified" badge when active (priority: Replaced > Invalid > NotRecommended > Verified).

### Changed

- Components UI:
  - AI-selecting stateful components now use combined attributes that show both the "Select" button and AI provider badges, with the button rendered above the provider strip.
  - Selecting components now use the dialog link line color for hover highlights and draw a connector line from the combined selection center to the "Select" button.
- Script components:
  - `AIScriptGenerateComponent` now orchestrates `script_generate` / `script_edit` together with `gh_get` / `gh_put` instead of the legacy `script_generator` tool, and exposes `GhJSON`, `Guid`, `Summary`, and `Message` outputs only.
  - `AIScriptGenerateComponent` and `AIScriptReviewComponent` no longer expose a `Guid` input; the target component is always provided via the selecting button.
  - Removed the monolithic `script_generator` AI tool in favor of smaller, focused tools that operate purely on GhJSON.
  - Updated `AIScriptGenerateComponent` and `AIScriptReviewComponent` to support processing multiple inputs in parallel.
  - Renamed `script_fix` tool to `script_review` to better reflect its review-focused behavior.
  - `script_generate` no longer includes a pre-placement `instanceGuid` in its tool result; instance GUIDs are only exposed via `script_generate_and_place_on_canvas` / `gh_put` using the real canvas instance GUIDs.
- `GhJsonDeserializer`:
  - Changed deserialization logic to default the UsingStandardOutputParam property to true when ShowStandardOutput is not present in the GhJSON ComponentState.
- Providers:
  - Added new Claude Opus 4.5 model to the Anthropic provider registry.
  - OpenRouter provider: added structured output support via `response_format: json_schema` / `structured_outputs` for JsonOutput requests and now populates `finish_reason` and `model` in metrics for chat completions.
- Script parameter modifier tools:
  - Moved all script parameter modifier AI tools to the `NotTested` category to clarify their experimental status.
- Component icons:
  - Updated McNeel forum and script component icons to outlined variants for better visual consistency.
  - Added `ghmerge` icon and refreshed `ghget` / `ghput` icons to align with the new GhJSON merge workflows.
- Canvas button:
  - Improved the default SmartHopper assistant prompt used by `CanvasButton` to guide users toward in-viewport scripting workflows and avoid unnecessary external code blocks or testing patterns, resulting in a smoother first-time UX.
- Script tools:
  - `script_review` now augments its system prompt with language-specific Grasshopper scripting guidance (Python/IronPython/C#/VB.NET) via `ScriptCodeValidator`, based on the detected script language.
  - Centralized script language normalization in `ScriptComponentFactory.NormalizeLanguageKeyOrDefault` and wired `script_generate` to use it when building prompts, ensuring consistent handling of language keys such as "python3" and "csharp".

### Fixed

- Chat UI metrics display:
  - Fixed metrics aggregation to show total token consumption per turn (from user message to next user message) instead of just the last message's metrics.
  - Added `GetTurnMetrics(turnId)` method to `ConversationSession` to aggregate metrics for all interactions in a turn.
  - Fixed tool results not inheriting TurnId from their corresponding tool calls, which caused incorrect turn grouping.
- Web chat dialog visibility:
  - `WebChatDialog` is now created as an owned tool window of the Rhino main Eto window and hidden from the taskbar, so it follows Rhino/Grasshopper focus and stays on top of Rhino while the application is active.
  - Confirmation dialogs shown via `StyledMessageDialog` (for example when replacing components with `gh_put` in edit mode) still appear above the chat dialog, but closing them no longer leaves the chat window hidden behind other Rhino/Grasshopper windows.
- `gh_put` tool:
  - Fixed infinite loop when using `GhPutComponents` with replacement mode. The `NewSolution` call inside the tool caused re-entrancy when the component blocked with `.GetAwaiter().GetResult()`, which pumps Windows messages and allows the new solution to start immediately.
  - Fixed "object expired during solution" error when replacing components. Removed the document disable/enable logic which was causing components to be in an invalid state. Uses `IsolateObject()` to properly clean up connections before component removal.
- Script tools:
  - Updated `script_generate` and `script_edit` tool schemas to require `nickname`, fixing crashes with OpenAI structured-output mode.

## [1.1.1-alpha] - 2025-11-24

### Changed

- Providers:
  - Added explicit Claude 3.x/4.x dated model identifiers to the Anthropic provider registry while keeping shorthand model names.
  - Switched the default Anthropic text/tool/json model to `claude-haiku-4-5`.
  - Added structured-output beta support for Anthropic Sonnet 4.5 / Opus 4.1 models and restricted Text2Json capability to those models.
  - Updated OpenAI provider models to include GPT-5.1 series models.

## [1.1.0-alpha] - 2025-11-23

### Added

- **VB Script support**: VB Script components now serialize and deserialize their three code sections, custom parameters, and settings correctly.
- **Parameter and script AI tools**: Added `gh_parameter_*` and `script_parameter_*` tools to flatten, graft, simplify, reverse, and manage script/component parameters via AI.
- **McNeel forum tools**: Enhanced `mcneel_forum_search`, `mcneel_forum_post_get`, and added `mcneel_forum_post_summarize` for AI-powered forum summaries.
- **Knowledge components**: New components for McNeel forum and web sources to search, retrieve, and summarize content directly in Grasshopper.
- **Web page reading**: `web_generic_page_read` now produces clean text for Wikipedia, Discourse, GitHub/GitLab files, and Stack Exchange.
- **Property management**: Introduced a new property-management system for more reliable GhJSON serialization of component parameters, values, and metadata.
- **GhJSON optimization**: Reduced irrelevant data in serialized Grasshopper documents, producing smaller, cleaner JSON.
- **GhJSON schema v2**: Improved component schema with consolidated `value` properties and extended parameter settings. This is now the default format for `gh_get` and `gh_put` and may affect saved GhJSON files.
- **Component generation and wiring**: New `gh_generate` and `gh_connect` AI tools to create component specifications and wire components automatically.
- **Script generator**: Added `script_generator` AI tool to create or edit Grasshopper script components from natural-language instructions.
- **Rhino 3DM analysis**: New `rhino_read_3dm` and `rhino_get_geometry` AI tools to analyze .3dm files and extract geometry from the active document.
- **Selection persistence**: Components based on `SelectingComponentBase` now remember their selected objects when saving and loading Grasshopper files.
- **CI/CD**: Added hotfix workflow system and release workflow documentation for emergency and regular releases.

### Changed

- **Renamed AI tools**: `gh_toggle_preview`, `gh_toggle_lock`, `gh_lock_selected`, `gh_unlock_selected`, `gh_hide_preview_selected`, and `gh_show_preview_selected` now use the `gh_component_*` prefix.
- **Script component output**: Script components no longer serialize the standard `out` parameter as a regular output; its visibility is controlled by a new `showStandardOutput` property to keep signatures stable.
- **JSON serialization**: Simple property values now serialize directly, empty strings are omitted, and document metadata includes schema version, Rhino version, and plugin dependencies.
- **Model validation**: Unregistered model names are now accepted, letting you use any provider model even if it is not yet in the built-in registry.
- **Error handling**: Errors from AI calls and tool execution are now surfaced more consistently in `AIReturn` and `ConversationSession`.
- **AI tool descriptions**: Improved descriptions and added specialized wrapper tools for common queries and actions.
- **CI/CD**: Extended PR validation and test workflows to cover `hotfix/**` and `release/**` branches; removed the obsolete `user-security-patch.yml` workflow.

### Removed

- **Legacy property manager**: Removed the old `PropertyManager` and migrated serialization/placement to the new `PropertyManagerV2`.
- **Legacy script tools**: Replaced `script_new` and `script_edit` with the unified `script_generator` tool; removed the corresponding `AIScriptNewComponent` and `AIScriptEditComponent` components.

### Fixed

- **Data tree processing**: Fixed `GetBranchFromTree` and `BranchFlatten` to handle single-path and flat trees correctly.
- **Model badges**: Invalid-model badge now appears correctly when a provider has no capable model instead of showing a replacement badge ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332)).
- **Chat error display**: Provider errors such as HTTP 400 and token-limit exceeded now surface correctly in the WebChat UI ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334)).
- **Rectangle serialization**: Rectangle3d round-trips correctly using a center-based format.
- **JSON round-trip fixes**: Improved serialization/deserialization for stand-alone parameters, persistent data, parameter modifiers, group InstanceGuids, and component connections (including index-based matching).
- **Script component stability**: Fixed null-reference errors in `gh_get` for script components and preserved parameter modifiers during round-trip serialization.
- **Smart placement**: `gh_put` now preserves relative pivots when present or auto-layouts components with `DependencyGraphUtils` when absent.
- **Type hints**: Default/generic "object" type hints are omitted, and generic type hints (`DataTree<Object>`, `List<Curve>`) are handled without exceptions.
- **Script editor freeze**: Fixed the script editor freezing issue when using the script edit tool ([#209](https://github.com/architects-toolkit/SmartHopper/issues/209)).

## [1.0.1-alpha] - 2025-10-13

### Changed

- Model capability validation now bypasses checks for unregistered models, allowing users to use any model name even if not explicitly listed in the provider's model registry.
- Centralized error handling in AIReturn and tool calls.
- Accurately aggregate metrics in Conversation Session. Cases with multiple tool calls, multiple interactions, etc. Calculate completion time per interaction.
- Improved AI Tool descriptions with better guided instructions. Also added specialized wrappers for targeted tool calls (gh_get_selected, gh_get_errors, gh_get_locked, gh_get_preview_off, gh_get_preview_on, gh_get_by_guid, gh_lock_selected, gh_unlock_selected, gh_hide_preview_selected, gh_show_preview_selected, gh_group_selected, gh_tidy_up_selected).
- Enhanced `list_filter` tool prompts to explicitly distinguish between indices (positions/keys) and values (item content), and expanded capabilities to support filtering, sorting, reordering, selecting, and other list manipulation operations based on natural language criteria.
- Added more predefined models in the provider's database.

### Fixed

- Fixed model badge display: show "invalid model" badge when provider has no capable model instead of "model replaced" ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332)) ([#329](https://github.com/architects-toolkit/SmartHopper/issues/329)).
- Fixed provider errors (e.g., HTTP 400, token limit exceeded) not surfacing to WebChat UI: `ConversationSession` now surfaces `AIInteractionError` from error AIReturn bodies to observers before calling `OnError`, ensuring full error messages are displayed in the chat interface ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334)).
- Fixed `list_filter` tool automatically sorting and deduplicating indices, which prevented reordering and expansion operations from working correctly. Now preserves both order and duplicates as returned by the AI ([#335](https://github.com/architects-toolkit/SmartHopper/issues/335)).

## [1.0.0-alpha] - 2025-10-11

### Added

- **Canvas assistant button**: Optional top-right canvas button to open the SmartHopper assistant, respecting the `EnableCanvasButton` setting.
- **Document context**: New `FileContextProvider` exposes document and selection metadata to AI conversations.
- **Conversation orchestration**: New `ConversationSession` service supports multi-turn flows, tool passes, special turns, and policy hooks.
- **Tool validation**: Tool calls are validated against registered tools, JSON schemas, and model capabilities before execution.
- **Streaming support**: Shared streaming adapter with idle-timeout SSE reading, live reasoning display, and provider-specific completion detection for OpenAI, Anthropic, MistralAI, and DeepSeek.
- **New providers**: Added Anthropic and OpenRouter providers, plus refreshed provider icons.
- **Model badges**: Components show badges for verified, deprecated, incompatible, or auto-replaced models before running.
- **Capability-based model selection**: `ModelManager` selects the best model per capability and checks streaming support.
- **WebChat UX**: Improved chat UI with auto-scroll, collapsible messages, better prompts, and default context (time, environment, selection).
- **Core models**: Introduced `AIRequest`, `AIReturn`, `AIBody`, and related models for clearer request/response handling.
- **Documentation**: Added summary documentation under `docs/`.
- **Tests**: Added tests for Context Manager, Model Manager, and data-tree processing.

### Changed

- **Settings UI**: Reorganized settings dialog into tabs, including SmartHopper Assistant and Trusted Providers sections.
- **Model selection**: Centralized capability-first model selection behind the provider interface.
- **Authentication**: Improved API key encryption and migrated to provider-internal key resolution so keys never appear in requests or logs.
- **About dialog**: Updated to list the currently supported AI providers.
- **Provider settings**: Disabled streaming controls for DeepSeek and OpenRouter until streaming support is available.
- **AIChat component**: Unified snapshot management so chat history and metrics stay consistent.
- **Toolbox**: Reorganized Grasshopper component categories and disabled experimental tools from the default build.
- **mcneel_forum_search**: Simplified to return raw results; use `mcneel_forum_post_summarize` for summaries.
- **Infrastructure**: Refactored `SmartHopper.Infrastructure` for clarity and standardized data-tree processing pipelines.

### Fixed

- **Chat rendering**: Fixed tool-call/result ordering, duplicate greetings, and reasoning-only message display in WebChat.
- **Reasoning metrics**: Fixed missing reasoning tokens for DeepSeek, OpenAI o-series, and GPT-5 models.
- **Image viewer**: Fixed image saving errors and image generation output not reaching the viewer.
- **Model selection**: Fixed "Invalid model" when the manager returned a wildcard and corrected `DataCount` in metrics.
- **Tools**: Fixed `list_generate` output and corrected script component GUIDs.
- **Streaming**: Fixed indefinite streaming hangs and ensured final metrics appear in the chat UI.
- **Persistence**: Prevented crashes when opening Grasshopper files with safe, versioned output persistence.
- **Providers**: Fixed DeepSeek array schema handling, MistralAI streaming status, and Anthropic system message placement.
- **AIChat component**: Prevented NullReferenceException when closing chat without responses and fixed intermittent missing metrics.

### Deprecated

- `CustomizeHttpClientHeaders` is deprecated for authentication; use request-scoped headers instead.

### Removed

- Removed legacy per-provider model retrieval methods (`RetrieveAvailable`, `RetrieveCapabilities`, `RetrieveDefault`); use `RetrieveModels()`.
- Removed the `TemplateProvider`.
- Replaced `ContextKeyFilter` and `ContextProviderFilter` with a single `ContextFilter`.
- Removed manual greeting placeholder logic from WebChat.

### Security

- Prevented API key leakage by centralizing API key handling inside providers and keeping reserved headers out of request logs.

## [0.5.3-alpha] - 2025-08-20

### Fixed

- Fix incorrect json schema required fields in `script_new` tool ([#304](https://github.com/architects-toolkit/SmartHopper/issues/304)).

## [0.5.2-alpha] - 2025-08-12

### Fixed

- StackOverflowException on first run due to recursive lazy defaults in provider settings (`SmartHopperSettings.GetSetting`, `AIProvider.GetSetting<T>`), guarded with thread-static recursion checks.
- Readiness guard in `SmartHopperSettings.RefreshProvidersLocalStorage` to avoid partial refresh before all providers register settings UI.
- OpenAI and MistralAI providers now fall back to static model lists/capabilities on API errors or empty API responses, preventing empty model selections.

## [0.5.1-alpha] - 2025-07-30

### Added

- Settings parameter to enable/disable AI generated greeting in chat.

### Fixed

- Greeting generation was using stored settings models instead of the provider's default model. To solve it, now if `AIUtils.GetResponse` doesn't get a model, it will use the provider's default model.
- Components triggered with a Boolean Toggle (permanent true value) weren't calculating when the toggle was turned to true.
- Lazy default values in `AI Provider Settings` to prevent race conditions at initialization.
- Fixed "List length in list_generate was not met for long requests" ([#277](https://github.com/architects-toolkit/SmartHopper/issues/277)).

## [0.5.0-alpha] - 2025-07-29

### Added

- **Model Capability Management System**
  - Introduced `AIModelCapabilities` and `AIModelCapabilityRegistry` for centralized, persistent model capability tracking.
  - Added capability checking and filtering methods for models (e.g., `GetCapabilities`, `SetCapabilities`, `FindModelsWithCapabilities`).
  - Tool-specific capability validation now prevents execution with incompatible models.
  - Default model is now managed by the `AIModelCapabilityRegistry`. Multiple models can be defined as Default for a set of capabilities.
  - `AIStatefulAsyncComponentBase` will now try to use the default model if the specified model is not compatible with the tool.
- **Provider-Specific Capability Management**
  - MistralAI:
    - Added `MistralModelManager` for dynamic API-based capability detection and registration.
    - Models now update their capabilities by querying the `/v1/models/{model_id}` endpoint.
    - Automatic mapping of Mistral model features (chat, function calling, vision) to internal capability flags.
  - OpenAI & DeepSeek:
    - Static mapping for capabilities, with support for function calling, structured output, and image generation.
- **Image Generation Support**: Comprehensive AI image generation capabilities using OpenAI DALL-E models.
  - New `DefaultImgModel` property in `IAIProvider` interface for provider capability detection.
  - New `img_generate` AI tool with support for prompt, size, quality, and style parameters.
  - Enhanced `AIUtils.GenerateImage()` method with provider-agnostic image generation.
  - New `AIImgGenerateComponent` UI component in SmartHopper > Img category.
- Improvements in `AITools`:
  - New `includeSubcategories` parameter to `gh_list_categories` tool.
  - New `nameFilter`, `includeDetails` and `maxResults` parameters to `gh_list_components` tool.
  - New `ImageViewer` component to visualize output images on canvas and save them to disk.
- Added component existence and connection type validation to `GHJsonLocal`.
- **Settings management in AI Providers**:
  - New `SetSetting` method in `AIProvider` that let's providers set custom settings within the provider key.
  - New `RefreshCachedSettings` method in `AIProvider` to refresh their cached settings.

### Changed

- Renamed `AIProvider.InitializeSettgins` to `AIProvider.ResetCachedSettings`. Set visibility to `private`.

### Fixed

- `gh_put` now automatically fixes GhJSON.
- OpenAI tool filter not being applied properly.
- Fixed "Parsing error when output contains { }" ([#276](https://github.com/architects-toolkit/SmartHopper/issues/276)).

## [0.4.1-alpha] - 2025-07-23

### Added

- New `ProgressInfo` class to `StatefulAsyncComponentBase` to provide progress information to the UI. It allows to display a dynamic progress reporting which branch is being processed.

### Fixed

- Multiple fixes to `StatefulAsyncComponentBase`:
  - Fixed issue: Components now transition to "Done" state when opening files with existing results instead of "Run me!" ([#113](https://github.com/architects-toolkit/SmartHopper/issues/113))
  - Calculate changed inputs based on actual values, not on object instances, to prevent false positives when connecting new sources with same values.
  - Fixed issue: Stuck components when using Boolean toggle ([#260](https://github.com/architects-toolkit/SmartHopper/issues/260)).
  - Fixed issue: Output metrics not being set when using Boolean toggle.
- Fixed issue ([#208](https://github.com/architects-toolkit/SmartHopper/issues/208)): enabled compatibility with params in `gh_toggle_preview` tool.
- Fixed WebChatDialog not automatically closing when Rhino is closed.

## [0.4.0-alpha] - 2025-07-22

### Added

- New `RemoveLastMessage` method to `WebChatDialog` to remove messages from the chat history.
- Added GitHub Actions workflow for automatic milestone management, moves open issues/PRs to next appropriate milestone when a milestone is closed
- JSON wrapper in `OpenAI provider` to prevent passing incorrect JSON schemas to the API.
- JSON cleaner in `DeepSeek provider` to extract data from malformed responses with `enum` property.

### Changed

- Enhanced chat greeting with loading animation and improved model handling ([#255](https://github.com/architects-toolkit/SmartHopper/issues/255)), including:
  - New loading message while generating the greeting in `InitializeNewConversation`, with spinning animation.
  - Update `chat-script.js` with new function to remove messages.
  - Modified `AddMessageToWebView` to automatically add the loading class when finish reason from responses is "loading".
  - Modified `AIUtils.GetResponse` to use the default model if none is specified.
  - Modified `InitializeNewConversation` to use the default model for greeting generation (a fast and cheap model).
- Modified `WebChatDialog` constructor to pass the provider name to the base class.
- Modified the construction of `WebChatDialog` in `WebChatUtils.ShowWebChatDialog` to pass the provider name.
- Modified `GetModel` in `AIStatefulAsyncComponentBase` to use the provider's global model defined in settings if none is specified.
- Updated release workflow to automatically assign PRs to milestones
- Enhanced new-branch workflow with versioning guidance
- Using the `StripThinkTags` in all `DataProcessing` tools to avoid including reasoning text in the processed data.

### Fixed

- Fix incorrect model handling in `AIStatefulAsyncComponentBase`.
- Fixed certificate creation tests to handle CI environment constraints
- Updated `GhRetrieveComponents` to use the correct ai tool `gh_list_components` instead of `gh_get_available_components`
- Fixes "Missing required parameter: ‘response_format.json_schema' in text-list-generate with OpenAI provider" ([#259](https://github.com/architects-toolkit/SmartHopper/issues/259)).
- Fixes "Check structured output compatibility with models" ([#273](https://github.com/architects-toolkit/SmartHopper/issues/273)).

## [0.3.6-alpha] - 2025-07-20

### Added

- Added icon to `AIModelsComponent`

## [0.3.5-alpha] - 2025-07-19

### Added

- New methods in AIProvider base class:
  - Add DefaultServerUrl property
  - Added CallApi method to AIProvider base class supporting GET/POST/DELETE/PATCH
  - Added RetrieveAvailableModels method to AIProvider base class with default to empty list
- Implemented RetrieveAvailableModels, CallApi and DefaultServerUrl to existing providers (MistralAIProvider, OpenAIProvider, and DeepSeekProvider).
- New AIModelsComponent component under SmartHopper > AI categories that uses provider's RetrieveAvailableModels() to fetch model list.

### Changed

- Update providersResources access modifiers from public to internal
- Clean up AboutDialog by removing MathJax attribution
- Moved provider selection logic from AIProviderComponentBase to AIProviderComponentBase
- Moved InputsChanged method with override for including HasProviderChanged from AIStatefulAsyncComponentBase to AIProviderComponentBase

### Removed

- Removed MathJax support from chat UI since it was not properly implemented and was generating security warnings on GitHub.

## [0.3.4-alpha] - 2025-07-11

### Added

- Added `Instructions` input to `AIChatComponent` ([#87](https://github.com/architects-toolkit/SmartHopper/issues/87))
- Added `systemPrompt` parameter to `WebChatUtils.ShowWebChatDialog`
- Context manager improvements:
  - Added support for "-*" to exclude all providers/context in one go
  - Added support for space as additional delimiters in filter strings
  - Explicitly handle "*" wildcard to include all providers/context by default
- Added `gh_group` AI tool for grouping components by GUID, with support to custom names and colors
- Added `list_generate` AI tool for generating a list of items from a prompt and count ([#6](https://github.com/architects-toolkit/SmartHopper/issues/6))
- New `AITextListGenerate` component implementing `list_generate` AI tool with type 'text' ([#6](https://github.com/architects-toolkit/SmartHopper/issues/6))
- Added `Category` property to `AITool` with default value "General"
- New `Filter` class for common include/exclude patterns processing

### Changed

- Several improvements to `AIChatComponent`:
  - Updated `WebChatDialog` to use provided system prompt or fall back to default
  - Improved default system prompt for AI Chat to focus on a Grasshopper assistant, including tool call examples
  - Added `gh_group` mention to default system prompt
- Modified manifest to reflect new instructions input feature in AI Chat Component
- Modified `AITextEvaluate`, `AITextGenerate`, `AIListEvaluate` and `AIListFilter` to exclude all context using the new "-*" filter
- Code reorganization:
  - Reorganized `AIProvider`, `AIContext` and `AITool` managers
  - Code cleanup in `AIChatComponent`, `WebChatDialog` and `WebChatUtils`
  - Renamed `SmartHopper.Config` to `SmartHopper.Infrastructure`
  - Renamed `SmartHopper.Config.Tests` to `SmartHopper.Infrastructure.Tests`
- Updated `StringConverter.StringToColor` to accept argb, rgb, html and known color names as input
- Change `GetResponse` parameter from `includeToolDefinitions` to `toolFilter`
- Updated AITool constructor to require category parameter
- Categorized existing tools with DataProcessing, Components, Knowledge and Scripting categories
- Updated unit tests to include category parameter
- Integrated the new `Filter` class in `GetFormattedTools` and `GetCurrentContext`

### Removed

- Removed unnecessary `GetModel` and `GetFormattedTools` methods in `OpenAIProvider`, `MistralAIProvider` and `TemplateProvider`
- Removed `GetResponse` method from `AIStatefulAsyncComponentBase` in favor of `CallAIToolAsync`

## [0.3.3-alpha] - 2025-06-23

### WIP

- Adding LaTeX support in chat UI with the MathJax library.

### Added

- Added DeepSeek provider ([#222](https://github.com/architects-toolkit/SmartHopper/issues/222)).
- Added temperature parameter support for MistralAI, OpenAI, and DeepSeek providers.
- Added slider UI control in settings dialog for numeric parameters.
- Added reasoning support:
  - Render reasoning panels for `<think>` tags in chat UI as collapsible `<details>` blocks.
  - Exclude reasoning from copy-paste (`mdContent`) and include in HTML display (`htmlContent`).
  - Added configurable `reasoning_effort` setting (low, medium, high) for OpenAI o-series models.
  - New `StripThinkTags` method in `Config.Utils.AI`.
  - Set up OpenAI and DeepSeek to return reasoning in the response.

### Changed

- Updated default OpenAI model to gpt-4.1-mini.
- Mention `DeepSeek` as available provider in the About dialog.
- Settings dialog improvements:
  - Added dropdown support for provider settings when a list of allowed values is provided.
  - Increased max tokens for OpenAI, MistralAI and DeepSeek providers to 100000.
  - Improved descriptions.
  - Setting values that are empty or whitespace will be removed from the settings file on `UpdateProviderSettings`.
- AI providers updates:
  - Updated deprecated OpenAI max_tokens parameter to max_completion_tokens.
  - Refactored OpenAI, MistralAI, DeepSeek and TemplateProvider settings validation to use centralized validation methods.
  - Renamed `OpenAI` to `OpenAIProvider`.
  - Renamed `OpenAISettings` to `OpenAIProviderSettings`.
  - Renamed `MistralAI` to `MistralAIProvider`.
  - Renamed `MistralAISettings` to `MistralAIProviderSettings`.
  - OpenAI, MisralAI and DeepSeek now remove `<think>` tags from messages before sending them to the API, using the `StripThinkTags` method from `Config.Utils.AI`.
  - Reorganized providers settings and moved them from `AIProvider` to `AIProviderSettings`.

### Removed

- Provider settings cleanup:
  - Removed `private LoadSettings` method from `OpenAISettings`.
  - Removed `private LoadSettings` method from `TemplateProviderSettings`.
  - Removed `CreateSettingsControl`, `GetSettings` and `LoadSettings` method from `IAIProviderSettings` and all implementations.
- Removed `ConcatenateItemsToJsonList` method from `ParsingTools` since it was not used.

### Fixed

- Fixed "AI List Filter might not be working as expected" ([#220](https://github.com/architects-toolkit/SmartHopper/issues/220)).
- Fixes "Accepted feature request: Add compatibility to Magistral thinking (by MistralAI)" ([#223](https://github.com/architects-toolkit/SmartHopper/issues/223)).
- Fixed limit to 4096 tokens for AI providers in settings dialog.
- Fixed errors in validation methods of providers.
- Fixed failing settings integrity check during initialization because providers were not loaded yet.
- Fixed settings dialog dropdown values not being saved and restored from storage.

## [0.3.2-alpha] - 2025-06-15

### Added

- Added undo support to `MoveInstance`, `SetComponentPreview`, and `SetComponentLock`.
- New `ScriptTools` class in `SmartHopper.Core.Grasshopper.Tools` for Grasshopper script components, including:
  - New `script_review` AI tool for reviewing Grasshopper scripts.
  - New `script_new` AI tool for generating Grasshopper scripts.
- Added support for script components in `GhPutTools`, enabling placement of script components with code from GhJSON.
- Enhanced `GetObjectsDetails` in `GHDocumentUtils` to serialize variable input and output parameters from script components to GhJSON.
- Extended `GhPutTools` to handle variable input and output parameters when placing script components from GhJSON.
- Added support for parameter modifiers (simplify, flatten, graft, reverse) in both input and output parameters for script components in `GhPutTools` and `GHDocumentUtils`.
- New `CallAiTool` method in `AIStatefulAsyncComponentBase` to handle provider and model selection, and metrics output.
- `AiTools` now define their own endpoint.
- New icons for all components.

### Changed

- Minimum Rhino version required increased to 8.19
- Updated SmartHopper logo
- Renamed `gh_retrieve_components` by `gh_get_available_components`
- Prevent `GHDocumentUtils.GetObjectsDetails` from generating humanReadable field if value is already human readable (numbers and strings)
- Renamed `evaluateList` and `filterList` AI tools to `list_evaluate` and `list_filter`
- Renamed `evaluateText` and `generateText` AI tools to `text_evaluate` and `text_generate`
- Migrated `GhPutTools` to `Utils` in `Core.Grasshopper`
- Split AI Tools into smaller files:
  - `TextTools` into `text_evaluate.cs` and `text_generate.cs`
  - `ListTools` into `list_evaluate.cs` and `list_filter.cs`
  - `GhObjTools` into `gh_tidy_up.cs`, `gh_toggle_preview.cs`, `gh_toggle_lock.cs`, `gh_move_obj.cs`
  - `GhPutTools` into `gh_put.cs`
  - `WebTools` into `web_generic_page_read.cs`, `web_rhino_forum_read_post.cs` and `web_rhino_forum_search.cs`
  - `GhGetTools` into `gh_get.cs`, `gh_list_components.cs` and `gh_list_categories.cs`
  - `ScriptTools` into `script_new.cs` and `script_review.cs`
- Now `Put` removes all default inputs and outputs from the component before adding a new script component.
- Improved OpenAI provider to support structured output.
- Improved `script_new` in several ways:
  - Now it creates component inputs and outputs.
  - It returns the instance GUID of the created component.
- Modified `AITextGenerate`, `AITextEvaluate`, `AIListEvaluate` and `AIListFilter` to use `AIToolManager` instead of calling the AI tool directly.
- Improved components descriptions.

### Deprecated

- `GetResponse` method in `AIStatefulAsyncComponentBase` is deprecated. Use `CallAiTool` instead.

### Removed

- Removed `Eto.Forms` reference from `SmartHopper.Config`.
- Removed the `GetEndpoint` method from `AIStatefulAsyncComponentBase`.

### Fixed

- Fixed MistralAI provider not working with structured output ([#112](https://github.com/architects-toolkit/SmartHopper/issues/112)).
- Fixed OpenAI error in API URI.
- Fixed CI Signature Tests in `SmartHopper.Config.Tests`.
- Fixed OpenAI logo quality.

## [0.3.1-alpha] - 2025-05-06

### Added

- Added the "Accepted feature request: Allow for copy-paste the chat in a good format when selecting the text" ([#86](https://github.com/architects-toolkit/SmartHopper/issues/86)).
- Added support for script components in `GhGet`.
- Allow `CreateComponentGrid` for fractional row positions for components, to create a more human-like layout.
- Added `gh_tidy_up` AI tool in `GhObjTools` for arranging selected components into a dependency-based grid layout.
- New `gh_tidy_up` component.
- New `SelectingComponentBase` for components that need the button to select other components.
- New `GHJsonAnalyzer` and `GHJsonFixer` classes for analyzing and fixing GHJSON formats.

### Changed

- Improved chat UI with timestamps for messages, collapsible tool messages, inline metrics per message, button to copy codeblocks to clipboard, and better formatting.
- Reorganization of JSON models for clearer structure.
- Migrated the `GhPut` tool from the `GhPutComponent` to the `GhPutTools` class, using the `AIToolManager`.
- `DeserializeJSON` now fixes invalid InstanceGuids in Grasshopper JSON documents when deserializing.
- Moved `DependencyGraphUtils` and `ConnectionGraphUtils` from `SmartHopper.Core.Graph` to `SmartHopper.Core.Grasshopper.Graph`.
- Improved `CreateComponentGrid` in `DependencyGraphUtils`:
  - Now returns original pivots relative to the most top-left component to ensure relative positioning
  - Uses a more human-like layout with column widths based on actual component widths
  - Uses horizontal margin of 50 and vertical spacing of 80
  - Centers Params from their actual center instead of the top-left position
  - Improved by detecting islands of components, ensure connected components stay together, and use barycenter heuristic algorithm for initial layer ordering
  - Minimizes connection length
  - Aligns parents with children
- Improved `MoveInstance`:
  - Added a nice animation so that components move smoothly to their new position.
  - Skip movement if initial and target positions are the same.
- Modified `GhGetComponents` to use the new `SelectingComponentBase`.
- Implemented the new `GHJsonAnalyzer` and `GHJsonFixer` in `GhPutTools` and `GhPutComponents`.

### Fixed

- Fixed issue with tool calls in chat messages. Now the code provides exactly the json structure expected by MistralAI and OpenAI.
- Fixed tooltip visibility at the bottom of the chat.
- Fixed component placement in `GhPut` tool was too separated.
- Fixed source components in `TopologicalSort` were not sorted in reverse order.
- Limited `ghget` connections to components within the result objects set.
- Fixed `gh_tidy_up` moving components on every execution.
- Fixed `CreateComponentGrid` joining last and last-1 column together.
- (automatically added) Fixes "Panels and params position is calculated from top-left, not from center" ([#184](https://github.com/architects-toolkit/SmartHopper/issues/184)).

## [0.3.0-alpha] - 2025-04-27

### Added

- Enabled the AIChat component to execute tools in Grasshopper.
- Added optional 'Filter' input to `GhGetComponents` component for filtering by errors, warnings, remarks, selected, unselected, enabled, disabled, previewon, previewoff, previewcapable, notpreviewcapable. Supports include/exclude syntax (+/-) provided as a list of tags, each tag in a separate line, comma-separated or space-separated.
- Added optional 'Type filter' input to `GhGetComponents` component to filter by component type (params, components, inputComponents, outputComponents and processingComponents).
- Added `ConnectionGraphUtils` class in `SmartHopper.Core.Graph` namespace with method `ExpandByDepth` to expand a set of component IDs by following connections up to the given depth.
- Added `GhRetrieveComponents` component and `ghretrievecomponents` AI tool for listing Grasshopper component types with descriptions, keywords, category filters, and list of inputs and outputs.
- Added `ghcategories` AI tool in `GhTools` to list Grasshopper component categories and subcategories with optional soft string filter.
- Added new `gh_toggle_preview` AI tool in `GhObjTools` for toggling Grasshopper component preview by GUID.
- Added new `gh_toggle_lock` AI tool in `GhObjTools` for toggling Grasshopper component lock state by GUID.
- Added new `gh_move_obj` AI tool in `GhObjTools` for moving Grasshopper component pivot by GUID with absolute or relative position.
- Added `MoveInstance` method in `GHCanvasUtils` to move existing instances by GUID with absolute or relative pivot positions.
- Improved security in Providers by accepting only signed assemblies.
- Added multiple CI Tests, for example, to ensure unsigned provider assemblies are rejected by `ProviderManager.VerifySignature`, to ensure only signed assemblies are loaded by `ProviderManager.LoadProviderAssembly`, and to ensure only enabled providers are registered by `ProviderManager.RegisterProviders`.
- Added `AIToolCall.cs`, a new model for AI tool call requests.
- Added `SmartHopperInitializer.cs`, a static class for safe startup and provider initialization.
- Added `StyledMessageDialog` class in `SmartHopper.Config.Dialogs` for consistent message dialog styling with the SmartHopper logo.
- Added `WebTools` to retrieve webpages from the Internet and provide them to the AI provider.
- Added `Search Rhino Forum` webtool to query posts in Rhino Forum.
- Added `Get Rhino Forum Post` webtool to retrieve full JSON of a Rhino Discourse forum post by ID.

### Changed

- Renamed the 'Branches Input' and 'Processed Branches' parameters to 'Data Count' and 'Iterations Count' in `DeconstructMetricsComponents`. Improved descriptions for both parameters.
- Modified `FilterListAsync` in `ListTools` to return indices instead of filtered list items, with `AIListFilter` component now handling the final list construction.
- Renamed `GhGetSelectedComponents` (GhGetSel) to `GhGetComponents`.
- Moved `GhGet` execution logic to external tools managed by `ToolManager`.
- Improved `ghget` tool's `typeFilter` input: supports include/exclude syntax (+/-) with multiple tokens (params, components, input, output, processing) and updated schema description with definitions and examples.
- Reorganized `SmartHopper.Core.Grasshopper` files in subfolders that match the namespace.
- Isolated settings so providers access them only via `ProviderManager`, not directly via `SmartHopperSettings`.
- SmartHopper icon is now used for all dialogs within SmartHopper (about, settings, messages and ai chat)

### Removed

- `GhGetComponent` was replaced by `GhGetSelectedComponents` (GhGetSel) and renamed back to `GhGetComponents`.
- Removed support for net48. From now on, Rhino 8 or later is required.
- Removed `ToolFunction` and `ToolArgument` in `AIResponse`, in favor of the more flexible `AIToolCall`.

### Fixed

- Fixed double‐encryption of sensitive settings in `SettingsDialog.SaveSettings()` causing unreadable API keys
- Fixed mismatch between in-memory and on-disk `TrustedProviders` when prompting in `ProviderManager.LoadProviderAssembly()`
- Fixed a bug in `DataProcessor` where results were being duplicated when multiple branches were grouped together to unsuccessfully prevent unnecessary API calls [#32](https://github.com/architects-toolkit/SmartHopper/issues/32)
- Fixed inconsistent list format handling between `AIListEvaluate` and `AIListFilter` components.
- Fixed `MistralAI` provider not loading `AI Tools`.
- Fixed `GhGetComponent` select functionality that was accidentally omitted in the new `GhTools`.

## [0.2.0-alpha] - 2025-04-06

### Added

- **Modular AI providers**: OpenAI and MistralAI providers are now separate, dynamically loaded plugins; a template provider is available for building custom providers.
- **AI Chat component**: New interactive chat component with a WebView-based chat dialog, message bubbles, and Markdown rendering.
- **Provider selection**: Added a "Default" provider option and a global default provider setting in the settings dialog.
- **SmartHopper tab**: Added a custom icon for the SmartHopper tab in Grasshopper.
- **Markdown support**: Chat messages are now rendered as Markdown with support for headings, code blocks, blockquotes, and inline formatting.
- **Context system**: Added context filtering by provider and key, plus automatic time and environment context in the AI Chat component.
- **Documentation**: Added a "Supported Data Types" section to README.md.

### Changed

- **AIChat execution**: The AI Chat component now always runs when the Run parameter is true, regardless of input changes.
- **Chat UI**: Improved chat dialog layout with responsive sizing, text wrapping, message selection and copying, and single-dialog-per-component behavior.
- **About dialog**: Restyled with smaller fonts, a minimum size, and an AI-generated content disclaimer.
- **Release packaging**: Release builds now produce separate Rhino8-Windows and Rhino8-Mac zip files.
- **Settings menu**: Rewrote the settings menu using Eto.Forms for cross-platform compatibility.
- **AI Context component**: Renamed to AI File Context.

### Fixed

- Fixed AI provider state not being saved or restored in Grasshopper files ([#41](https://github.com/architects-toolkit/SmartHopper/issues/41)).
- Fixed AI Chat using the default provider correctly when "Default" is selected.
- Fixed settings menu hiding on Windows ([#94](https://github.com/architects-toolkit/SmartHopper/issues/94)).
- Fixed AI Chat freezing Rhino ([#85](https://github.com/architects-toolkit/SmartHopper/issues/85)).
- Fixed settings menu incompatibility with Mac ([#12](https://github.com/architects-toolkit/SmartHopper/issues/12)).
- Added AI disclaimer to the chat and about dialogs ([#114](https://github.com/architects-toolkit/SmartHopper/issues/114)).
- Fixed chat dialog freezing and taskbar focus issues.

### Removed

- Removed OpenAI and MistralAI providers from the legacy SmartHopper.Config project.
- Removed the HtmlAgilityPack dependency.

## [0.1.2-alpha] - 2025-03-17

### Changed

- Updated pull-request-validation.yml workflow to use version-tools for version validation
- Improved PR title validation with more detailed error messages and support for additional conventional commit types
- Added "security" as a valid commit type in PR title validation
- Modified update-dev-version-date.yml workflow to create a PR instead of committing changes directly to the branch

### Removed

- Removed Test GitHub Actions workflow

### Fixed

- Fixed version badge update workflow to only modify the version badge and not affect other badges in README.md
- Fixed badge addition logic in version-tools action to properly handle cases when badges don't exist
- Fixed security-patch-release.yml workflow to create a PR instead of pushing directly to main, resolving repository rule violations
- Fixed version-calculator to always perform the requested increment type without conditional logic, ensuring consistent behavior
- Fixed security-patch-release.yml workflow to create a release draft only when no PR is created
- Added new security-release-after-merge.yml workflow to create a release draft when a security patch PR is merged
- Fixed GitHub release creation by removing invalid target_commitish parameter

### Security

- (automatically added) Security release to update all workflow actions to the latest version.
- Updated several github workflows to use the latest version of actions:
  - Updated tj-actions/changed-files from v45.0 to v46.0.1
  - Updated actions/checkout to v4 across all workflows
  - Updated actions/setup-dotnet to v4
  - Updated actions/upload-artifact to v4
  - Updated actions/github-script to v7
- Enhanced pull-request-validation.yml workflow with improved error logging for version and PR title checks
- Added new security-patch-release.yml workflow for creating security patch releases outside the milestone process
- Implemented GitHub Actions security best practices by pinning actions to full commit SHAs instead of version tags
- Updated security-patch-release.yml workflow to create a PR instead of pushing directly to main, resolving repository rule violations

## [0.1.1-alpha] - 2025-03-03

### Added

- Added the new GhGetSelectedComponents component.
- Added the new AiContext component ([#40](https://github.com/architects-toolkit/SmartHopper/issues/40)).
- Added the new ListTools class with methods:
  - `FilterListAsync` (migrated from `AIListFilter` component)
  - `EvaluateListAsync` (migrated from `AIListEvaluate` component)

### Changed

- Updated README.md to better emphasize the plugin's ability to enable AI to directly read and interact with Grasshopper files.
- New About menu item using Eto.Forms instead of WinForms.
- Refactored AI text evaluation tools to improve code organization and reusability:
  - Added generic `AIEvaluationResult<T>` for standardized tool-component communication
  - Created `ParsingTools` class for reusable AI response parsing
  - Created `TextTools` with method `EvaluateTextAsync` (replacement of `AiTextEvaluate` main function)
  - Added `GenerateTextAsync` methods to `TextTools` (migrated from `AITextGenerate` component)
  - Updated `AITextGenerate` component to use the new generic tools
  - Added regions in `TextTools` to improve code organization
- Refactored AI list processing tools to improve code organization and reusability:
  - Added `ParseIndicesFromResponse` method to `ParsingTools` for reusable response parsing
  - Added `ConcatenateItemsToJson` method to `ParsingTools` for formatting list data
  - Added `ConcatenateItemsToJsonList` method to `ParsingTools` for list-to-JSON conversion
  - Added regions in `ListTools` and `ParsingTools` to improve code organization
  - Updated `AIListFilter` component to use the new generic tools
  - Updated `AIListEvaluate` component to use the new generic tools
  - Fixed error handling in list processing components to use standardized error reporting
  - Improved list processing to ensure entire lists are processed as a unit

### Fixed

- Restored functionality to set Persistent Data with the GhPutComponents component.
- Restored functionality to generate pivot grid if missing in JSON input in GhPutComponents.
- AI messages will only include context if it is not null or empty.

## [0.1.0-alpha] - 2025-01-27

### Added

- Added the new AITextEvaluate component.

### Changed

- Renamed the AI List Check components to AI List Evaluate.
- Improved AI provider's icon visualization.

### Removed

- Removed components based on the old Component Base.
- Removed the code for the old Component Base.

### Fixed

- Fixed Feature request: Full rewrite of the Component Base ([#20](https://github.com/architects-toolkit/SmartHopper/issues/20))
- Fixed Feature request: AI Text Check Component ([#4](https://github.com/architects-toolkit/SmartHopper/issues/4))

## [0.0.0-dev.250126] - 2025-01-26

### Added

- Added a new Component Base for AI-Powered components, including these features:
  - Debouncing timer to prevent fast recalculations
  - Enhanced state management system with granular state tracking
  - Better stability in the state management, that prevents unwanted recalculations
  - Store outputs and prevent from recalculating on file open
  - Store outputs and prevent from recalculating on modifying Graft/Flatten/Simplify
  - Persistent error tracking through states
  - Compatibility with button and boolean toggle in the Run input
  - Compatibility with Data Tree processing (Input and Output)
  - Manual cancellation while processing
- Added a new library with testing components.

### Changed

- General clean up and refactoring, including the suppression of unnecessary comments, and the removal of deprecated features.
- Migrate AI Text Generate component to use the new Component Base.
- Refactor DataTree libraries in Core to unify and simplify functionality.

### Fixed

- Fixed lack of comprehensive error when API key is not correct ([#13](https://github.com/architects-toolkit/SmartHopper/issues/13))
- Fixed Changing Graft/Flatten from an output requires recomputing the component ([#7](https://github.com/architects-toolkit/SmartHopper/issues/7))
- Fixed Feature request: Store outputs and prevent from recalculating on file open ([#8](https://github.com/architects-toolkit/SmartHopper/issues/8))
- Fixed Bug: Multiple calls to SolveInstance cause multipe API calls (in dev branch) ([#24](https://github.com/architects-toolkit/SmartHopper/issues/24))

## [0.0.0-dev.250104] - 2025-01-04

### Added

- Added metrics for AI Provider and AI Model in AI-Powered components ([#11](https://github.com/architects-toolkit/SmartHopper/issues/11))

### Fixed

- Fixed bug with the Model input in AI-Powered components ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))
- Fixed model parameter handling in IAIProvider interface to ensure proper model selection across providers ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))
- Fixed issue with AI response metrics not returning the tokens used in all branches, but only the last one ([#2](https://github.com/architects-toolkit/SmartHopper/issues/2))

## [0.0.0-dev.250101] - 2025-01-01

### Added

- Initial release of SmartHopper
- Core plugin architecture for Grasshopper integration
- Base component framework for custom nodes
- GitHub Actions workflow for automated validation
  - Version format checking
  - Changelog updates verification
  - Conventional commit enforcement
- Comprehensive documentation and examples
  - README with setup instructions
  - CONTRIBUTING guidelines
