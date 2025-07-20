---
description: Add a new git branch based on the description provided by the user
---

# New Branch Creation Workflow

## Prerequisites

If no description of the purpose of the branch is provided, ask for it and stop the workflow.

## Branch Naming Convention

Use the following pattern for branch names:

`prefix/0.0.0-descriptive-title`

Where:
- `prefix`: Type of change (see available prefixes below)
- `0.0.0`: Target version for the changes
- `descriptive-title`: 1-4 words describing the branch purpose (kebab-case)

### Available Prefixes

- `feature`: New functionality
- `fix`: Bug fixes
- `docs`: Documentation changes
- `refactor`: Code changes that neither fix bugs nor add features
- `test`: Adding missing tests or correcting tests
- `chore`: Build process or auxiliary tool changes
- `ci`: CI/CD configuration changes
- `perf`: Performance improvements
- `style`: Code style changes (formatting, etc.)

### Determining the Target Version

1. **Check Current Versions**:

   - Check `Solutions.props` for the current `dev` version
   - Check `CHANGELOG.md` for the previous release version

2. **Version Bump Rules**:

   | Type of Change | Release Type | Example Version Bump |
   |----------------|--------------|----------------------|
   | Bug fixes, minor corrections | Patch | 1.0.0 → 1.0.1 |
   | New features (backward compatible) | Minor | 1.0.1 → 1.1.0 |
   | Breaking changes | Major | 1.1.0 → 2.0.0 |

If the current X.Y.Z-dev version in `dev` branch already reflects the appropriate bump for your change type, use that version number. For example, if Solutions.props has 0.4.1-dev and the previous release was 0.4.0-alpha, this means the current `dev` branch already includes a patch bump. In this case, create the new branch as 0.4.1 for patch changes. Only bump to 0.5.0 for minor changes or 1.0.0 for major changes.

3. **Detailed Decision Criteria**:

   **Patch (0.0.X) when:**
   - Fixing bugs
   - Updating documentation
   - Adding/updating tests
   - Code refactoring with no behavior changes
   - Build/CI configuration changes
   - Dependency updates (patch/minor versions)

   **Minor (0.X.0) when:**
   - Adding new backward-compatible features
   - Adding new API endpoints/methods
   - Deprecating features (without removing them)
   - Significant performance improvements
   - Adding new optional configuration

   **Major (X.0.0) when:**
   - Making breaking API changes
   - Removing deprecated features
   - Changing existing behavior in non-backward-compatible ways
   - Major architectural changes

## Workflow Steps

1. Ensure there are no changes pending to commit. Stop the execution if so and ask the user to commit them.

2. Ensure we are in `dev` branch and it is synced with remote.

   git checkout dev
   git pull public dev

3. Create a new local branch from `dev`. Replace prefix, X.Y.Z, and descriptive-title as appropriate.

   git checkout -b prefix/X.Y.Z-descriptive-title

4. Tell the user that the new branch was created, and explain why you chose the target version number.

Stop here, do not implement any change in files.

Stop the workflow here.