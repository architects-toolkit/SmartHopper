# Getting Started with SmartHopper

SmartHopper is an AI-powered plugin for Grasshopper 3D that lets you integrate large language models directly into your parametric design workflows.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This guide introduces SmartHopper's capabilities and the basic patterns for using AI in your Grasshopper definitions. It covers the most common workflows so you can start building immediately.

**You should read this if you:**

- Are new to SmartHopper and want to understand what it can do
- Want to learn the basic Input / Output workflow pattern
- Need quick examples of common AI-assisted design tasks
- Are choosing an AI provider and need to know what models support

---

## End-User Guide

### What Can SmartHopper Do?

SmartHopper connects Grasshopper to AI providers like OpenAI, Anthropic, and Google Gemini. You can:

- **Generate text** -- descriptions, code, analysis, summaries
- **Analyze images** -- describe photos, extract information from drawings
- **Convert speech** -- transcribe audio to text, generate speech from text
- **Produce structured data** -- extract numbers, booleans, JSON from AI responses
- **Work with files** -- convert PDFs, Word documents, and web pages into AI-ready content
- **Use tools** -- let AI interact with your Grasshopper canvas (read/write components, toggle previews)

### Quick Links

#### Components

- [Input Components](../Components/Input/index.md) -- wrap data for AI processing
- [Output Components](../Components/Output/index.md) -- call AI and extract typed results
- [AI Components](../Components/AI/index.md) -- model management and legacy monolithic components
- [All Components](../Components/index.md) -- full component catalog

#### Providers

- [Provider Overview](../Providers/index.md) -- supported AI providers
- [AI Capabilities](../Providers/AICapability.md) -- what each model can do

#### Architecture

- [Architecture Overview](../Architecture.md) -- how SmartHopper is built
- [AI Call Pipeline](../Providers/AICall/index.md) -- how AI requests are processed

### Common Workflows

#### Text Generation

Connect a prompt to an output component that calls the AI and extracts text:

```text
[Text2AI] --> [AI2Text] --> [Text Panel]

```

#### Image Analysis

Send an image to a vision-capable output component:

```text
[Img2AI] --> [AI2Text] --> [Text Panel]

```

#### Structured Data Extraction

Get numbers, booleans, or JSON from AI:

```text
[Text2AI] --> [AI2Number] --> [Addition Component]

```

#### File Processing

Convert documents to AI-ready content:

```text
[File2AI] --> [AI2Text] --> [Text Panel]

```

### Choosing a Provider

SmartHopper supports multiple AI providers. Each provider offers different models with different capabilities. See [AI Capabilities](../Providers/AICapability.md) to understand what each model supports.

### Need Help?

- Check the [component documentation](../Components/index.md) for detailed guides
- Look at the **End-User Guide** section in any documentation file for user-focused instructions
- Review [Architecture](../Architecture.md) for system-level understanding
- Visit the [GitHub repository](https://github.com/architects-toolkit/SmartHopper) for issues and discussions

---

## Developer Reference

### Programmatic Text Generation

```csharp
// Example: creating an input payload and extracting text
var textInput = new Text2AI("Generate a parametric facade description");
var payload = textInput.ToPayload();

var output = new AI2Text(payload, provider: "OpenAI", model: "gpt-4");
string result = await output.ComputeAsync();

```

### Programmatic Structured Data Extraction

```csharp
// Example: extracting a numeric value from an AI response
var payload = new AIInputPayload();
payload.AddText("What is 245 divided by 5?");

var extractor = new AI2Number(payload, provider: "OpenAI", model: "gpt-4");
double? value = await extractor.ExtractAsync();

```

---

## Architecture & Design

SmartHopper uses a composable **Input / Output** pattern:

```text
[Input Component]  -->  [Output Component]  -->  [Your Result]
 (prepare data)        (call AI & extract)

```

1. **Input components** wrap your data (text, images, files, web content) into a unified `AIInputPayload`.
2. **Output components** call the AI provider, then extract the specific result type you need (text, number, image, JSON, etc.). The provider and model are configured on the output component.

This design decouples data preparation from AI invocation, allowing any input type to connect to any output type without requiring monolithic N*M component combinations.
