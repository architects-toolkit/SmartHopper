# Versioning Actions

This directory contains reusable GitHub Actions for semantic version management across the SmartHopper release pipeline.

## Shared Version Utilities

All versioning actions use **consistent version parsing patterns** to ensure uniform behavior across the pipeline.

### Version Format

All versions follow semantic versioning with optional pre-release suffixes:

- **Stable**: `X.Y.Z` (e.g., `1.4.3`)
- **Pre-release**: `X.Y.Z-STAGE[.DATE]` (e.g., `1.4.3-alpha`, `1.4.3-alpha.240101`)

### Shared Parsing Logic

The following functions are standardized across all actions:

#### `parseVersion(versionStr)`

Parses a version string into components:

```javascript
{
  major: number,
  minor: number,
  patch: number,
  suffix: string | null,      // Full suffix (e.g., "alpha.240101")
  original: string
}
```

#### `formatVersion(version)`

Formats a version object back to string:

```javascript
formatVersion({ major: 1, minor: 4, patch: 3, suffix: "alpha" })
// Returns: "1.4.3-alpha"
```

#### `getStage(suffix)`

Extracts release stage from suffix:

```javascript
getStage("alpha.240101")  // Returns: "alpha"
getStage("beta")          // Returns: "beta"
getStage(null)            // Returns: "stable"
```

### Regex Pattern

All actions use this consistent regex for version parsing:

```regex
^(\d+)\.(\d+)\.(\d+)(?:-([a-zA-Z0-9.]+))?$
```

This pattern:

- Captures major, minor, patch as separate groups
- Allows optional suffix with dots (e.g., `alpha.240101`)
- Handles both stable (`1.4.3`) and pre-release (`1.4.3-alpha`) formats

## Actions Overview

### Core Parsing Actions

#### `parse-version`

Parses a version string into components (major, minor, patch, suffix, stage).

**Inputs:**

- `version`: Version string to parse

**Outputs:**

- `major`, `minor`, `patch`: Version components
- `suffix`: Full suffix (if any)
- `stage`: Release stage (alpha, beta, rc, stable)
- `is-prerelease`: Boolean flag

#### `format-version`

Formats version components back into a semantic version string.

**Inputs:**

- `major`, `minor`, `patch`: Version components
- `suffix`: Optional suffix

**Outputs:**

- `version`: Formatted version string

### Milestone Management

#### `manage-milestones`

Creates next-stage milestones and manages active milestones for release versions.

**Shared Utilities:**

- `parseVersion()` - Parses version strings
- `formatVersion()` - Formats version objects
- `getStage()` - Extracts stage from suffix

#### `move-milestone-items`

Moves open issues and PRs from closed milestones to target milestones.

**Shared Utilities:**

- `parseVersion()` - Parses version strings
- `formatVersion()` - Formats version objects
- `getStage()` - Extracts stage from suffix

### Version Promotion

#### `promote-version`

Promotes a version from one stage to the next (alpha→beta→rc→stable).

**Uses:** `parse-version` action for consistent parsing

### Version Retrieval and Updates

#### `get-version`

Extracts current version from Solution.props with component parsing.

**Standardized Parsing:**

- Uses consistent regex pattern
- Extracts stage from suffix
- Outputs major, minor, patch, suffix, stage

#### `update-version`

Updates version in Solution.props with component parsing.

**Standardized Parsing:**

- Uses consistent regex pattern
- Extracts stage from suffix
- Outputs major, minor, patch, suffix, stage

## Shared Version Utilities Implementation

### Pattern: Inline Shared Functions

All JavaScript actions that use version parsing include the same shared utility functions marked with clear delimiters:

```javascript
// ===== SHARED VERSION UTILITIES (used across versioning actions) =====
function parseVersion(versionStr) { ... }
function formatVersion(version) { ... }
function getStage(suffix) { ... }
// ===== END SHARED VERSION UTILITIES =====
```

### Why Inline Functions?

GitHub Actions limitations make inline functions the most practical approach:

- `actions/github-script` doesn't support passing scripts as inputs
- Composite actions can't effectively output multi-line code blocks
- Inline functions are self-contained and require no external dependencies
- Clear delimiters make utilities easy to identify and maintain

### Shared Functions Reference

#### parseVersion()

Parses a semantic version string into components.

**Input:** `"1.4.3-alpha.240101"`

**Output:**

```javascript
{
  major: 1,
  minor: 4,
  patch: 3,
  suffix: "alpha.240101",
  original: "1.4.3-alpha.240101"
}
```

#### formatVersion()

Formats a version object back to string.

**Input:**

```javascript
{ major: 1, minor: 4, patch: 3, suffix: "alpha" }
```

**Output:** `"1.4.3-alpha"`

#### getStage()

Extracts release stage from suffix.

**Examples:**

- `getStage("alpha.240101")` → `"alpha"`
- `getStage("beta")` → `"beta"`
- `getStage(null)` → `"stable"`

### Actions Using Shared Utilities

- **manage-milestones** - Uses all three functions
- **move-milestone-items** - Uses all three functions

### Maintenance

When updating shared utilities:

1. Update the function in `manage-milestones/action.yml`
2. Update the function in `move-milestone-items/action.yml`
3. Update the reference documentation in this README
4. Ensure both implementations remain identical
