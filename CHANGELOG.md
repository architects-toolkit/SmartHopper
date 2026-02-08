# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

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

### Fixed

- gh_get component: Restored full filter pipeline (attribute, type, and category filters) that was lost during ghjson-dotnet migration. Component now properly filters by error/warning/remark, selected/unselected, enabled/disabled, preview states, component types (params/components/startnodes/endnodes/middlenodes/isolatednodes), and Grasshopper categories.
- gh_get component: Restored connection depth expansion functionality using `ConnectionGraphUtils.ExpandByDepth()`. The `connectionDepth` parameter now correctly expands the selection to include connected components at the specified depth.
- gh_put component: Restored external connection capture and reconnection in edit mode. When replacing components, the tool now captures connections to external (non-replaced) components before removal and restores them after placement, preserving the component's integration in the existing definition.

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
  - `AIScriptGeneratorComponent` now orchestrates `script_generate` / `script_edit` together with `gh_get` / `gh_put` instead of the legacy `script_generator` tool, and exposes `GhJSON`, `Guid`, `Summary`, and `Message` outputs only.
  - `AIScriptGeneratorComponent` and `AIScriptReviewComponent` no longer expose a `Guid` input; the target component is always provided via the selecting button.
  - Removed the monolithic `script_generator` AI tool in favor of smaller, focused tools that operate purely on GhJSON.
  - Updated `AIScriptGeneratorComponent` and `AIScriptReviewComponent` to support processing multiple inputs in parallel.
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

- **VB Script Serialization Support**: Complete implementation of 3-section VB Script serialization/deserialization:
  - **VBScriptCode Model**: New model class with separate properties for `imports`, `script`, and `additional` code sections
  - **ComponentState Enhancement**: Added `vbCode` property to support VB Script 3-section structure alongside existing `value` property
  - **GhJsonSerializer**: Extracts VB Script 3 sections separately via reflection using ScriptSource properties (UsingCode, ScriptCode, AdditionalCode)
  - **GhJsonDeserializer**: Restores VB Script 3 sections to correct ScriptSource properties with proper section mapping
  - **VB Parameter Management**: Implements IGH_VariableParameterComponent interface for proper parameter creation/destruction
  - Parameter settings applied via CreateParameter/DestroyParameter with VariableParameterMaintenance() call
  - Full support for custom input/output parameters with names, optional/required flags, and modifiers
  - **UI Thread Safety**: All VB Script parameter and code operations wrapped in `RhinoApp.InvokeOnUiThread()` to prevent UI blocking
 - **New AI Tools for parameter and script modification**:
   - Parameter tools: `gh_parameter_flatten`, `gh_parameter_graft`, `gh_parameter_reset_mapping`, `gh_parameter_reverse`, `gh_parameter_simplify`, `gh_parameter_bulk_inputs`, `gh_parameter_bulk_outputs`
   - Script tools: `script_parameter_add_input`, `script_parameter_add_output`, `script_parameter_remove_input`, `script_parameter_remove_output`, `script_parameter_set_type_input`, `script_parameter_set_type_output`, `script_parameter_set_access`, `script_toggle_std_output`, `script_set_principal_input`, `script_parameter_set_optional`
 - **McNeel Forum AI Tools Enhancement**:
  - `mcneel_forum_search`: Enhanced search tool with configurable result limit (1-50 posts), returning matching posts as raw JSON objects
  - `mcneel_forum_post_get`: Renamed from `web_rhino_forum_read_post` for consistency, retrieves full forum post by ID
  - `mcneel_forum_post_summarize`: New subtool that generates AI-powered summaries of forum posts using default provider/model
- New Knowledge components for McNeel forum and web sources, enabling search, retrieval, and summarization workflows directly in Grasshopper.
- **web_generic_page_read Enhancements**: Tool now delivers clean text for Wikipedia/Wikimedia articles, Discourse forums (raw markdown), GitHub/GitLab file URLs (raw/plain/markdown), and Stack Exchange questions via official APIs.
- **Property Management System V2**: Complete refactoring of property management with modern, maintainable architecture:
  - **PropertyManagerV2**: New property management system with clean separation of concerns between filtering, extraction, and application
  - **PropertyFilter**: Intelligent property filtering based on object type and serialization context
  - **PropertyHandlers**: Specialized handlers for different property types (PersistentData, SliderCurrentValue, Expression, etc.)
  - **PropertyFilterConfig**: Centralized configuration for property whitelists, blacklists, and category-specific properties
  - **SerializationContext**: Support for different contexts (AIOptimized, FullSerialization, CompactSerialization, ParametersOnly)
  - **ComponentCategory**: Proper categorization of components (Panel, Slider, Script, etc.) for targeted property extraction
  - **PropertyManagerFactory**: Factory methods for creating PropertyManagerV2 instances with common configurations
- **GhJSON Optimization**: Reduced irrelevant data in serialization output:
  - Groups now only include members present in the current GhJSON components selection
  - Removed runtime-only properties: `VolatileData`, `IsValid`, `IsValidWhyNot`, `TypeDescription`, `TypeName`, `Boundingbox`, `ClippingBox`, `ReferenceID`, `IsReferencedGeometry`, `IsGeometryLoaded`, `QC_Type`
  - Fixed contradictory property handling where `VolatileData` and `DataType` were in both omitted and whitelist
  - Fixed `IsPropertyInWhitelist()` method to properly check omitted properties before whitelist
  - Removed `Type` and `HumanReadable` properties from `ComponentProperty` model to reduce JSON size
- **Enhanced GhJSON Schema**: Implemented component schema improvements following complete property reference specification:
  - **Parameter Properties**: `parameterName`, `dataMapping`, `simplify`, `reverse`, `invert`, `unitize`, `expression`, `variableName`, `isPrincipal`, `locked`
  - **Component Properties**: `locked`, `hidden`, universal `value` property with type-specific mapping
  - **Value Consolidation**: Number Slider (`currentValue` → `value`), Panel (`userText` → `value`), Scribble (`text` → `value`), Script (`script` → `value`), Value List (`listItems` → `value`)
  - **Removed Properties**: `expressionContent` (use `expression`), `access`, `description`, `optional`, `type`, `objectType` (excluded as implicit/redundant), `humanReadable`, redundant slider properties
  - Extended `ComponentProperties` with new schema properties: `Id`, `Params`, `InputSettings`, `OutputSettings`, `ComponentState`
  - **BREAKING**: New schema format is now the default for all `gh_get` and `gh_put` operations
  - Kept legacy `Pivot` format for compactness and compatibility
- New AI Tools for component generation and connection:
  - `gh_generate`: Generate GhJSON from component specifications (name + parameters), returns valid GhJSON for gh_put.
  - `gh_connect`: Connect Grasshopper components by creating wires between outputs and inputs using component GUIDs.
- New AI Tool for script editing and creation:
  - `script_generator`: Unified tool that creates or edits Grasshopper script components based on natural language instructions and an optional component GUID. Replaces legacy `script_new` and `script_edit` tools.
