# Version Tools

A set of composite actions for managing versioning in the SmartHopper project.

## Overview

This directory contains several composite actions that help with various aspects of version management:

1. **Version Tools** (`action.yml`) - The main action for core version operations:
   - `get-version`: Extract the current version from Solution.props
   - `update-version`: Update the version in Solution.props (automatically updates README badges)
   - `update-badge`: Only update the version badge in README.md

2. **Version Calculator** (`version-calculator/action.yml`) - For calculating new versions based on semantic versioning rules:
   - Supports multiple increment types: `patch`, `minor`, `major`, `date`, `auto-date`, `pre-release`
   - Automatically detects and updates dates in development versions when using `auto-date`

3. **Changelog Updater** (`changelog-updater/action.yml`) - For updating the CHANGELOG.md file:
   - Updates both structure and content
   - Supports security fixes
   - Maintains proper changelog format

## Usage Examples

### Getting the Current Version

```yaml
- name: Get current version
  id: current-version
  uses: ./.github/actions/version-tools
  with:
    task: get-version
```

### Calculating a New Version

```yaml
- name: Calculate new version
  id: calculate-version
  uses: ./.github/actions/version-tools/version-calculator
  with:
    version: ${{ steps.current-version.outputs.version }}
    increment: patch  # Options: none, patch, minor, major, date, auto-date
    change-pre-release: none  # Options: none, dev, alpha, beta, rc, remove
```

### Auto-updating Date in Development Versions

```yaml
- name: Auto-update version date if needed
  id: update-date
  uses: ./.github/actions/version-tools/version-calculator
  with:
    version: ${{ steps.current-version.outputs.version }}
    increment: auto-date
```

### Changing Pre-release Type

```yaml
- name: Change to beta release
  id: change-to-beta
  uses: ./.github/actions/version-tools/version-calculator
  with:
    version: ${{ steps.current-version.outputs.version }}
    increment: none
    change-pre-release: beta
```

### Updating the Version in Solution.props

```yaml
- name: Update version
  uses: ./.github/actions/version-tools
  with:
    task: update-version
    new-version: ${{ steps.calculate-version.outputs.new-version }}
```

### Updating the CHANGELOG.md

```yaml
- name: Update changelog
  uses: ./.github/actions/version-tools/changelog-updater
  with:
    version: ${{ steps.calculate-version.outputs.new-version }}
    security-fix: true
    security-description: "Fixed critical security vulnerability in XYZ component"
```

## Action Inputs and Outputs

### Version Tools (Main Action)

**Inputs:**
- `task`: (required) One of `get-version`, `update-version`, `update-badge`
- `new-version`: (required for update-version) New version to set
- `branch`: (optional) Branch name for context

**Outputs:**
- `version`: Current version from Solution.props
- `major`, `minor`, `patch`, `suffix`: Version components
- `badges-changed`: Whether badges were changed

### Version Calculator

**Inputs:**
- `version`: (required) Input version in format X.X.X or X.X.X-suffix.YYMMDD
- `increment`: (required) Type of increment:
  - `none`: No increment, only apply other changes
  - `patch`: Increment patch version
  - `minor`: Increment minor version
  - `major`: Increment major version
  - `date`: Explicitly update the date part of a pre-release version
  - `auto-date`: Auto-detect if this is a dated version and update it if needed
- `change-pre-release`: (optional) Change or add pre-release suffix:
  - `none`: No change to pre-release suffix (default)
  - `dev`: Change to development pre-release
  - `alpha`: Change to alpha pre-release
  - `beta`: Change to beta pre-release
  - `rc`: Change to release candidate
  - `remove`: Remove pre-release suffix entirely
- `pre-release-date`: (optional) Date to use for pre-release in format YYMMDD

**Outputs:**
- `new-version`: Calculated new version
- `is-prerelease`: Whether the calculated version is a pre-release
- `was-date-updated`: Whether the date was updated in a development version
- `major`, `minor`, `patch`, `suffix`: Version components

### Changelog Updater

**Inputs:**
- `version`: (required) Version to add to changelog
- `date`: (optional) Release date (defaults to today)
- `update-structure-only`: (optional) Only update structure without adding entries
- `security-fix`: (optional) Whether this is a security fix
- `security-description`: (optional) Description of the security fix
- `changelog-path`: (optional) Path to CHANGELOG.md file

**Outputs:**
- `updated`: Whether the changelog was updated
- `unreleased-content`: Content that was in the Unreleased section
