# img_to_text Tool

Describes or analyzes an image using a vision AI model, returning a text description.

## Overview

| Property | Value |
|---|---|
| Tool name | `img_to_text` |
| Category | `Img` |
| Required capability | `AICapability.Image2Text` (`ImageInput \| TextOutput`) |
| Source | `SmartHopper.Core.Grasshopper/AITools/img_to_text.cs` |

## Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `imageUrl` | string | One of `imageUrl`/`imageBase64` | — | Public HTTP(S) URL of the image to analyze |
| `imageBase64` | string | One of `imageUrl`/`imageBase64` | — | Base64-encoded image data (no data URI prefix) |
| `mimeType` | string | No | `image/png` | MIME type when using `imageBase64` (e.g. `image/jpeg`) |
| `prompt` | string | No | `"Describe this image in detail. Include all visible content, text, objects, charts, and the apparent purpose or context of the image."` | Custom instruction for the AI |

At least one of `imageUrl` or `imageBase64` must be provided.

## Result

```json
{
  "description": "A modern office building with glass facade...",
  "__envelope": { ... }
}
```

The `description` key contains the AI-generated text. The `__envelope` metadata follows the [ToolResultEnvelope](./ToolResultEnvelope.md) convention.

## Provider compatibility

| Provider | Supported | Notes |
|---|---|---|
| OpenAI | ✅ | URL and base64 data URI (`data:mime;base64,...`) |
| Anthropic | ✅ | Native `image` content block with `base64` or `url` source |
| MistralAI | ✅ | OpenAI-compatible `image_url` block with data URI support |
| DeepSeek | ❌ | No `ImageInput` capability registered |

## Usage examples

### Describe a public image URL

```json
{
  "imageUrl": "https://example.com/photo.jpg",
  "prompt": "Describe the architectural style of this building."
}
```

### Describe an extracted document image

When used after `file_to_md` with `extractImages: true`, pass each image's `base64Data` and `mimeType`:

```json
{
  "imageBase64": "<base64 data from file_to_md images array>",
  "mimeType": "image/jpeg",
  "prompt": "Summarize what this diagram shows."
}
```

## Related

- [ToolResultEnvelope.md](./ToolResultEnvelope.md) — envelope metadata convention
- `file_to_md` `describeImages` parameter calls `img_to_text` automatically for each extracted image — see `describeImages`, `imageMode`, and `imageDescriptionPrompt` parameters on the `file_to_md` tool
- `AIImgToTextComponent` — standalone Grasshopper component that wraps this tool for direct canvas use
