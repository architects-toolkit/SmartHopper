# Persistence (V2)

Safe, versioned persistence of component outputs used by `StatefulComponentBase`.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/IO/Persistence.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This documentation explains how SmartHopper persists component output data trees reliably across Grasshopper save/load cycles without relying on GH's internal type cache. Understanding the persistence system is essential if you are building custom components that need to maintain state or if you are extending the framework with new data types.

**You should read this if:**

- You are developing custom components that inherit from `StatefulComponentBase`
- You need to add support for new Grasshopper data types in the persistence layer
- You are debugging save/load issues with component outputs
- You want to understand the internal encoding and versioning strategy

---

## End-User Guide

### Purpose

- Persist output data trees reliably across save/load without relying on GH's internal type cache.
- Be forward-compatible via explicit versioning and canonical string payloads.
- Never throw during read; collect non-fatal warnings for the caller to surface.

### Where it is used

- `StatefulComponentBase.Write(GH_IWriter)` writes current output trees using `GHPersistenceService.WriteOutputsV2()`.
- `StatefulComponentBase.Read(GH_IReader)` reads output trees using `GHPersistenceService.ReadOutputsV2()` and restores them to the component's outputs.

See: `src/SmartHopper.Core/ComponentBase/StatefulComponentBase.cs`.

### Versioning and keys

- Version key: `PO.Version` (see `PersistenceConstants.VersionKey`).
- Current version: `2` (`PersistenceConstants.CurrentVersion`).
- Per-output storage key: `PO2_{paramGuidN}` (`PersistenceConstants.KeyForOutputV2(Guid)`). One entry per output parameter `InstanceGuid`.
- Legacy restore flag: `EnableLegacyRestore = false` by default. Do not enable unless performing a controlled migration.

Source: `src/SmartHopper.Core/IO/PersistenceConstants.cs`.

### Data format (V2)

- Each output is stored as a binary-serialized `GH_LooseChunk` containing a `GH_Structure<GH_String>`.
- Each item string is a canonical payload: `typeHint|serialized`. See `SafeGooCodec`.
- Trees preserve paths, order, and counts.

Source:

- `src/SmartHopper.Core/IO/GHPersistenceService.cs`
- `src/SmartHopper.Core/IO/SafeStructureCodec.cs`

### Supported item types

Handled explicitly by `SafeGooCodec`:

- `GH_String` — `GH_String|{value}`
- `GH_Number` — `GH_Number|{value}` (InvariantCulture)
- `GH_Integer` — `GH_Integer|{value}` (InvariantCulture)
- `GH_Boolean` — `GH_Boolean|1` or `0` (also accepts `true`/`false` on decode)
- `GH_VersatileImage` — `GH_VersatileImage|{json}` where JSON contains:
  - `k` — `VersatileImageKind` (`Bitmap`, `LocalFile`, `Url`, `Base64`, `DataUri`)
  - `v` — `RawValue` (path, URL, base64, or data-URI; null when `k == Bitmap`)
  - `b` — Base64-encoded PNG bitmap (only present when `k == Bitmap`)
  - `i`, `c`, `p`, `s`, `m` — `Id`, `Context`, `PageOrSlide`, `SourceDocument`, `MimeType`
- `GH_VersatileAudio` — `GH_VersatileAudio|{json}` where JSON contains:
  - `k` — `VersatileAudioKind` (`LocalFile`, `Url`, `Base64`, `DataUri`)
  - `v` — `RawValue` (path, URL, base64, or data-URI)
  - `i`, `c`, `p`, `s`, `m` — `Id`, `Context`, `PageOrSlide`, `SourceDocument`, `MimeType`
- `GH_AIInputPayload` — `GH_AIInputPayload|{json}` where JSON contains:
  - `capability` — `AICapability` enum value (int)
  - `payloadType` — `AIInputPayloadType` enum value (int)
  - `hint` — MIME type or format hint string
  - `interactions` — Array of interaction objects, each with:
    - `$type` — discriminator (`text`, `image`, `audio`, `toolCall`, `toolResult`, `runtimeMessage`)
    - Common fields: `turnId`, `time`, `agent`
    - Type-specific fields (e.g., `content` for text, `imageUrl`/`imageData` for image, etc.)

Fallbacks and warnings:

