# AIInputPayload Wire Type Architecture

## Overview

The **AIInputPayload** wire type enables a composable input/output architecture for SmartHopper components. Instead of monolithic "Xâ†’AIâ†’Y" components, the new architecture uses:

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
- `Text` â€” Text content interactions
- `Image` â€” Image content interactions
- `Audio` â€” Audio content interactions
- `Context` â€” Context provider filters
- `Unknown` â€” Mixed or unclassified payloads

### Merging Strategy

When multiple `GH_AIInputPayload` inputs are wired to the same branch path on an output component:

1. **Order matters**: Payloads are merged in wire index order (first received â†’ first interaction)
2. **Context payloads are special**: Context filters are extracted and concatenated with commas
3. **Result**: A single `AIBody` with all interactions in sequence

Example:
```
Wire 0: Text("Hello") â†’ Interaction 0
Wire 1: Text("World") â†’ Interaction 1
Wire 2: Context("time") â†’ Context filter added
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
AIContextComponent("time") â†’ GH_AIInputPayload(Context("time"))
                                    â†“
                          [Output Component]
                                    â†“
                    Generates filter: "time" (or merged with other contexts)
                                    â†“
                    Passes to AIBodyBuilder.WithContextFilter()
```

## File Structure

### Core Types (SmartHopper.Core.Grasshopper/Types/)

- `AIInputPayload.cs` â€” Core payload class and `AIInputPayloadType` enum
- `GH_AIInputPayload.cs` â€” Grasshopper goo wrapper
- `AIInputPayloadParameter.cs` â€” Grasshopper parameter type
- `AIInputPayloadMerger.cs` â€” Branch-aware merging logic
- `AIInputPayloadRenderer.cs` â€” User-readable rendering

### Image Support (SmartHopper.Core.Grasshopper/Types/)

- `VersatileImage.cs` â€” Versatile image source adapter
- `GH_AIImage.cs` â€” Grasshopper goo wrapper for images

### Audio Support (SmartHopper.Infrastructure/AICall/Core/Interactions/)

- `AIInteractionAudio.cs` â€” Audio interaction type

### Components (SmartHopper.Components/Input/)

- `AIContextComponent.cs` â€” Context provider input adapter

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

## Future Phases

- **Phase 4**: Output components (`AI2Text`, `AI2List`, `AI2Json`, etc.)
- **Phase 5**: Data tree processing centralization
- **Phase 6**: Batch and sentinel centralization
- **Phase 7**: Fallback resolver (modality fallback)
- **Phase 8**: Audio provider support
- **Phase 9**: Shim wrappers and panel reorganization

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about AIInputPayload.


## End-User Guide

End-user guidance for AIInputPayload.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for AIInputPayload.
