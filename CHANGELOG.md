# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Settings parameter to enable/disable AI generated greeting in chat.

### Fixed

- Greeting generation was using stored settings models instead of the provider's default model. To solve it, now if `AIUtils.GetResponse` doesn't get a model, it will use the provider's default model.
- Components triggered with a Boolean Toggle (permanent true value) weren't calculating when the toggle was turned to true.
- Lazy default values in `AI Provider Settings` to prevent race conditions at initialization.
- Fixed "List length in list_generate was not met for long requests" ([#277](https://github.com/architects-toolkit/SmartHopper/issues/277)

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
