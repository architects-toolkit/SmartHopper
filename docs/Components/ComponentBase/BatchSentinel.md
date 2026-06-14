# BatchSentinel

Static helper that owns the `##SH_BATCH:{customId}##` placeholder protocol used by [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) when batch mode is active. Single source of truth for the format — no other class should hand-roll the prefix/suffix.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/BatchSentinel.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Batch sentinels are the mechanism that allows SmartHopper to preserve data-tree shape while AI requests are queued for asynchronous batch processing. This is key to understanding how batch results are later mapped back into Grasshopper structures.

**You should read this if you:**

- Need to understand how batch mode placeholders work
- Want to implement custom batch processing logic
- Are debugging batch result replacement in a data tree

---

## End-User Guide

### API

```csharp
public static class BatchSentinel
{
    public const string Prefix = "##SH_BATCH:";
    public const string Suffix = "##";

    public static string Wrap(string customId);
    public static bool   Is(string value);
    public static bool   TryExtract(string value, out string customId);
}

```

### Why a sentinel?

When a component runs in batch mode each per-branch AI call must return *something* immediately so the data tree retains its shape. The helper wraps the queued item's `customId` in a recognisable string; later, after the provider finishes the batch, `ProcessBatchResults<T>` walks the tree, recognises the sentinel via `TryExtract` and replaces it with the decoded result. Items that are not sentinels are passed through unchanged so a single tree can mix batched and non-batched values.

---

## Developer Reference

### Detecting and extracting a sentinel

```csharp
var value = someDataTreeItem.Value;
if (BatchSentinel.Is(value))
{
    if (BatchSentinel.TryExtract(value, out var customId))
    {
        // Use customId to look up the corresponding batch result
        var result = batchResults[customId];
        // Replace the sentinel with the real value
        treeItem.Value = result;
    }
}

```

### Wrapping a custom ID before submission

```csharp
string customId = Guid.NewGuid().ToString();
string placeholder = BatchSentinel.Wrap(customId);
// Store placeholder in the data tree while the real result is queued

```

---

## Architecture & Design

The sentinel protocol ensures that:

1. Grasshopper's data tree structure remains intact during the batch waiting period.
2. Each queued item can be uniquely identified by its `customId`.
3. Mixed trees containing both batched and non-batched values are handled seamlessly.
4. The replacement process is deterministic and fully reversible via `TryExtract`.

### Related

- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
