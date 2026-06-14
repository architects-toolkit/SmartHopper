# VersatileAudio Type System

Versatile audio adapter for handling heterogeneous audio inputs (file paths, URLs, base64, data-URIs) with unified access and document extraction metadata.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core/Types/VersatileAudio.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

VersatileAudio provides a unified way to handle audio from multiple sources in SmartHopper components and AI interactions. It parallels the existing VersatileImage system and supports document extraction workflows.

**You should read this if you:**

- Need to handle audio inputs from files, URLs, or base64 data in your components
- Want to understand how audio flows into AI interactions via AIInputPayload
- Are building Grasshopper components that process or generate audio
- Need to preserve document extraction metadata with audio sources

---

## End-User Guide

### Overview

- Files: `src/SmartHopper.Core/Types/VersatileAudio.cs`, `GH_VersatileAudio.cs`, `VersatileAudioParameter.cs`, `VersatileAudioCodec.cs`
- Parallel to the already-documented `VersatileImage` system
- Enables flexible audio input handling across components and AI interactions

### Core Concepts

#### VersatileAudioKind Enum

Specifies the source kind of audio:

- `LocalFile` — Local file path (e.g., `C:\audio.mp3`)
- `Url` — HTTP(S) URL (e.g., `https://example.com/audio.mp3`)
- `Base64` — Base64-encoded audio data
- `DataUri` — Data URI with embedded base64 audio (e.g., `data:audio/mp3;base64,...`)

#### VersatileAudio Class

**Immutable adapter** wrapping heterogeneous audio inputs with automatic kind detection and metadata support.

##### Properties

- `Kind` (VersatileAudioKind) — the source kind (auto-detected or explicit)
- `RawValue` (string) — the raw value (path, URL, base64, or data-URI string)
- `MimeType` (string) — the MIME type of the audio (e.g., "audio/mp3", "audio/wav", "audio/mpeg")
- `Id` (string, nullable) — unique identifier for this audio within a document (e.g., "audio-1", "audio-2"); only populated when extracted from a document
- `Context` (string, nullable) — contextual description of where the audio was found (e.g., "Page 3", "Slide 5"); only populated when extracted from a document
- `PageOrSlide` (int) — page or slide number where the audio was found (1-based); 0 means unknown or not applicable; only populated when extracted from a document
- `SourceDocument` (string, nullable) — source document path or identifier; only populated when extracted from a document

##### Factory Methods

###### String-Based Construction