- New utility classes for centralized Grasshopper operations:
  - `GHConnectionUtils`: Connect components by creating wires between parameters.
  - `GHGenerateUtils`: Generate GhJSON component specifications.
  - `RhinoFileUtils`: Read and analyze .3dm files.
  - `RhinoGeometryUtils`: Extract geometry information from active Rhino document.
- New AI Tools for Rhino 3DM file analysis:
  - `rhino_read_3dm`: Analyze .3dm files and extract metadata, object counts, layer information, and detailed object properties.
  - `rhino_get_geometry`: Extract detailed geometry information from the active Rhino document (selected objects, by layer, or by type).
- New test project `SmartHopper.Core.Grasshopper.Tests` with comprehensive unit test coverage:
  - `AIResponseParserTests`: 40+ tests for parsing edge cases (JSON arrays, markdown blocks, ranges, text formats)
  - `PropertyManagerTests`: 30+ tests for type conversion, property setting, and persistent data handling
- **SelectingComponentBase Persistence**: Selected objects list now persists when saving and loading Grasshopper files
  - Stores selected object GUIDs during write operations
  - Restores selected objects by GUID lookup during read operations
  - Updates component message to reflect restored selection count
  - Handles missing objects gracefully (objects deleted after selection)
- New hotfix workflow system for emergency production fixes:
  - **hotfix-0-new-branch.yml** - Creates `hotfix/X.X.X-description` branch from main with automatic patch version increment
  - **hotfix-1-release-hotfix.yml** - Prepares `release/X.X.X-hotfix-description` branch with version updates, changelog, and PR to main
  - Automatic version conflict resolution:
    - All milestones with patch ≥ hotfix patch are incremented (updated from highest to lowest to prevent collisions)
    - Dev branch version is bumped via PR if it conflicts (respects protected branch)
  - Hotfix PRs trigger all existing validations (version check, code style, tests) before merging
  - After merge to main, existing release workflows (release-3, release-4, release-5) handle GitHub Release creation, build, and Yak upload
  - Comprehensive documentation in `.github/workflows/HOTFIX_WORKFLOW.md`
- Comprehensive workflow documentation:
  - **RELEASE_WORKFLOW.md** - Complete guide for regular milestone-based releases
  - **HOTFIX_WORKFLOW.md** - Complete guide for emergency hotfix releases

### Changed

- Renamed AI Tools:
  - `gh_toggle_preview` to `gh_component_toggle_preview`
  - `gh_toggle_lock` to `gh_component_toggle_lock`
  - `gh_lock_selected` to `gh_component_lock_selected`
  - `gh_unlock_selected` to `gh_component_unlock_selected`
  - `gh_hide_preview_selected` to `gh_component_hide_preview_selected`
  - `gh_show_preview_selected` to `gh_component_show_preview_selected`
- **Script Component "out" Parameter Handling**: The standard output/error parameter ("out") in script components is no longer serialized as a regular output parameter. Instead, its visibility state is controlled by the new `showStandardOutput` property in `ComponentState`, which maps to the component's `UsingStandardOutputParam` property. This prevents signature changes after deserialization.
- **ComponentProperty JSON Serialization**: Simple types (bool, int, string, double, etc.) now serialize directly without the `{"value": ...}` wrapper for cleaner, more compact JSON output. Complex types retain the wrapper structure for backward compatibility.
- **Empty String Omission**: Empty string properties (e.g., group `name`, component `nickName`) are now omitted from JSON output for cleaner, more compact serialization. Only non-empty values are included.
- **Document Metadata Improvements**:
  - Fixed Grasshopper version detection (now reads from Instances assembly instead of settings)
  - Added `schemaVersion` property (set to "1.0") to GrasshopperDocument
  - Added `rhinoVersion` property to track Rhino version
  - Added `parameterCount` property to track standalone parameters separately from components
  - Changed `createdAt` to `created` for consistency
  - Default document `version` to "1"
  - Added `dependencies` array to track plugin dependencies (excludes system assemblies)
- More robust GhJson schema, serialization and deserialization methods.
- Model capability validation now bypasses checks for unregistered models, allowing users to use any model name even if not explicitly listed in the provider's model registry.
- Centralized error handling in AIReturn and tool calls.
- Accurately aggregate metrics in Conversation Session. Cases with multiple tool calls, multiple interactions, etc. Calculate completion time per interaction.
- Improved AI Tool descriptions with better guided instructions. Also added specialized wrappers for targeted tool calls (gh_get_selected, gh_get_errors, gh_get_locked, gh_get_hidden, gh_get_visible, gh_get_by_guid, gh_lock_selected, gh_unlock_selected, gh_hide_preview_selected, gh_show_preview_selected, gh_group_selected, gh_tidy_up_selected).
- **BREAKING (Internal):** Reorganized `SmartHopper.Core.Grasshopper.Utils` namespace structure for better maintainability:
  - Created organized subfolders: `Canvas/`, `Serialization/`, `Parsing/`, `Rhino/`, `Internal/`
  - Renamed utility classes for clarity (e.g., `GHCanvasUtils` → `CanvasAccess`, `GHComponentUtils` → `ComponentManipulation`)
  - Updated all internal references to use new organized namespaces
  - **Note:** This is an internal refactoring with no impact on public APIs or plugin functionality
- Extended PR validation and CI test workflows to run on `hotfix/**` and `release/**` branches
- **user-security-patch.yml** workflow is now obsolete, removed from workflows

### Removed

- **Legacy PropertyManager**: Removed obsolete `PropertyManager.cs` and all references to the old property management system:
  - Removed `PropertyManager.IsPropertyInWhitelist()`, `PropertyManager.SetProperties()`, `PropertyManager.IsPropertyOmitted()`, and `PropertyManager.GetChildProperties()` methods
  - Updated `DocumentIntrospection.cs` to use `PropertyManagerV2` with `PropertyManagerFactory.CreateForAI()`
  - Updated `GhJsonPlacer.cs` to use `PropertyManagerV2` for property application
  - Updated `PropertyManagerTests.cs` to test the new `PropertyManagerV2` system instead of the old `PropertyManager`

- **Legacy script tools and components**:
  - Removed `script_new` and `script_edit` AI tools in favor of the unified `script_generator` tool.
  - Removed `AIScriptNewComponent` and `AIScriptEditComponent` Grasshopper components in favor of `AIScriptGeneratorComponent`.

### Fixed

- **DataTreeProcessor Bug Fixes**:
  - Fixed `GetBranchFromTree` incorrectly returning branches from different paths when tree had single path (flat tree fallback bug). Now strictly returns only branches matching the requested path.
  - Fixed `BranchFlatten` topology creating one processing unit per path instead of flattening all branches together. Now correctly creates single processing unit with all items from all branches flattened into one list.
