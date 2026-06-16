# Miscellaneous Components

Miscellaneous utility components for AI metrics and diagnostics.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/Misc/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

The Miscellaneous components category collects small but important utility components that do not fit into other domains. Currently, it houses the metrics deconstruction tool, which is essential for understanding the performance and cost characteristics of AI-powered workflows.

**You should read this if:**

- You need to break down AI call metrics into individual fields for reporting or analysis
- You are monitoring token usage, model selection, or execution time across your SmartHopper graphs
- You are building dashboards or logging systems for AI workflow diagnostics

---

## End-User Guide

### Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `DeconstructMetricsComponent` | Deconstruct Metrics | Diagnostics | Deconstructs AI metrics into individual components (tokens, time, provider, model) |

---

## Developer Reference

The `DeconstructMetricsComponent` exposes AI call metadata as individual outputs. The following examples show how to use it programmatically and how to consume its outputs in custom C# scripts.

### Deconstructing Metrics Programmatically

```csharp
using SmartHopper.Components.Misc;
using SmartHopper.Core.AI.Models;

// Create the deconstructor and provide a metrics object
var deconstructor = new DeconstructMetricsComponent();
var metrics = new AIMetrics
{
    Provider = "OpenAI",
    Model = "gpt-4o",
    InputTokens = 1200,
    OutputTokens = 450,
    DurationMs = 2300
};

deconstructor.Params.Input[0].AddVolatileDataListAtPath(
    new GH_Path(0), metrics);
deconstructor.ExpireSolution(true);

// Read individual outputs
string provider = deconstructor.Params.Output[0]
    .VolatileData.get_FirstItem(true).Value as string;
int inputTokens = (int)deconstructor.Params.Output[2]
    .VolatileData.get_FirstItem(true).Value;

```

### Aggregating Metrics Across Multiple Runs

```csharp
using SmartHopper.Components.Misc;
using System.Linq;

// Suppose you have collected metrics from multiple AI components
var deconstructor = new DeconstructMetricsComponent();
int totalTokens = 0;

foreach (var metric in collectedMetrics)
{
    deconstructor.Params.Input[0].ClearData();
    deconstructor.Params.Input[0].AddVolatileDataListAtPath(
        new GH_Path(0), metric);
    deconstructor.ExpireSolution(true);

    int tokens = (int)deconstructor.Params.Output[4]
        .VolatileData.get_FirstItem(true).Value;
    totalTokens += tokens;
}

Console.WriteLine($"Total tokens consumed: {totalTokens}");

```

---

## Architecture & Design

- Miscellaneous components provide utility functions for AI call analysis
- Metrics deconstruction enables detailed performance monitoring
- Used for debugging and optimization of AI workflows

