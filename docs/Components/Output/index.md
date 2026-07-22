# Output Components

Adapter components that extract and convert AI results from `AIReturn` into Grasshopper data types.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Components/Output/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-13 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Output components are the core of every AI workflow in SmartHopper. They receive `AIInputPayload` from input components, call the AI provider, and convert the response into the specific Grasshopper data type you need -- text, numbers, images, JSON, or code. The provider and model are configured directly on the output component.

**You should read this if you:**

- Want to extract a specific result type from an AI response
- Need to understand which output component to use
- Are building a custom output adapter

---

## End-User Guide

### What Do Output Components Do?

Output components receive input payloads, call the AI provider, and convert the response into a Grasshopper data type you can use downstream. The provider and model are set directly on the output component (via right-click menu or inputs).

```text
[Input Component] → [Output Component] → [Your Result]
                         ▲ you are here

```

### Which Component Should I Use?

| I want to get... | Use this component | Nickname |
| --- | --- | --- |
| A single text string | `AI2TextComponent` | AI->Text |
| A list of text items | `AI2TextListComponent` | AI->TextList |
| Markdown-formatted text | `AI2MarkdownComponent` | AI->Markdown |
| A number | `AI2NumberComponent` | AI->Number |
| A list of numbers | `AI2NumberListComponent` | AI->NumberList |
| An integer | `AI2IntegerComponent` | AI->Integer |
| A list of integers | `AI2IntegerListComponent` | AI->IntegerList |
| A boolean (true/false) | `AI2BooleanComponent` | AI->Boolean |
| A list of booleans | `AI2BooleanListComponent` | AI->BooleanList |
| An image | `AI2ImgComponent` | AI->Img |
| Structured JSON | `AI2JsonComponent` | AI->Json |
| Grasshopper JSON | `AI2GhJsonComponent` | AI->GhJSON |
| Python/C# code | `AI2ScriptComponent` | AI->Script |
| Speech audio | `AI2SpeechComponent` | AI->Speech |

### Visual Guide

<!-- PLACEHOLDER: Screenshot showing Output components in the Grasshopper ribbon -->
<!-- - Component location: SmartHopper tab → Output panel -->
<!-- - Typical wiring: Input component payload output → Output component payload input -->

### Common Questions

**Q: Can I connect multiple output components to the same input component?**
A: Yes. Each output component independently calls the AI and extracts its own result type.

**Q: What if the AI didn't produce the type I asked for?**
A: The output component will show an error or warning. Make sure your prompt asks for the expected output format.

**Q: How does AI->Json produce structured data?**
A: When you provide a JSON schema (via `AIRequestParameters`), the AI is constrained to produce valid JSON matching that schema. See [AIRequestParameters](../../Architecture/AIRequestParameters.md).

---

## Developer Reference

### Base Class

All output components inherit from `AIOutputAdapterBase`:

```csharp
public abstract class AIOutputAdapterBase : AIStatefulAsyncComponentBase
{
    public override AICapability RequiredCapability { get; }

    protected abstract void ExtractOutputs(AIReturn result, IGH_DataAccess DA);
}

```

See [AIOutputAdapterBase](../ComponentBase/AIOutputAdapterBase.md) for the full API.

### Creating a Custom Output Adapter

```csharp
public class AI2MyTypeComponent : AIOutputAdapterBase
{
    public override AICapability RequiredCapability => AICapability.TextOutput;

    protected override void ExtractOutputs(AIReturn result, IGH_DataAccess DA)
    {
        // 1. Get text from AI result
        string text = result.GetTextContent();

        // 2. Convert to your type
        MyType value = MyType.Parse(text);

        // 3. Set output
        DA.SetData(0, value);
    }
}

```

### Full Component Catalog

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `AI2TextComponent` | AI->Text | Text | Extracts text from AI results |
| `AI2TextListComponent` | AI->TextList | Text | Extracts a list of text items |
| `AI2MarkdownComponent` | AI->Markdown | Text | Extracts markdown-formatted text |
| `AI2NumberComponent` | AI->Number | Numbers | Extracts numeric values |
| `AI2NumberListComponent` | AI->NumberList | Numbers | Extracts a list of numbers |
| `AI2IntegerComponent` | AI->Integer | Numbers | Extracts integer values |
| `AI2IntegerListComponent` | AI->IntegerList | Numbers | Extracts a list of integers |
| `AI2BooleanComponent` | AI->Boolean | Boolean | Extracts boolean values |
| `AI2BooleanListComponent` | AI->BooleanList | Boolean | Extracts a list of booleans |
| `AI2ImgComponent` | AI->Img | Images | Extracts generated images |
| `AI2JsonComponent` | AI->Json | JSON | Extracts structured JSON |
| `AI2GhJsonComponent` | AI->GhJSON | Grasshopper | Converts to Grasshopper JSON format |
| `AI2ScriptComponent` | AI->Script | Code | Generates Python/C# scripts |
| `AI2SpeechComponent` | AI->Speech | Audio | Generates speech audio from text |

---

## Architecture & Design

### Design Rationale

Output adapters are the components that actually call the AI provider. They receive `AIInputPayload` from input adapters, execute the AI request, and convert the response into typed Grasshopper data. Instead of building one monolithic component per input-output combination, any input adapter can connect to any output adapter.

### Data Flow

```text
AIInputPayload (from input component) → AIOutputAdapterBase calls AI provider
    → AIReturn → ExtractOutputs() → Grasshopper data type → downstream components

```

### Structured Output (JSON Schema)

When `AIBody.JsonOutputSchema` is set, the AI provider constrains its output to match the schema. The `AI2JsonComponent` then extracts the structured data without parsing ambiguity.

### Related Documentation

- [Input Components](../Input/index.md) -- the other half of the adapter pattern
- [AI Components](../AI/index.md) -- model management and legacy monolithic components
- [AIRequestParameters](../../Architecture/AIRequestParameters.md) -- request customization (JSON schema)
- [ComponentBase](../ComponentBase/index.md) -- base class hierarchy
