# Screenshot Tools

Capture the visible Grasshopper canvas or a Rhino viewport as a bounded base64 PNG without changing the document.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/screenshots.cs` |
| **Components** | `Canvas To Image`, `Viewport To Image` |
| **Since Version** | 2.0.0 |
| **Last Updated** | 2026-09-06 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

Use these tools when an AI agent or MCP client needs visual context that structural GhJSON queries do not provide. Use the matching Grasshopper components when a definition needs a reusable `VersatileImage` capture.

## End-User Guide

### `canvas_screenshot`

Captures the current Grasshopper canvas viewport with `GH_Canvas.GetCanvasScreenBuffer`. It does not render a high-resolution tiled image.

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `maxWidth` | integer | `1920` | Maximum output width, from 1 to 4096 pixels. |
| `maxHeight` | integer | `1080` | Maximum output height, from 1 to 4096 pixels. |

### `viewport_screenshot`

Captures the active Rhino viewport, or the first viewport matching `viewName`, with `ViewCapture.CaptureToBitmap`.

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `viewName` | string | active view | Optional viewport name, matched case-insensitively. |
| `width` | integer | `1024` | Maximum output width, from 1 to 4096 pixels. |
| `height` | integer | `1024` | Maximum output height, from 1 to 4096 pixels. |

Both tools preserve aspect ratio and do not upscale captures smaller than the requested bounds. Results contain `imageBase64`, `mimeType`, `width`, and `height`; viewport results also contain the resolved `viewName`.

### Grasshopper Components

- **Canvas To Image** wraps `canvas_screenshot` and outputs a persistent `VersatileImage`, width, and height.
- **Viewport To Image** wraps `viewport_screenshot` and also outputs the resolved viewport name.
- Both inherit the stateful component lifecycle. Connect a Button or boolean pulse to `Run`; the last successful image remains available while waiting, after a failed capture, and after reopening a saved definition.

## Developer Reference

`CanvasCaptureService` and `ViewportCaptureService` marshal capture access to Rhino's UI thread. `ImageCaptureUtilities` performs bounded aspect-ratio resizing and PNG encoding. The `Screenshots` AITool provider is discovered automatically by `AIToolManager`; its tools are read-only and therefore exposed by MCP without enabling mutating tools.

The stateful components execute the same AITools through `AIToolCall.Exec()` rather than duplicating capture logic. PNG data is stored as `VersatileImageKind.Base64`, which is supported by the existing `VersatileImageCodec` persistence path.

### Code Examples

Capture the Grasshopper canvas directly:

```csharp
using SmartHopper.Core.Grasshopper.Utils.Internal;

var result = await new CanvasCaptureService()
    .CaptureCanvasAsync(maxWidth: 1920, maxHeight: 1080);

Console.WriteLine($"Canvas captured: {result.Width}x{result.Height}");
var pngBase64 = result.ImageBase64;
```

Capture a named Rhino viewport:

```csharp
using SmartHopper.Core.Grasshopper.Utils.Internal;

var result = await new ViewportCaptureService()
    .CaptureViewportAsync(viewName: "Perspective", width: 1024, height: 1024);

Console.WriteLine($"Viewport '{result.ViewName}' captured: {result.Width}x{result.Height}");
var pngBase64 = result.ImageBase64;
```

## Architecture & Design

Screenshot tools read visual state but do not mutate the Grasshopper or Rhino document. Captures can still contain sensitive geometry, filenames, annotations, or client information. Base64 payloads are not written to logs. MCP users should configure bearer authentication when local clients are not fully trusted and should only send captures to external AI providers with user intent.

The 4096-pixel per-axis limit constrains memory and response growth. Structural queries such as `gh_get` or `gh_report` remain preferable when an image is unnecessary.

## Related Documentation

- [Tools](./index.md)
- [img2text Tool](./img2text.md)
- [MCP Server](../Architecture/mcp-server.md)
- [ToolResultEnvelope](./ToolResultEnvelope.md)
