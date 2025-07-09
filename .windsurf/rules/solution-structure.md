---
trigger: always_on
---

# Solution structure
- SmartHopper.Core: Contains the core functionality
- SmartHopper.Core.Grasshopper: Type converters, utilities & tool definitions
- SmartHopper.Components: Grasshopper components (inherit ComponentBase/AIStatefulAsyncComponentBase)
- SmartHopper.Infrastructure: Settings, Provider manager, Context manager and AITool manager
- SmartHopper.Menu: Menu bar setup
- SmartHopper.Components.Test: xUnit tests (not for production)
- SmartHopper.Providers.*: AI provider projects