---
trigger: glob
globs: .github/workflows/release-**.yml
---

# Yak Package Management for Rhino/Grasshopper

## Overview
Yak is the package manager for Rhino and Grasshopper. It enables publishing, distribution, and installation of plugins.

## Key Information

### Authentication
- Generate a non-expiring API key using `yak login --ci`
- Store this key as a GitHub secret named `YAK_AUTH_TOKEN`
- In GitHub Actions, set the `YAK_TOKEN` environment variable to this secret

### Pre-release Versioning
- Versions with suffixes `-alpha`, `-beta`, or `-rc` are considered pre-releases
- Pre-releases must use the `--pre` flag when pushing to Yak
- Regular releases have no suffix in their version

### Package Structure

A Yak package is a ZIP file containing your plugin files and a `manifest.yml` file. The structure should be as follows:

```
MyPlugin/
├── manifest.yml          # Required: Package metadata
├── MyPlugin.rhp          # Windows plugin (RHP file)
├── MyPlugin.gha          # Grasshopper assembly (if applicable)
├── MyPlugin.Mac.rhp      # Mac plugin (for Rhino 7+)
└── README.md             # Optional: Documentation
```

#### Manifest File

The `manifest.yml` must include:

```yaml
name: MyPlugin
version: 1.0.0
description: A brief description of the plugin
authors: ["Author Name"]
repository_url: https://github.com/username/repository
keywords: ["rhino", "grasshopper", "category"]
icon: icon.png  # Optional: 64x64px PNG

# Platform-specific files
platforms:
  - name: windows
    file: MyPlugin.rhp
    rhino_version: "7.0"
    platform: x64
    runtime: net48  # or net6.0-windows
  - name: mac
    file: MyPlugin.Mac.rhp
    rhino_version: "7.0"
    platform: x64
    runtime: net6.0-macos
```

### File Structure
- Windows and Mac plugins must be packaged separately
- Each platform requires its own Yak push command
- Include all dependencies in the package
- Follow Rhino's plugin structure guidelines for each platform

### Workflow Integration
1. Download Yak CLI executable from McNeel's server
2. Determine if the version is a pre-release
3. Download release assets from GitHub release
4. Push to Yak server with appropriate flags
5. Include proper error handling

## CLI Reference

```bash
# Download Yak CLI
curl -o yak.exe http://files.mcneel.com/yak/tools/latest/yak.exe

# Push a package (non-pre-release)
./yak.exe push path/to/package.zip

# Push a pre-release package
./yak.exe push path/to/package.zip --pre
```

## Environment Variables
- `YAK_TOKEN`: Authentication token for pushing packages to Yak

## Further Resources
- [Yak CLI Reference](https://developer.rhino3d.com/guides/yak/yak-cli-reference/)
- [The Anatomy of a Package](https://developer.rhino3d.com/guides/yak/the-anatomy-of-a-package)
