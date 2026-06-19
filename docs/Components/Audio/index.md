# Audio Components

Audio components provide audio playback and visualization capabilities within Grasshopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/Audio/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Audio components extend Grasshopper with the ability to play, visualize, and process audio directly on the canvas. They bridge the gap between parametric design and multimedia workflows, enabling speech-to-text and text-to-speech integrations.

**You should read this if you:**

- Want to play or visualize audio files inside Grasshopper
- Need to integrate speech-to-text or text-to-speech into your definitions
- Are building custom components that consume or produce audio data

---

## End-User Guide

### Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `AudioViewerComponent` | Audio Viewer | Playback | Displays and plays audio files with waveform visualization |

---

## Developer Reference

### Using the Audio Viewer

The `AudioViewerComponent` can be referenced programmatically to display audio data:

```csharp
// Create an audio viewer instance and load an audio file
var audioViewer = new AudioViewerComponent();
audioViewer.LoadAudioFile("path/to/audio.wav");
audioViewer.Play();

```

### Integrating with AI Audio Workflows

Audio components work seamlessly with AI-powered speech components:

```csharp
// Pipe Audio2AI output into the viewer
var audioInput = Audio2AIComponent.CaptureMicrophoneInput();
var viewer = new AudioViewerComponent();
viewer.SetAudioStream(audioInput);
viewer.RefreshWaveform();

```

---

## Architecture & Design

- Audio components integrate with the AI audio interaction system
- Support for multiple audio formats (WAV, MP3, MPEG, etc.)
- Waveform visualization for audio content inspection
- Used in conjunction with `Audio2AIComponent` for speech-to-text workflows
- Used with `AI2SpeechComponent` for text-to-speech output playback
