# AIInputPayload Wire Type Architecture

## Overview

The **AIInputPayload** wire type enables a composable input/output architecture for SmartHopper components. Instead of monolithic "X→AI→Y" components, the new architecture uses:

- **Input Adapters**: Synchronous, stateless components that produce `GH_AIInputPayload` outputs
- **AIInputPayload Wire**: Carries AI interactions between components
- **Output Components**: Consume payloads and execute AI operations

## Core Concepts

### AIInputPayload

A payload is a container for AI interactions with metadata:

```csharp
public sealed class AIInputPayload
{
    public List<IAIInteraction> Interactions { get; }
    public AICapability InputCapabilityAtSource { get; }
    public string Hint { get; }  // Optional MIME type
    public AIInputPayloadType PayloadType { get; }
}
```

**Payload Types:**
- `Text` — Text content interactions
- `Image` — Image content interactions
- `Audio` — Audio content interactions
- `Context` — Context provider filters
- `Unknown` — Mixed or unclassified payloads

### Merging Strategy

When multiple `GH_AIInputPayload` inputs are wired to the same branch path on an output component:

1. **Order matters**: Payloads are merged in wire index order (first received → first interaction)
2. **Context payloads are special**: Context filters are extracted and concatenated with commas
3. **Result**: A single `AIBody` with all interactions in sequence

Example:
```
Wire 0: Text("Hello") → Interaction 0
Wire 1: Text("World") → Interaction 1
Wire 2: Context("time") → Context filter added
Result: AIBody with 2 text interactions + context filter "time"
```

### User-Readable Rendering

Each payload type has a custom renderer for Grasshopper UI display:

- **Text**: Shows agent and content preview (truncated at 100 chars)
- **Image**: Lists image sources (URL, base64, file path)
- **Audio**: Lists audio files with MIME type and language hints
- **Context**: Shows context provider filter expression

Use `AIInputPayloadRenderer.RenderToUserText(payload)` for formatted display.

## Component Integration

### Input Adapters

Input adapters are **synchronous, stateless** components that:
1. Accept Grasshopper data (text, images, files, etc.)
2. Create `AIInteraction` objects
3. Wrap in `AIInputPayload`
4. Output as `GH_AIInputPayload`

Example: `Text2AI` component
```csharp
public class Text2AIComponent : GH_Component
{
    protected override void SolveInstance(IGH_DataAccess DA)
    {
        string text = null;
        DA.GetData(0, ref text);
        
        var payload = AIInputPayload.FromText(text, AIAgent.User);
        DA.SetData(0, new GH_AIInputPayload(payload));
    }
}
```

### Output Components

Output components:

1. Receive multiple `GH_AIInputPayload` inputs per branch
2. Merge payloads using `AIInputPayloadMerger.MergePerBranch()`
3. Build final `AIBody` with system prompt
4. Execute AI call
5. Process and output results

## AIContext Component

The `AIContextComponent` is a special input adapter that:

1. **Input**: Provider ID (e.g., "time", "file", "rhino")
2. **Processing**: Calls `AIContextManager.GetCurrentContext(providerId)`
3. **Output**:
   - `GH_AIInputPayload` with context filter
   - Display text showing context data

**Important**: The context filter in the payload is **not** used directly by output components. Instead, output components generate their own filter based on all connected context payloads.

Example flow:

```
AIContextComponent("time") → GH_AIInputPayload(Context("time"))
                                    ↓
                          [Output Component]
                                    ↓
                    Generates filter: "time" (or merged with other contexts)
                                    ↓
                    Passes to AIBodyBuilder.WithContextFilter()
```

## File Structure

### Core Types (SmartHopper.Core/Models/)

- `AIInputPayload.cs` — Core payload class and `AIInputPayloadType` enum
- `GH_AIInputPayload.cs` — Grasshopper goo wrapper
- `AIInputPayloadParameter.cs` — Grasshopper parameter type
- `AIInputPayloadMerger.cs` — Branch-aware merging logic
- `AIInputPayloadRenderer.cs` — User-readable rendering

### Image Support (SmartHopper.Core/Models/)

- `VersatileImage.cs` — Versatile image source adapter
- `GH_AIImage.cs` — Grasshopper goo wrapper for images

### Audio Support (SmartHopper.Infrastructure/AICall/Core/Interactions/)

- `AIInteractionAudio.cs` — Audio interaction type

### Components (SmartHopper.Components/Input/)

- `AIContextComponent.cs` — Context provider input adapter

## Usage Examples

### Creating Text Payload

```csharp
var payload = AIInputPayload.FromText("Hello, AI!", AIAgent.User);
var gooPayload = new GH_AIInputPayload(payload);
```

### Creating Image Payload

```csharp
var imageSource = VersatileImage.FromString("path/to/image.png");
var interaction = imageSource.ToInteraction();
var payload = AIInputPayload.FromImage(interaction);
```

### Merging Payloads

```csharp
var payloads = new List<AIInputPayload> { payload1, payload2, payload3 };
var mergedBody = AIInputPayloadMerger.MergePerBranch(payloads);
```

### Rendering for UI

```csharp
var displayText = AIInputPayloadRenderer.RenderToUserText(payload);
var summary = AIInputPayloadRenderer.GetSummary(payload);
```

## Design Principles

1. **Composability**: Small, focused input adapters can be combined flexibly
2. **Order Preservation**: Interaction order is maintained during merging
3. **Type Safety**: `AIInputPayloadType` enables type-specific rendering
4. **Statelessness**: Input adapters have no internal state or AI calls
5. **Extensibility**: New payload types can be added without breaking existing code

## Current State

The AIInputPayload architecture is fully implemented with:
- **Input Adapters**: Text, image, audio, and context providers
- **Output Components**: Full suite of AI2* components for converting AI responses to Grasshopper data types
- **Data Tree Processing**: Centralized through `AIInputPayloadMerger` and component integration
- **Streaming Support**: Integrated with async component base classes
- **Tool Integration**: Complete tool result envelope support with metadata tracking
