# SmartHopper Providers and Models

- SmartHopper reads default provider and model settings from the Rhino/Grasshopper environment.
- Call `providers_list` to discover providers and their `enabled`, `configured`, and `isDefault` status, plus the top-level `defaultProvider`. Only configured providers can run calls successfully.
- Call `providers_models` with a provider name to inspect that provider's models and capabilities.
- Use `gh_component_set_provider` for a per-component override on components that support provider selection (optionally wiring a model Panel).
- Do not assume a model supports tools, images, structured output, streaming, or batch execution; inspect declared capabilities.
- Configure providers through SmartHopper settings. Most remote providers require an API key; local/custom providers may require an endpoint.
- Never request, display, log, or place API keys in the Grasshopper definition or chat. Direct the user to the settings UI.
