# Contributing to SmartHopper

I'm so happy to have you here! All contributions are welcome to SmartHopper! Here's how you can help:

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

   - Open a new discussion in the **Ideas** category in [GitHub Discussions](https://github.com/architects-toolkit/SmartHopper/discussions/categories/ideas)
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

You can use the discussion forum to engage with the community:
- Share your work: [Show and Tell](https://github.com/architects-toolkit/SmartHopper/discussions/categories/show-and-tell)
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
```

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

When developing locally, you can use the helper script `tools/Build-Solution.ps1` to set up signing keys, update InternalsVisibleTo, build the solution, and Authenticode-sign the SmartHopper assemblies.

1. Open **Developer PowerShell for Visual Studio 2022** as Administrator.
2. cd to the repository root.
3. Run the build script (defaults to `Debug`):

   ```powershell
   .\tools\Build-Solution.ps1
   ```

   To build a different configuration (for example `Release`):

   ```powershell
   .\tools\Build-Solution.ps1 -Configuration Release
   ```

The script will:

- Ensure `signing.pfx` exists at the solution root (creating it and prompting for a password if missing).
- Ensure `signing.snk` exists at the solution root (creating it if missing).
- Run `tools/Update-InternalsVisibleTo.ps1` so `SmartHopperPublicKey` matches your SNK.
- Build `SmartHopper.sln` with the selected configuration.
- Authenticode-sign the SmartHopper assemblies in `bin/<SolutionVersion>/<Configuration>` using `signing.pfx`.

#### Passing the PFX password

If you want to avoid interactive password prompts (e.g., for scripting), you can pass a `SecureString` password:

```powershell
# Create a SecureString password
$pwd = Read-Host "Enter PFX password" -AsSecureString

# Pass it to the build script
.\tools\Build-Solution.ps1 -Configuration Release -PfxPassword $pwd
```

Alternatively, for fully non-interactive use (not recommended for security reasons):

```powershell
$pwd = ConvertTo-SecureString "your-password" -AsPlainText -Force
.\tools\Build-Solution.ps1 -Configuration Release -PfxPassword $pwd
```

**Note:** If you don't provide `-PfxPassword`, the signing script will prompt interactively when needed.

**Environment requirement:** Run `tools/Build-Solution.ps1` (and the underlying signing scripts) from **Developer PowerShell for Visual Studio 2022** or another VS Developer shell. This ensures `sn.exe` and Windows SDK tools are on `PATH`. Running from a plain PowerShell may fail with errors like `sn.exe not found`.
