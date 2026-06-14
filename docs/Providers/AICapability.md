# AICapability

Defines the capabilities that an AI model can support, using flags enum for flexible capability composition.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AIModels/AICapability.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-13 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document defines how SmartHopper determines what operations a specific AI model can perform. Capabilities are used for model selection, component validation, and provider feature detection.

**You should read this if you:**

- Are implementing a new AI provider and need to declare model capabilities
- Want to understand why certain models appear in or are excluded from selection menus
- Are building components that require specific AI capabilities (text, images, audio, tools)

---

## End-User Guide

### What Are AI Capabilities?

AI capabilities describe what a model can do. When you select a provider and model in SmartHopper, the system checks whether that model supports the operation you're trying to perform.

For example:

- **Text generation** requires `TextInput` and `TextOutput`
- **Image generation** requires `ImageOutput`
- **Vision (image understanding)** requires `ImageInput` and `TextOutput`
- **Tool calling** requires `FunctionCalling`

### How Capabilities Affect Model Selection

SmartHopper automatically filters models based on the capability required by a component:

- **AI2Text components**: Only show models with `TextOutput`
- **AI2Img components**: Only show models with `ImageOutput`
- **AI tools**: Require models with `FunctionCalling`

If a model doesn't support the required capability, it won't appear in the model selection menu for that component.

### Visual Guide

<!-- PLACEHOLDER: Screenshot showing model selection dropdown filtered by capability -->
<!-- - Component: AI2Text with model menu open -->
<!-- - Notice: Only models with TextOutput capability are shown -->

### Common Questions

**Q: Why can't I see my favorite model in the list?**
A: The model may not support the capability required by the component. For example, GPT-4 without vision won't appear in AI2Text when an image is connected.

**Q: Can I force a model to be used even if it lacks the declared capability?**
A: No, this is a safety feature. However, you can override model selection in advanced scenarios using the Settings input.

**Q: What's the difference between AudioInput and SpeechInput?**
A: `SpeechInput` is for voice/speech-to-text. `AudioInput` includes speech plus general audio (music, sound effects). Models with `AudioInput` automatically support speech too.

---

## Developer Reference

### API Overview

```csharp
[Flags]
public enum AICapability
{
    None = 0,
    TextInput = 1 << 0,
    ImageInput = 1 << 1,
    AudioInput = SpeechInput | (1 << 2),
    VideoInput = 1 << 3,
    TextOutput = 1 << 4,
    ImageOutput = 1 << 5,
    AudioOutput = SpeechOutput | (1 << 6),
    JsonOutput = 1 << 7,
    FunctionCalling = 1 << 8,
    Reasoning = 1 << 9,
    SpeechInput = 1 << 10,
    SpeechOutput = 1 << 11,
    VideoOutput = 1 << 12,
    EmbedOutput = 1 << 13,

    // Composite capabilities
    Text2Text = TextInput | TextOutput,
    ToolChat = Text2Text | FunctionCalling,
    ReasoningChat = Text2Text | Reasoning,
    ToolReasoningChat = Text2Text | Reasoning | FunctionCalling,
    Text2Json = TextInput | JsonOutput,
    Text2Image = TextInput | ImageOutput,
    Text2Speech = TextInput | SpeechOutput,
    Speech2Text = SpeechInput | TextOutput,
    Audio2Text = AudioInput | TextOutput,
    Text2Audio = TextInput | AudioOutput,
    Image2Text = ImageInput | TextOutput,
    Image2Image = ImageInput | ImageOutput,
}

```

### Individual Capabilities

| Flag | Bit | Description |
| --- | --- | --- |
| `None` | 0 | No capabilities; placeholder value |
| `TextInput` | 1 << 0 | Supports text prompts and content input |
| `ImageInput` | 1 << 1 | Supports image understanding/vision |
| `SpeechInput` | 1 << 10 | Supports speech-to-text (voice input) |
| `AudioInput` | Composite | Supports general audio (includes SpeechInput) |
| `VideoInput` | 1 << 3 | Supports video understanding/analysis |
| `TextOutput` | 1 << 4 | Can generate text output |
| `ImageOutput` | 1 << 5 | Can generate images |
| `SpeechOutput` | 1 << 11 | Can do text-to-speech |
| `AudioOutput` | Composite | Can generate general audio (includes SpeechOutput) |
| `JsonOutput` | 1 << 7 | Can produce structured JSON |
| `FunctionCalling` | 1 << 8 | Supports tool/function calling |
| `Reasoning` | 1 << 9 | Supports enhanced reasoning/thinking |
| `VideoOutput` | 1 << 12 | Can generate video |
| `EmbedOutput` | 1 << 13 | Can produce embedding vectors |

