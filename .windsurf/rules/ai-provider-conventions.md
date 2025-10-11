---
trigger: glob
globs: **/SmartHopper.Providers.*/*.cs
---

# AI Provider conventions
- Check other providers to see how they implement required code
- Implement AIProvider, AIProviderModels, AIProviderFactory and AIProviderSettings
- Use native AIRequest, AIToolCall, AIInteractionText, AIInteractionImage, AIInteractionToolCall, AIInteractionToolResult... methods and models
- UI settings are created in _controlFactories in SettingsDialog.cs by parsing SettingsDescriptor