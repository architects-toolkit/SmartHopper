# Local file access security

User approval layer for every model-supplied filesystem path, mirroring the external-source retrieval consent (`ExternalSourceAccess`).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/FileSystem/`, `src/SmartHopper.Infrastructure/Dialogs/LocalFilePrompt.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-11-21 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Read this if you want to understand which file paths AI tools may touch, how the consent prompt and persisted file/folder grants work, and which locations are refused outright.

You should also read this if you are:

- adding or changing an AI tool that accepts a path argument
- debugging why a tool call was refused with `UnauthorizedAccessException`
- changing the local-file permissions storage or the Settings > Security allowlist
- reviewing how synchronous tool code avoids deadlocking on the Rhino UI thread

## End-User Guide

When an AI tool wants to read, write or open a local file, a prompt asks whether to allow it once, always allow that file, always allow that folder, or deny. Write requests flag when they would overwrite an existing file; open requests warn that running a document may execute embedded scripts.

- Allow once: grants the exact path for one logical tool invocation, including nested helpers.
- Always allow this file: persists an exact-path grant.
- Always allow this folder: persists a directory-subtree grant.
- Deny: the tool call is refused.

Approvals are per capability — a Read grant does not cover Write or Open. Grants live in `%APPDATA%/Grasshopper/SmartHopper/local-file-permissions.json` and are managed in Settings > Security > "Local file permissions", which mirrors the external-sources allowlist: `RWE file|folder path` rows, per-row removal, and a pending-entry row. Manual additions are validated, canonicalized and rejected when the location is protected. Manual edits take effect on Save; prompt-based Always decisions persist immediately.

Some locations are always refused and never prompt: credential and key directories (`.ssh`, `.aws`, `.azure`, `.kube`, `.gnupg`), browser profile trees, SmartHopper's own configuration (except the Screenshots export folder), the `SmartHopper.json` settings file, and OS system folders. SmartHopper's own pipeline files — the Screenshots export folder and `%TEMP%/smarthopper-*` — are pre-authorized and never prompt.

## Developer Reference

`SmartHopper.Infrastructure.FileSystem` exposes the gate:

- `LocalPath.TryCanonicalize(path, out canonical, out error)` — rooted paths only, `GetFullPath`, reparse-point resolution when the file exists, separator-boundary `IsUnder`, case-aware `Comparer` per OS.
- `LocalPath.IsDenied(canonical)` / `IsPreAuthorized(canonical, capability)` — the hard-deny and silent-allow tiers.
- `LocalFileAccess.RequireAsync(path, capability, toolName, ct)` — canonicalizes, checks tiers and grants, serializes the prompt on a global lock, persists Always decisions, throws `UnauthorizedAccessException` on denial, no presenter or timeout (`NoResponse`).
- `LocalFileAccess.RequireBlocking(path, capability, toolName)` — for synchronous call sites; refuses on the Rhino UI thread (a prompt can only render there, so waiting would deadlock) and blocks safely on background threads.
- `LocalFileAccess.BeginScope()` — opened per tool call by `AIToolManager`; one-call decisions (and denials) are shared across nested touches of the same path+capability.
- `LocalFileAccess.GrantForCurrentScope(path, capability)` — pre-grants trusted flows (e.g. files the user attached) without prompting.
- `LocalFileAllowlist` — `GetGrants`/`IsGranted`/`Allow`/`Revoke`/`RevokeAll` over the persisted file; re-reads on each lookup so revocation is immediate.
- `LocalFileCapability` — `Read`, `Write`, `Execute` flags; grants are capability-specific.

Gated tools today: `file2md` (Read), `img2text` local `imageUrl` (Read), `gh_put` `.ghjson` arguments (Read), `gh_batch_import_results` (Read per entry), `gh_document_save` (Write on an explicit target), `gh_document_open` (Execute on a file not already open — activating an open document needs no consent), `*_screenshot` `savePath` (Write on absolute paths; relative names stay in the Screenshots folder and never overwrite), `rhino_read_3dm` (Read).

Tools must call the gate before touching a model-supplied path and let `UnauthorizedAccessException` propagate as an explicit refusal. `McpServerOptions.BypassMutationsApproval` does not bypass file access. Files the user opens or writes through the Grasshopper/Rhino UI itself are not gated; the boundary is model-supplied paths, not user-driven IO.

## Architecture & Design

The decision order inside `RequireAsync` is: canonicalize → reject invalid capability → denied tier → pre-authorized roots → persisted allowlist → scope grants → serialized prompt. The prompt applies its own 120 s deadline; an unanswered prompt resolves to `NoResponse`, a refusal for that invocation, and is remembered in the scope's grant map so it fails fast without re-prompting.

The allowlist file stores `{ scope, path, capabilities, grantedUtc }` entries — canonical paths only, never credentials or wildcards. Atomic replacement and a cross-process write lease preserve concurrent updates; malformed or unreadable permission files fail closed.

`LocalFilePrompt` renders the consent dialog on the Rhino UI thread and reports `IsAwaitingConsent`/`AwaitingConsentChanged` so the canvas consent badge can repaint, shared with the external-source prompt.

Network-free Infrastructure tests cover canonicalization, the denied tier, pre-authorized roots, file/folder grant persistence and revocation, subtree matching, scope-limited decisions, capability isolation and the no-presenter fail-closed path. Desktop consent behavior additionally needs Rhino validation.
