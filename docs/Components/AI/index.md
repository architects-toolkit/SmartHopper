# AI Components

Components for AI model management and legacy monolithic AI operations.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/AI/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page gives you a bird's-eye view of all AI-related components in SmartHopper: the model management component, the composable adapter pattern (Input + Output), and the legacy monolithic components that combine both in a single node.

**You should read this if you:**

- Want to understand the different ways to use AI in Grasshopper
- Need to decide between the adapter pattern and legacy components
- Are looking for a specific AI component

---

## End-User Guide

### Two Ways to Use AI

SmartHopper offers two approaches to AI workflows:

**Composable Adapters (recommended)**

```text
[Input Component] → [Output Component]
```

- Flexible: mix and match any input type with any output type
- The output component handles both the AI call and result extraction
- Provider and model are configured on the output component
- See [Input Components](../Input/index.md) and [Output Components](../Output/index.md)

**Legacy Monolithic Components**

```text
[Monolithic Component]  (input + AI + output in one)
```

- Simpler for basic use cases
- Limited: each component handles one specific input-output combination
- Kept for backward compatibility; prefer adapters for new workflows

### Provider & Model Management

- [AIModelsComponent](./AIModelsComponent.md) -- lists available AI models for the selected provider, using dynamic API retrieval with static fallback.

### Visual Guide

<!-- PLACEHOLDER: Screenshot showing a composable adapter workflow vs a monolithic component -->
<!-- - Adapter workflow: Text2AI → AI2Text (output component handles AI call) -->
<!-- - Monolithic: AIText2Text (single component) -->

### Common Questions

**Q: Should I use adapters or legacy components?**
A: Use the composable adapter pattern for new workflows. It is more flexible and is the actively developed path. Legacy components are maintained for backward compatibility.

**Q: How do I choose a provider and model?**
A: Right-click any output component and select your provider from the menu. The model can be set via the "Settings" input or the right-click menu. Use `AIModelsComponent` to list available models for a specific provider.

---

## Developer Reference

### Input Adapters

Input adapters convert Grasshopper data into `AIInputPayload` wire types. See [Input Components](../Input/index.md) for the full catalog.

| Component | File | Purpose |
| --- | --- | --- |
| Text2AI | `src/SmartHopper.Components/Input/Text2AIComponent.cs` | Text input to AI payload |
| TextList2AI | `src/SmartHopper.Components/Input/TextList2AIComponent.cs` | Text list to AI payload |
| AIContext | `src/SmartHopper.Components/Input/AIContextComponent.cs` | Context filters for AI |

A minimal input adapter implementation:

```csharp
public class Text2AIComponent : AIInputAdapterBase
{
    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Text", "T", "Input text", GH_ParamAccess.item);
    }

    protected override AIInputPayload ConvertInput(IGH_DataAccess DA)
    {
        string text = string.Empty;
        DA.GetData("Text", ref text);
        return new AIInputPayload { Text = text };
    }
}

```

### Output Adapters

Output adapters consume `AIInputPayload`, call the AI provider, and convert the responses to Grasshopper types. The provider and model are set on the output component. See [Output Components](../Output/index.md) for the full catalog.

| Component | File | Output Type | Purpose |
| --- | --- | --- | --- |
| AI2Text | `.../Output/AI2TextComponent.cs` | Text | AI response to text |
| AI2TextList | `.../Output/AI2TextListComponent.cs` | Text List | AI response to text list |
| AI2Boolean | `.../Output/AI2BooleanComponent.cs` | Boolean | AI response to boolean |
| AI2BooleanList | `.../Output/AI2BooleanListComponent.cs` | Boolean List | AI response to boolean list |
| AI2Number | `.../Output/AI2NumberComponent.cs` | Number | AI response to number |
| AI2NumberList | `.../Output/AI2NumberListComponent.cs` | Number List | AI response to number list |
| AI2Integer | `.../Output/AI2IntegerComponent.cs` | Integer | AI response to integer |
| AI2IntegerList | `.../Output/AI2IntegerListComponent.cs` | Integer List | AI response to integer list |
| AI2Json | `.../Output/AI2JsonComponent.cs` | JSON | AI response to JSON |
| AI2GhJson | `.../Output/AI2GhJsonComponent.cs` | GH JSON | AI response to Grasshopper JSON |
| AI2Img | `.../Output/AI2ImgComponent.cs` | Image | AI response to image |
| AI2Markdown | `.../Output/AI2MarkdownComponent.cs` | Markdown | AI response to markdown |
| AI2Script | `.../Output/AI2ScriptComponent.cs` | Script | AI response to executable script |
| AI2Speech | `.../Output/AI2SpeechComponent.cs` | Audio | AI response to speech audio |

A minimal output adapter implementation:

```csharp
public class AI2TextComponent : AIOutputAdapterBase
{
    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("Text", "T", "AI generated text", GH_ParamAccess.item);
    }

    protected override void ConvertOutput(AIResponse response, IGH_DataAccess DA)
    {
        DA.SetData("Text", response.Text);
    }
}
```

### Legacy Components (Monolithic)

These combine input and output in a single component. For new workflows, prefer the composable pattern above.

| Component | File | Purpose |
| --- | --- | --- |
| AIText2Text | `.../Text/AIText2TextComponent.cs` | Text to text |
| AIText2Boolean | `.../Text/AIText2BooleanComponent.cs` | Text to boolean |
| AIText2TextList | `.../Text/AIText2TextListComponent.cs` | Text to text list |
| AIText2Json | `.../JSON/AIText2JsonComponent.cs` | Text to JSON |
| AIText2Img | `.../Img/AIText2ImgComponent.cs` | Text to image |
| AIImg2Text | `.../Img/AIImg2TextComponent.cs` | Image to text |
| JsonObject2Text | `.../JSON/JsonObject2TextComponent.cs` | JSON to text |
| JsonArray2TextList | `.../JSON/JsonArray2TextListComponent.cs` | JSON array to text list |

---

## Architecture & Design

### Adapter Pattern vs Monolithic

The adapter pattern was introduced to solve the combinatorial explosion problem:

- **Before**: N input types * M output types = N*M monolithic components
- **After**: N input adapters + M output adapters = N+M components, all interchangeable

The `AIInputPayload` wire type is the bridge between input and output adapters. Any input adapter can connect to any output adapter. The output adapter handles both the AI call and the result extraction.

### Migration Path

Legacy monolithic components remain functional but are not being extended. New capabilities (audio, forum posts, structured output) are only available through the adapter pattern.

### Related Documentation

- [Input Components](../Input/index.md) -- full input adapter catalog
- [Output Components](../Output/index.md) -- full output adapter catalog
- [ComponentBase](../ComponentBase/index.md) -- base class hierarchy
- [AIInputPayload](../../Architecture/AIInputPayload.md) -- the wire type connecting adapters
- [AI Capabilities](../../Providers/AICapability.md) -- model capability flags