- Fixed model badge display: show "invalid model" badge when provider has no capable model instead of "model replaced" ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332)).
- Fixed provider errors (e.g., HTTP 400, token limit exceeded) not surfacing to WebChat UI: `ConversationSession` now surfaces `AIInteractionError` from error AIReturn bodies to observers before calling `OnError`, ensuring full error messages are displayed in the chat interface ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334)).
- **Rectangle Serialization**: Fixed Rectangle3d serialization/deserialization to use center-based format (`rectangleCXY`) instead of origin-based (`rectangleOXY`), ensuring correct position and orientation after round-trip. Uses interval-based constructor to guarantee proper reconstruction.
- **IsPrincipal Property Cleanup**: Removed `IsPrincipal` from appearing as a top-level property on standalone parameter components (Colour, Number, Text, etc.). It now only appears in `inputSettings`/`outputSettings` `additionalSettings` when set to `true`, reducing JSON clutter.
- **Script Component Null Reference**: Fixed `ArgumentNullException` in `gh_get` tool when processing script components. Added null check in `ScriptComponentHelper.GetScriptLanguageType()` method before calling `.Contains()` on potentially null language name string.
- **Stand-Alone Parameter Serialization**: Fixed `GhJsonSerializer` to properly serialize stand-alone parameters (e.g., `Param_Colour`, `Param_Number`, `Param_Box`, etc.). Previously only `IGH_Component` objects were serialized; now both `IGH_Component` and `IGH_Param` objects are processed. Stand-alone parameters (those without a parent component) are now included in the serialization output and their connections are properly extracted.
- **PersistentData Deserialization**: Fixed `GhJsonDeserializer` to properly deserialize internalized data (PersistentData) for stand-alone parameters. The deserializer now uses `PropertyManagerV2` instead of simple reflection, which correctly handles the nested data tree structure and type-specific conversions for all parameter types (Color, Point, Vector, Line, Plane, Circle, Arc, Box, Rectangle, Interval, Number, Integer, Boolean, Text).
- **Connection Matching by Index**: Fixed connection serialization/deserialization to use parameter index instead of parameter name. Connections now include `paramIndex` property for reliable matching regardless of display name settings (full names vs nicknames). The deserializer uses index-based matching with fallback to name-based matching for backward compatibility. Fixed stand-alone parameter to stand-alone parameter connections to properly set `paramIndex` to 0 (single input).
- **Group InstanceGuid**: Fixed group serialization to include the actual `InstanceGuid` instead of all zeros. Groups now properly serialize their unique identifier for correct reconstruction.
- **InstanceGuid Generation**: Fixed deserialization to always generate new InstanceGuids instead of reusing GUIDs from JSON. This prevents "An item with the same key has already been added" errors when placing components that already exist in the document. Grasshopper now automatically generates unique GUIDs for all deserialized components.
- **Stand-Alone Parameter Connections**: Fixed `ConnectionManager` to support connections between stand-alone parameters (e.g., Colour → Panel). Previously only component-to-component and parameter-to-component connections were supported.
- **Smart Pivot Handling in gh_put**: Improved component placement logic to intelligently handle positions:
  - When pivots are present in GhJSON: Components are placed with their relative positions preserved, offset to prevent overlap with existing canvas components (positioned below the lowest existing component with 100px spacing)
  - When pivots are absent: Uses `DependencyGraphUtils.CreateComponentGrid` algorithm (same as `gh_tidy_up`) to automatically calculate optimal grid layout based on component connections
  - Removed `RecalculatePivots` option from `DeserializationOptions` and `gh_put` tool - pivot handling is now automatic based on GhJSON content
