# BatchSentinel

`src/SmartHopper.Core/ComponentBase/BatchSentinel.cs`

Static helper that owns the `##SH_BATCH:{customId}##` placeholder protocol used by [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) when batch mode is active. Single source of truth for the format — no other class should hand-roll the prefix/suffix.

## API

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

## Why a sentinel?

When a component runs in batch mode each per-branch AI call must return *something* immediately so the data tree retains its shape. The helper wraps the queued item's `customId` in a recognisable string; later, after the provider finishes the batch, `ProcessBatchResults<T>` walks the tree, recognises the sentinel via `TryExtract` and replaces it with the decoded result. Items that are not sentinels are passed through unchanged so a single tree can mix batched and non-batched values.
