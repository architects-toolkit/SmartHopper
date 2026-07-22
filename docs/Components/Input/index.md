# Input Components

Adapter components that wrap various data types and external sources into `AIInputPayload` for AI processing.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Components/Input/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-13 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Input components are the starting point of every AI workflow in SmartHopper. They convert your Grasshopper data -- text, images, files, web pages, audio, forum posts -- into a unified format that any output component can process.

**You should read this if you:**

- Want to send data to an AI model from Grasshopper
- Need to understand which input component to use for your data type
- Are building a custom input adapter

---

## End-User Guide

### What Do Input Components Do?

Input components are the **first step** in an AI workflow. They take your data and package it into a format the AI can understand (`AIInputPayload`). You then connect the output to an output component, which calls the AI and extracts the result.

```text
[Your Data] → [Input Component] → [Output Component] → [Your Result]
                  ▲ you are here

```

### Which Component Should I Use?

| I want to send... | Use this component | Nickname |
| --- | --- | --- |
| A text prompt | `Text2AIComponent` | Text2AI |
| Multiple text items | `TextList2AIComponent` | TextList2AI |
| A list of numbers | `NumberList2AIComponent` | NumberList2AI |
| A list of integers | `IntegerList2AIComponent` | IntegerList2AI |
| Boolean values | `BooleanList2AIComponent` | BooleanList2AI |
| An image (file or URL) | `Img2AIComponent` | Img2AI |
| An audio file | `Audio2AIComponent` | Audio2AI |
| A local file (PDF, DOCX...) | `File2AIComponent` | File2AI |
| A web page | `Web2AIComponent` | Web2AI |
| JSON data | `Json2AIComponent` | Json2AI |
| Grasshopper JSON | `GhJSON2AIComponent` | GhJSON2AI |
| A structured prompt | `AIPromptComponent` | AI Prompt |
| Context filters | `AIContextComponent` | AI Context |
| Forum posts/topics | See Forum components below | -- |

### Forum Components

| Component | Nickname | Source |
| --- | --- | --- |
| `DiscoursePost2AIComponent` | Discourse Post2AI | Discourse forums |
| `DiscourseTopic2AIComponent` | Discourse Topic2AI | Discourse forums |
| `LadybugPost2AIComponent` | Ladybug Post2AI | Ladybug Tools forum |
| `LadybugTopic2AIComponent` | Ladybug Topic2AI | Ladybug Tools forum |
| `McNeelPost2AIComponent` | McNeel Post2AI | McNeel Discourse forum |
| `McNeelTopic2AIComponent` | McNeel Topic2AI | McNeel Discourse forum |

### Visual Guide

<!-- PLACEHOLDER: Screenshot showing Input components in the Grasshopper ribbon -->
<!-- - Component location: SmartHopper tab → Input panel -->
<!-- - Typical wiring: Input component output → Output component "Payload" input -->

### Common Questions

**Q: Can I combine multiple input types?**
A: Yes. Connect multiple input components to the same output component. They will be merged into a single payload.

**Q: What file formats does File2AI support?**
A: PDF, DOCX, XLSX, PPTX, HTML, EML, and EPUB. See [File to Markdown](../../Usage/file-to-markdown.md) for details.

**Q: What does the `Image Mode` input do in File2AI and Web2AI?**
A: It controls how extracted images are represented in the Markdown payload. `skip`/`link` keeps the raw image references (File2AI skips image extraction entirely); `embed`, `describe`, and `caption` use AI to generate captions or descriptions and require a configured AI provider.

**Q: Do I need a specific model for image or audio input?**
A: Yes. The model must support the corresponding capability (e.g., `ImageInput` for images). See [AI Capabilities](../../Providers/AICapability.md).

---

## Developer Reference

### Base Class

: Most input components inherit from `AIInputAdapterBase`, a synchronous adapter that builds `AIInputPayload`. `File2AIComponent` and `Web2AIComponent` inherit from `AIStatefulAsyncComponentBase` so they can perform AI-powered image descriptions through the batch system.

