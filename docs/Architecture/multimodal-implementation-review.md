<!-- docs-validation: ignore -->
# Multimodal Documents — Implementation Review

Verification record for the multimodal-documents feature: what the deterministic
suite proves, what it does not prove, and the standing limitations. Companion to
[multimodal-documents.md](multimodal-documents.md) (architecture contract) and
[../Testing/multimodal-documents.md](../Testing/multimodal-documents.md) (manual
verification matrix).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.DocumentProcessing/`, `src/SmartHopper.DocumentRendering/`, `src/SmartHopper.ProviderSdk/Documents/`, `src/SmartHopper.Infrastructure/AICall/Fallback/` |
| **Since Version** | 2.0.0 |
| **Last Updated** | 2026-11-11 |
| **Documentation Maintainer** | SmartHopper contributors |

---

## What the Deterministic Tests Prove

All suites are Rhino-free, provider-network-free, and run against the real
production parser/renderer/contracts — not reimplementations.

- **Evidence preservation** (`SmartHopper.DocumentProcessing.Tests`,
  `SmartHopper.ProviderSdk.Tests/Documents/`): the synthetic fixture produces a
  `DocumentEvidence` with per-page Markdown, dimensions, renders, embedded
  assets, warnings and coverage; source/page/asset/figure provenance survives
  JSON and interaction round-trips; page-less web documents keep article order.
- **Selection and limits**: `Pages`/`Region` JSON parsing rejects empty,
  duplicate, fractional, out-of-range, non-finite and overflowing values; the
  32-rendered-page cap, the 64 MiB aggregate encoded budget (counted on base64
  length, not raw PNG bytes) and the 128 MiB source cap are enforced; a missing
  or untrusted renderer yields partial coverage (`IsComplete=false`) with
  warnings, not a silent text-only pass.
- **Capability gating** (`RequestModalityPreparationTests`): the resolved
  primary provider/model is never upgraded because the source carries images;
  `SendToModel` media requires `ImageInput` (display-only images add no
  requirement); unknown capability is not confirmed support; disabled fallback,
  missing helpers, invalid pins, helper errors and cancellation all block
  incompatible submission explicitly; canonical request and session evidence
  are never mutated.
- **Helper metrics once** (`VisualEvidenceEnrichmentTests`): conversions are
  metered exactly once per run — including when the primary call later fails;
  repeat conversions of identical evidence within a run reuse the cached result
  and do not double-charge; a helper is never recursively fallen back.
- **MCP typed images** (`JsonRpcDispatcherTests`): model-visible
  `ToolResultImage`s emit as ordered native `image` blocks before the text
  block; image bytes never appear in JSON or `structuredContent`;
  `nativeImageOrder` reports the emitted asset IDs; `_meta`
  `["smarthopper/helperUsage"]` forwards helper metrics on success and error;
  the `consumerSupportsImages` tri-state contract holds (`supported`,
  `unsupported`, `unknown` + `clientAdaptationRequired`).
- **Streaming and session preparation** (`ConversationSessionTests`,
  `RequestModalityPreparationTests`): streaming calls, special turns and
  tool-call continuations run the same central preparation as `Exec`;
  tool-result images reach providers only through prepared, capability-checked
  views.
- **Renderer trust** (`RendererIntegrityGateTests`): the gate requires
  `Official` classification AND a host-matching strong name AND (on Windows) a
  host-matching Authenticode signature — anything less is refused.
- **JS staging locks** (`tools/chat-fallback-controls.cjs`): the composer locks
  only while attachments are staged; during a running turn it stays editable
  and the send button reads "Queue".

## What the Tests Do NOT Prove

- **Model comprehension.** Transport, conversion and gating tests say nothing
  about whether a real model understood a document. Comprehension is assessed
  only through the live matrix in
  [../Testing/multimodal-documents.md](../Testing/multimodal-documents.md).
- **Independent-extraction equivalence.** The normalized-text comparison checks
  character preservation (Unicode NFKC + whitespace removal), not reading
  order, table quality, formula semantics or visual interpretation.
- **Renderer conflict-freedom under every host.** The dedicated ALC keeps
  PDFtoImage/SkiaSharp out of the default context by construction; it is
  validated in the test environment, not under every possible third-party
  plugin load order.

## Synthetic Fixture Comparison

Source: `src/SmartHopper.DocumentProcessing.Tests/TestAssets/synthetic-scientific-evidence.pdf`
(4 pages; frozen `groundtruth.json`).

- Extracted text matches an independent extraction on all 4 pages after
  normalization — including the scan-only page 2 (0 characters on both sides).
- Renders preserve the distinguishing evidence per page: page 1 vector plot
  (29 vector paths, no rasters) at 1190×1684 px; page 2 raster scan; page 3
  rotated/cropped visible area (515×740 pt → 1030×1480 px) including the green
  annotation; page 4 table/equation page. The requested detail crop renders at
  up to 4096 px on the longest side.
- A one-page real-world PDF was additionally compared locally through the
  `SH_TEST_REAL_PDF` opt-in (normalized text equal; illustration and layout
  preserved). Real-world files are never vendored.

## Platform Validation

- Windows (net7.0-windows): reader, renderer, packaging and test suite
  validated.
- macOS (net7.0): the renderer is packaged for osx-x64 and osx-arm64 but is
  **not validated** — the isolated-load path and native assets ship untested.

## Trust-Gate Scope

`RendererIntegrityGate` is a load-time trust decision for the renderer
assembly only. The renderer's dedicated `AssemblyLoadContext` is **dependency
isolation** (it prevents PDFtoImage/SkiaSharp version conflicts with Rhino's
default context) — it is not a security sandbox. Rendered PDF content remains
untrusted input: the renderer never executes document scripts and the pipeline
bounds file size, page count and encoded output, but a malicious document can
still exercise the parser/renderer within those limits.

## Standing Limitations

- **Rendered ≠ understood.** Page renders capture visible composition; they do
  not recover hidden PDF semantics, and a model may still misread them.
- **Coverage means the selected subset.** `Coverage.IsComplete` describes only
  the pages/regions the caller selected — never the whole source document.
- **Helper cost.** Explicit enrichment and fallback conversions are paid
  provider calls; metrics land on the helper's provider/model, distinct from
  the primary call.
- **Batch.** Queued provider submissions run central preparation per item, but
  explicit AI visual enrichment (`AI File To Markdown` / `AI Web To Markdown`)
  is synchronous and cannot be batched — those components fail explicitly under
  `Run`+batch instead of queuing.
- **MCP transport ≠ capability.** Native `image` blocks prove only that the
  client received images; whether the remote model accepts them is the
  client's `consumerSupportsImages` decision.
- **macOS renderer unvalidated** (see above).
