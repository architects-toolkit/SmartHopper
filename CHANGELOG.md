# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.4] - 2026-07-14

### Added

- **DeepSeek provider**: Added `ReasoningEffort` and `TopP` settings. `ReasoningEffort` is a single dropdown that controls thinking mode and reasoning depth (`none` disables thinking; `high` or `max` enables it). `TopP` exposes nucleus sampling. `TopP` is included in every chat completion request; `ReasoningEffort` is mapped to DeepSeek's `thinking` and `reasoning_effort` parameters for DeepSeek-V4 and `deepseek-reasoner` models.
- **DeepSeek provider**: Removed the artificial `MaxTokens` cap of `8192`; the setting now supports values up to the API output limit.

### Fixed

- **DeepSeek provider**: Fixed multi-step tool call requests that failed with `invalid_request_error` because consecutive tool results were merged into a single message, breaking DeepSeek's requirement that each assistant `tool_calls` message is followed by one `tool` message per `tool_call_id`. Assistant tool calls and reasoning are now coalesced into one message while tool result messages remain separate; `reasoning_content` is emitted on assistant messages with `tool_calls` (with an empty string fallback) and stripped from text-only assistant messages to save bandwidth; and tool result content is now sent as a compact JSON string instead of a stripped/quoted string, matching the behavior of OpenAI and MistralAI.
- **DeepSeek and OpenRouter providers**: Removed the optional `name` field from `role=tool` result messages. While OpenAI's function-calling guide examples include `name` on tool results, DeepSeek's strict OpenAI-compatible validator appears to reject these messages, causing intermittent `insufficient tool messages` errors in long tool-call sequences. OpenRouter's documented tool-calling schema also omits `name` on tool result messages, so it was removed there for consistency. MistralAI's official examples include `name` on tool results, so it is preserved for that provider.

## [1.4.3] - 2026-07-05

Stable release from 1.4.3-rc with no changes.

## [1.4.3-rc] - 2026-05-18

### Added

- **Component Name Aliases**: Added `ComponentNameAliases` utility that maps informal AI-emitted component names (e.g., "python", "csharp", "slider") to their canonical Grasshopper names (e.g., "Python 3 Script", "C# Script", "Number Slider") before GhJSON placement. Aliases are resolved against the live Grasshopper component server to obtain actual GUIDs, preserving the original name for handler matching.

### Fixed

- Fixed ScriptGenerate output not being placed by GhPlace component.
- Fixed `gh_put` failing to instantiate components when AI uses informal names (e.g., "Python" instead of "Python 3 Script").
- Fixed script code not being applied to placed script components: alias resolution now sets `ComponentGuid` from the live component server while preserving the original name so GhJSON's deserialization handlers still match and apply extensions (e.g., script code).

## [1.4.2-rc] - 2026-05-17

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

### Added

- **New AI models** across all providers (Apr 2026 update):
  - OpenAI: `gpt-5.5`, `gpt-image-2` (new image flagship)
  - Anthropic: `claude-opus-4-7`
  - DeepSeek: `deepseek-v4-pro`, `deepseek-v4-flash`
  - MistralAI: multiple dated aliases and new `devstral-2-25-12` code-agent model
  - OpenRouter: mirrored models from native providers

### Changed

- **AI model rankings**: Adjusted default models and rankings across providers based on official documentation
- **Infrastructure**: Improved AI capability bit ordering and model catalog consistency
- **CI/CD**: Enhanced workflow automation for model verification, provider discovery, and stabilization branch management

## [1.4.2-beta] - 2026-04-15

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

### Changed

- **Infrastructure**: Improved provider stability, thread safety, and timeout handling
- **CI/CD**: Enhanced milestone management, stabilization workflows, and release automation

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

- **Contributors Workflow**: Automated GitHub workflow to maintain the contributors section in CHANGELOG.md

### Changed

- **Provider Security**: Three-tier verification mode (Soft/Hard/Strict) with cross-platform SHA-256 hash verification for Windows and macOS
- **UX & Components**: Renamed `AIScriptGenerator` to `AIScriptGenerate` for consistent naming; improved dialog messaging and sizing
- **CI/CD**: Enhanced automation for version management, git hooks, and public key anonymization

### Fixed

