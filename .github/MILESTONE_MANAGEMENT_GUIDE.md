# Milestone Management System Guide

## Overview

The milestone management system automates the creation and lifecycle of milestones across different release stages (alpha → beta → rc → stable). The key principle is **scoping all operations to the same major version**, allowing different major versions to coexist independently.

## System Behavior Summary

| Scenario | Released Version | Action | Milestones Created | Milestones Closed | Notes |
| --- | --- | --- | --- | --- | --- |
| **Alpha Release** | `1.4.3-alpha` | Creates beta + next minor alpha | `1.4.3-beta`, `1.5.0-alpha` | Older `1.x.x-beta` only | Closes only beta milestones in major version 1 |
| **Alpha Release** | `2.0.0-alpha` | Creates beta + next minor alpha | `2.0.0-beta`, `2.1.0-alpha` | Older `2.x.x-beta` only | Closes only beta milestones in major version 2 |
| **Beta Release** | `1.4.3-beta` | Creates rc | `1.4.3-rc` | Older `1.x.x-rc` only | Closes only rc milestones in major version 1 |
| **RC Release** | `1.4.3-rc` | Creates stable | `1.4.3` | Older `1.x.x` (stable) only | Closes only stable milestones in major version 1 |
| **Stable Release** | `1.4.3` | No action | — | — | No milestones created for stable releases |

## Coexistence Examples

### Multiple Major Versions in Flight

```text
Open Milestones:
├── 1.3.2-beta      ✓ Active (latest beta in v1)
├── 1.4.3-beta      ✓ Active (latest beta in v1)  ← Would close 1.3.2-beta when 1.4.3-beta created
├── 1.5.0-alpha     ✓ Active (unlimited alphas)
├── 1.6.0-alpha     ✓ Active (unlimited alphas)
├── 2.0.0-beta      ✓ Active (latest beta in v2)
├── 2.1.0-alpha     ✓ Active (unlimited alphas)
└── 3.0.0-rc        ✓ Active (latest rc in v3)
```

**Key Points:**

- ✅ `1.3.2-beta` and `2.0.0-beta` coexist (different major versions)
- ✅ Unlimited alpha milestones allowed per major version
- ✅ Only one active beta per major version
- ✅ Only one active rc per major version
- ✅ Only one active stable per major version

### Closure Behavior

When `1.4.3-alpha` is released (published):

- ✅ Closes `1.4.3-alpha` milestone (release triggers closure)
- ✅ Creates `1.4.3-beta` milestone (next stage)
- ✅ Creates `1.5.0-alpha` milestone (next minor)
- ✅ Closes older `1.x.x-beta` milestones (keeps only latest beta per major)
- ✅ **Does NOT** affect `2.x.x-beta` milestones (different major version)

When `1.4.3-beta` is released (published):

- ✅ Closes `1.4.3-beta` milestone
- ✅ Creates `1.4.3-rc` milestone (next stage)
- ✅ Creates `1.5.0-alpha` milestone (next minor, if not exists)
- ✅ Closes older `1.x.x-rc` milestones (keeps only latest rc per major)
- ✅ **Does NOT** affect `1.x.x-beta` or `2.x.x-rc` milestones

When `2.0.0-beta` is released (published):

- ✅ Closes `2.0.0-beta` milestone
- ✅ Creates `2.0.0-rc` milestone (next stage)
- ✅ Creates `2.1.0-alpha` milestone (next minor)
- ✅ Closes older `2.x.x-beta` milestones
- ✅ **Does NOT** affect `1.x.x-beta` milestones (different major version)

## Issue Migration on Milestone Closure

When a milestone is closed, open issues/PRs are migrated:

