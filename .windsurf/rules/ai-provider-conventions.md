---
trigger: glob
globs: **/SmartHopper.Providers.*/*.cs
---

# AI Provider conventions
- Use SmartHopper.Providers.Template as a starting point
- Implement IAIProviderFactory & IAIProviderSettings
- AI providers must wrap any chain-of-thought output or reasoning summary in `<think></think>` tags so that `ChatResourceManager.CreateMessageHtml` can detect and render reasoning.
- UI settings are created in _controlFactories in SettingsDialog.cs by parsing SettingsDescriptor.