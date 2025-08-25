# IO

Safe, versioned persistence for Grasshopper component outputs.

## Purpose

- Provide robust read/write of component outputs across document saves/loads.
- Avoid GH internal type lookups on load to reduce fragility and crashes.
- Support forward-compatible schema evolution via explicit versioning.

## Contents

- [Persistence](./Persistence.md) — schema/versioning, codecs, supported types, and extension guidance.

## Code locations

- `src/SmartHopper.Core/IO/`
  - `GHPersistenceService` — Grasshopper implementation of the persistence service
  - `IPersistenceService` — contract for persistence
  - `PersistenceConstants` — version keys and per-output key builder
  - `SafeGooCodec` — item-level encode/decode to canonical strings
  - `SafeStructureCodec` — tree-level encode/decode using `SafeGooCodec`

## Related

- Base integration in `StatefulAsyncComponentBase` read/write paths.
- See [StatefulAsyncComponentBase](../ComponentBase/StatefulAsyncComponentBase.md) for where persistence is invoked.
