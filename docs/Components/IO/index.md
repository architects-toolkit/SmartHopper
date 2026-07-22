# IO

Safe, versioned persistence for Grasshopper component outputs.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/IO/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

The IO subsystem ensures that component outputs survive document saves and loads without relying on fragile Grasshopper internal type lookups. It uses explicit versioning and canonical encoding to keep your data safe across SmartHopper updates.

**You should read this if you:**

- Need to persist component outputs across Grasshopper document sessions
- Want to understand how SmartHopper avoids type-lookup crashes on file load
- Are implementing a new component that needs forward-compatible data storage

---

## End-User Guide

### Purpose

- Provide robust read/write of component outputs across document saves/loads.
- Avoid GH internal type lookups on load to reduce fragility and crashes.
- Support forward-compatible schema evolution via explicit versioning.

### Contents

- [Persistence](./Persistence.md) — schema/versioning, codecs, supported types, and extension guidance.

---

## Developer Reference

### Persisting Component Outputs

Use the persistence service inside a stateful component's write hook to save outputs:

```csharp
protected override void Write(GH_IWriter writer)
{
    base.Write(writer);

    var persistence = new GHPersistenceService();
    persistence.WriteOutputTree(writer, "MyOutput", myOutputTree, version: 2);
}

```

### Restoring Component Outputs

Load persisted data safely by providing a fallback when the schema version does not match:

```csharp
protected override void Read(GH_IReader reader)
{
    base.Read(reader);

    var persistence = new GHPersistenceService();
    myOutputTree = persistence.ReadOutputTree(reader, "MyOutput", expectedVersion: 2);

    if (myOutputTree == null)
    {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not restore persisted output.");
    }
}

```

---

## Architecture & Design

- `src/SmartHopper.Core/IO/`
  - `GHPersistenceService` — Grasshopper implementation of the persistence service
  - `IPersistenceService` — contract for persistence
  - `PersistenceConstants` — version keys and per-output key builder
  - `SafeGooCodec` — item-level encode/decode to canonical strings
  - `SafeStructureCodec` — tree-level encode/decode using `SafeGooCodec`

- Base integration in `StatefulComponentBase` read/write paths.
- See `StatefulComponentBase` (in `src/SmartHopper.Core/ComponentBase/StatefulComponentBase.cs`) for where persistence is invoked.
