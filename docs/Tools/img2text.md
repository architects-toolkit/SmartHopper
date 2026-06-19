# img2text Tool

Describes or analyzes an image using a vision AI model, returning a text description.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/img_to_text.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

The `img2text` tool enables AI-powered image analysis within Grasshopper workflows. You can extract descriptions, identify objects, read text in images, or analyze diagrams without leaving the canvas.

**You should read this if you:**

- Want to analyze images as part of a parametric design workflow
- Need to convert visual data (photos, renders, diagrams) into structured text
- Are building a pipeline that processes documents with images via `file2md`
- Want to understand which providers support vision capabilities

---

## End-User Guide

### What Is This?

The `img2text` tool sends an image to a vision-capable AI model and returns a text description. You can provide images via a public URL or base64-encoded data, and customize the prompt to guide the analysis (e.g., "describe the architectural style" or "extract all visible text").

### When to Use It

- **Document analysis**: After `file2md` extracts images from a PDF, describe each image to generate alt-text or summaries
- **Design review**: Feed renders or photos to the AI for feedback on composition, materials, or style
- **Data extraction**: Read charts, diagrams, or signs from images

### Parameters

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `imageUrl` | string | One of `imageUrl`/`imageBase64` | — | Public HTTP(S) URL of the image to analyze |
| `imageBase64` | string | One of `imageUrl`/`imageBase64` | — | Base64-encoded image data (no data URI prefix) |
| `mimeType` | string | No | `image/png` | MIME type when using `imageBase64` (e.g. `image/jpeg`) |
| `prompt` | string | No | `"Describe this image in detail..."` | Custom instruction for the AI |

At least one of `imageUrl` or `imageBase64` must be provided.

### Result

```json
{
  "description": "A modern office building with glass facade...",
  "__envelope": { }
}

```

The `description` key contains the AI-generated text. The `__envelope` metadata follows the [ToolResultEnvelope](./ToolResultEnvelope.md) convention.

### Provider Compatibility

| Provider | Supported | Notes |
| --- | --- | --- |
| OpenAI | Yes | URL and base64 data URI (`data:mime;base64,...`) |
| Anthropic | Yes | Native `image` content block with `base64` or `url` source |
| MistralAI | Yes | OpenAI-compatible `image_url` block with data URI support |
| DeepSeek | No | No `ImageInput` capability registered |

### Common Questions

**Q: Can I analyze a local image file?**
A: Yes, convert it to base64 and pass it via `imageBase64` with the correct `mimeType`.

**Q: Why is my provider not working with images?**
A: Verify the provider and selected model support vision capabilities (`AICapability.Image2Text`).

---

## Developer Reference

### API Overview

```csharp
// The tool is implemented as an AITool subclass
public class img_to_text : AITool
{
    public override string Name => "img_to_text";
    public override string Category => "Img";
    public override AICapability RequiredCapability => AICapability.Image2Text;
}

```

### Key Types

| Type | Purpose |
| --- | --- |
| `img_to_text` | Tool implementation |
| `AITool` | Base class for all SmartHopper AI tools |
| `AIInteractionImage` | Normalized image representation for providers |
| `ToolResultEnvelope` | Standard metadata wrapper for tool results |

### Code Examples

#### Calling the Tool from a Component

```csharp
// Build the tool request
var toolRequest = new AIToolRequest("img2text")
{
    Parameters = new Dictionary<string, object>
    {
        { "imageUrl", "<https://example.com/photo.jpg"> },
        { "prompt", "Describe the architectural style of this building." }
    }
};

// Execute via the provider
var result = await toolManager.ExecuteToolAsync(toolRequest);
var description = result.Payload["description"].ToString();

```

**Output**: The AI-generated description string.

#### Processing a Base64 Image from file2md

```csharp
// After file2md extracts images from a document
foreach (var image in file2mdResult.Images)
{
    var toolRequest = new AIToolRequest("img2text")
    {
        Parameters = new Dictionary<string, object>
        {
            { "imageBase64", image.Base64Data },
            { "mimeType", image.MimeType },
            { "prompt", "Summarize what this diagram shows." }
        }
    };

    var result = await toolManager.ExecuteToolAsync(toolRequest);
    descriptions.Add(result.Payload["description"].ToString());
}

```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| Missing image input | Neither `imageUrl` nor `imageBase64` provided | Supply one image source |
| Invalid base64 | Corrupted or improperly encoded image data | Verify base64 string is valid |
| Provider not supported | Selected provider lacks `Image2Text` capability | Switch to OpenAI, Anthropic, or MistralAI |
| URL unreachable | `imageUrl` returns 404 or is blocked | Verify URL is publicly accessible |

---

## Architecture & Design

### Design Rationale

**Problem**: Grasshopper workflows need to analyze visual data, but there is no standard way to send images to AI models across different providers.

**Approach**: Provide a unified `img2text` tool that normalizes image inputs into `AIInteractionImage` objects. Each provider then formats the image into its native API shape (OpenAI `image_url`, Anthropic `image` block, etc.).

**Trade-offs**:

- Unified interface (simple for users) vs provider-specific feature loss (some providers support advanced vision features not exposed)
- Base64 encoding (works everywhere) vs URL references (faster, smaller payloads)

### System Relationships

```text
Grasshopper Component → AIToolRequest("img2text") → ToolManager → Provider → AI Model
                                      ↑
                              AIInteractionImage (normalized)

```

### Related Documentation

- [ToolResultEnvelope](./ToolResultEnvelope.md)
- `file2md` tool — document conversion that can extract images for analysis
- `AIImg2TextComponent` — standalone Grasshopper component that wraps this tool for direct canvas use (see Components/AI category)
