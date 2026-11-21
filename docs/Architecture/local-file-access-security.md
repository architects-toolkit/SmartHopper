# Local file access security

## Ownership And Scope

- `SmartHopper.Infrastructure.FileSystem` owns local-path permissions: `LocalPath` canonicalization and sensitivity tiers, `LocalFileAllowlist` persistence, `LocalFileAccess` approval, `LocalFilePrompt` interactive consent.
- Every AI tool path argument is canonicalized (`GetFullPath`, root required, reparse-point targets resolved when the file exists) and checked before the file is touched.
- The gate covers tool-level access: `file2md` (Read), `img2text` local `imageUrl` (Read), `gh_put` `.ghjson` path arguments (Read), `gh_batch_import_results` (Read per entry), `gh_document_save` (Write on an explicit target), `gh_document_open` (Execute on a file that is not already open — activating an open document needs no consent), `*_screenshot` `savePath` (Write on absolute paths), and `rhino_read_3dm` (Read).
- Internal files SmartHopper produces itself are pre-authorized and never prompt: the screenshot export directory and `%TEMP%/smarthopper-*` pipeline files (e.g. chat PDF attachments).
- Files the user opens or writes through the Grasshopper/Rhino UI itself are not gated; the boundary is model-supplied paths, not user-driven IO.

## Tiers

- **Denied** — refused outright, never prompts: `.ssh`, `.aws`, `.azure`, `.kube`, `.gnupg` segments anywhere; browser credential stores (Chrome, Edge, Brave, Chromium, Firefox, Opera profile trees); SmartHopper's own configuration directory except the Screenshots export folder; the `SmartHopper.json` settings file; `Windows`, `Program Files` and `CommonProgramFiles` on Windows; `/etc`, `/usr`, `/bin`, `/sbin`, `/boot`, `/root`, `/proc`, `/sys`, `/private/etc`, `/System` elsewhere.
- **Pre-authorized** — allowed silently: `Screenshots` (Read/Write) and `smarthopper*` files under the temp directory (Read/Write).
- **Gated** — everything else asks through the consent prompt.

## Approval Contract

`LocalFileAccess.RequireAsync` asks the interactive host before a path is touched. No presenter, dismissal, denial, cancellation or invalid input fails closed. Canvas mutation approval and the MCP mutation-bypass setting do not apply to file access.

The consent prompt applies its own deadline (120 s): an unanswered prompt resolves to `NoResponse` — a refusal for that invocation, never a grant. Prompts serialize on a global lock; within one logical invocation (`BeginScope`, opened per tool call by `AIToolManager`), a decision for a path+capability pair is recorded in the scope's grant map, so nested touches share it — including denials, which fail fast without re-prompting. `GrantForCurrentScope` lets trusted internal flows (files the user attached themselves) cover the rest of the scope without prompting.

- Allow once: grants the exact path and capability for one logical tool invocation, including nested helpers.
- Always allow this file: persists an exact-path grant.
- Always allow this folder: persists a directory-subtree grant (separator-boundary matching, case-aware per OS).
- Deny: the tool sees an `UnauthorizedAccessException` refusal.
- Write prompts flag overwrites; Execute prompts warn that opening the document may run embedded scripts.

Grants are capability-specific: a Read grant does not cover Write or Execute.

## Storage And Settings

The allowlist is a dedicated user-owned file:

`%APPDATA%/Grasshopper/SmartHopper/local-file-permissions.json`

It stores `{ scope, path, capabilities, grantedUtc }` entries — canonical paths only, never credentials or wildcards. Atomic replacement and a cross-process write lease preserve concurrent updates; lookups re-read grants so revocation is not hidden by a cache; malformed files fail closed.

Settings > Security > "Local file permissions" mirrors the external-sources list: editable `RWE file|folder path` rows with "−" removal and a pending-entry row; manual additions are validated, canonicalized and rejected when the location is in the denied tier. Removals and manual additions take effect on Save; prompt-based Always decisions persist immediately.

## Tool Contract

Tools must call `RequireAsync` before touching a model-supplied path and let `UnauthorizedAccessException` propagate as an explicit refusal. Synchronous call sites (image loading, GhJSON string resolution, PNG export) block on the gate but first refuse on the Rhino UI thread when a prompt would be needed — a prompt can only render on that thread, so waiting there would deadlock.

Network-free Infrastructure tests cover canonicalization, the denied tier, pre-authorized roots, file/folder grant persistence and revocation, subtree matching, scope-limited decisions, capability isolation and the no-presenter fail-closed path. Desktop consent behavior additionally needs Rhino validation.