### Composite Capabilities

| Composite | Constituent Flags | Use Case |
| --- | --- | --- |
| `Text2Text` | TextInput \| TextOutput | Standard chat/completion |
| `ToolChat` | Text2Text \| FunctionCalling | Chat with tool calling |
| `ReasoningChat` | Text2Text \| Reasoning | Chat with reasoning |
| `ToolReasoningChat` | Text2Text \| Reasoning \| FunctionCalling | Full-featured chat |
| `Text2Json` | TextInput \| JsonOutput | Structured output |
| `Text2Image` | TextInput \| ImageOutput | Image generation |
| `Text2Speech` | TextInput \| SpeechOutput | Text-to-speech |
| `Speech2Text` | SpeechInput \| TextOutput | Speech recognition |
| `Audio2Text` | AudioInput \| TextOutput | General audio understanding |
| `Text2Audio` | TextInput \| AudioOutput | General audio generation |
| `Image2Text` | ImageInput \| TextOutput | Vision/image description |
| `Image2Image` | ImageInput \| ImageOutput | Image editing |

### Extension Methods

```csharp
public static class AICapabilityExtensions
{
    // Formats capabilities for logging (e.g., "TextInput, TextOutput, FunctionCalling")
    public static string ToDetailedString(this AICapability capabilities);
}

```

### Code Examples

#### Checking a Single Capability

```csharp
var modelCapabilities = AICapability.TextInput | AICapability.TextOutput;
bool canChat = (modelCapabilities & AICapability.TextOutput) == AICapability.TextOutput;
// true

```

#### Checking Multiple Capabilities

```csharp
var required = AICapability.TextInput | AICapability.ImageOutput;
var model = AICapability.TextInput | AICapability.TextOutput | AICapability.ImageOutput;

bool supportsAll = (model & required) == required;
// true - model has both TextInput and ImageOutput

```

#### Using Composite Capabilities

```csharp
// Register a model that supports image generation
var capabilities = AICapability.Text2Image;

// This automatically includes TextInput and ImageOutput
var hasImageOut = capabilities.HasFlag(AICapability.ImageOutput);
// true

```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| `InvalidOperationException` | Capability check failed unexpectedly | Verify enum values aren't corrupted |
| Model not in list | Model lacks required capability | Check capability flags in model registration |

---

## Architecture & Design

### Design Rationale

**Problem**: Need a flexible way to describe model capabilities that:

- Allows combining multiple capabilities (e.g., text + image input)
- Is efficient to check (single bitwise AND operation)
- Is extensible (new capabilities can be added without breaking existing code)

**Approach**: Use `[Flags]` enum with bitwise composition.

**Trade-offs**:

- **Benefit**: Fast capability checks, compact representation
- **Benefit**: Natural composition via OR operations
- **Cost**: Limited to 32/64 flags (sufficient for current use)
- **Cost**: Requires understanding of bitwise operations

### System Relationships

```text
[Provider Plugin] --registers--> [AIModelCapabilities] --uses--> [AICapability]
                                              |
                                              v
[AI Component] <--queries-- [ModelManager] <--filters by--> [AICapability]

```

### Design Patterns

- **[Flags Enum](https://docs.microsoft.com/en-us/dotnet/api/system.flagsattribute)**: Enables efficient composition and checking
- **Composite Pattern**: Higher-level capabilities (e.g., `Text2Text`) compose lower-level ones

### Related Documentation

- [AIModelCapabilities](./AIModelCapabilities.md) -- model registration and capability declaration
- [ProviderManager](./ProviderManager.md) -- global provider registry
- [Architecture.md](../Architecture.md) -- provider system overview
