# Components

Grasshopper components live in `src/SmartHopper.Components/` and build on the shared base classes in `src/SmartHopper.Core/ComponentBase/`.

> For the visual end-user component catalog with screenshots, visit **[docs.smarthopper.xyz/docs/components](https://docs.smarthopper.xyz/docs/components)**.

## Ribbon panels (`SmartHopper` tab)

| Panel | Subcategory string | Docs |
| --- | --- | --- |
| **AI, Settings & Metrics** | `A. AI` | [AI Components](./AI/index.md) |
| **Input** | `B. Input` | [Input Components](./Input/index.md) |
| **Output** | `C. Output` | [Output Components](./Output/index.md) |
| **GhJSON** | `D. GhJSON` | [Canvas & GhJSON Components](./Grasshopper/index.md) |
| **Forums** | `E. Forums` | [Knowledge Components](./Knowledge/index.md) |
| **Documents** | `F. Documents` | [Knowledge Components](./Knowledge/index.md) |
| **Media** | `G. Media` | [Audio & Media Components](./Audio/index.md) |
| **JSON** | `H. JSON` | [JSON Components](./JSON/index.md) |
| **Text & Lists** | `I. Text & Lists` | [AI Components](./AI/index.md) |
| **Script** | `J. Script` | [AI Components](./AI/index.md) |

Specialized components use `GH_Exposure.obscure` to stay dropdown-only (the unexpanded ribbon shows ~50 core icons); every component remains searchable from the canvas and inside its panel dropdown. Custom parameter types (`AIInputPayloadParameter`, `VersatileImageParameter`, `VersatileAudioParameter`) use `GH_Exposure.hidden` and never appear in the ribbon.

## Base class hierarchy & architecture

- [ComponentBase reference](./ComponentBase/index.md) — `AsyncComponentBase`, `StatefulComponentBase`, `SelectingComponentBase`, `SelectingStatefulComponentBase`, `ProviderComponentBase`, `AIProviderComponentBase`, `AIStatefulAsyncComponentBase`, `AISelectingStatefulAsyncComponentBase`, `AIInputAdapterBase`, and `AIOutputAdapterBase`
- [Output persistence (`IO`)](./IO/index.md) — versioned persistence for component outputs across `.gh` file saves and state transitions
- [Test components](./Test/index.md) — `src/SmartHopper.Components.Test/` (Debug-only components for tests requiring Rhino/Grasshopper runtime)

## Example: Implementing an Output Adapter

Output components derive from `AIOutputAdapterBase`, which registers `Input >`, `Settings`, `Run?`, and `Metrics` automatically and decodes branch results through `GetOutputMappings()`:

```csharp
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

public class MyAIOutputComponent : AIOutputAdapterBase
{
    public MyAIOutputComponent()
        : base("My AI", "MYAI", "Call the AI provider on the connected input(s) and return text.", GH_Exposure.primary)
    {
    }

    public override Guid ComponentGuid => new Guid("YOUR-GUID-HERE");

    protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

    protected override string GetInternalSystemPrompt()
    {
        return "You are a helpful assistant.";
    }

    protected override IReadOnlyList<OutputMapping> GetOutputMappings()
    {
        return new[]
        {
            new OutputMapping
            {
                ParamName = "Response",
                NickName = "R",
                Description = "AI-generated text response.",
                ParamType = typeof(Param_String),
                Access = GH_ParamAccess.tree,
                Extractor = OutputMapping.Single(aiReturn =>
                {
                    var text = aiReturn?.Body?.GetLastAssistantText();
                    return string.IsNullOrWhiteSpace(text) ? null : new GH_String(text.Trim());
                }),
            },
        };
    }
}
```
