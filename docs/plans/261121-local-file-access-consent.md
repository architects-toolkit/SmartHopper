<!-- docs-validation: ignore -->

# Plan: unified local-file access consent

**Status:** proposal — not yet implemented
**Date:** 2026-11-21
**Target:** `architects-toolkit/SmartHopper` → `main`

**Problem:** AI tools can read, write, and activate local files chosen by the model
with no user pre-authorization. A prompt-injected model can overwrite project
files, run script components from a crafted `.gh`, or copy private files into the
conversation. Web retrieval already has a consent model
(`ExternalSourceAccess`); local file access has none, and the in-flight fix PRs
each invent a different mechanism.

**Goal:** one centralized gate for model-initiated local file access that works
the same way web retrieval approval works — canonical target, scope-scoped
one-call grants, persisted "always allow" permissions, a single prompt, fail
closed, revocation in Settings > Security.

---

## 1. Finding inventory (security scan `scan-5af98132580543a0a2c2d1b59dd44be9`, 2026-11-20)

| # | Finding | Severity | Local-file related | Status |
|---|---------|----------|--------------------|--------|
| 1 | `model-verification.yml` PowerShell injection via issue body | Critical | No (CI) | PR [#965](https://github.com/architects-toolkit/SmartHopper/pull/965) |
| 2 | `gh_document_open` opens/runs any `.gh` on disk without consent | Medium | Execute | PR [#966](https://github.com/architects-toolkit/SmartHopper/pull/966) |
| 3 | `gh_document_save` overwrites any `.gh`/`.ghx` without consent | Medium | Write | PR [#968](https://github.com/architects-toolkit/SmartHopper/pull/968) |
| 4 | Screenshot tools write PNG to any `savePath` | Medium | Write | PR [#967](https://github.com/architects-toolkit/SmartHopper/pull/967) |
| 5 | `file2md` reads any local file without asking | Medium | Read | No PR |
| 6 | `img2text` reads any local file for the vision model | Low | Read | No PR |
| 7 | OpenDocument converter zip bomb | Low | Read-adjacent (resource exhaustion, not authorization) | No PR |
| 8 | Audio Viewer Play shell-opens any existing file | Low | Execute (component-driven, not tool-driven) | No PR |

Findings 2–6 share one root cause: **no centralized authorization boundary
between model-supplied paths and the filesystem.** Each remediation so far
patches one tool with its own mechanism.

### Additional file-access surface (not flagged but same shape)

- `gh_put`: string arguments that are existing `.ghjson` paths are read from
  disk (`gh_put.cs:582-587`).
- `gh_batch_import_results`: reads model-supplied `filePaths` after the canvas
  review; the review covers placement, not the file read (`gh_batch_import_results.cs:155-175`).
- `_rhino_read_3dm` / `_rhino_get_geometry_3dm`: disabled tools reading `.3dm`
  files; must be gated before enabling (finding 5's recommendation).
- `gh_document_list`: returns absolute `filePath` of open documents — a recon
  channel that hands the model real target paths (low; noted for awareness).
- Trusted path to preserve: `WebChatDialog.PreparePdfAttachmentAsync` calls
  `file2md` on a user-attached temp snapshot — user-initiated, must not prompt.

## 2. Current mechanisms (why they diverge)

| Mechanism | Used by | Decisions | Persistence | Prompt |
|-----------|---------|-----------|-------------|--------|
| `ExternalSourceAccess` + `ExternalSourceAllowlist` + `ExternalSourcePrompt` | web retrieval (`ExternalDataHttpClient`, image materializer, URL converter) | Deny / AllowOnce / AlwaysAllow / NoResponse | `external-source-permissions.json`, exact origins, atomic writes, live re-read | Modal Eto dialog, 120 s timeout → NoResponse |
| `ConsentGate` + `IConsentProposal`/`IConsentPresenter` + `MutatingOperationExecutor` | canvas mutations, plan approval | Approve/Partial/Reject/Cancel/Unavailable, single-use | none | presenter per kind (WebChat cards, Eto dialogs) |
| `CanvasChangeReviewService` | mutating canvas tools (diff review) | per-item accept | none | review dialog + canvas overlay |

The three in-flight PRs pick different mechanisms:

- **#966** adds `OpenDocumentConsent` — a bespoke `IConsentProposal` + Eto
  presenter, one-shot, no persistence.
- **#968** routes save through `CanvasChangeReviewService` — the canvas-diff
  review stretched to show a path + overwrite row, one-shot.
- **#967** hard-sandboxes `savePath` to
  `%APPDATA%/Grasshopper/SmartHopper/Screenshots` — no consent at all.

None of them grants the user a revocable "always allow this folder" permission,
and none of them covers `file2md`/`img2text`/the other read paths. The fix is
one gate, not a fifth mechanism.

## 3. Design: `LocalFileAccess` — the file-system twin of `ExternalSourceAccess`

Mirror the web-retrieval gate one-for-one, in a new
`SmartHopper.Infrastructure.FileSystem` namespace (file access is not
networking, so a sibling to `Networking`, not a child). Types:

- **`LocalFileCapability`** — flags enum: `Read`, `Write`, `Execute`.
  `Execute` covers activating a document (runs its solution/scripts) and
  shell-opening a file. Grants are per `(target, capability)`: allowing the
  model to read `D:\Projects` never allows writing it.
- **`LocalFileAccessDecision`** — `Deny`, `AllowOnce`, `AlwaysAllow` (this exact
  file), `AlwaysAllowFolder` (containing directory subtree, same capability),
  `NoResponse`. The folder option is the file analogue of an origin grant:
  web grants cover every path on an origin; folder grants cover every file
  under a directory.
- **`LocalPath`** — canonicalization: `Path.GetFullPath`, rooted-path required,
  trailing separators trimmed, `.`/`..` resolved, `file://` URIs normalized to
  paths. Where the file or an ancestor exists, reparse points are resolved
  (`File.ResolveLinkTarget`) so a granted directory cannot be escaped through a
  junction/symlink, and a path reached through a link is keyed by its real
  target. Case-insensitive comparison on Windows and macOS.
- **`LocalFileAllowlist`** — persists grants to
  `%APPDATA%/Grasshopper/SmartHopper/local-file-permissions.json`, same file
  discipline as `ExternalSourceAllowlist` (atomic rename, cross-process write
  lease, re-read on every lookup, malformed = fail closed). Entry:
  `{ scope: file|directory, path, capabilities[], grantedUtc }`. Directory
  entries match descendants via separator-boundary containment, never raw
  `StartsWith`.
- **`LocalFileAccess`** — static gate: `RequireAsync(path, capability, ct)`,
  `BeginScope()` (AsyncLocal one-call grant map), `PromptAsync` host delegate,
  `GrantForCurrentScope(path, capability)` for seeding trusted
  (user-supplied) paths, `IsAwaitingConsent` for the canvas badge. Same
  serialized-prompt + 120 s `NoResponse` semantics as sources; no presenter →
  deny; cancelled/denied remembered in-scope so repeated injected calls fail
  fast.
- **`LocalFilePrompt`** (`Infrastructure.Dialogs`) — modal on the Rhino UI
  thread. Title names the operation ("Allow the AI to read / write / open this
  file?"), shows the canonical path and the calling tool, plus risk lines:
  *contents are sent to your AI provider* (Read), *overwrites the existing
  file / creates directories* (Write), *the file can run scripts on this
  machine* (Execute). Buttons: **Allow once**, **Always allow this file**,
  **Always allow this folder**, **Deny**. Denied-tier paths never reach the
  prompt — the gate refuses first.

### Sensitivity tiers

| Tier | Examples | Behaviour |
|------|----------|-----------|
| Normal | project dirs, Downloads, Documents | full decision set incl. folder grants |
| Denied | `.ssh`, `.aws`, `.azure`, `.kube`, `.gnupg`, browser credential stores, SmartHopper's own config dir (permission stores), `Windows\`, `Program Files*`, `/usr`, `/etc` | **hard deny, never prompts** — the chat cannot retrieve or write these paths at all; the gate refuses before any dialog |

### Pre-authorized roots (implicit, not persisted)

- `%APPDATA%/Grasshopper/SmartHopper/Screenshots` (Write) — keeps #967's
  export sandbox working without prompts.
- SmartHopper temp files (`%TEMP%/smarthopper-*`) — attachments and internal
  pipelines.
- A document's own current `FilePath` for `gh_document_save` with no
  `filePath` argument — saving in place is the user's own file's normal flow.

### WebChat / component / MCP semantics

- `ToolManager.ExecuteTool` wraps every tool call in
  `LocalFileAccess.BeginScope()` next to the existing
  `ExternalSourceAccess.BeginScope()` (`ToolManager.cs:167`) — one approval
  covers nested reads of the same target within one logical invocation.
- `McpServerOptions.BypassMutationsApproval` does **not** bypass file access —
  same rule as sources ("mutation bypass does not apply to source access").
- The WebChat attachment path seeds a scope grant
  (`GrantForCurrentScope`) instead of special-casing `file2md`.
- `ConsentBadgeOverlay` listens to `LocalFileAccess.IsAwaitingConsent` too —
  extract the awaiting counter into a tiny shared hub both gates report to.
- Settings > Security gains a "Local file permissions" section beside
  "External data sources": rows of canonical path + kind (file/folder) +
  capabilities, add/remove staged until Save, same row UX as origins.

## 4. Tool wiring

| Tool | Capability | Gate point |
|------|-----------|------------|
| `file2md` | Read | before `FileConverterRegistry.ConvertAsync`; drop raw-text fallback for model-initiated paths (user-supplied scope grants keep attachments working) |
| `img2text` | Read | inside `TryReadLocalImage`, plus image-extension check and size cap |
| `gh_put` (.ghjson path) | Read | before `File.ReadAllText` |
| `gh_batch_import_results` | Read | per `filePaths` entry |
| `_rhino_read_3dm`, `_rhino_get_geometry_3dm` | Read | gate before enabling the tools |
| `gh_document_save` | Write | explicit `filePath` argument; in-place save is pre-authorized. Replaces #968's review-overlay route with the shared prompt (which carries the overwrite flag) |
| `gh_document_open` | Execute | new file only — activating an already-open document needs no consent. Prompt warns scripts may run. Replaces #966's bespoke proposal |
| `screenshots` `savePath` | Write | **decision needed** — see Q3 |
| `gh_document_list` | — | optional: keep paths (model needs them to address docs); document as accepted recon surface |
| Audio Viewer Play | Execute-adjacent | separate small fix: require a supported audio extension, reject UNC — not part of the gate (user click, not a tool call) |

## 5. Decisions

1. **Grant granularity:** file + folder (decided).
2. **Denied tier:** hard deny — no prompt, no grant path; the chat cannot
   retrieve or write sensitive/system locations at all (decided).
3. **Screenshots:** *pending* — hard sandbox (keep #967 as is) vs sandbox
   default for relative names + Write-consent gate for absolute paths.
4. **`gh_document_save` in place:** no prompt when saving to the document's
   own path (decided).
5. **In-flight PRs:** rework #966/#968 onto the gate (decided).

## 6. Rollout (one PR per batch, independent → `main`)

1. **Merge [#965](https://github.com/architects-toolkit/SmartHopper/pull/965)** — unrelated critical CI fix, land first.
2. **Gate foundation** — `LocalPath`, `LocalFileAllowlist`, `LocalFileAccess`,
   shared awaiting hub, `LocalFilePrompt`, Settings section, `ToolManager`
   scope, Infrastructure tests mirroring `ExternalSourceAccessTests`.
3. **Read-side tools** — `file2md`, `img2text`, `gh_put`,
   `gh_batch_import_results`; re-enable `_rhino_*_3dm` behind the gate.
4. **Write/execute tools** — `gh_document_save`, `gh_document_open`,
   `screenshots` (per Q3); rework or supersede #966/#968.
5. **Loose ends** — ODT decompression caps (finding 7), Audio Viewer extension
   check (finding 8), `gh_document_list` path-leak decision.

## 7. Verification

- Infrastructure tests: canonicalization (traversal, reparse points, case),
  file vs directory grant matching, capability isolation, scope-scoped
  decisions, denial before any disk access, `NoResponse` on timeout, malformed
  permission file fails closed, revocation visible immediately.
- Per-tool tests: injected path outside any grant is refused with no prompt
  stubbed (`PromptAsync = null` → deny).
- Manual Rhino pass: prompt fires on UI thread, buttons grant the right scope,
  settings revoke takes effect on the next call.