| Closed Milestone Type | Target Milestone | Example |
| --- | --- | --- |
| `X.Y.Z-alpha` | `X.(Y+1).0-alpha` | `1.4.3-alpha` → `1.5.0-alpha` |
| `X.Y.Z-beta` | `X.(Y+1).0-alpha` | `1.4.3-beta` → `1.5.0-alpha` |
| `X.Y.Z-rc` | `X.(Y+1).0-alpha` | `1.4.3-rc` → `1.5.0-alpha` |
| `X.Y.Z` (stable) | `X.(Y+1).0-alpha` or `X.Y.(Z+1)` | `1.4.3` → `1.5.0-alpha` |

**Note:** Target milestone is created automatically if it doesn't exist.

**Rationale:** Pre-release milestones (alpha, beta, rc) represent work towards a specific release. When closed, issues migrate to the next minor version's alpha, aligning with the natural progression of the release cycle.

## Release Promotion Workflow

```text
1.4.3-alpha (released)
    ↓
    Creates: 1.4.3-beta, 1.5.0-alpha
    Closes: older 1.x.x-beta milestones
    ↓
    (15 days, no issues reported)
    ↓
1.4.3-beta (released)
    ↓
    Creates: 1.4.3-rc
    Closes: older 1.x.x-rc milestones
    ↓
    (15 days, no issues reported)
    ↓
1.4.3-rc (released)
    ↓
    Creates: 1.4.3 (stable)
    Closes: older 1.x.x (stable) milestones
    ↓
    (Stable release complete)
```

## Manual Control

You can manually control promotion paths by:

1. **Closing a milestone** → Stops its promotion path
2. **Reopening a milestone** → Resumes its promotion path
3. **Deleting a milestone** → Removes it entirely (issues migrate to next version)

## Scope Principle

**All milestone operations are scoped to the same major version:**

- Closing older milestones only affects milestones with the same major version
- Different major versions maintain independent milestone hierarchies
- This allows parallel development of multiple major versions

## Implementation Details

### Files Modified

- `.github/actions/versioning/manage-milestones/action.yml` - Creates/closes milestones
- `.github/actions/versioning/move-milestone-items/action.yml` - Migrates issues
- `.github/workflows/milestone-management.yml` - Orchestrates the workflow

### Key Functions

**`closeOlderMilestones(suffix, majorVersion)`**

- Filters milestones by suffix AND major version
- Sorts by minor.patch descending
- Closes all but the latest

**`move-milestone-items` action**

- Triggered on milestone closure
- Determines target milestone based on closed milestone type
- Migrates all open issues/PRs

## Examples

### Example 1: Parallel v1 and v2 Development

```text
Release: 1.4.3-alpha
  → Creates: 1.4.3-beta, 1.5.0-alpha
  → Closes: 1.4.2-beta (if exists)

Release: 2.0.0-alpha (same day)
  → Creates: 2.0.0-beta, 2.1.0-alpha
  → Closes: (no older 2.x.x-beta)

Result: Both 1.4.3-beta and 2.0.0-beta coexist ✓
```

### Example 2: Multiple Alphas

```text
Open Milestones:
  1.4.0-alpha
  1.4.1-alpha
  1.4.2-alpha
  1.4.3-alpha ← Latest

All remain open. No closure happens for alphas.
When 1.4.3-alpha closes, issues migrate to 1.5.0-alpha.
```

### Example 3: Beta Progression

```text
Release: 1.4.3-beta
  → Creates: 1.4.3-rc
  → Closes: 1.4.2-beta, 1.4.1-beta, 1.4.0-beta
  → Keeps: 1.4.3-beta (the newly created one)
  → Issues from 1.4.3-beta migrate to 1.5.0-alpha

Result: Only 1.4.3-beta remains open in v1 ✓
```

## Troubleshooting

### Issue: Old milestone not closing

**Cause:** Different major version
**Solution:** Check if the old milestone has a different major version. This is expected behavior.

### Issue: Milestone not created

**Cause:** Already exists or invalid version format
**Solution:** Check logs for "already exists" message. Verify version format is `X.Y.Z` or `X.Y.Z-suffix`.

### Issue: Issues not migrating

**Cause:** No target milestone exists
**Solution:** The action creates the target milestone automatically. Check logs for creation status.
