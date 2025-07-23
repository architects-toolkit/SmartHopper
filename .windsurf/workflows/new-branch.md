---
description: Add a new git branch based on the description provided by the user
---

# New Branch Creation Workflow

## Prerequisites

The user should provide a description of the purpose of the new branch. If no description is provided, ask for it and stop the workflow.

Check that current branch is `dev`. If not, switch to `dev` before continuing.

## Branch Naming Convention

Use the following pattern for branch names:

`prefix/X.Y.Z-descriptive-title`

Where:
- `prefix`: Type of change (see available prefixes below)
- `X.Y.Z`: Target version for the changes
- `descriptive-title`: 1-4 words describing the branch purpose (kebab-case)

### Available Prefixes

- `feature`: New functionality
- `bugfix`: Bug fixes
- `hotfix`: Bugs that need to be fixed urgently in production
- `docs`: Documentation changes
- `refactor`: Code changes that neither fix bugs nor add features
- `test`: Adding missing tests or correcting tests
- `chore`: Build process or auxiliary tool changes
- `ci`: CI/CD configuration changes
- `perf`: Performance improvements
- `style`: Code style changes (formatting, etc.)

### Determining the Target Version

1. **Check Current Base Version**:

   - Read the `<SolutionVersion>` entry in `Solution.props` to determine the current BASE VERSION (e.g., `0.4.0-alpha`).
   - Do not derive the current BASE VERSION from `CHANGELOG.md`; always base on the `<SolutionVersion>` in `Solution.props`.

2. **Check Previous Release Version**:

   - Read the `CHANGELOG.md`.
   - The first section should be [Unreleased], listing all changes for the current development.
   - The second section should be the Previous Release. Extract the PREVIOUS RELEASE VERSION (e.g. "## [0.3.6-alpha] - 2025-07-20" → "0.3.6-alpha").

3. **Determining Current Development Status**

   Compare the CURRENT BASE VERSION and the PREVIOUS RELEASE VERSION to identify the release level being developed in `dev`. Compare only numbers, skip suffixes. The result can be a PATCH release (`0.0.X`), a MINOR release (`0.X.0`), MAJOR release (`X.0.0`) or NONE (all digits remain equal, version unchanged, `0.0.0`). 

4. **Determining New Branch Release Level**

   Identify the logical release level for the suggested changes in the new branch, based on the description provided by the user. It can be:

   - **Patch** (`0.0.X`): for bug fixes, documentation updates, tests, refactors, and CI changes). 
   - **Minor** (`0.X.0`): for new backward-compatible features.
   - **Major** (`X.0.0`): for breaking changes.

5. **Determine Target Version**

   Compare the Required release level (step 4) vs. Current Development Status (step 3). If they match, keep the BASE VERSION as the TARGET VERSION. If they do not match, set the TARGET VERSION in the new branch's name by increasing the patch, minor or major number.

## Workflow Steps

1. Ensure there are no changes pending to commit. Stop the execution if so and ask the user to commit them.

2. Ensure we are in `dev` branch and it is synced with remote.

   git checkout dev
   git pull public dev

3. Create a new local branch from `dev`. Replace prefix, X.Y.Z, and descriptive-title as appropriate. Ask for confirmation if you doubt.

   git checkout -b prefix/X.Y.Z-descriptive-title

4. Tell the user that the new branch was created, and explain why you chose the target version number.

Stop here, do not implement any change in files.