- **Parameter Modifier Serialization**: Fixed `ParameterMapper` to properly extract and apply parameter modifiers (`Reverse`, `Simplify`, `Locked`) and `DataMapping` for component parameters. These settings are now serialized in the `additionalSettings` object and correctly restored during deserialization. Note: The `Invert` property does not exist in the `IGH_Param` interface and is reserved in `AdditionalParameterSettings` for future use or specific parameter type extensions.
- **Removed Optional Property**: Removed redundant `optional` property from `ParameterSettings` model as it provides no useful information for serialization/deserialization.
- **Script Component Parameter Modifiers**: Fixed issue where parameter modifiers (Reverse, Simplify, Locked, Invert) were not being serialized/deserialized for script component parameters. `ScriptParameterMapper.ExtractSettings()` now extracts `AdditionalSettings` just like regular `ParameterMapper`, ensuring modifiers are preserved during round-trip serialization.
- **Script Component Type Hint Normalization**: Type hints with value "object" (case-insensitive) are no longer serialized or deserialized, as "object" is the default/generic type hint. This reduces JSON size, avoids case sensitivity issues (Object vs object), and eliminates redundant data.
- **Generic Type Hint Handling**: Improved handling of generic type hints (e.g., `DataTree<Object>`, `List<Curve>`) by detecting `<>` syntax and extracting base types before applying, preventing `TypeHints.Select()` exceptions and reducing log noise.
- (automatically added) Fixes "script_edit tool freezes the script editor" ([#209](https://github.com/architects-toolkit/SmartHopper/issues/209)).

## [1.0.1-alpha] - 2025-10-13

### Changed

- Model capability validation now bypasses checks for unregistered models, allowing users to use any model name even if not explicitly listed in the provider's model registry.
- Centralized error handling in AIReturn and tool calls.
- Accurately aggregate metrics in Conversation Session. Cases with multiple tool calls, multiple interactions, etc. Calculate completion time per interaction.
- Improved AI Tool descriptions with better guided instructions. Also added specialized wrappers for targeted tool calls (gh_get_selected, gh_get_errors, gh_get_locked, gh_get_hidden, gh_get_visible, gh_get_by_guid, gh_lock_selected, gh_unlock_selected, gh_hide_preview_selected, gh_show_preview_selected, gh_group_selected, gh_tidy_up_selected).
- Enhanced `list_filter` tool prompts to explicitly distinguish between indices (positions/keys) and values (item content), and expanded capabilities to support filtering, sorting, reordering, selecting, and other list manipulation operations based on natural language criteria.
- Added more predefined models in the provider's database.

### Fixed

- Fixed model badge display: show "invalid model" badge when provider has no capable model instead of "model replaced" ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332)) ([#329](https://github.com/architects-toolkit/SmartHopper/issues/329)).
- Fixed provider errors (e.g., HTTP 400, token limit exceeded) not surfacing to WebChat UI: `ConversationSession` now surfaces `AIInteractionError` from error AIReturn bodies to observers before calling `OnError`, ensuring full error messages are displayed in the chat interface ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334)).
- Fixed `list_filter` tool automatically sorting and deduplicating indices, which prevented reordering and expansion operations from working correctly. Now preserves both order and duplicates as returned by the AI ([#335](https://github.com/architects-toolkit/SmartHopper/issues/335)).

## [1.0.0-alpha] - 2025-10-11

### Added

- Improvements in `CanvasButton`:
  - New SmartHopper Assistant setting `EnableCanvasButton` (default: `true`) to enable/disable the canvas button.
  - `CanvasButton` now respects `EnableCanvasButton`: when disabled, the button is hidden and non-interactive.
  - New `CanvasButton` to trigger the SmartHopper assistant dialog from a dedicated button at the top-right corner of the canvas.
  - CanvasButton now initializes the chat provider and model from SmartHopper settings (consistent with app-wide configuration).

- Context providers:
  - New `FileContextProvider` exposing `current-file_selected-count` (number of selected files), `file-name` (the current document file name or "Untitled"), `selected-count` (number of selected objects in the current document), `object-count` (total number of document objects), `component-count` (total number of components in the current document), `param-count` (total number of parameters in the current document), `scribble-count` (total number of scribbles/notes in the current document), and `group-count` (total number of groups in the current document). Registered globally at Core assembly load so it is available to both components and the canvas button.

- Conversation and policies:
  - ConversationSession service introducing:
    - `IConversationSession`, `IConversationObserver`, `SessionOptions` interfaces/models
    - `ConversationSession` orchestrating multi-turn flows and tool passes; executes provider calls via `AIRequestCall.Exec()` in non-streaming mode, and streams incremental `AIReturn` deltas via provider adapters when available; notifies observers with `OnStart`, `OnInteractionCompleted`, `OnToolCall`, `OnToolResult`, `OnFinal`, `OnError`
    - Always-on `PolicyPipeline` foundation with request and response policy hooks
  - Special Turn system for executing AI requests with custom overrides:
    - New `SpecialTurnConfig` to configure special turns with request overrides (interactions, provider, model, endpoint, capability, context/tool filters), execution behavior (force non-streaming, custom timeout), and history persistence strategies
    - Four history persistence strategies: `PersistResult` (only result), `PersistAll` (all interactions with filtering), `Ephemeral` (no persistence), `ReplaceAbove` (replace history with result, filtered)
    - `InteractionFilter` uses flexible allowlist/blocklist approach with `Allow()`, `Block()`, and fluent `WithAllow()`/`WithBlock()` methods; automatically supports future interaction types without code changes
    - Predefined filters: `InteractionFilter.Default`, `InteractionFilter.PreserveSystemContext`, `InteractionFilter.AllowAll`
    - `ConversationSession.ExecuteSpecialTurnAsync()` creates isolated `AIRequestCall` clone for execution; observers are not notified during execution, only when results are persisted to main conversation
    - Isolated execution prevents internal special turn interactions (system prompts, tool calls) from appearing in UI
    - Built-in `GreetingSpecialTurn` factory for AI-generated greetings
    - Special turns support both streaming and non-streaming modes in isolated execution context
    - Parallel special turns allowed (no locking)
    - Refactored greeting generation to use special turn infrastructure, eliminating 140+ lines of duplicated code

- AICall and core models:
  - Added `Do` method to `AIRequest` to execute the request and return a `AIReturn`, as well as multiple methods to simplify the process of executing requests.
  - Unified logic for `AIToolCall` and `AIRequestCall` in a `AIRequestBase`.
  - New `AIRuntimeMessage` model to handle information, warning and error messages on AI Call.
  - IAIRequest.WantsStreaming flag to indicate streaming intent and surface validation hints.
  - New IAIKeyedInteraction interface to identify interactions by key.
  - New IAIRenderInteraction interface to render interactions.

- Model management:
  - `ModelManager.SetDefault(provider, model, caps, exclusive)` helper to manage per-capability defaults.
  - Centralized streaming capability check in `ModelManager.ModelSupportsStreaming(provider, model)` and updated validation to consult it.

- Streaming infrastructure:
  - Introduced internal base class `AIProviderStreamingAdapter` under `src/SmartHopper.Infrastructure/AIProviders/` to centralize common streaming adapter helpers (HTTP setup, auth, URL building, SSE reading). Enables provider-specific adapters to reuse infrastructure while keeping behavior consistent.
  - `AIProviderStreamingAdapter.ApplyExtraHeaders(HttpClient, IDictionary<string,string>)` helper to apply request-scoped headers (excluding Authorization) from `AIRequestCall.Headers`.
  - `ConversationSession.Stream()` now gates streaming based on model capability and yields a clear error when unsupported.
  - Provider-level streaming toggle via `IAIProviderSettings.EnableStreaming`. Added `EnableStreaming` setting descriptors to OpenAI, MistralAI, and DeepSeek provider settings (default `true`).

- Tools validation:
  - AI tool validation system to improve reliability and observability:
    - Added three validators implementing `IValidator<AIInteractionToolCall>`:
      - `ToolExistsValidator` (ensures tool is registered)
      - `ToolJsonSchemaValidator` (validates tool arguments against JSON schema)
      - `ToolCapabilityValidator` (ensures selected provider/model supports tool-required capabilities)
    - New request policy `AIToolValidationRequestPolicy` runs after `ToolFilterNormalizationRequestPolicy`, validates all pending tool calls, and attaches diagnostics to the request and policy context; errors block execution early.
    - `PolicyPipeline.Default` updated to register `AIToolValidationRequestPolicy`.
    - Request validation now considers request-level messages so policy diagnostics can gate execution.

- Components UI and badges:
  - New component badges to visually identify verified and deprecated models.
  - Component badges extended to surface model validation state without executing an AI call:
    - Invalid/Incompatible model badge (red cross) when the configured model lacks the component's required capability or is unknown.
    - Replaced/Fallback model badge (blue refresh) when the current configured model would be auto-replaced by selection logic.
    - Badge display logic simplified to prioritize a single, most relevant badge for clarity.
  - `AIStatefulAsyncComponentBase.RequiredCapability` virtual property (default `Text2Text`) to declare per-component capability requirements.
  - `AIStatefulAsyncComponentBase.TryGetCachedBadgeFlags(out verified, out deprecated, out invalid, out replaced)` to expose the extended badge cache.

- Diagnostics:
  - Introduced `AIMessageCode` enum and `AIRuntimeMessage.Code` property for machine‑readable diagnostics. Default code is `Unknown (0)` to keep all existing emits backward compatible.

- Utilities:
  - Shared `HttpHeadersHelper.ApplyExtraHeaders(HttpClient, IDictionary<string,string>)` utility under `src/SmartHopper.Infrastructure/Utils/` to centralize extra header application across streaming and non‑streaming calls (excludes reserved headers).
  - Centralized sanitization utilities with tests for GhJSON, script content, and AI responses to ensure consistent cleanup of malformed or unsafe content.

- Providers:
  - New `Anthropic` and `OpenRouter` providers.
  - Anthropic provider: Full round-trip support for tool results. Decodes `tool_result` content blocks to `AIInteractionToolResult` and encodes tool results back to Anthropic-compliant `tool_result` blocks (`{"type":"tool_result","tool_use_id":"...","content":[{"type":"text","text":"..."}]}`).
  - Updated provider icons using the latest Lobe Icons set.

- Documentation:
  - Summary documentation at `docs/` (linked in README).

- Repository organization:
  - AICall folder reorganization.

- Tests:
  - New test component `DataTreeProcessorEqualPathsTestComponent` under `SmartHopper.Components.Test/DataProcessor/` to manually validate `DataTreeProcessor.RunFunctionAsync` with equal-path, single-item trees. Outputs result tree, success flag, and messages.
  - New tests for Context Manager and Model Manager.

### Changed

- Providers – Anthropic:
  - Unified encoding/decoding helpers. Extracted `BuildTextMessage`, `BuildToolResultMessage`, and `ExtractToolResultText` in `AnthropicProvider.cs` and updated both `Encode(IAIInteraction)` and `Encode(AIRequestCall)` to use them, removing duplicated logic for `AIInteractionText` and `AIInteractionToolResult`.
  - Switched URL members to use System.Uri for stronger typing and to satisfy CA1054/CA1055/CA1056:
    - `AIProvider.DefaultServerUrl` is now `Uri` (was `string`).
    - `AIProviderStreamingAdapter.BuildFullUrl(string)` now returns `Uri`.
    - `AIProviderStreamingAdapter.CreateSsePost` now accepts a `Uri` parameter.
    - `AIInteractionImage.ImageUrl` is now `Uri` (was `string`).
  - Added `AIInteractionImage.SetResult(Uri imageUrl, string imageData = null, string revisedPrompt = null)` overload; kept string overload for backward compatibility.
  - Fixed tool call detection in streaming responses
    - Added `content_block_start` event handling to detect `tool_use` blocks
    - Streaming adapter now properly yields `AICallStatus.CallingTools` when tools are invoked
    - Fixed `content_block_delta` to check for `text_delta` type before processing text
    - Added support for `input_json_delta` events (tool argument streaming)
    - Enhanced debug logging for streaming events and tool detection
    - Non-streaming `Decode()` method now ensures `Arguments` field is never null

- Providers – OpenAI:
  - Simplified message encoding to use sequential approach (matching MistralAI pattern) instead of complex coalescing/deduplication logic. Eliminates duplicate tool call handling issues and improves reliability.
  - **Streaming adapter now extracts and streams reasoning content** from structured content arrays (o-series & gpt-5 models). Parses `type: "reasoning"` and `type: "thinking"` parts during streaming and appends to `AIInteractionText.Reasoning` field for live UI display.
  - **Fixed reasoning-only streaming**: Adapter now emits snapshots immediately when reasoning is received, even before text content arrives. Ensures live reasoning display in UI without waiting for answer text.

- Providers – MistralAI:
  - **Streaming adapter now extracts and streams thinking content** from structured content arrays. Parses `type: "thinking"` blocks during streaming and appends to `AIInteractionText.Reasoning` field for live UI display.
  - **Fixed reasoning-only streaming**: Adapter now emits snapshots immediately when thinking is received, even before text content arrives. Ensures live reasoning display in UI without waiting for answer text.

- Providers – DeepSeek:
  - **Fixed reasoning-only streaming**: Adapter now emits snapshots immediately when `reasoning_content` is received, even before text content arrives. Ensures live reasoning display in UI without waiting for answer text.

- UI and settings:
  - AI Chat component default system prompt to a generic one.
  - Settings dialog now organized in tabs.
    - Added tab for SmartHopper Assistant configuration (triggered from the canvas button on the top-right).
    - Added tab for Trusted Providers configuration.
  - CanvasButton chat now reuses a single `WebChatDialog` via a stable `componentId`, preventing multiple dialog instances from opening on repeated clicks.
  - Updated the About dialog to reflect the list of currently supported AI providers.
  - Provider settings: Disabled the "Enable Streaming" option for `DeepSeek` and `OpenRouter` (control is non-interactive) and updated the setting description to "Streaming is not available for this provider yet." Defaults set to `false` for both providers.

- Security/authentication and headers:
  - Improved API key encryption. Includes migration method.
  - Authentication refactor and centralized API key handling:
    - Providers select the auth scheme in `PreCall(...)`, while API keys are resolved internally by providers (never placed on `AIRequestCall`).
    - `AIProvider.CallApi(...)` now supports `"none"`, `"bearer"` and `"x-api-key"` (applies header using provider API key).
    - Streaming adapters apply auth via `AIProviderStreamingAdapter.ApplyAuthentication(...)` using provider-internal keys; `ApplyExtraHeaders(...)` now excludes reserved headers (`Authorization`, `x-api-key`).
  - Unified extra header handling via `HttpHeadersHelper`: both `AIProvider.CallApi(...)` and `AIProviderStreamingAdapter.ApplyExtraHeaders(...)` now delegate to the shared helper to eliminate duplication and ensure consistent reserved header filtering for streaming and non-streaming paths.

- Infrastructure and core models:
  - Complete refactor of `SmartHopper.Infrastructure` for clarity and organization.
  - Added `AIAgent`, `AIRequest` and `AIBody` models to improve clarity and extensibility. Refactored all code to use the new models.
  - Renamed `IChatModel` to `AIInteraction`.
  - Renamed `AIEvaluationResult` to `AIReturn`.
  - Renamed `AIResponse` to `AIReturnBody`.
  - Refactored all AI-powered tools to use the new `AIRequest` and `AIReturn` models.
  - Unified `GetResponse` and `GenerateImage` methods in `AIProvider` to a generic `Call` method.
  - `IAIReturn.Metrics` is writable; metrics now initialized in `AIProvider.Call()` with Provider, Model, and CompletionTime.
  - Providers refactored to use `AIInteractionText.SetResult(...)` for consistent content/reasoning assignment.
  - Renamed capabilities to Text2Text, ToolChat, ReasoningChat, ToolReasoningChat, Text2Json, Text2Image, Text2Speech, Speech2Text and Image2Text.
  - Standardized async data-tree processing in stateful components via shared `RunProcessingAsync` pipelines and configurable `ProcessingUnitMode`, improving consistency and enabling better progress tracking for item-based workflows.

- Model management and selection:
  - Simplified model selection policy in `ModelManager.SelectBestModel`: capability-first ordering using defaults for requested capability → best-of-rest; removed the separate "default-compatible" tier; selection is now fully centralized in `ModelManager` with no registry-level fallback or wildcard resolution.
  - Unified model retrieval via `IAIProviderModels.RetrieveModels()` with centralized registration in `ModelManager`. Components (e.g., `AIModelsComponent`) and tests updated to query `ModelManager` instead of calling per-provider legacy methods.
  - Provider-scoped model selection:
    - Added `IAIProvider.SelectModel(requiredCapability, requestedModel)` to encapsulate model resolution behind provider interface.
    - `AIProvider` base now implements `SelectModel(...)` delegating to centralized `ModelManager.SelectBestModel` while honoring provider defaults/settings.
    - `AIRequestBase.GetModelToUse()` refactored to call `provider.SelectModel(...)` instead of `ModelManager.Instance` directly.
    - Removed remaining direct calls to `ModelManager.Instance.SelectBestModel` outside provider internals.
    - Propagated model validation messages to components UI.

- Requests execution and validation:
  - `AIRequestCall.Exec()` is now explicitly single-turn (no tool orchestration). Multi-turn and tool processing are handled by `ConversationSession.RunToStableResult` when used explicitly.
  - `AIRequestBase.IsValid()` now blocks streaming when the selected provider disables streaming via settings or when the model is not streaming-capable, surfacing a clear validation error; these streaming validations now include `AIMessageCode` values (`StreamingDisabledProvider`, `StreamingUnsupportedModel`).
  - `AIRequestCall.IsValid()` now emits structured `AIMessageCode` values for provider/model and body validation:
    - `ProviderMissing`, `UnknownProvider`, `UnknownModel`, `NoCapableModel`, `CapabilityMismatch`
    - Endpoint/body issues are tagged as `BodyInvalid`
  - `AIStatefulAsyncComponentBase.UpdateBadgeCache()` prioritizes structured `Message.Code` for invalid/replaced decisions (`ProviderMissing`, `UnknownProvider`, `UnknownModel`, `NoCapableModel`, `CapabilityMismatch`) and falls back to message text only when `Code == Unknown`.

- Tools and components:
  - Grasshopper AI tools refactor: replaced legacy mutable `AIBody` usage with `AIBodyBuilder` + `AIReturn.CreateSuccess(body, toolCall)` for consistent immutable response construction. Updated tools: `gh_get`, `gh_put`, `gh_list_categories`. Ensured `AIToolCall.FromToolCallInteraction` is used and preserved existing error handling.
  - Verified badge now requires capability match (`Verified && HasCapability(RequiredCapability)`).
  - Badge cache computation now evaluates against the currently configured model (immediate UI feedback) and also surfaces replacement intent via selection fallback.
  - AIChatComponent: removed duplicated `_sharedLastReturn` storage and its lock. Removed internal methods `SetLastReturn(AIReturn)` and `GetLastReturn()`; components should rely on the base snapshot via `SetAIReturnSnapshot(...)` and use it for outputs.
  - AIChatComponent: unified snapshot management using base class snapshot exclusively. Renamed method to `SetAIReturnSnapshot(AIReturn)` for consistency across components. Updated chat transcript output to read from the base snapshot, ensuring live updates and metrics stay in sync.
  - AIChatWorker: removed worker-local `lastReturn` cache and fallback. `onUpdate` now updates only the base snapshot via `SetAIReturnSnapshot(...)`, and `SetOutput` reads exclusively from `CurrentAIReturnSnapshot` to keep chat history and metrics consistent.
  - Output lifecycle: `AIStatefulAsyncComponentBase` now exposes `protected virtual bool ShouldEmitMetricsInPostSolve()`; `OnSolveInstancePostSolve` respects this hook. Default behavior unchanged (metrics emitted in post-solve) unless overridden.
  - Refactor: Extracted timeout magic numbers (120/1/600) into named constants in `AIToolCall` (`DEFAULT_TIMEOUT_SECONDS`, `MIN_TIMEOUT_SECONDS`, `MAX_TIMEOUT_SECONDS`).
  - Disabled several untested or experimental AI tools/components by excluding them from the build (prefixed filenames with `_`) to keep the default toolbox focused on stable features.
  - Reorganized Grasshopper AI tool and component categories (including testing/experimental groups) for clearer grouping and discoverability inside Grasshopper.
  - AIListFilter: fix incorrect index array parsing
  - `mcneel_forum_search` simplified: now only accepts `query` and `limit` parameters and returns raw `results` and `count` without automatic AI summaries; use `mcneel_forum_post_summarize` explicitly when summaries are needed.

- Streaming behavior:
  - OpenAI provider: nested `OpenAIStreamingAdapter` now derives from `AIProviderStreamingAdapter` and reuses shared helpers; streaming behavior and statuses remain unchanged.
  - Centralized streaming capability check in `ModelManager.ModelSupportsStreaming(provider, model)` and updated validation to consult it.
  - `ConversationSession.Stream()` now gates streaming based on model capability and yields a clear error when unsupported.

- WebChat:
  - WebChatDialog: refactored to align with new base class API and recent infrastructure changes.
  - WebChatDialog greeting flow is now fully event-driven via `ConversationSession` observer callbacks. The UI no longer inserts or replaces a temporary greeting bubble; it only updates the status label during generation and renders greeting content from partial/final events.
  - Interaction override behavior clarified: greeting generation uses the initial request interactions (e.g., system prompt) to preserve context; normal user-initiated turns override from the current conversation history (last return interactions).
  - Default conversation context enabled for WebChat (Canvas Button and AIChatComponent): sets `AIBody.ContextFilter` to `"time, environment, selection"` so the assistant receives time, environment, and selection count by default. Implemented in `WebChatUtils.EnsureDialogOpen(...)` and `WebChatUtils.WebChatWorker`.
  - Improved default prompts in WebChat for clearer assistant behavior and tool guidance.
  - Improved UI with better collapsible messages, auto-scroll to bottom feature, "new messages" information tooltip, and improved thinking message
  - Dedicated error messages for validation errors in UI not being passed to APIs
  - Ensured fidelity between UI and conversation history

- Conversation orchestration:
  - `ConversationSession` now uses a unified internal loop (`TurnLoopAsync`) for both streaming and non‑streaming APIs to prevent logic drift.
  - Streaming persistence semantics updated: deltas are persisted into history per chunk in arrival order (no grouping or reordering at the end of the stream). Finalization only updates the "last return" snapshot.
  - Tool-call handling: removed internal deduplication-by-Id for `tool_call` interactions. Multiple tool calls with the same Id emitted by providers are now preserved in history. Session avoids introducing duplicates on its own when force-appending missing tool_calls prior to execution.
  - Fixed streaming delta notifications to only emit `OnDelta` for text interactions; non-text interactions (tool calls, tool results) now properly use `OnInteractionCompleted` after completion.

- Streaming adapters internals:
  - Streaming infrastructure: Introduced an enhanced SSE reader overload in `AIProviderStreamingAdapter.ReadSseDataAsync(HttpResponseMessage, TimeSpan?, Func<string,bool>?, CancellationToken)` that supports idle timeout, robust cancellation (disposing the underlying stream), and provider-specific terminal detection. The simple overload now delegates to the enhanced version (deduplication).
  - Providers updated to use enhanced SSE reader with a conservative 60s idle timeout:
    - OpenAI: uses new overload while keeping provider-level final chunk handling intact.
    - Anthropic: passes terminal predicate for `type == "message_stop"` to ensure early completion even without `[DONE]`.
    - MistralAI: passes terminal predicate when `finish_reason` appears in the payload to end the stream reliably.

### Security

- Prevented secret leakage by centralizing API key usage inside provider internals for both non-streaming and streaming flows.
- `AIRequestCall`, `AIReturn`, and logs do not contain API keys; reserved headers are applied internally only.

### Deprecated

- `CustomizeHttpClientHeaders` is deprecated for authentication/header setup. Providers must stop overriding it for auth and use request-scoped headers instead.

### Removed

- Providers and models:
  - Removed legacy model retrieval methods across providers/tests/docs: `RetrieveAvailable`, `RetrieveCapabilities`, and `RetrieveDefault`. Providers must expose models exclusively via `RetrieveModels()` during async initialization.
  - Removed the `TemplateProvider` since it will be explained in documentation.

- Context and metrics:
  - Removed the `ContextKeyFilter` and `ContextProviderFilter` in favor of a single `ContextFilter` that filters the providers.
  - Removed `AIToolCall.ReplaceReuseCount()` in favor of unified metrics handling.

- WebChat:
  - WebChatDialog: removed the assistant greeting loading placeholder and manual replacement logic in `InitializeNewConversation()`; greeting is appended by the session and rendered solely from observer updates.

### Fixed

- ConversationSession:
  - Fixed TurnId mismatch between tool calls and their results: tool results now inherit the TurnId from their originating tool call instead of receiving a new TurnId from the current turn iteration. This ensures proper correlation in WebChat rendering keys.

- Streaming providers:
  - **All providers** (DeepSeek, MistralAI, OpenAI): Fixed reasoning-only streaming chunks being overridden by content chunks in UI. When transitioning from reasoning-only to content streaming, providers now emit a completed (Finished) interaction for reasoning before starting content stream, triggering proper UI segmentation to prevent override.
  - DeepSeek: Fixed `OutputTokensReasoning` always showing 0. Now properly extracts reasoning tokens from nested `usage.completion_tokens_details.reasoning_tokens` field in both streaming and non-streaming responses.
  - OpenAI: Fixed `OutputTokensReasoning` always showing 0 for reasoning models (o1/o3/GPT-5). Now properly extracts reasoning tokens from nested `usage.completion_tokens_details.reasoning_tokens` field in both streaming and non-streaming responses.

- WebChatDialog:
  - Fixed assistant messages appearing out of order in the UI when tool calls are made. Empty assistant text interactions (which represent the decision to call tools) are now preserved in conversation history but skipped during UI rendering. The actual assistant response after tool execution renders as a separate segment (seg2) in the correct position after tool results.
  - Fixed duplicate greeting messages in UI. `OnFinal` now uses dedup keys for non-streamed interactions (like greetings) instead of creating new segmented keys, ensuring they upsert into existing bubbles rendered during history replay.
  - Fixed AI-generated greetings not streaming. Greeting initialization now uses `ConversationSession.Stream()` with streaming validation and fallback to `RunToStableResult()` on failure, matching the pattern used for regular user messages.

- Components – ImageViewer:
  - Fixed "ImageViewer" saving images errors. Now it will create a temporary file that will be deleted after saving to prevent file system issues.

- Model selection and metrics:
  - Fixed "Invalid model" when model manager was providing the wildcard instead of the actual default model name.
  - Corrected DataCount in metrics.

- Tools:
  - Fixed incorrect result output in `list_generate` tool.
  - Tool-call executions now retain correct provider/model context via `FromToolCallInteraction(..., provider, model)` to improve traceability and metrics accuracy.
  - Corrected script component GUIDs to match Grasshopper runtime values, ensuring tools and GhJSON generation can reliably create and reference script components.

- WebChat and streaming UI:
  - Prevent assistant replies from overwriting previous assistant messages: final assistant bubble now re-keys from the streaming key to the interaction's dedup key, so each turn is preserved in order.
  - WebChatDialog streaming: first assistant chunk now creates a new assistant message in the UI, subsequent chunks update the same bubble with the full accumulated text instead of replacing with only the last chunk; final content is persisted to history once on completion.
  - WebChatDialog streaming: partial assistant updates now also update internal `_lastReturn` and emit `ChatUpdated` events on every chunk, ensuring state consistency between UI and observers throughout streaming.
  - WebChatDialog non-streaming: fixed loss of AI metrics by merging `AIReturn.Metrics` into the final assistant interaction in `WebChatObserver.OnFinal` so per-message metrics are preserved in chat history and UI.
  - WebChat: reasoning-only assistant messages now render. `ChatResourceManager` renders the `Reasoning` as a collapsible panel and auto-expands it when there is no answer content, fixing empty message bubbles during streaming.
  - AIChat/WebChatDialog: Ensure the initial system prompt is added as the first system message in chat history and rendered in the UI on dialog initialization.
  - WebChatDialog: fixed compile-time errors by implementing missing methods (`InitializeWebViewAsync`, `ExecuteScript`, `RenderAndUpdateDom`) and UI handlers (`ClearButton_Click`, `SendButton_Click`, `UserInputTextArea_KeyDown`); added internal `DomUpdateKind` enum; ensured all UI/WebView operations marshal to Rhino's main UI thread.
  - HtmlChatRenderer: restored compatibility by adding `RenderInteraction(...)` wrapper used by `WebChatDialog`.
  - Introduced an internal DOM update queue to avoid running multiple WebView scripts concurrently, preventing race conditions and render glitches.
  - Fixed a loop in tool-call execution that could cause repeated or stuck tool-handling cycles.

- Providers:
  - DeepSeek: Do not force `response_format: json_object` for array schemas; use text output and a guiding system prompt instead. Decoder made robust to unwrap arrays from `content` parts and from wrapper objects (`items`, `list`, or malformed `enum`) to ensure a plain JSON array is returned.
  - MistralAI:
    - Streaming adapter fixes replacing invalid `AICallStatus.Error`/`NoContent` with `Finished`, using `AIReturn.CreateError(...)` for errors, and aligning streaming statuses (Processing → Streaming → Finished) with the OpenAI adapter pattern.
    - Fixed retrieval of available models when the API did not return the expected model list.
  - Anthropic: Fixed mapping/placement of system messages to ensure correct role semantics and prompt conditioning.

- Components – AIChat:
  - AIChatComponent: Prevent NullReferenceException when closing chat without responses. `SetOutput()` now null-checks the last interaction and outputs an empty string (with a debug notice) when none exists.
  - AIChatComponent: Eliminated duplicated/nested branches in "Chat History" output by centralizing output setting in `SolveInstance()` and removing the worker's `SetPersistentOutput` call. Ensures last interaction appears and output updates consistently from a single snapshot source.
  - AIChatComponent: Synchronized outputs. Metrics are now emitted from `SolveInstance()` together with "Chat History" (reading from base snapshot). Base post-solve metrics emission disabled via `ShouldEmitMetricsInPostSolve()` override to avoid duplicates. Fixes intermittent metrics not updating alongside chat during streaming/incremental updates.

- Components – Persistence and stability:
  - Prevent crash on GH file open by introducing a safe, versioned persistence (v2) for `StatefulAsyncComponentBase` that stores outputs as canonical string trees keyed by output parameter GUIDs. Legacy output restore is skipped by default and can be enabled via a feature flag.
  - WebChatDialog stability issues in certain scenarios.
  - Build stability after refactor (compilation issues resolved).
  - Infrastructure stability fixes.

- Image generation pipeline:
  - Fixed AI image output not reaching `ImageViewer` due to strict success check in `AIImgGenerateComponent`. Now treats missing `success` as true and only fails when an `error` is present, allowing the image URL/bitmap to flow to outputs.

- Streaming stability:
  - Streaming metrics propagation: after streaming completes, usage metrics (provider, model, input/output tokens, finish_reason) are now displayed in the chat UI. Implemented by:
    - Requesting OpenAI to include usage in the final stream chunk via `stream_options.include_usage = true` and parsing `prompt_tokens`/`completion_tokens` in `OpenAIProvider` streaming adapter.
    - Suppressing metrics during partial updates and merging final `AIReturn.Metrics` into the last assistant message in `WebChatObserver.OnFinal` when the final result has no interactions.
  - Streaming stability: Fixed indefinite streaming hangs across providers by using the enhanced SSE reader with idle timeouts and terminal event detection. OpenAI, Anthropic, and Mistral adapters now properly detect completion signals (e.g., `finish_reason`, `message_stop`) and exit the stream even if `[DONE]` is omitted by the provider.

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
- Removed `GetResponse` method from `AIStatefulAsyncComponentBase` in favor of `CallAiToolAsync`

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

- Added modular provider architecture:
  - Created new provider project structure (SmartHopper.Providers.MistralAI) with dedicated resources.
  - Created new provider project structure (SmartHopper.Providers.OpenAI) with dedicated resources.
  - Added IAIProviderFactory interface for dynamic provider discovery.
  - Implemented ProviderManager for runtime loading and management of providers.
  - Added IsEnabled property to IAIProvider interface to allow disabling template or experimental providers.
  - Created SmartHopper.Providers.Template project as a guide for implementing new providers.
- Added the new AIChat component with interactive chat interface and proper icon.
- Added WebView-based chat interface with AIChatComponent, WebChatDialog class, HtmlChatRenderer utility class, and ChatResourceManager.
- Added RunOnlyOnInputChanges property to StatefulAsyncComponentBase to control component execution behavior.
- Added AI provider selection improvements:
  - "Default" option in the AI provider selection menu to use the provider specified in SmartHopper settings.
  - Default provider selection in the settings dialog to set the global default AI provider.
- Added custom icon for the SmartHopper tab in Grasshopper.
- Added comprehensive Markdown formatting support:
  - Headings, code blocks, blockquotes, and inline formatting.
  - HTML tags like underline in Markdown text.
  - Dedicated Markdown class in the Converters namespace for centralized markdown processing.
- Added a "Supported Data Types" section to README.md documenting currently supported and planned Grasshopper-native types.
- New update-changelog-issues action and github-pr-update-changelog-issues to automatically mention missing closed issues in the changelog.

### Changed

- Refactored AI provider architecture:
  - Migrated MistralAI provider to a separate project (SmartHopper.Providers.MistralAI).
  - Migrated OpenAI provider to a separate project (SmartHopper.Providers.OpenAI).
  - Updated SmartHopperSettings to use ProviderManager for provider discovery.
  - Modified AIStatefulAsyncComponentBase to use the new provider handling approach.
  - Changed provider discovery to load assemblies from the main application directory instead of a separate "Providers" subdirectory.
  - Enhanced ProviderManager to only register providers that have IsEnabled set to true.
  - Added warning log when duplicate AI providers are encountered during registration instead of silently ignoring them.
- Modified AIChatComponent to always run when the Run parameter is true, regardless of input changes.
- Improved version badge workflow to also update badges when color doesn't match the requirements based on version type.
- Improved ChatDialog UI with numerous enhancements:
  - Modern chat-like interface featuring message bubbles and visual styling.
  - Better layout with proper text wrapping to prevent horizontal scrolling.
  - Responsive message sizing that adapts to the dialog width (80% max width with 350px minimum).
  - Message selection and copying capabilities with a context menu.
  - Automatic message height adjustment based on content and removal of visible scrollbars.
  - Improved scrolling behavior.
  - Allow only one chat dialog to be open per AI Chat Component. When running the component again, if there is a linked chat dialog, it will be focused instead of opening a new one.
- Enhanced About dialog:
  - Decreased font size.
  - Defined a minimum size.
  - Better layout and styling.
- Improved code organization:
  - All chat messages are now treated as markdown by default for consistent formatting.
  - Changed AI components to use the default provider from SmartHopper settings when "Default" is selected.
  - Updated component icon display to show the actual provider icon when "Default" is selected.
- Improved Web-based AIChat implementation:
  - Refactored WebChat resource management to use embedded resources instead of file system for improved security.
  - Enhanced WebView initialization for better cross-platform compatibility in Eto.Forms.
  - Improved error handling and debugging in ChatResourceManager and WebChatDialog.
  - Refactored WebChat HTML, CSS, and JavaScript into separate files for improved maintainability.
- Enhanced release-build.yml workflow:
  - Automatically build and attach artifacts to published releases.
  - Create platform-specific zip files (Rhino8-Windows, Rhino8-Mac) instead of a single zip with subfolders.
- Improved error handling in the AIStatefulAsyncComponentBase.
- Updated settings menu to use Eto.Forms and Eto.Drawing.
- Renamed the AI Context component to AI File Context.
- Enhanced context management system:
  - Support for multiple simultaneous context providers
  - Automatic time and environment context in AIChatComponent
  - Filtering capabilities for context by provider ID and specific context keys
  - Context filtering with comma-separated lists for multiple criteria
  - Exclusion filtering with minus prefix (e.g., "-time" excludes the time provider while including all others)
- Modified AboutDialog to inform users about the nature and limitations of AI-generated content

### Removed

- Removed MistralAI provider from SmartHopper.Config project as part of the modular architecture implementation.
- Removed OpenAI provider from SmartHopper.Config project as part of the modular architecture implementation.
- Removed dependency on HtmlAgilityPack

### Fixed

- Fixed AI provider handling:
  - Enable the AI Provider to be stored and restored from AI-powered components on writing and reading the file ([#41](https://github.com/architects-toolkit/SmartHopper/issues/41)).
  - Fixed AIChatComponent to properly use the default provider from settings when "Default" is selected in the context menu.
- Fixed build error for non-string resources in .NET Framework 4.8 target by adding GenerateResourceUsePreserializedResources property.
- Fixes "Bug: Settings menu hides sometimes" ([#94](https://github.com/architects-toolkit/SmartHopper/issues/94)).
- Fixes "Bug: AI Chat component freezes all Rhino!" ([#85](https://github.com/architects-toolkit/SmartHopper/issues/85)).
- Fixes "Bug: Settings Menu is incompatible with Mac" ([#12](https://github.com/architects-toolkit/SmartHopper/issues/12)).
- Fixes "AI disclaimer in chat and about" ([#114](https://github.com/architects-toolkit/SmartHopper/issues/114)).
- Fixed a bug opening the chat dialog that eventually froze the application.
- Fixed a bug where the chat dialog was not on top when clicking on it from the windows taskbar.

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
