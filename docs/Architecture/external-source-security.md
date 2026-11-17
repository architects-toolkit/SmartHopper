# External source security

## Ownership And Scope

- `SmartHopper.Infrastructure.Networking` owns source permissions, persistence and the guarded HTTP client.
- AI-controlled URL conversion, document images, forum retrieval and media downloads use `ExternalDataHttpClient.Create()`.
- Configured provider API connections, account login and catalogue infrastructure traffic are separate service connections, not arbitrary data-source retrieval.
- Browser images use the consent-gated host bridge and receive inline data; CSP blocks direct HTTP(S)/file image and media loading. WebView document/bridge isolation is a separate security boundary.
- `ExternalImageMaterializer` resolves outgoing image URLs before provider handoff, so a provider cannot bypass approval by retrieving those URLs itself. Unlike advisory request policies, materialization errors abort the call.

## Approval Contract

`ExternalSourceAccess.RequireAsync` asks the interactive host before a new source is contacted. No presenter, dismissal, denial, cancellation or invalid configuration fails closed. Canvas mutation approval and MCP mutation bypass settings do not apply to source access.

The consent prompt applies its own deadline (120 s), independent of the generic tool timeout: an unanswered prompt resolves to a distinct `NoResponse` decision — treated as a refusal for that invocation and reported as "the consent prompt was not answered" rather than a misleading execution-timeout error. Prompts serialize on a global lock; within one logical invocation (`BeginScope`), a decision for an origin is recorded in the scope's grant map, so nested retrievals of the same origin share it — including denials, which fail fast without re-prompting.

- Allow once: grants the exact origin for one logical retrieval/tool invocation, including nested helpers.
- Always allow this origin: persists its canonical scheme, IDN ASCII hostname and effective port.
- Deny: sends no request to that origin.
- A redirect is a new request and must pass approval. Authorization and other caller request headers are not forwarded. Fresh server-session cookies can be used only within the same exact origin; redirects to another origin/port never receive them.
- A hostname resolving to a private address additionally requires approval for that exact IP endpoint. This prevents a saved public-host grant silently becoming a grant to the user's LAN.

## Storage And Settings

The allowlist is a dedicated user-owned file:

`%APPDATA%/Grasshopper/SmartHopper/external-source-permissions.json`

It contains only canonical origins, never credentials, paths, query tokens or wildcard domains/networks. Atomic replacement and a cross-process write lease preserve concurrent updates. Lookups re-read grants so revocation is not hidden by a long-lived cache. Malformed/unreadable permissions fail closed.

Settings > Security owns external-origin management, provider trust/integrity controls, privacy/telemetry consent and logging/export controls. Existing settings retain their original persisted keys and defaults. Manual origin additions and removals are staged until Save; prompt-based Always Allow decisions persist immediately.

External responses have a configurable decompressed size cap: 16 MiB by default, adjustable from 1 to 1024 MiB in Security. Web-page conversion retains its separate 10 MB default limit. Approval controls retrieval, not permission to disclose private content to an external AI provider.

## Transport

The guarded transport disables automatic redirects, ambient cookies and proxies. Direct source routing is an approved workflow change: proxy-only networks require explicit future proxy support, not a silent proxy bypass of destination checks. Fresh session-cookie jars are isolated per exact origin and bounded to 32 origins with 32 cookies each. Its socket callback resolves an approved hostname once, validates the actual destination and connects to that exact IP. Mapped and standard NAT64 IPv4 addresses are normalized before checks; other transition addresses receive conservative classification. Unspecified, multicast and known instance-metadata destinations are never permitted. TLS certificate validation remains enabled.

## Async Media Contract

Use `VersatileImage.ToBitmapAsync(CancellationToken)` and `VersatileAudio.ToByteArrayAsync(CancellationToken)`. The legacy synchronous download APIs are removed with user approval. Image/audio viewers resolve data without blocking the UI; image-load generations reject stale completions. Grasshopper's synchronous bitmap cast never performs I/O and succeeds only for an already-resolved bitmap.

Network-free Infrastructure tests cover origin canonicalization, persistence, scope-limited decisions, denial before transport, redirect approval/header isolation, response limits and private-address classification. Desktop consent/casting/viewer behavior additionally needs Rhino validation.
