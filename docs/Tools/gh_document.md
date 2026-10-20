# Document Tools

AI tools to list, switch, create, open, read and save the Grasshopper documents open in Rhino.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/AITools/gh_document.cs`, `src/SmartHopper.Core.Grasshopper/AITools/gh_document_save.cs`, `src/SmartHopper.Core.Grasshopper/Utils/Canvas/GhDocumentAccess.cs` |
| **Since Version** | 2.0.0-dev |
| **Last Updated** | 2026-10-20 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

**You should read this if you:**

- Want the AI to see or work with more than the active Grasshopper document
- Need to know how documents are identified across tools
- Are reviewing file-access boundaries of AI tools

---

## End-User Guide

| Tool | What it does |
| --- | --- |
| `gh_document_list` | Lists open documents: `documentId`, `name`, `filePath`, `isActive`, `isModified`, `objectCount`, plus `activeDocumentId`. |
| `gh_document_switch` | Shows another open document in the canvas (`documentId`). Category `ViewControl`. |
| `gh_document_new` | Creates a blank document and activates it. |
| `gh_document_open` | Opens an absolute `.gh`/`.ghx` path and activates it; if already open, activates the open copy (`alreadyOpen: true`). |
| `gh_document_save` | Saves the active document, or `documentId`; optional absolute `.gh`/`.ghx` `filePath` (required for unsaved documents). |
| `gh_get` (`documentId`) | Reads a non-active document without switching the canvas. `viewportOnly` is ignored with a warning for non-active documents. |

All other canvas tools keep acting on the active document; switch first to edit another one.

---

## Developer Reference

- `documentId` is `GH_Document.DocumentID` (GUID), stable while the document is open — the same id used by WebChat document tracking.
- `GhDocumentAccess` wraps `Instances.DocumentServer` / `Instances.ActiveCanvas` (list, find by id or path, activate, `new GH_Document()` + `AddDocument`, `GH_DocumentIO.Open`) and marshals to the Rhino UI thread via `CanvasAccess.RunOnUiThread`.
- `CanvasAccess.SaveDocument(GH_Document, string)` saves a specific document; the parameterless overload keeps saving the active one.
- Surfaces: `gh_document_switch`, `gh_document_new` and `gh_document_open` are exposed to Chat, Direct and MCP only (not Batch components). MCP gates `gh_document_switch` with `AllowViewControl`.

---

## Architecture & Design

- **File boundary**: `GrasshopperFilePath.TryNormalize` accepts only absolute `.gh`/`.ghx` paths (open targets must exist), so the tools cannot read or write arbitrary file types.
- **No canvas mutation**: these tools change which document is active or create/open documents, but never modify an existing document's objects, so they do not go through canvas change review or undo coordination.
- **Read without switching**: `gh_get` uses `CanvasSelector.From(GH_Document)` for non-active documents, so the AI can inspect other files while the user keeps working in the active one.
