# Persistence (V2)

Safe, versioned persistence of component outputs used by `StatefulComponentBase`.

## Purpose

- Persist output data trees reliably across save/load without relying on GH's internal type cache.
- Be forward-compatible via explicit versioning and canonical string payloads.
- Never throw during read; collect non-fatal warnings for the caller to surface.

## Where it is used

- `StatefulComponentBase.Write(GH_IWriter)` writes current output trees using `GHPersistenceService.WriteOutputsV2()`.
- `StatefulComponentBase.Read(GH_IReader)` reads output trees using `GHPersistenceService.ReadOutputsV2()` and restores them to the component's outputs.

See: `src/SmartHopper.Core/ComponentBase/StatefulComponentBaseV2.cs`.

## Versioning and keys

- Version key: `PO.Version` (see `PersistenceConstants.VersionKey`).
- Current version: `2` (`PersistenceConstants.CurrentVersion`).
- Per-output storage key: `PO2_{paramGuidN}` (`PersistenceConstants.KeyForOutputV2(Guid)`). One entry per output parameter `InstanceGuid`.
- Legacy restore flag: `EnableLegacyRestore = false` by default. Do not enable unless performing a controlled migration.

Source: `src/SmartHopper.Core/IO/PersistenceConstants.cs`.

## Data format (V2)

- Each output is stored as a binary-serialized `GH_LooseChunk` containing a `GH_Structure<GH_String>`.
- Each item string is a canonical payload: `typeHint|serialized`. See `SafeGooCodec`.
- Trees preserve paths, order, and counts.

Source:

- `src/SmartHopper.Core/IO/GHPersistenceService.cs`
- `src/SmartHopper.Core/IO/SafeStructureCodec.cs`

## Codecs

- `SafeStructureCodec`
  - `EncodeTree(GH_Structure<IGH_Goo>) -> GH_Structure<GH_String>`
  - `DecodeTree(GH_Structure<GH_String>, out List<string> warnings) -> GH_Structure<IGH_Goo>`
- `SafeGooCodec` (thin facade over `GooCodecRegistry`)
  - `Encode(IGH_Goo) -> string`
  - `TryDecode(string, out IGH_Goo goo, out string warning) -> bool`
- `GooCodecRegistry`
  - Maintains a list of `IGooCodec` implementations checked in priority order.
  - Built-in codecs: `StringCodec`, `NumberCodec`, `IntegerCodec`, `BooleanCodec`, `VersatileImageCodec`, `VersatileAudioCodec`.
  - Custom codecs can be registered at runtime via `GooCodecRegistry.Register(IGooCodec)`.

Source:

- `src/SmartHopper.Core/IO/SafeStructureCodec.cs`
- `src/SmartHopper.Core/IO/SafeGooCodec.cs`
- `src/SmartHopper.Core/IO/Codecs/GooCodecRegistry.cs`
- `src/SmartHopper.Core/IO/Codecs/IGooCodec.cs`

## Supported item types

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

Fallbacks and warnings:

- Unknown `typeHint` decodes to `GH_String` with a warning.
- Parse failures (number/int/bool) decode to `GH_String` with a warning.
- Any exception during decode results in `GH_String` with a warning.

## Service behavior

- `GHPersistenceService.WriteOutputsV2(...)`
  - Sets `PO.Version = 2`.
  - For each output GUID: encode tree, write to a `GH_LooseChunk`, store as byte array under `PO2_{guid}`.
  - Returns `true` on success; logs exceptions in DEBUG, returns `false` on failure.
- `GHPersistenceService.ReadOutputsV2(...)`
  - Checks `PO.Version == 2`; otherwise, returns empty result.
  - For each output GUID: reads byte array, reconstructs `GH_LooseChunk`, reads string tree, decodes to goo tree.
  - Never throws; logs per-item decode warnings in DEBUG, skips corrupt entries, returns what could be restored.

Source: `src/SmartHopper.Core/IO/GHPersistenceService.cs`.

## Extending to new types

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
