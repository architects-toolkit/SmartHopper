# Media Components (`G. Media`)

Components in `src/SmartHopper.Components/Img/` and `src/SmartHopper.Components/Audio/` for generating, analyzing, viewing, saving, and capturing images and audio under the **`SmartHopper → G. Media`** ribbon panel.

## Component catalog (`SmartHopper → G. Media`)

| Component | Display Name | Nickname | Purpose |
| --- | --- | --- | --- |
| `AIText2ImgComponent` | AI Text To Image | `AIText2Img` | Generates images from text prompts (`Prompt`, `Size`, `Quality`, `Style`, `Aspect Ratio` → `Image`, `Revised Prompt`) using `text2img` |
| `AIImg2TextComponent` | AI Image To Text | `AIImg2Text` | Describes or analyzes images (`Image`, optional `Prompt` → `Description`) using `img2text` |
| `ImageViewerComponent` | Image Viewer | `ImgView` | Displays an image on the Grasshopper canvas and optionally saves it to `File Path` when `Save?` is `True` |
| `AudioViewerComponent` | Audio Viewer | `AudioView` | Previews audio with an on-canvas waveform and Play button; saves to `File Path` when `Save?` is `True` (held `True` re-saves on every input change, a pulse saves once) |
| `CanvasCaptureComponent` | Canvas To Image | `Canvas2Img` | Captures the visible Grasshopper canvas as a `VersatileImage` (`Image`, `Width`, `Height`) using `canvas_screenshot` |
| `ViewportCaptureComponent` | Viewport To Image | `Viewport2Img` | Captures the active or named Rhino viewport as a `VersatileImage` (`Image`, `Width`, `Height`, `View Name`) using `viewport_screenshot` |

## Related documentation

- [Input Components (`Image to AI`, `Audio to AI`)](../Input/index.md)
- [Output Components (`AI to Image`, `AI to Speech`)](../Output/index.md)
- [VersatileAudio architecture](../../Architecture/VersatileAudio.md)
