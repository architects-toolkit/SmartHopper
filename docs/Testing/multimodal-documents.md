# Multimodal Document Verification

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.DocumentProcessing.Tests/`, `src/SmartHopper.Infrastructure.Tests/AICall/`, `src/SmartHopper.ProviderSdk.Tests/Documents/` |
| **Since Version** | 2.0.0 |
| **Last Updated** | 2026-11-11 |
| **Documentation Maintainer** | SmartHopper contributors |

---

## Why Read This?

Extraction, image transport, and model comprehension are different claims. This
procedure verifies them separately; a successful render or mocked provider test
does not demonstrate that a real model understood a paper.

## End-User Guide

### Local Conversion

Use `File to Markdown` or `file2md` without `describeImages`. Verify page selection,
source Markdown, rendered pages, image provenance, warnings, and visual coverage.
This must not make a provider call. `extractImages=false` explicitly requests
text-only conversion; it does not establish complete visual coverage.

The checked-in fixture is
`src/SmartHopper.DocumentProcessing.Tests/TestAssets/synthetic-scientific-evidence.pdf`.
Its companion `groundtruth.json` records synthetic source facts, not research
findings. Use private documents only with the user's permission; do not check them
or their extracted content into the repository.

### Live Comprehension Matrix

Run each condition with a fresh conversation and the same source, selected pages,
and questions. Record the actual provider/model and helper provider/model,
fallback policy, selected/rendered pages, diagnostics, response, and reported
usage. Do not give the model `groundtruth.json` or the expected answers.

| Condition | Content delivered | What to verify |
| --- | --- | --- |
| Text-only baseline | Native text/Markdown, no images or helper descriptions | The model acknowledges missing visual evidence rather than inventing plot values. |
| Direct vision | Extracted text plus original page images | A registry-confirmed image-capable model receives the images and answers from visible evidence. |
| Vision-helper fallback | Original text plus contextual helper evidence | The selected text-only primary stays selected; the helper reads the images; no unsupported images reach the primary. |
| Explicit AI Markdown | Original Markdown plus labeled AI visual evidence | Source text remains available and generated observations retain page/asset references. |
| Missing/failed helper | Original evidence retained, outbound call blocked | The error reports incomplete understanding; it is not presented as a successful full reading. |

Use these questions verbatim for the synthetic fixture:

1. "On page 1, what are the Sensor A and Sensor B readings at time 2 seconds?
   State the units, cite the page and figure, and distinguish visually estimated
   values from values explicitly stated in source text."
2. "Which series is blue and dashed? On which series and time point is the
   vertical error bar drawn, and approximately what are its lower and upper
   endpoints? Do not guess an unreadable value."
3. "Does the document establish a causal relationship or statistical
   significance? Separate statements in the source from your interpretation."
4. "What are the exact control and treatment means and units in Table 1 on page
   4? Is the equation available as native text?"
5. "What visible annotation appears on page 3? Explain whether page 2 has native
   extracted text or must be read visually."

Compare responses manually against the frozen source record:

- Figure 1 uses linear axes, Time (s) and Temperature (C). At time 2, A is
  approximately 10 C and B approximately 30 C.
- B is blue/dashed; A is red/solid. The error bar is on A at time 2, with
  approximate endpoints 8 C and 12 C. These observations require visual evidence;
  the native prose intentionally does not supply the series values or endpoints.
- The source does not establish causation or statistical significance.
- Page 4 supplies exact native-text means: control 12.5 mM, treatment 9.0 mM,
  and `E = mc^2`.
- Page 3 has a green rectangular annotation and a rotated/cropped visible page.
  Page 2 is a raster scan with no native PDF text.

Inspect the page-1 detail crop as well as the full-page render when labels are
too small. Do not require an exact numeric reading from indistinct pixels or
turn approximate plot readings into exact measurements.

### Three Surfaces

- Components: wire one `File to AI` payload into the destination; repeat with a
  vision model and a text-only primary with an explicitly configured helper.
- Chat: attach the same PDF, wait for preparation, then send the questions.
  Check queued messages keep their own attachment snapshots.
- MCP: declare `consumerSupportsImages` explicitly or adapt in the client.
  Native MCP image blocks alone do not prove that the external model accepts
  images. Correlate image-block order with the returned asset descriptors.

## Developer Reference

Keep unit tests deterministic and provider-network-free. They verify capability
gating, request copies, provenance, coverage, helper failures/cost propagation,
typed MCP images, and continuation behavior. Rhino/Grasshopper wiring checks
belong in the test-component project, not unit-test projects.

Independent extraction checks must verify the PDF content hash against the saved
production evidence before comparison. Compare native text after Unicode NFKC
normalization and whitespace removal; retain mismatches for inspection. This
normalization checks character preservation, not reading order, Markdown table
quality, formula semantics, or visual interpretation.

Store private source comparisons, renders, and provider responses in ignored
local evidence directories. Freeze the results once captured; presentation
changes must not silently regenerate the experiment.

## Architecture & Design

The canonical document preserves source text and visuals. Request preparation
creates a capability-compatible view without mutating that source or silently
replacing the selected primary model. Helper interpretations are generated
evidence, not a substitute source of truth.

No automated scientific-comprehension score is defined here. A live check must
report the actual answers and limitations, not infer comprehension from test
counts or renderer success.

Related: [Multimodal architecture](../Architecture/multimodal-documents.md) and
[test suite](index.md).
