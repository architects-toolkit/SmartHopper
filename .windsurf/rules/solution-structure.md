---
trigger: always_on
---

# Solution structure
- SmartHopper.Core: Contains the core functionality
- SmartHopper.Core.Grasshopper: Type converters, utilities & tool definitions
- SmartHopper.Components: Grasshopper components (inherit ComponentBase/AIStatefulAsyncComponentBase)
- SmartHopper.Components.Test: This project is not build in Release. Defines components that are used for testing purposes, not for production.
- SmartHopper.Infrastructure: Settings, Provider manager, Context manager and AITool manager
- SmartHopper.Menu: Menu bar setup
- SmartHopper.Infrastructure.Test: xUnit tests (not for production)
- SmartHopper.Providers.*: AI provider projects