# Contributing to SmartHopper

We are so happy to have you here! We welcome contributions to SmartHopper! Here's how you can help:

## Code of Conduct

- Be respectful to others
- Focus on what is best for the community
- Show courtesy and respect towards other community members

## Ways to Contribute

### 1. **Report Bugs**

If you encounter a bug when using SmartHopper, please do the following:

   - Use the [GitHub Issues](https://github.com/architects-toolkit/SmartHopper/issues/new/choose) tab
   - Open a new issue using the **Bug Report** template
   - Provide detailed reproduction steps
   - Include system specifications
   - Upload sample files or screenshots if possible

### 2. **Suggest Features**

Would you like to see a feature added to SmartHopper? If so, please do the following:

   - Open a **Feature Request** in [GitHub Issues](https://github.com/architects-toolkit/SmartHopper/issues/new/choose)
   - Describe the use case
   - Provide examples if possible

### 3. **Submit Changes**

Are you skilled in coding and want to contribute to SmartHopper? You can help fixing bugs and adding new features. If so, please do the following:

   - Fork the repository
   - Create a new branch referencing the issue you want to fix or feature you want to add
   - Submit a Pull Request following the [Pull Request Guidelines](#pull-request-guidelines) explained below

### 4. **Release Checklist**

Before submitting a release, please ensure you have completed the checks in the [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

## Discussion Forum

We have a discussion forum available for you to engage with the community:
- General discussions: [SmartHopper Discussions](https://github.com/architects-toolkit/SmartHopper/discussions/categories/general)
- Feature requests: [Ideas](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas)
- Support questions: [Q&A](https://github.com/architects-toolkit/SmartHopper/discussions/categories/q-a)

## Pull Request Guidelines

1. Focus each PR on a single feature or bug fix
2. Follow the conventional commits format for PR titles (e.g., `feat: add new feature`, `fix(component): resolve issue`)
3. Update the CHANGELOG.md with your changes (use the [Changelog Guidelines](#changelog-guidelines) below)
4. If your changes affect versioning, update the version in Solution.props, and make sure it follows semantic versioning (X.Y.Z[-suffix[.N]])
5. Provide relevant description in the PR (use the [Pull Request Description Template](#pull-request-description-template) below)
6. The PR will be merged once you have the sign-off of the maintainers

### Changelog Guidelines

The changelog follows the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format. Update the CHANGELOG.md file with your changes under the appropriate section:

```markdown
## [Unreleased]

### Added
- New features that have been added
- Example: feat(component): add new grasshopper component for processing point clouds

### Changed
- Changes in existing functionality
- Example: refactor(core): improve performance of mesh operations

### Deprecated
- Features that will be removed in upcoming releases
- Example: deprecate(legacy): mark old API for removal in next major version

### Removed
- Features that have been removed
- Example: remove(cleanup): remove deprecated v1 endpoints

### Fixed
- Bug fixes
- Example: fix(component): resolve crash when processing empty geometry

### Security
- Changes related to security
- Example: security(auth): update authentication library to patch vulnerability
```

Note: Only add the sections that are relevant to your changes. Each entry should correspond to a commit in your PR and follow the conventional commits format.

### Pull Request Description Template

```markdown
# Pull Request Title in format: <type>(<optional-scope>): <description>

where:

- <type> is one of:
  - feat: A new feature
  - fix: A bug fix
  - docs: Documentation changes
  - style: Code style changes (formatting, missing semi-colons, etc)
  - refactor: Code refactoring without changing functionality
  - perf: Performance improvements
  - test: Adding or modifying tests
  - build: Changes to build process or tools
  - ci: Changes to CI configuration files and scripts
  - chore: Other changes that don't modify src or test files
  - revert: Revert a previous commit
- <optional-scope> is the scope of the change, such as core, ui, or components
- <description> is a short description of the change

Example: feat(component): add new grasshopper component

## Description

Brief description of the changes and the problem it solves.

## Breaking Changes

List any breaking changes here. Mention if there is no breaking change.

## Testing Done

Describe the testing you've done to validate your changes.

## Checklist

- [ ] This PR is focused on a single feature or bug fix
- [ ] Version in Solution.props was updated, if necessary, and follows semantic versioning
- [ ] CHANGELOG.md has been updated
- [ ] PR title follows [Conventional Commits](https://www.conventionalcommits.org/en/v1.1.0/) format
- [ ] PR description follows [Pull Request Description Template](https://github.com/architects-toolkit/SmartHopper/blob/main/CONTRIBUTING.md#pull-request-description-template)

# Visual Studio 2022 Setup

Follow these steps to configure Visual Studio 2022 for SmartHopper development:

1. Ensure you have **Visual Studio 2022** installed with the following workloads:
   - .NET desktop development
   - **(Optional)** .NET cross-platform development
2. Clone the repository:
   ```powershell
   git clone https://github.com/SmartHopper/SmartHopper-public.git
   ```
3. Open `SmartHopper.sln` in Visual Studio 2022.
4. In **Solution Explorer**, right-click the solution and select **Restore NuGet Packages**.
5. Verify that all projects target **.NET 7** and that Rhino/Grasshopper SDK references resolve.

### Initializing Code Signing

When developing locally, you must generate and apply both strong-name and Authenticode signatures.

1. Open **Developer PowerShell for Visual Studio 2022** as Administrator.
2. cd to the repository root.
3. Generate a strong-name key:
   ```powershell
   .\Sign-StrongNames.ps1 -Generate
   ```
4. Create a self-signed PFX for Authenticode via the Authenticode script:
   ```powershell
   .\Sign-Authenticode.ps1 -Generate -Password '<password>'
   ```
5. Build the solution from Visual Studio or via the command line:
   ```powershell
   dotnet build SmartHopper.sln -c Release
   ```
6. Authenticode-sign provider DLLs (e.g. for Grasshopper testing):
   ```powershell
   .\Sign-Authenticode.ps1 -Sign bin\Debug\net7.0-windows -Password '<password>'
   ```

**Note:** Repeat steps 1, 2, and 6 after every build to ensure your providers are signed and SmartHopper can load them.
