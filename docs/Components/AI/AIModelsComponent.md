# AIModelsComponent

Grasshopper component that lists available AI models for the currently selected provider.

- Source: `src/SmartHopper.Components/AI/AIModelsComponent.cs`
- Base: `AIProviderComponentBase`
- Category: SmartHopper > AI

## Purpose

Provide an up-to-date list of models by querying the provider API when possible and falling back to locally known models with capabilities when the API is unavailable.

## Inputs / Outputs

- Inputs: none
- Outputs:
  - `Models (Tree<Text>)` — List of model names

## Behavior

- On solve:
  1. Initializes the selected provider.
  2. Attempts dynamic retrieval via `provider.Models.RetrieveApiModels()`.
     - Returns distinct, case-insensitive, alphabetically sorted names.
     - On success, emits a runtime remark: "Using dynamic model list from provider API".
  3. If the API list is empty/failed, falls back to `provider.Models.RetrieveModels()` and outputs the `Model` names sorted by:
     - `Verified` (desc)
     - `Rank` (desc)
     - `Deprecated` (asc)
     - `Model` (asc, case-insensitive)
     - Emits a runtime warning: "Provider API models unavailable. Using fallback static model list."
  4. Errors (e.g., no provider or no models) emit a runtime error with a concise message.

- Execution trigger:
  - `RunOnlyOnInputChanges = false` so provider changes retrigger execution.

## Notes

- API retrieval is asynchronous and cancellation-aware.
- Exceptions in dynamic retrieval are handled silently to allow graceful fallback.
- Sorting ensures predictable UI lists; capability metadata is only used when falling back to static models.

## Related

- Provider model interfaces: `IAIProviderModels` with `RetrieveApiModels()` and `RetrieveModels()` in `src/SmartHopper.Infrastructure/AIModels/IAIProviderModels.cs`.
- Example implementations:
  - `MistralAIProviderModels.RetrieveApiModels()`
  - `OpenAIProviderModels.RetrieveApiModels()`
