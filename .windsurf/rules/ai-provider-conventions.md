---
trigger: glob
globs: **/SmartHopper.Providers.*/*.cs
---

# AI Provider conventions

- Check existing providers before adding or changing provider behavior.
- A provider project should expose:
  - `<Provider>Provider` deriving from `AIProvider<T>` or `AIProvider`.
  - `<Provider>ProviderModels` deriving from `AIProviderModels`.
  - `<Provider>ProviderFactory` implementing `IAIProviderFactory`.
  - `<Provider>ProviderSettings` deriving from `AIProviderSettings`.
- Keep provider-specific API differences inside the provider project: endpoint selection, payload encoding, response decoding, schema adaptation, streaming parsing, and provider-specific headers.
- Use infrastructure contracts and models: `AIRequestCall`, `AIBody`, `AIReturn`, `AIToolCall`, `AIInteractionText`, `AIInteractionImage`, `AIInteractionToolCall`, `AIInteractionToolResult`, and `AIModelCapabilities`.
- Do not bypass the provider pipeline. Prefer `PreCall()` for request setup, base `CallApi()` for HTTP execution, and `PostCall()`/`Decode()` for normalization.
- Mark secret settings with `SettingDescriptor.IsSecret`; settings UI is generated from descriptors through `SettingsDialog`.
- Do not place API keys or secret headers on `AIRequestCall.Headers`, `AIReturn`, logs, or source code. Set `AIRequestCall.Authentication` and let provider internals apply secrets just-in-time.
- Register model capabilities in `AIProviderModels.RetrieveModels()` using concrete API-ready model names. Use `Verified`, `Deprecated`, `Rank`, aliases, streaming support, and capability flags to guide model selection.
- Callers should select models through `provider.SelectModel(requiredCapability, requestedModel)` rather than directly calling `ModelManager.SelectBestModel`.