- Unknown `typeHint` decodes to `GH_String` with a warning.
- Parse failures (number/int/bool) decode to `GH_String` with a warning.
- Any exception during decode results in `GH_String` with a warning.

### Service behavior

- `GHPersistenceService.WriteOutputsV2(...)`
  - Sets `PO.Version = 2`.
  - For each output GUID: encode tree, write to a `GH_LooseChunk`, store as byte array under `PO2_{guid}`.
  - Returns `true` on success; logs exceptions in DEBUG, returns `false` on failure.
- `GHPersistenceService.ReadOutputsV2(...)`
  - Checks `PO.Version == 2`; otherwise, returns empty result.
  - For each output GUID: reads byte array, reconstructs `GH_LooseChunk`, reads string tree, decodes to goo tree.
  - Never throws; logs per-item decode warnings in DEBUG, skips corrupt entries, returns what could be restored.

Source: `src/SmartHopper.Core/IO/GHPersistenceService.cs`.

---

## Developer Reference

### Codecs

- `SafeStructureCodec`
  - `EncodeTree(GH_Structure<IGH_Goo>) -> GH_Structure<GH_String>`
  - `DecodeTree(GH_Structure<GH_String>, out List<string> warnings) -> GH_Structure<IGH_Goo>`
- `SafeGooCodec` (thin facade over `GooCodecRegistry`)
  - `Encode(IGH_Goo) -> string`
  - `TryDecode(string, out IGH_Goo goo, out string warning) -> bool`
- `GooCodecRegistry`
  - Maintains a list of `IGooCodec` implementations checked in priority order.
  - Built-in codecs: `StringCodec`, `NumberCodec`, `IntegerCodec`, `BooleanCodec`, `VersatileImageCodec`, `VersatileAudioCodec`, `AIInputPayloadCodec`.
  - Custom codecs can be registered at runtime via `GooCodecRegistry.Register(IGooCodec)`.

Source:

- `src/SmartHopper.Core/IO/SafeStructureCodec.cs`
- `src/SmartHopper.Core/IO/SafeGooCodec.cs`
- `src/SmartHopper.Core/IO/Codecs/GooCodecRegistry.cs`
- `src/SmartHopper.Core/IO/Codecs/IGooCodec.cs`

### Extending to new types

To persist a new GH type safely, implement `IGooCodec` and register it with `GooCodecRegistry`:

```csharp
public class MyCustomCodec : IGooCodec
{
    public string TypeHint => "GH_MyCustom";
    public int Priority => 0;

    public bool CanEncode(IGH_Goo goo) => goo is GH_MyCustom;

    public string Encode(IGH_Goo goo)
    {
        var custom = (GH_MyCustom)goo;
        return $"{custom.Value}"; // payload without prefix
    }

    public bool TryDecode(string data, out IGH_Goo goo, out string warning)
    {
        warning = null;
        goo = new GH_MyCustom(data);
        return true;
    }
}

```

Then register at plugin startup (or before first save/load):

```csharp
GooCodecRegistry.Register(new MyCustomCodec());

```

Guidelines:

1. Implement `IGooCodec` with a unique `TypeHint` and a `CanEncode` check for your goo type.
2. Keep the payload format stable and reversible. Use `CultureInfo.InvariantCulture` for numeric formats.
3. Maintain backward compatibility: new readers must still decode old payloads; never remove existing type hints.
4. On decode failure, return a `GH_String` with a warning — never throw.

---

## Architecture & Design

The persistence system is designed around forward-compatible encoding and safe decoding. Each output parameter is stored under a versioned key (`PO2_{guid}`) inside a binary `GH_LooseChunk`. The V2 format encodes every data tree item as a canonical string with a type hint prefix, enabling the decoder to route each item to the correct codec without relying on Grasshopper's runtime type information.

The codec registry pattern allows the system to support built-in types while remaining open to extension. `SafeStructureCodec` handles the tree-level encoding and decoding, while `SafeGooCodec` handles individual item serialization. During read operations, the system never throws; instead, it collects warnings and falls back to `GH_String` for any items that cannot be decoded. This ensures that a single corrupt item does not prevent the entire output from being restored.

Versioning is explicit: the `PO.Version` key determines which read path is taken. If the version does not match `CurrentVersion`, the read returns an empty result rather than attempting partial or risky decoding. Legacy restore is disabled by default to prevent accidental migration of old data formats.

