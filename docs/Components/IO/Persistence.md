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
- `SafeGooCodec`
  - `Encode(IGH_Goo) -> string`
  - `TryDecode(string, out IGH_Goo goo, out string warning) -> bool`

Source:

- `src/SmartHopper.Core/IO/SafeStructureCodec.cs`
- `src/SmartHopper.Core/IO/SafeGooCodec.cs`

## Supported item types

Handled explicitly by `SafeGooCodec`:

- `GH_String` — `GH_String|{value}`
- `GH_Number` — `GH_Number|{value}` (InvariantCulture)
- `GH_Integer` — `GH_Integer|{value}` (InvariantCulture)
- `GH_Boolean` — `GH_Boolean|1` or `0` (also accepts `true`/`false` on decode)

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

To persist a new GH type safely:

1. Add a new `typeHint` case in `SafeGooCodec.Encode(IGH_Goo)` producing a canonical string representation.
2. Add the corresponding case in `SafeGooCodec.TryDecode(...)` to parse the string back to an `IGH_Goo` instance.
3. Use `CultureInfo.InvariantCulture` for numeric formats and keep strings unescaped if possible; if escaping is needed, keep the prefix format `typeHint|payload` stable and implement reversible escaping.
4. Maintain backward compatibility: new readers must still decode old payloads; never remove existing type hints.