- Fixed provider hash verification performance with offline detection, caching, and reduced timeout
- Fixed macOS compatibility issues including race conditions, GhJSON validation, and JSON parsing ([#389](https://github.com/architects-toolkit/SmartHopper/issues/389), [#395](https://github.com/architects-toolkit/SmartHopper/issues/395), [#393](https://github.com/architects-toolkit/SmartHopper/issues/393))
- Fixed script tools to handle markdown-wrapped AI responses and GhJSON validation requirements
- Fixed GitHub Pages deployment to correctly place hash manifest files

## [1.4.0-alpha] - 2026-02-15

Many thanks to the following contributors to this release:

- [nofcfy-fanqi](https://github.com/nofcfy-fanqi) **First contribution!**
- [nof2504](https://github.com/nof2504) **First contribution!**
- [marc-romu](https://github.com/marc-romu)

### Added

- **Provider Hash Verification**: SHA-256 hash verification system for AI provider DLLs with manual verification menu item and comprehensive status dialog
- **Enhanced About Dialog**: Automatic display of current SmartHopper version and platform information

### Fixed

- **macOS Compatibility**: Improved cross-platform support with appropriate security warnings, URL handling fixes, and deadlock prevention in component state management
- **Settings**: Fixed encryption initialization to use local hash storage

### Security

- Platform-appropriate security: Authenticode + hash verification on Windows, hash verification only on macOS

### Known Issues

- WebChatDialog crashes on macOS due to Eto.Forms WKWebView URL handling limitations

## [1.3.0-alpha] - 2024-02-08

### Changed

- **GhJSON API**: Refactored to use organized ghjson-dotnet façade classes, removing deep namespace dependencies
- **Selection**: Changed `ISelectingComponent` to support scribble selection
- **CI/CD**: Added annual automation for copyright year updates and license header normalization

## [1.2.4] - 2024-02-08

### Changed

- Bump to stable version

## [1.2.4-alpha] - 2026-01-11

### Fixed

- AIStatefulAsyncComponentBase: Fixed issue where `context_usage_percent` was not being set correctly.

## [1.2.3-alpha] - 2026-01-11

### Added

- **Context Management**: Automatic context tracking with pre-emptive summarization at 80% usage, context limits for all providers, and error detection with retry
- **WebChat Debug**: Debug Update button and DOM synchronization for efficient chat view refreshes
- **GhJSON Tools**: Added `gh_get_start` and `gh_get_end` tools to retrieve start/end nodes with optional runtime data

### Changed

- **Context Management**: Conversation summaries use dedicated `AIAgent.Summary` with collapsible UI styling and automatic system prompt merging
- **GhJSON Serialization**: Excluded `PersistentData` and `VolatileData` for LLM-safe output; runtime data available via `_with_data` tools

### Fixed

- Fixed context usage percentage display in debug logs and WebChat UI metrics
- Fixed `gh_get` serialization context to preserve internalized parameter values
- Standardized node classification terminology and improved canvas guidance tools

## [1.2.2-alpha] - 2025-12-27

### Added

- **Chat**: Added `instruction_get` tool for detailed operational guidance to chat agents

### Changed

- **Infrastructure**: Refactored streaming adapters with provider-agnostic normalization, reduced code duplication, and improved state management
- **Chat UI**: Optimized DOM updates with keyed queue, template caching, and lighter streaming animations

### Fixed

- Fixed DeepSeek provider reasoning content propagation and duplicated reasoning display
- Fixed user messages not appearing in chat UI
- Fixed context provider selection counting with Rhino UI thread fallback
- Reduced WebChat dialog freezes during streaming with DOM batching and throttling ([#261](https://github.com/architects-toolkit/SmartHopper/issues/261))

## [1.2.1-alpha] - 2025-12-07

### Added

- **Script Tools**: Added `script_generate_and_place_on_canvas` wrapper tool to reduce token consumption
- **GhJSON Tools**: Added `_with_data` variants for runtime data extraction (`gh_get_selected_with_data`, `gh_get_by_guid_with_data`, `gh_get_errors_with_data`)
- **gh_put**: Added `instanceGuids` array to tool result for subsequent queries

### Fixed

- Fixed script tool returning incorrect instance GUID from canvas placement
- Fixed GhJSON validation to treat missing connections as valid
- Fixed chat UI collapsing identical user messages and TurnId inconsistency for tool results

## [1.2.0-alpha] - 2025-12-06

### Added

- **Dialog Canvas Links**: Visual connection lines from dialogs to linked Grasshopper components
- **Component Replacement**: Edit mode for `gh_put` with undo support and position preservation
- **GhJSON Merge**: Merge utility with AI tool and Grasshopper component for combining documents
- **Script Tools**: GhJSON-based `script_generate` and `script_edit` with full parameter modifier support, geometry validation, and language-specific guidance
- **Model Badges**: "Not Recommended" badge when model is discouraged for component's AI tools

### Changed

- **Components UI**: Combined attributes for AI-selecting components with improved dialog link visualization
- **Script Components**: Replaced monolithic `script_generator` with focused GhJSON-based tools; removed Guid input
- **Providers**: Added Claude Opus 4.5; OpenRouter structured output support
- **Icons**: Updated to outlined variants for consistency

### Fixed

- Fixed chat UI metrics to show total token consumption per turn
- Fixed WebChat dialog visibility as owned tool window following Rhino focus
- Fixed `gh_put` infinite loop and component expiration errors in replacement mode
- Fixed script tool schema requirements for OpenAI structured-output mode

## [1.1.1-alpha] - 2025-11-24

### Changed

- **AI models**: Added Claude 3.x/4.x dated identifiers, Anthropic structured-output support, and OpenAI GPT-5.1 series models

## [1.1.0-alpha] - 2025-11-23

### Added

- **VB Script Support**: Complete serialization/deserialization for 3-section VB Script structure with custom parameter management
- **Parameter Tools**: New AI tools for parameter modification (flatten, graft, reverse, simplify, bulk inputs/outputs)
- **Script Tools**: Unified script generation tool with parameter management capabilities
- **McNeel Forum Integration**: Search, retrieval, and summarization tools for McNeel forum posts
- **Web Reading**: Enhanced support for Wikipedia, Discourse forums, GitHub/GitLab, and Stack Exchange
- **Rhino 3DM Analysis**: Tools for analyzing .3dm files and extracting geometry from Rhino documents
- **Component Tools**: GhJSON generation and component connection tools
- **Knowledge Components**: Grasshopper components for McNeel forum and web knowledge workflows
- **Selection Persistence**: Selected objects now persist when saving and loading Grasshopper files
- **Hotfix Workflows**: Emergency hotfix release system with automated version management

### Changed

- **Minimum Requirements**: Rhino 8.24 or later required
- **GhJSON Schema**: BREAKING - New schema format with improved property management and reduced JSON size
- **Property Management**: Complete refactoring with modern architecture and better type handling
- **AI Tool Names**: Renamed for consistency (e.g., `gh_toggle_preview` → `gh_component_toggle_preview`)
- **Script Components**: Unified script generator replaces separate new/edit tools
- **Serialization**: Optimized JSON output with cleaner formatting and empty string omission
- **Model Validation**: Allows use of unregistered models
- **Error Handling**: Centralized error handling in AIReturn and tool calls
- **Metrics**: Improved conversation session metrics aggregation
- **CI/CD**: Extended validation workflows for hotfix and release branches

### Removed

- Legacy PropertyManager system
- Legacy script tools (`script_new`, `script_edit`) and components

### Fixed

- DataTreeProcessor branch handling and flattening
- Model badge display for invalid models
- Provider error surfacing in WebChat UI
- Rectangle serialization format
- Stand-alone parameter serialization and connections
- Connection matching by index
- Group and InstanceGuid serialization
- Component pivot handling in gh_put
- Script component parameter modifiers and type hints
- Fixes "script_edit tool freezes the script editor" ([#209](https://github.com/architects-toolkit/SmartHopper/issues/209))

## [1.0.1-alpha] - 2025-10-13

### Changed

- **Model Validation**: Allows use of unregistered models
- **Error Handling**: Centralized error handling in AIReturn and tool calls
- **Metrics**: Improved conversation session metrics aggregation
- **AI Tools**: Enhanced tool descriptions and specialized wrappers
- **List Filter**: Expanded capabilities for filtering, sorting, and reordering

### Fixed

- Model badge display for invalid models ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332), [#329](https://github.com/architects-toolkit/SmartHopper/issues/329))
- Provider error surfacing in WebChat UI ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334))
- List filter preserving order and duplicates ([#335](https://github.com/architects-toolkit/SmartHopper/issues/335))

## [1.0.0-alpha] - 2025-10-11

### Added

- **Canvas Button**: Assistant dialog trigger from canvas with configurable enable/disable setting
- **Context Providers**: File and document context (selection count, object count, component count, etc.)
- **Conversation Session**: Multi-turn conversation orchestration with observer pattern and policy pipeline
- **Special Turn System**: Isolated AI request execution with custom overrides and history persistence strategies
- **Streaming Infrastructure**: Provider-agnostic streaming adapters with centralized HTTP/auth handling
- **Tool Validation**: AI tool validation system with existence, schema, and capability checks
- **Component Badges**: Visual indicators for verified, deprecated, invalid, and replaced models
- **New Providers**: Anthropic and OpenRouter providers
- **Diagnostics**: Machine-readable `AIMessageCode` enum for structured error reporting
- **Documentation**: Summary documentation at `docs/`

### Changed

- **Infrastructure Refactor**: Complete reorganization of `SmartHopper.Infrastructure` with new models (`AIAgent`, `AIRequest`, `AIBody`, `AIReturn`)
- **Model Management**: Centralized model selection and capability validation
- **Streaming**: Enhanced reasoning content streaming across all providers (OpenAI, MistralAI, DeepSeek)
- **Authentication**: Centralized API key handling with improved encryption
- **UI/Settings**: Tabbed settings dialog, improved WebChat with collapsible messages and auto-scroll
- **Request Execution**: Explicit single-turn `AIRequestCall.Exec()`, multi-turn via `ConversationSession`
- **Tools**: Refactored to use immutable `AIBodyBuilder` pattern

### Security

- Centralized API key usage prevents secret leakage in logs and requests

### Deprecated

- `CustomizeHttpClientHeaders` for authentication (use request-scoped headers instead)

### Removed

- Legacy model retrieval methods (`RetrieveAvailable`, `RetrieveCapabilities`, `RetrieveDefault`)
- Legacy context filters (`ContextKeyFilter`, `ContextProviderFilter`)
- TemplateProvider

### Fixed

- Streaming reasoning content display and metrics across all providers
- WebChat message ordering, duplicate greetings, and metrics propagation
- Model selection wildcard resolution and validation badges
- Tool execution context and script component GUIDs
- Component persistence stability and image generation pipeline
- Streaming stability with idle timeouts and terminal detection

## [0.5.3-alpha] - 2025-08-20

### Fixed

- Fixed JSON schema validation in script tool ([#304](https://github.com/architects-toolkit/SmartHopper/issues/304)).

## [0.5.2-alpha] - 2025-08-12

### Fixed

- Fixed provider initialization issues and fallback behavior for API errors

## [0.5.1-alpha] - 2025-07-30

### Added

- Setting to enable/disable AI-generated greeting in chat

### Fixed

- Fixed model selection and component trigger issues
- Fixed list generation for long requests ([#277](https://github.com/architects-toolkit/SmartHopper/issues/277))

## [0.5.0-alpha] - 2025-07-29

### Added

- **Model Capability Management**: Centralized capability tracking with provider-specific detection and tool validation
- **Image Generation**: AI image generation support with DALL-E models and image viewer component
- **AI Tools**: Enhanced filtering and component validation
- **Settings**: Provider settings management improvements

### Changed

- Improved provider settings initialization

### Fixed

- Fixed GhJSON and parsing issues ([#276](https://github.com/architects-toolkit/SmartHopper/issues/276))

## [0.4.1-alpha] - 2025-07-23

### Added

- Progress reporting for async components

### Fixed

- Fixed component state transitions and boolean toggle handling ([#113](https://github.com/architects-toolkit/SmartHopper/issues/113), [#260](https://github.com/architects-toolkit/SmartHopper/issues/260))
- Fixed preview toggle compatibility with parameters ([#208](https://github.com/architects-toolkit/SmartHopper/issues/208))
- Fixed dialog closing behavior

## [0.4.0-alpha] - 2025-07-22

### Added

- Chat message removal and milestone management automation
- Provider-specific JSON schema handling

### Changed

- Enhanced chat greeting with loading animation and improved model handling ([#255](https://github.com/architects-toolkit/SmartHopper/issues/255))
- Improved component model selection and reasoning text handling
- Enhanced CI/CD workflows for milestone and PR management

### Fixed

- Fixed model handling and structured output compatibility ([#259](https://github.com/architects-toolkit/SmartHopper/issues/259), [#273](https://github.com/architects-toolkit/SmartHopper/issues/273))

## [0.3.6-alpha] - 2025-07-20

### Added

- Added icon to AIModels component

## [0.3.5-alpha] - 2025-07-19

### Added

- Provider API methods and model retrieval
- AIModels component for listing available models

### Changed

- Improved provider architecture and code organization

### Removed

- Removed MathJax support from chat UI

## [0.3.4-alpha] - 2025-07-11

### Added

- Instructions input to AIChat component ([#87](https://github.com/architects-toolkit/SmartHopper/issues/87))
- Context filtering improvements with wildcard support
- Component grouping and list generation tools ([#6](https://github.com/architects-toolkit/SmartHopper/issues/6))
- AI tool categorization

### Changed

- Improved chat system prompts and context management
- Renamed SmartHopper.Config to SmartHopper.Infrastructure
- Improved tool filtering and organization

## [0.3.3-alpha] - 2025-06-23

### Added

- **New Provider**: DeepSeek provider ([#222](https://github.com/architects-toolkit/SmartHopper/issues/222))
- **Reasoning Support**: Collapsible reasoning panels in chat UI with configurable effort for OpenAI o-series models
  - Reorganized providers settings and moved them from `AIProvider` to `AIProviderSettings`.

### Changed

- Removed `<tool_call>` tags from messages before sending them to the API, using the `StripThinkTags` method from `Config.Utils.AI`.

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

- **Script Tools**: New AI tools for reviewing and generating Grasshopper scripts with full parameter modifier support
- **Undo Support**: Added undo capability to canvas operations (move, preview toggle, lock toggle)
- **Icons**: Updated icons for all components

### Changed

- **Minimum Requirements**: Rhino 8.19 or later required
- **AI Tools**: Renamed tools for consistency (e.g., `evaluateText` → `text_evaluate`, `evaluateList` → `list_evaluate`)
- **OpenAI Provider**: Added structured output support

### Deprecated

- `GetResponse` method in `AIStatefulAsyncComponentBase` (use `CallAiTool` instead)

### Fixed

- Fixed MistralAI provider structured output compatibility ([#112](https://github.com/architects-toolkit/SmartHopper/issues/112))
- Fixed OpenAI API URI error

## [0.3.1-alpha] - 2025-05-06

### Added

- **Chat UI**: Copy codeblocks to clipboard, collapsible tool messages, and inline metrics per message
- **Component Layout**: `gh_tidy_up` tool and component for arranging components into dependency-based grid layout
- **Script Components**: Added support in `GhGet`

### Changed

- **Component Movement**: Improved layout algorithm with smooth animations and better positioning
- **GhJSON**: Enhanced validation and automatic fixing of invalid InstanceGuids

### Fixed

- Fixed tool call JSON structure for MistralAI and OpenAI providers
- Fixed component placement spacing in `gh_put` tool
- Fixed `gh_tidy_up` moving components on every execution
- Fixes "Panels and params position is calculated from top-left, not from center" ([#184](https://github.com/architects-toolkit/SmartHopper/issues/184))

## [0.3.0-alpha] - 2025-04-27

### Added

- **AI Chat Tool Execution**: Enabled AIChat component to execute Grasshopper tools
- **Component Tools**: New tools for toggling preview/lock, moving components by GUID, and retrieving component types
- **Web Tools**: Added tools to retrieve webpages and search/query Rhino Forum posts
- **Component Filtering**: Enhanced `GhGetComponents` with filter and type filter inputs
- **Security**: Provider assemblies must be signed for acceptance

### Changed

- **Minimum Requirements**: Rhino 8 or later required (removed .NET Framework 4.8 support)
- **Component Organization**: Reorganized `SmartHopper.Core.Grasshopper` files into namespace-matching subfolders
- **Settings**: Isolated provider settings access through `ProviderManager`
- **UI**: SmartHopper icon now used consistently across all dialogs

### Fixed

- Fixed double-encryption of sensitive settings causing unreadable API keys
- Fixed mismatch between in-memory and on-disk `TrustedProviders`
- Fixed `DataProcessor` result duplication with grouped branches ([#32](https://github.com/architects-toolkit/SmartHopper/issues/32))
- Fixed MistralAI provider not loading AI tools

## [0.2.0-alpha] - 2025-04-06

### Added

- **AIChat Component**: Interactive chat interface with WebView-based UI and proper icon
- **Modular Provider Architecture**: Dynamic provider discovery and runtime loading with separate provider projects
- **Provider Selection**: "Default" option to use global default provider from settings
- **Markdown Support**: Comprehensive formatting with headings, code blocks, blockquotes, and inline formatting
- **Context Management**: Multiple simultaneous context providers with filtering capabilities
- **Component Execution**: RunOnlyOnInputChanges property to control component behavior

### Changed

- **Chat UI**: Modern interface with message bubbles, responsive sizing, and improved scrolling
- **Provider Architecture**: Migrated MistralAI and OpenAI to separate projects
- **Settings**: Updated to use Eto.Forms for cross-platform compatibility
- **Context**: Renamed AI Context to AI File Context

### Fixed

- Fixed AI provider storage and restoration in files ([#41](https://github.com/architects-toolkit/SmartHopper/issues/41))
- Fixes "Bug: Settings menu hides sometimes" ([#94](https://github.com/architects-toolkit/SmartHopper/issues/94))
- Fixes "Bug: AI Chat component freezes all Rhino!" ([#85](https://github.com/architects-toolkit/SmartHopper/issues/85))
- Fixes "Bug: Settings Menu is incompatible with Mac" ([#12](https://github.com/architects-toolkit/SmartHopper/issues/12))

## [0.1.2-alpha] - 2025-03-17

### Changed

- **CI/CD**: Enhanced PR title validation and workflow automation

### Fixed

- Fixed version badge and GitHub Actions workflows

### Security

- Updated GitHub Actions to latest versions and implemented security best practices

## [0.1.1-alpha] - 2025-03-03

### Added

- **GhGetSelectedComponents**: New component for selecting Grasshopper components
- **AI Context**: New component for providing context to AI tools ([#40](https://github.com/architects-toolkit/SmartHopper/issues/40))

### Changed

- **About Dialog**: Updated to use Eto.Forms for cross-platform compatibility
- **Code Organization**: Refactored AI text and list processing tools for improved reusability

### Fixed

- Fixed Persistent Data functionality in GhPutComponents
- Fixed pivot grid generation when missing in JSON input

## [0.1.0-alpha] - 2025-01-27

### Added

- **AITextEvaluate**: New component for AI text evaluation

### Changed

- **Component Naming**: Renamed AI List Check to AI List Evaluate
- **Component Base**: Full rewrite of component framework

### Fixed

- Fixed Feature request: Full rewrite of the Component Base ([#20](https://github.com/architects-toolkit/SmartHopper/issues/20))
- Fixed Feature request: AI Text Check Component ([#4](https://github.com/architects-toolkit/SmartHopper/issues/4))

## [0.0.0-dev.250126] - 2025-01-26

### Added

- **Component Base**: New framework for AI-powered components with debouncing, state management, and output persistence
- **Testing Components**: New library for testing components

### Changed

- **Component Migration**: Migrated AI Text Generate to new Component Base
- **DataTree**: Refactored libraries for unified functionality

### Fixed

- Fixed API key error handling ([#13](https://github.com/architects-toolkit/SmartHopper/issues/13))
- Fixed Graft/Flatten recompute requirement ([#7](https://github.com/architects-toolkit/SmartHopper/issues/7))
- Fixed output persistence on file open ([#8](https://github.com/architects-toolkit/SmartHopper/issues/8))
- Fixed multiple API calls on SolveInstance ([#24](https://github.com/architects-toolkit/SmartHopper/issues/24))

## [0.0.0-dev.250104] - 2025-01-04

### Added

- **Metrics**: Added AI Provider and AI Model metrics to AI-powered components ([#11](https://github.com/architects-toolkit/SmartHopper/issues/11))

### Fixed

- Fixed model input handling in AI-powered components ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))
- Fixed AI response metrics to include all branches ([#2](https://github.com/architects-toolkit/SmartHopper/issues/2))

## [0.0.0-dev.250101] - 2025-01-01

### Added

- **Initial Release**: Core plugin architecture for Grasshopper integration with base component framework
- **CI/CD**: GitHub Actions workflow for automated validation (version format, changelog, conventional commits)
- **Documentation**: README with setup instructions and CONTRIBUTING guidelines
