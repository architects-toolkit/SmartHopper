---
trigger: always_on
---

# Testing and build expectations

- SmartHopper targets Rhino 8 / Grasshopper 1 with .NET 7:
  - Windows: `net7.0-windows`.
  - macOS: `net7.0`.
- Do not add unit tests that require Rhino/Grasshopper references.
- If validation needs Rhino/Grasshopper references, create a testing component in `SmartHopper.Components.Test` instead of a unit test.
- Test projects must stay runnable in CI without Rhino or Grasshopper runtime activation.
- CI behavior:
  - Windows runs all tests after Release build.
  - macOS runs only `SmartHopper.Infrastructure.Tests` for `net7.0`; Core and Grasshopper tests depend on WindowsDesktop/WinForms APIs.
- Local official build/signing flows are Windows-oriented and require Developer PowerShell for Visual Studio when using scripts that call Strong Name or Windows SDK tools.
- Do not commit generated signing keys, local certificates, provider API keys, or other local credentials.
- Prefer focused tests at the lowest layer that owns the behavior:
  - Infrastructure contracts and managers → `SmartHopper.Infrastructure.Tests`.
  - Core non-Rhino behavior → `SmartHopper.Core.Tests`.
  - Grasshopper helper logic that does not require Rhino/Grasshopper references or runtime activation → `SmartHopper.Core.Grasshopper.Tests`.