```csharp
public abstract class AIInputAdapterBase : GH_Component
{
    protected abstract AIInputPayload BuildPayload(IGH_DataAccess DA);
}

```

See [AIInputAdapterBase](../ComponentBase/AIInputAdapterBase.md) for the synchronous adapter API and [AIStatefulAsyncComponentBase](../ComponentBase/AIStatefulAsyncComponentBase.md) for the async/batch base class.

### Creating a Custom Input Adapter

```csharp
public class MyData2AIComponent : AIInputAdapterBase
{
    protected override AIInputPayload BuildPayload(IGH_DataAccess DA)
    {
        // 1. Read your input data
        string data = "";
        DA.GetData(0, ref data);

        // 2. Build and return the payload
        return new AIInputPayload(
            AIInteraction.FromText(data, AIInteractionRole.User));
    }
}

```

### Full Component Catalog

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `Text2AIComponent` | Text2AI | Text | Wraps text input into an AIInputPayload |
| `TextList2AIComponent` | TextList2AI | Text | Wraps a list of text items into an AIInputPayload |
| `NumberList2AIComponent` | NumberList2AI | Numbers | Wraps a list of numbers into an AIInputPayload |
| `IntegerList2AIComponent` | IntegerList2AI | Numbers | Wraps a list of integers into an AIInputPayload |
| `BooleanList2AIComponent` | BooleanList2AI | Boolean | Wraps a list of boolean values into an AIInputPayload |
| `Img2AIComponent` | Img2AI | Images | Wraps image files or URLs for vision processing |
| `Audio2AIComponent` | Audio2AI | Audio | Wraps audio files for speech-to-text processing |
| `File2AIComponent` | File2AI | Files | Converts local files to markdown and wraps into payload; `Image Mode` selects skip/embed/describe/caption |
| `Web2AIComponent` | Web2AI | Web | Fetches web content and wraps into payload; `Image Mode` selects link/embed/describe/caption; links and images are always kept |
| `Json2AIComponent` | Json2AI | JSON | Wraps JSON data into an AIInputPayload |
| `GhJSON2AIComponent` | GhJSON2AI | Grasshopper | Wraps Grasshopper JSON into an AIInputPayload |
| `AIPromptComponent` | AI Prompt | Prompts | Creates a structured AI prompt with system/user roles |
| `AIContextComponent` | AI Context | Context | Injects AI context data into an AIInputPayload |
| `DiscoursePost2AIComponent` | Discourse Post2AI | Forums | Wraps Discourse forum posts |
| `DiscourseTopic2AIComponent` | Discourse Topic2AI | Forums | Wraps Discourse forum topics |
| `LadybugPost2AIComponent` | Ladybug Post2AI | Forums | Wraps Ladybug forum posts |
| `LadybugTopic2AIComponent` | Ladybug Topic2AI | Forums | Wraps Ladybug forum topics |
| `McNeelPost2AIComponent` | McNeel Post2AI | Forums | Wraps McNeel forum posts |
| `McNeelTopic2AIComponent` | McNeel Topic2AI | Forums | Wraps McNeel forum topics |

---

## Architecture & Design

### Design Rationale

Input adapters exist to solve the combinatorial problem: instead of building N*M monolithic components (one for each input-output combination), the adapter pattern lets N input types connect to M output types through a single unified format.

### Data Flow

```text
Grasshopper data → AIInputAdapterBase.BuildPayload() → AIInputPayload
    → Output component receives payload → calls AI provider → extracts typed result

```

### Related Documentation

- [AIInputPayload](../../Architecture/AIInputPayload.md) -- the wire type all adapters produce
- [Output Components](../Output/index.md) -- the other half of the adapter pattern
- [AI Components](../AI/index.md) -- model management and legacy monolithic components
- [ComponentBase](../ComponentBase/index.md) -- base class hierarchy
- [File to Markdown](../../Usage/file-to-markdown.md) -- file conversion used by File2AI
