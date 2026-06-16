---
trigger: always_on
---

# Solution structure

- `SmartHopper.Core`: Core UI, component base classes, chat UI host, context providers, and shared component infrastructure.
- `SmartHopper.Core.Grasshopper`: Grasshopper-specific utilities, converters, canvas helpers, GhJSON integration, and AI tool definitions.
- `SmartHopper.Components`: Production Grasshopper components. Components generally inherit from base classes in `SmartHopper.Core/ComponentBase`.
- `SmartHopper.Components.Test`: Test-only Grasshopper components. This project is not built in Release and must not contain production components.
- `SmartHopper.Infrastructure`: Provider manager, model manager, context manager, AI tool manager, AICall contracts, settings, dialogs, security, and shared infrastructure.
- `SmartHopper.Menu`: Rhino/Grasshopper menu bar setup and settings entry points.
- `SmartHopper.Infrastructure.Tests`, `SmartHopper.Core.Tests`, `SmartHopper.Core.Grasshopper.Tests`: xUnit test projects. Avoid tests that require Rhino runtime licensing.
- `SmartHopper.Providers.*`: AI provider plugin projects. Each provider owns API-specific request/response adaptation while using infrastructure contracts.

## Placement rule

Put shared logic at the highest layer that owns the concern:

- Core UI/component behavior → `SmartHopper.Core`.
- Grasshopper canvas, data tree, and GhJSON behavior → `SmartHopper.Core.Grasshopper`.
- End-user component wiring → `SmartHopper.Components`.
- Provider/model/context/tool orchestration and settings → `SmartHopper.Infrastructure`.
- API-specific quirks → the matching `SmartHopper.Providers.<Name>` project.