- `FromString(string input)` — creates from a string with auto-detection of source kind
  - Detects URLs (http://, https://)
  - Detects data-URIs (data:audio/...;base64,...)
  - Detects base64 (valid base64 string)
  - Falls back to local file path
  - Throws `ArgumentException` if input is null or whitespace

###### Document Extraction

- `FromExtractedDocument(string base64Data, string mimeType, string id, string context, int pageOrSlide, string sourceDocument)` — creates from extracted document data
  - Use this when creating audio sources from PDF/DOCX/PPTX file extraction
  - Automatically sets `Kind` to `Base64`
  - Stores document metadata for context preservation

###### Deserialization

- `FromDeserialized(VersatileAudioKind kind, string rawValue, string mimeType, string id, string context, int pageOrSlide, string sourceDocument)` — reconstructs from persisted data
  - Used by `VersatileAudioCodec` to restore saved outputs
  - Enables round-trip serialization/deserialization

##### Conversion Methods

- `ToByteArray()` — converts audio source to byte array
  - For `LocalFile`: loads from disk
  - For `Url`: downloads via HTTP(S)
  - For `Base64`: decodes base64 string
  - For `DataUri`: extracts and decodes base64 from data-URI
  - Throws `InvalidOperationException` if conversion fails

- `ToDataUri()` — converts to data-URI format (e.g., `data:audio/mp3;base64,...`)
  - Useful for embedding in HTML or JSON
  - Automatically encodes to base64 if needed

- `ToInteraction()` — converts to `AIInteractionAudio` for AI processing
  - Sets `Data` (byte array) or `FilePath` depending on source kind
  - Preserves `MimeType`
  - Preserves document metadata if available

##### Utility Methods

- `GetAudioSize()` — returns the size of the audio data in bytes
  - For `LocalFile`: file size from disk
  - For `Url`: downloads and measures
  - For `Base64`/`DataUri`: decodes and measures
  - Returns 0 if size cannot be determined

- `GetMimeTypeFromSource(string source, VersatileAudioKind kind)` — detects MIME type from source
  - Infers from file extension for local files
  - Extracts from data-URI if present
  - Falls back to "audio/mpeg" as default

- `DetectSourceKind(string input)` — auto-detects source kind from input string
  - Checks for URL patterns (http://, https://)
  - Checks for data-URI pattern (data:...;base64,...)
  - Checks for valid base64
  - Falls back to local file path

- `ToString()` — returns a formatted string representation
  - Example: `VersatileAudio (Url): https://example.com/audio.mp3`

### Grasshopper Integration

#### GH_VersatileAudio (Goo Wrapper)

Grasshopper goo wrapper for `VersatileAudio`.

- **Type Name**: "VersatileAudio"
- **Type Description**: "A versatile audio type accepting file paths, URLs, base64, data-URIs, and extracted document audio with metadata."
- **Validity**: Checked via `IsValid` property (true if wrapped value is not null)
- **Casting**: Supports casting from:
  - `GH_VersatileAudio` (identity)
  - `VersatileAudio` (direct wrapping)
  - `string` (via `FromString()`)
  - `GH_String` (via `FromString()`)
- **Duplication**: Full deep copy via `Duplicate()`

#### VersatileAudioParameter

Grasshopper parameter type for `GH_VersatileAudio`.

- **Parameter Name**: "VersatileAudio"
- **Abbreviation**: "VAudio"
- **Exposure**: Hidden (internal use)
- **Access**: Item (single values)
- **GUID**: `F478886E-B6B2-4340-8013-DE56284CBCA0`
- **Casting**: Automatically converts compatible inputs to `GH_VersatileAudio`

#### VersatileAudioCodec

Codec for serializing/deserializing `GH_VersatileAudio` in Grasshopper documents.

- **Type Hint**: "GH_VersatileAudio"
- **Encoding**: Serializes to compact JSON with keys:
  - `k` — kind (enum name)
  - `v` — raw value
  - `m` — MIME type
  - `i` — ID
  - `c` — context
  - `p` — page/slide number
  - `s` — source document
- **Decoding**: Reconstructs from JSON, with fallback to `GH_String` on error
- **Persistence**: Enables round-trip save/load of audio data in definitions

### Supported Audio Formats

Automatically detected by file extension:

- `.mp3` — MPEG audio
- `.wav` — WAV audio
- `.m4a` — MPEG-4 audio
- `.aac` — Advanced Audio Coding
- `.flac` — Free Lossless Audio Codec
- `.ogg` — Ogg Vorbis
- `.wma` — Windows Media Audio
- `.opus` — Opus audio

---

## Developer Reference

### Creating from String

```csharp
// Auto-detects source kind
var audio = VersatileAudio.FromString("path/to/audio.mp3");
var audio = VersatileAudio.FromString("https://example.com/audio.mp3");
var audio = VersatileAudio.FromString("data:audio/mp3;base64,SUQzBAA...");
```

### Creating from Document Extraction

```csharp
var audio = VersatileAudio.FromExtractedDocument(
    base64Data: "SUQzBAA...",
    mimeType: "audio/mpeg",
    id: "audio-1",
    context: "Page 3",
    pageOrSlide: 3,
    sourceDocument: "document.pdf");
```

### Converting to AI Interaction

```csharp
var audio = VersatileAudio.FromString("path/to/audio.mp3");
var interaction = audio.ToInteraction();
// interaction.Data contains audio bytes
// interaction.MimeType is "audio/mpeg"
```

### Grasshopper Component Usage

```csharp
public class Audio2AIComponent : AIInputAdapterBase
{
    protected override void SolveInstance(IGH_DataAccess DA)
    {
        GH_VersatileAudio ghAudio = null;
        DA.GetData(0, ref ghAudio);
        
        if (ghAudio?.Value == null)
            return;
        
        var audio = ghAudio.Value;
        var interaction = audio.ToInteraction();
        var payload = AIInputPayload.FromAudio(interaction);
        
        DA.SetData(0, new GH_AIInputPayload(payload));
    }
}
```

---

## Architecture & Design

### Design Principles

1. **Flexibility**: Accepts multiple input formats (file, URL, base64, data-URI)
2. **Auto-Detection**: Automatically determines source kind from input
3. **Metadata Preservation**: Maintains document extraction context
4. **Lazy Loading**: Downloads/decodes only when `ToByteArray()` is called
5. **Immutability**: Once created, cannot be modified
6. **Serialization**: Full round-trip support via codec
7. **Type Safety**: Grasshopper goo wrapper enables type-safe parameter passing

### Integration Points

- **AIInputPayload**: Audio payloads carry `AIInteractionAudio` interactions
- **Audio2AIComponent**: Input adapter converting audio files to AI payloads
- **AI2SpeechComponent**: Output adapter generating audio from AI results
- **AudioViewerComponent**: Displays and plays audio with waveform visualization
- **File Converters**: Extract audio from documents (PDF, DOCX, PPTX)
