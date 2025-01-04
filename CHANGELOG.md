# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Added metrics for AI Provider and AI Model in AI-Powered components ([#11](https://github.com/architects-toolkit/SmartHopper/issues/11))

### Fixed
- Fixed bug with the Model input in AI-Powered components ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))
- Fixed model parameter handling in IAIProvider interface to ensure proper model selection across providers ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))

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