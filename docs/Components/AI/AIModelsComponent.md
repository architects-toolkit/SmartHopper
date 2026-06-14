# AIModelsComponent

Grasshopper component that lists available AI models for the currently selected provider.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/AI/AIModelsComponent.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This component queries the provider API for an up-to-date model list and falls back to static models when needed. Understanding its behavior helps you ensure your Grasshopper workflows always use valid model names.

**You should read this if you:**

- Need to list available AI models in Grasshopper
- Want to understand dynamic vs static model retrieval
- Are working with provider model management

---

## End-User Guide

### Purpose

Provide an up-to-date list of models by querying the provider API when possible and falling back to locally known models with capabilities when the API is unavailable.

- Base: `AIProviderComponentBase`
- Category: SmartHopper > AI

### Inputs / Outputs

- Inputs: none
- Outputs:
  - `Models (Tree<Text>)` — List of model names

### Behavior

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

### Notes

- API retrieval is asynchronous and cancellation-aware.
- Exceptions in dynamic retrieval are handled silently to allow graceful fallback.
- Sorting ensures predictable UI lists; capability metadata is only used when falling back to static models.

---

## Developer Reference

`AIModelsComponent` derives from `AIProviderComponentBase`. The solve loop accesses the provider's model interface:

```csharp
public class AIModelsComponent : AIProviderComponentBase
{
    public AIModelsComponent()
        : base("AI Models", "Models",
               "Lists available AI models for the selected provider",
               "SmartHopper", "AI")
    {
        this.RunOnlyOnInputChanges = false;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Models", "M", "List of model names", GH_ParamAccess.tree);
    }

    protected override async Task DoWorkAsync(CancellationToken token)
    {
        var provider = GetSelectedProvider();
        var models = await provider.Models.RetrieveApiModelsAsync(token);

        if (models == null || models.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                "Provider API models unavailable. Using fallback static model list.");
            models = provider.Models.RetrieveModels()
                .OrderByDescending(m => m.Verified)
                .ThenByDescending(m => m.Rank)
                .ThenBy(m => m.Deprecated)
                .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                .Select(m => m.Model)
                .ToList();
        }
        else
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                "Using dynamic model list from provider API");
            models = models.Distinct(StringComparer.OrdinalIgnoreCase)
                           .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                           .ToList();
        }

        SetOutputData("Models", models);
    }
}
```

Using the component output to configure another AI component:

```csharp
// In a downstream component or script:
var modelsComponent = new AIModelsComponent();
modelsComponent.GetSelectedProvider(); // configure provider via right-click menu
var models = modelsComponent.GetOutputData("Models", new List<string>());

// Use the first model name for a request
var selectedModel = models.FirstOrDefault();
var request = new AIRequestCall { Model = selectedModel };
```

---

## Architecture & Design

- Provider model interfaces: `IAIProviderModels` with `RetrieveApiModels()` and `RetrieveModels()` in `src/SmartHopper.Infrastructure/AIModels/IAIProviderModels.cs`.
- Example implementations:
  - `MistralAIProviderModels.RetrieveApiModels()`
  - `OpenAIProviderModels.RetrieveApiModels()`
