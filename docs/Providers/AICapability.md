# AICapability

`src/SmartHopper.Infrastructure/AIModels/AICapability.cs`

A `[Flags]` enum that defines the capabilities supported by AI models. Used throughout SmartHopper for model selection, capability validation, component behavior configuration, and badge rendering.

## Purpose

Provides a unified, composable way to express what input/output modalities and advanced features an AI model supports. This enables:

- **Capability-aware model selection** — choose the best model for the task
- **Validation** — ensure the selected model can handle the requested operation
- **Component configuration** — components declare their `RequiredCapability` to enable appropriate UI and validation
- **Badge rendering** — display model capabilities visually to the user

## Individual Capabilities

### Input Capabilities

| Flag | Bit | Description |
|------|-----|-------------|
| `TextInput` | 1 << 0 | Accepts textual input (prompts, text content) |
| `ImageInput` | 1 << 1 | Accepts image input (vision, image understanding) |
| `SpeechInput` | 1 << 10 | Accepts speech input (voice, speech-to-text) |
| `AudioInput` | SpeechInput \| (1 << 2) | Accepts audio input (music, sound, general audio). Inherits `SpeechInput` |
| `VideoInput` | 1 << 3 | Accepts video input (video understanding, analysis) |

### Output Capabilities

| Flag | Bit | Description |
|------|-----|-------------|
| `TextOutput` | 1 << 4 | Produces textual output |
| `ImageOutput` | 1 << 5 | Generates images as output |
| `SpeechOutput` | 1 << 11 | Produces speech as output (text-to-speech) |
| `AudioOutput` | SpeechOutput \| (1 << 6) | Produces audio as output (music, sound). Inherits `SpeechOutput` |
| `JsonOutput` | 1 << 7 | Produces structured JSON output |
| `VideoOutput` | 1 << 12 | Generates video as output |
| `EmbedOutput` | 1 << 13 | Produces embedding vectors (for similarity search, clustering) |

### Advanced Capabilities

| Flag | Bit | Description |
|------|-----|-------------|
| `FunctionCalling` | 1 << 8 | Supports tool/function calling with structured arguments |
| `Reasoning` | 1 << 9 | Enhanced reasoning capabilities (long deliberation, thinking) |

## Composite Capabilities

Predefined combinations for common use cases:

| Composite | Definition | Use Case |
|-----------|-----------|----------|
| `Text2Text` | TextInput \| TextOutput | Standard text chat |
| `ToolChat` | Text2Text \| FunctionCalling | Text chat with tool calling |
| `ReasoningChat` | Text2Text \| Reasoning | Text chat with reasoning |
| `ToolReasoningChat` | Text2Text \| Reasoning \| FunctionCalling | Text chat with reasoning and tools |
| `Text2Json` | TextInput \| JsonOutput | Structured output generation |
| `Text2Image` | TextInput \| ImageOutput | Image generation |
| `Text2Speech` | TextInput \| SpeechOutput | Text-to-speech |
| `Text2Audio` | TextInput \| AudioOutput | General audio generation |
| `Speech2Text` | SpeechInput \| TextOutput | Speech recognition |
| `Audio2Text` | AudioInput \| TextOutput | Audio understanding |
| `Image2Text` | ImageInput \| TextOutput | Image description, vision |
| `Image2Image` | ImageInput \| ImageOutput | Image editing, transformation |

## Usage

### Model Registration

When registering a model with the `ModelManager`, specify its capabilities:

```csharp
var capabilities = AICapability.ToolChat | AICapability.ImageInput;
var modelCap = new AIModelCapabilities(
    providerName: "OpenAI",
    modelName: "gpt-4-vision",
    capabilities: capabilities,
    isVerified: true);

ModelManager.Instance.RegisterCapabilities(modelCap);
```

### Component Declaration

Components declare their required capability:

```csharp
public class MyAIComponent : AIStatefulAsyncComponentBase
{
    public override AICapability RequiredCapability => AICapability.Text2Text;
}
```

### Capability Checking

Use bitwise operations to check capabilities:

```csharp
var model = ModelManager.Instance.GetModelCapabilities("OpenAI", "gpt-4");
if ((model.Capabilities & AICapability.FunctionCalling) == AICapability.FunctionCalling)
{
    // Model supports tool calling
}
```

### Formatting

Use `AICapabilityExtensions.ToDetailedString()` for logging:

```csharp
var caps = AICapability.Text2Text | AICapability.ImageInput;
Console.WriteLine(caps.ToDetailedString());
// Output: "TextInput | TextOutput | ImageInput"
```

## Design Notes

- **Hierarchical inheritance**: `AudioInput` and `AudioOutput` inherit from `SpeechInput` and `SpeechOutput` respectively, so models with audio support automatically support speech.
- **Composability**: Flags can be combined with bitwise OR (`|`) to express complex capability sets.
- **Extensibility**: New capabilities can be added by defining new flags and updating the enum.
- **Backward compatibility**: `None` (0) is used as a default/placeholder value.

## Related

- [AIModelCapabilities](./AIModelCapabilities.md) — wraps capabilities with metadata (verified, deprecated, rank, aliases)
- [ModelManager](./ModelManager.md) — central registry and selection logic
- [AIStatefulAsyncComponentBase](../Components/ComponentBase/AIStatefulAsyncComponentBase.md) — uses `RequiredCapability` for validation and model selection
