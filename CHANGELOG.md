# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]



### Fixed
- Fixes "Bug: Unmatching paths in list components return duplicated values" ([#32](https://github.com/architects-toolkit/SmartHopper/issues/32)).

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
