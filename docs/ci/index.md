# CI and release automation

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `.github/workflows/` |
| **Since Version** | 2.0.0 |
| **Last Updated** | 2026-08-29 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

This section provides the entry point for understanding SmartHopper's CI, branching, and release
automation.

---

## End-User Guide

Use the [branching and releases guide](./branching-and-releases.md) when preparing or publishing a
release.

---

## Developer Reference

The integration branch is explicit in automation:

```csharp
var integrationBranch = "main";
```

Release metadata is read from the solution:

```csharp
var releaseVersion = solutionVersion;
```

---

## Architecture & Design

The CI system keeps integration on `main`, uses release branches for stabilization, and uses
immutable tags as the source of truth for release artifacts.

---

- [Branching and releases](./branching-and-releases.md) — authoritative branch, version, tag, and release workflow contract.
