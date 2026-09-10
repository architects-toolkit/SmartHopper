# SmartHopper Icon Set — Design Brief

Brief for generating a new, coherent icon set (e.g. with an AI icon-set generator such as mew.design) covering the Grasshopper component icons and app identity assets of SmartHopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Components/Resources/` |
| **Since Version** | ? |
| **Last Updated** | 2026-09-07 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

Component icons are the primary visual language of SmartHopper on the Grasshopper canvas. This brief defines the badge grammar, color system, and per-icon design specs so a regenerated icon set stays coherent.

**You should read this if you:**

- Are generating or redrawing component or brand icons
- Need to understand the badge grammar (direction, source, and AI markers)
- Are adding a new component and need a consistent icon spec

---

## End-User Guide

### What users see

Every component icon is a main central glyph plus one or two small corner badges that encode semantics: direction (data into AI vs out of AI), source/type, and whether the component calls an AI provider. The numbered sections below describe this grammar in detail.

---

## Developer Reference

### Icon assets and assignment

Component icons are 24×24 PNGs embedded as `Bitmap` resources and exposed through each component's `Icon` override:

```csharp
// Each component surfaces its icon from embedded resources.
protected override Bitmap Icon => Properties.Resources.aichat;
```

The Grasshopper category tab icon is registered once at assembly load:

```csharp
// SmartHopperAssemblyPriority registers the category identity.
Instances.ComponentServer.AddCategoryIcon("SmartHopper", Resources.smarthopper);
```

---

## Architecture & Design

### Design brief

The numbered sections below are the full generation brief: technical constraints, the current icon system, the proposed badge grammar, the per-icon inventory, and the color system.

### 1. Technical constraints

| Constraint | Value | Why |
| --- | --- | --- |
| Format | PNG, transparent background | Embedded as `Bitmap` resources in `SmartHopper.Components/Properties/Resources.resx` |
| Component icon size | **24×24 px** | Every file in `src/SmartHopper.Components/Resources/` is 24×24 |
| Rendered size | ~24 px on canvas, ~16–20 px in ribbon/tab | Grasshopper scales component icons; they must read at 16 px |
| App icon | `.ico` (multi-size) + 256×256 PNG | `smarthopper.ico` and `smarthopper_256.png` in `SmartHopper.Infrastructure/Resources/`; used by the About dialog, styled dialogs, and the floating canvas chat button (`CanvasButton`) |
| Theme | Must work on light **and** dark Grasshopper themes | Avoid pure black or pure white-only silhouettes; prefer mid-tone fills + outlines |
| Legibility rule | Max **2 badges** per icon, badges ≥ 8 px | At 24 px, anything finer turns to noise |

### 2. Current system

Every component icon is a **main central glyph** plus **one or two small badges** at the bottom corners that encode semantics. The codebase already reveals a consistent grammar:

- `toai-*` icons = **input** components (data → AI payload): type glyph + "into AI" marker.
- `aito*` icons = **output** components (AI → data): type glyph + "from AI" marker.
- Knowledge icons = source glyph (forum brand) + action badge (search / get / open / summarize).
- GhJSON icons = document glyph + operation badge (get / put / merge / diff / patch).
- "Side" icons (settings, context, models, metrics) have no direction badge.

### 3. Proposed badge grammar

Fixed positions so badges are predictable:

- **Bottom-right corner** = *direction/action* badge:
  - `→` arrow pointing into a small AI spark → **input** (`toai-*` family).
  - `←` arrow coming out of a small AI spark → **output** (`aito*` family).
  - Action glyphs for non-directional ops: magnifier (search), tray/download (get), external-link arrow (open), paragraph/contract lines (summarize), split arrows (deconstruct), funnel (filter), merge arrows, diff bars, puzzle/patch, checkmark-shield (validate), floppy/down-arrow (save), folder/open (open file), grid/tidy (tidy up), link/plug (connect), clipboard/report (report), server node (MCP).
- **Bottom-left corner** = *source/type modifier* badge:
  - Forum sources: **McNeel** (rhino head or "M" monogram), **Ladybug** (ladybug dot/beetle), **generic Discourse** (speech bubble / "D").
  - Format modifiers: `MD` (markdown), `{}` (JSON), `[]` (array/list), `<>` or `</>` (script), `{;}`/grid (GhJSON).
- **AI-powered marker**: a small four-point spark/star in **SmartHopper green**, placed top-right *or* integrated into the direction badge. All components that call an AI provider should carry it; pure utility components (Tidy Up, JSON merge, viewers) should not.

Recommended: generate the set **modularly** — first ~15 core glyphs and ~12 badges as standalone assets, then ask the generator to compose each icon. That guarantees consistency that one-shot generation won't.

### 4. Similarity groups found in the codebase

1. **Direction pairs** — the largest pattern: `toai-*` (B. Input) vs `aito*` (C. Output). Same type glyph, mirrored direction badge.
2. **Data-type family** — text, text list, boolean, integer, number, JSON, list, image, audio, speech, file, web, GhJSON, context, prompt.
3. **Forum family** — 3 sources (McNeel, Ladybug, Discourse) × the same actions (search, get post, open post, summarize post, summarize topic, post→AI, topic→AI).
4. **GhJSON document ops** — get/put/merge/diff/validate/patch/open/save: one document glyph + operation badges.
5. **JSON toolkit** — object/array/item/schema/merge/clean/to-text: braces/brackets base + modifiers.
6. **Side/config icons** — settings, extra settings, models, context providers, file metadata, metrics: no direction badge; these live next to the AI pipeline.
7. **Viewers/captures** — image viewer, audio viewer, canvas/viewport capture: "display/capture" family, no AI badge.

### 5. Full icon inventory and per-icon design spec

#### 5.1 Brand / app identity

| Asset | Used by | Design spec |
| --- | --- | --- |
| `smarthopper.png` (24×24) | Grasshopper category tab icon (`SmartHopperAssemblyPriority.cs`) | The SmartHopper grasshopper-head mascot, simplified to a flat silhouette that survives 16 px. Green primary. |
| `smarthopper.ico` + `smarthopper_256.png` | App icon: About dialog, styled dialogs, floating canvas chat button | Same mascot at high res; needs 256/48/32/16 px .ico frames. |
| `img/smarthopper.png` | Repo/README logo | Large-format variant of the mascot. |
| Provider logos (8) | `SmartHopper.Providers.*`: anthropic, deepseek, gemini, localai, mistralai, ollama, openai, openrouter | **Do not AI-generate** — third-party trademarks. Keep official brand marks, normalized to a consistent square frame. |

#### 5.2 "A. AI" category — assistant & configuration

| Component | Icon resource | Design spec |
| --- | --- | --- |
| AI Chat (`AIChatComponent`) | `aichat` | Chat bubble containing the grasshopper/AI spark — the flagship icon, should feel closest to the brand mark. |
| Settings (`AISettingsComponent`) | `settings` | Gear + AI spark badge (bottom-right). Settings, not a data op, so no direction arrow. |
| Extra Settings (`AIExtraSettingsComponent`) | `settingsextra` | Gear + small "…"/sliders badge — visually tied to `settings` but reads as "extended". |
| Context Providers (`AIContextProvidersComponent`) | `contextproviders` | Stacked layers/plug glyph + tiny list badge; conveys "registered context sources". |
| File Metadata (`AIFileMetadataComponent`) | `context` | Document with a tag/label line — represents file-level metadata context. |
| AI Models (`AIModelsComponent`) | `aimodels` | Stacked/layered nodes or a list with a spark — "catalog of models". |

#### 5.3 "B. Input" category — data → AI (`toai-*`)

Shared spec: **type glyph center + "arrow into AI spark" badge bottom-right**. List variants add a bottom-left `≡`/`[]` badge.

| Component | Icon resource | Central glyph | Badges |
| --- | --- | --- | --- |
| AI Prompt (`AIPromptComponent`) | `toaiprompt` | Speech/prompt bubble | to-AI |
| AIContext (`AIContextComponent`) | `toaicontext` | Stacked layers (context) | to-AI |
| Text to AI (`Text2AIComponent`) | `toaitext` | "T" letterform | to-AI |
| Text List to AI (`TextList2AIComponent`) | `toailist` | Lines list `≡` | to-AI |
| Boolean to AI (`Boolean2AIComponent`) | `toaibool` | Toggle / check | to-AI |
| Boolean List to AI (`BooleanList2AIComponent`) | **new:** `toaiboollist` | Toggle / check | to-AI + list |
| Integer to AI (`Integer2AIComponent`) | `toaiinteger` | "#" or "123" | to-AI |
| Integer List to AI | **new:** `toaiintegerlist` | "#" or "123" | to-AI + list |
| Number to AI (`Number2AIComponent`) | `toainumeric` | "1.0" / decimal | to-AI |
| Number List to AI | **new:** `toainumericlist` | "1.0" / decimal | to-AI + list |
| JSON to AI (`Json2AIComponent`) | `toaijson` | Curly braces `{ }` | to-AI |
| Image to AI (`Img2AIComponent`) | `toaiimg` | Picture/landscape | to-AI |
| Audio to AI (`Audio2AIComponent`) | `toaiaudio` | Speaker/waveform | to-AI |
| File to AI (`File2AIComponent`) | `toaifile` | Document/page | to-AI |
| Web to AI (`Web2AIComponent`) | `toaiweb` | Globe | to-AI |
| Canvas to AI (`GhJSON2AIComponent`) | `toaighjson` | GH document / grasshopper-node glyph | to-AI |
| Discourse Post to AI | `toaidiscoursepost` | Post card | to-AI + Discourse bubble |
| Discourse Topic to AI | `toaidiscoursetopic` | Thread/topic stack | to-AI + Discourse bubble |
| McNeel Post to AI | **missing** (`Icon => null`) | Post card | to-AI + McNeel mark |
| McNeel Topic to AI | **missing** | Topic stack | to-AI + McNeel mark |
| Ladybug Post to AI | **missing** | Post card | to-AI + Ladybug mark |
| Ladybug Topic to AI | **missing** | Topic stack | to-AI + Ladybug mark |

#### 5.4 "C. Output" category — AI → data (`aito*`)

Shared spec: **type glyph center + "AI spark with arrow out" badge bottom-right**. Mirror of 5.3.

| Component | Icon | Central glyph |
| --- | --- | --- |
| AI to Text | `aitotext` | "T" |
| AI to Text List | `aitotextlist` | "T" + list lines |
| AI to Boolean | `aitobool` | Toggle/check |
| AI to Boolean List | `aitoboollist` | Toggle + list badge |
| AI to Integer | `aitointeger` | "#"/"123" |
| AI to Integer List | `aitointlist` | "#" + list badge |
| AI to Number | `aitonumeric` | "1.0" |
| AI to Number List | `aitonumericlist` | "1.0" + list badge |
| AI to JSON | `aitojson` | `{ }` |
| AI to Markdown | `aitomd` | "M↓" / markdown mark |
| AI to Image | `aitoimg` | Picture |
| AI to Speech | `aitospeech` | Microphone/speaker waves |
| AI to Script | `aitoscript` | `</>` code glyph |
| AI to GhJSON | `aitogh` | GH document |

#### 5.5 "Text" category — AI text ops

| Component | Icon | Design spec |
| --- | --- | --- |
| AI Text To Text | `textgenerate` | "T" + AI spark + small pencil/generate mark |
| AI Text To Text List | `textlistgenerate` | "T" + list lines + AI spark |
| AI Text To Boolean | `textevaluate` | "T" + check/?-evaluate badge — reads as "question → true/false" |

#### 5.6 "List" category

| Component | Icon | Design spec |
| --- | --- | --- |
| AI List Filter | `listfilter` | List `≡` + funnel badge + AI spark |
| AI List To Boolean | `listevaluate` | List `≡` + check/evaluate badge + AI spark |

#### 5.7 "JSON" category — pure data toolkit (no AI)

| Component | Icon | Design spec |
| --- | --- | --- |
| JSON Object | `jsonobj` | `{ }` braces |
| JSON Array | `jsonarray` | `[ ]` brackets |
| JSON Get Value | **new:** `jsongetvalue` (replaces shared `jsonitem`) | `{ }` + key/value arrow-out badge |
| JSON Set Value | **new:** `jsonsetvalue` (replaces shared `jsonitem`) | `{ }` + key/value arrow-in badge |
| JSON Merge | `jsonmerge` | Two `{ }` merging |
| JSON Sanitizer | `jsonclean` | `{ }` + spark/wipe mark |
| JSON Object → Text | `jsontotext` | `{ }` + "T" arrow |
| JSON Array → Text List | `jsonarraytolist` | `[ ]` + `≡` arrow |
| JSON Schema | `jsonschema` | `{ }` + small blueprint/grid badge |
| JSON Schema Object | `jsonschemaobj` | Schema badge + object braces |
| JSON Schema Prop | `jsonschemaprop` | Schema badge + single key glyph |
| AI Text → JSON | `jsonai` | `{ }` + AI spark (only AI-powered member of the family) |

#### 5.8 "Knowledge" category — research sources

| Component | Icon | Design spec |
| --- | --- | --- |
| Web To Markdown | `webtomd` | Globe + `MD` badge; blue deterministic-helper treatment |
| AI Web To Markdown | **new:** `aiwebtomd` (replaces shared `webtomd`) | Globe + `MD` badge + green AI accent; use the AI accent on the main globe rather than adding a third badge |
| File To Markdown | `filetomd` | Document + `MD` badge; pink knowledge treatment |
| AI File To Markdown | `fileai` | Document + AI spark; use a folded page corner to imply conversion rather than adding a third `MD` badge |
| Discourse Search | `discourseforumsearch` | Speech-bubble source glyph + magnifier badge |
| Discourse Post Get | `discoursepostget` | Post card + tray/download badge |
| Discourse Post Open | `discoursepostopen` | Post card + external-link arrow |
| Discourse Post Deconstruct | `discoursepostdeconstruct` | Post card + split/deconstruct badge |
| AI Discourse Post Summarize | `discoursepostsummarize` | Post card + contract-lines badge + AI spark |
| AI Discourse Topic Summarize | `discoursetopicsummarize` | Topic stack + summarize badge + AI spark |
| McNeel × (search / post get / post open / post summarize / topic summarize) | `mcneel*` | Same action badges, source glyph = McNeel mark (rhino/"M") |
| Ladybug × same 5 actions | `ladybug*` | Same action badges, source glyph = ladybug beetle |

#### 5.9 "Grasshopper" category — GhJSON document ops

| Component | Icon | Design spec |
| --- | --- | --- |
| Get GhJSON | `ghget` | GH document + tray/download-out badge (canvas → document) |
| Place GhJSON | `ghput` | GH document + arrow-onto-canvas badge (document → canvas) |
| Merge GhJSON | `ghmerge` | Two GH documents + merge arrows |
| Diff GhJSON | `ghdiff` | GH document + diff bars (split compare) |
| Apply GhPatch | `ghjsonpatch` | GH document + puzzle/patch piece badge |
| Apply GhPatch to Canvas | **missing** | GH document + patch badge + canvas arrow |
| Validate GhJSON | **missing** | GH document + check-shield badge |
| Retrieve Components | `components` | Component grid/box + search-ish badge |
| Tidy Up | `tidyup` | Scattered boxes → grid alignment mark |
| Save GhJSON | **missing** | GH document + floppy/down badge |
| Open GhJSON | **missing** | GH document + folder/open badge |
| Save GhPatch | **missing** | Patch document + save badge |
| Open GhPatch | **missing** | Patch document + open badge |
| AI Smart Connect | **missing** | Two component nodes + wire/plug + AI spark |
| AI Canvas Report | **missing** | Clipboard/report + canvas grid + AI spark |

#### 5.10 "Img" / "Audio" categories

| Component | Icon | Design spec |
| --- | --- | --- |
| AI Text To Image | `texttoimg` | "T" → picture + AI spark |
| AI Image To Text | `imgtotext` | Picture → "T" + AI spark |
| Image Viewer | `imgviewer` | Picture + eye/display frame — no AI badge |
| Canvas To Image | **new:** `canvascapture` (replaces shared `aitoimg`) | Grasshopper canvas frame + capture/camera badge; blue non-AI helper |
| Viewport To Image | **new:** `viewportcapture` (replaces shared `aitoimg`) | Perspective viewport frame + capture/camera badge; blue non-AI helper |
| Audio Viewer | `audio` | Speaker/waveform in a display frame — no AI badge |

#### 5.11 "Script" category

| Component | Icon | Design spec |
| --- | --- | --- |
| AI Script Generate | `scriptgenerate` | `</>` code glyph + AI spark |
| AI Script Review | `scriptreview` | `</>` + magnifier/check badge + AI spark |

#### 5.12 "Utils" / "MCP"

| Component | Icon | Design spec |
| --- | --- | --- |
| Deconstruct Metrics | `metricsdeconstruct` | Chart bars + split/deconstruct badge |
| Combine Metrics | **missing** | Chart bars + merge badge |
| SmartHopper MCP Server | **missing** (no `Icon` override) | Server/plug node + SmartHopper spark — represents the loopback MCP endpoint |

### 6. New composed icons required because of missing or shared assets

The new set must include the following **22 distinct 24×24 component icons** in addition to replacing the icons already listed above. Fourteen fill current gaps; eight separate components that currently reuse an icon with different semantics. Resource names below are proposed identifiers and can be adjusted during integration.

| Component | Proposed resource | Reason a dedicated icon is required | Composition |
| --- | --- | --- | --- |
| Boolean List to AI | `toaiboollist` | Reuses scalar `toaibool` | Boolean full glyph + list badge + to-AI badge |
| Integer List to AI | `toaiintegerlist` | Reuses scalar `toaiinteger` | Integer full glyph + list badge + to-AI badge |
| Number List to AI | `toainumericlist` | Reuses scalar `toainumeric` | Decimal-number full glyph + list badge + to-AI badge |
| JSON Get Value | `jsongetvalue` | Shares `jsonitem` with Set Value | JSON-object full glyph + value-out badge |
| JSON Set Value | `jsonsetvalue` | Shares `jsonitem` with Get Value | JSON-object full glyph + value-in badge |
| AI Web To Markdown | `aiwebtomd` | Shares `webtomd` with the deterministic Web To Markdown component | Globe full glyph + Markdown badge + green AI treatment |
| Canvas To Image | `canvascapture` | Reuses `aitoimg`, incorrectly implying AI image generation | Canvas-frame full glyph + capture badge |
| Viewport To Image | `viewportcapture` | Reuses `aitoimg`, incorrectly implying AI image generation | Perspective-viewport full glyph + capture badge |
| McNeel Post to AI | `toaimcneelpost` | Current `Icon => null` | Post full glyph + McNeel source badge + to-AI badge |
| McNeel Topic to AI | `toaimcneeltopic` | Current `Icon => null` | Topic full glyph + McNeel source badge + to-AI badge |
| Ladybug Post to AI | `toailadybugpost` | Current `Icon => null` | Post full glyph + Ladybug source badge + to-AI badge |
| Ladybug Topic to AI | `toailadybugtopic` | Current `Icon => null` | Topic full glyph + Ladybug source badge + to-AI badge |
| Apply GhPatch to Canvas | `ghpatchapplycanvas` | Current `Icon => null` | Canvas-network full glyph + apply-patch badge |
| Validate GhJSON | `ghvalidate` | Current `Icon => null` | GhJSON-document full glyph + validate badge |
| Save GhJSON | `saveghjson` | Current `Icon => null` | GhJSON-document full glyph + save badge |
| Open GhJSON | `openghjson` | Current `Icon => null` | GhJSON-document full glyph + open-file badge |
| Save GhPatch | `saveghpatch` | Current `Icon => null` | GhPatch-document full glyph + save badge |
| Open GhPatch | `openghpatch` | Current `Icon => null` | GhPatch-document full glyph + open-file badge |
| AI Smart Connect | `aighconnect` | Current `Icon => null` | Two-node network full glyph + connect badge + green AI treatment |
| AI Canvas Report | `aighreport` | Current `Icon => null` | Canvas-report full glyph + green AI badge |
| Combine Metrics | `metricscombine` | Current `Icon => null` | Metrics-chart full glyph + merge badge |
| SmartHopper MCP Server | `mcpserver` | No `Icon` override | Server-node full glyph + MCP/connection badge |

### 7. Color system

Color should accelerate recognition, but **never carry meaning alone**: each role also has a unique full glyph or badge silhouette. Use color on the principal glyph and the semantically important badge, with dark slate outlines and off-white cutouts for contrast.

| Role | Color | Hex | Use | Why |
| --- | --- | --- | --- | --- |
| Structural ink | Dark slate | `#28323C` | Outlines, internal dividers, small neutral details | Softer than black and provides a common visual skeleton |
| Configuration / context | Slate gray | `#707985` | Settings, extra settings, context, models, metadata, MCP infrastructure | Neutral: these components configure or describe rather than transform data |
| AI / generation | SmartHopper green | `#3FAE68` | AI spark, to/from-AI direction badges, generated outputs, AI Chat | Ownable brand cue and positive association; one consistent signal for provider-backed work |
| Mutation / transformation | Amber orange | `#E18432` | Set, merge, filter, sort, connect, patch, place, tidy, edit | Clearly warns that data or canvas state is being changed without implying an error |
| Deterministic helpers | Utility blue | `#347FB8` | JSON/GhJSON read-only tools, validation, conversion, viewers, capture, metrics | Conventional informational color; separates deterministic operations from AI generation |
| Knowledge / external content | Raspberry pink | `#C75887` | Web, file, Discourse, McNeel, Ladybug and research flows | Distinct from computational tools and visible at small sizes without looking like an error |
| Canvas / Grasshopper accent | Blue-green teal | `#258F91` | Canvas, viewport, component-network and GhJSON main glyphs | Gives Grasshopper-specific operations their own recognizable domain color |
| Knockout / highlight | Off-white | `#F4F6F8` | Interior cutouts, badge symbols, separation keylines | Remains readable on dark canvas themes |
| Error/status only | Red | `#C0392B` | Existing invalid-model runtime badge only | Reserve red for actual errors; never use it as a component-category color |

#### 7.1 Color application rules

1. Use at most **two semantic colors** in one icon, plus slate and off-white neutrals.
2. The **main glyph color identifies the domain**: gray configuration, pink knowledge, teal canvas/GhJSON, blue deterministic data/media, or green AI-first components.
3. The **action badge color identifies behavior**: green AI/generation, orange mutation, blue read/validate/convert, pink external retrieval.
4. A knowledge component that invokes AI uses a **pink main glyph + green AI badge**. A canvas mutation uses a **teal main glyph + orange mutation badge**.
5. Input adapters that only package existing data use the domain color for the main glyph and a green to-AI badge; this does not imply that conversion itself generates content.
6. Keep a dark-slate outer contour and off-white negative-space details so icons survive both light and dark Grasshopper themes.
7. Do not use gradients, shadows, semitransparent hairlines, or red/green color contrast as the only distinction.
8. Third-party source marks may keep recognizable brand shapes, but recolor them raspberry pink where trademark guidance permits. If recoloring is not permitted, use the official mark inside a consistent pink source-badge frame.

### 8. Deduplicated drawing inventory

Generate these drawings as reusable masters before composing the 24×24 icons. A drawing appears **once** in this list even when dozens of icons reuse it.

#### 8.1 Full-size drawings

Full-size drawings occupy approximately **14–18 px** of the 24×24 artboard and provide the icon's primary silhouette.

| ID | Drawing | Default color | Visual definition | Reused for |
| --- | --- | --- | --- | --- |
| F01 | SmartHopper mascot | Green | Simplified grasshopper head; no graduation-cap detail below 24 px | Brand, category, AI Chat, canvas button |
| F02 | Chat bubble | Green | One bold rounded speech bubble | AI Chat |
| F03 | Gear | Gray | Six-tooth gear with large center hole | Settings, Extra Settings |
| F04 | Context layers | Gray | Three offset stacked cards/layers | AIContext, Context Providers |
| F05 | Model catalog | Gray | Three connected model nodes or stacked chips | AI Models |
| F06 | Metadata document | Gray | Page with folded corner and one tag line | File Metadata |
| F07 | Prompt bubble | Blue | Speech/prompt card with one strong text line | AI Prompt |
| F08 | Text | Blue | Bold `T`-like typographic silhouette, custom drawn rather than a font dependency | Text adapters and text operations |
| F09 | List | Blue | Three thick horizontal rows with aligned bullets | Text/list components |
| F10 | Boolean | Blue | Toggle with check, readable without color | Boolean components |
| F11 | Integer | Blue | Bold `123` or `#` silhouette | Integer components |
| F12 | Decimal number | Blue | Bold `1.0` silhouette, visibly different from integer | Number components |
| F13 | JSON object | Blue | Heavy paired curly braces with central key dot | JSON components |
| F14 | JSON array | Blue | Heavy paired square brackets with two item marks | JSON array components |
| F15 | Image | Blue | Landscape frame with sun and one mountain | Image inputs, outputs and viewer |
| F16 | Audio / waveform | Blue | Speaker or microphone with two bold waveform arcs | Audio input, speech output and viewer |
| F17 | Generic file | Pink | Folded-corner page with one content line | File knowledge and file-to-AI |
| F18 | Web / globe | Pink | Globe with only two latitude/longitude divisions | Web knowledge and web-to-AI |
| F19 | Markdown document | Pink | Folded page with a large down-chevron/`M` shape; avoid tiny letters | Markdown output/conversion |
| F20 | Script / code | Blue | Bold paired angle brackets with central slash | Script output/generate/review |
| F21 | GhJSON document | Teal | Folded page containing a three-node Grasshopper graph | GhJSON input/output and document tools |
| F22 | GhPatch document | Teal | GhJSON document with one broken/replaced connection | GhPatch save/open/apply |
| F23 | Component network | Teal | Three rectangular nodes joined by wires | Retrieve, connect, put and canvas operations |
| F24 | Canvas frame | Teal | Rectangular canvas boundary containing two wired nodes | Canvas capture, patch-to-canvas, report |
| F25 | Perspective viewport | Teal | Trapezoidal frame containing a simple cube edge | Viewport capture |
| F26 | Forum post | Pink | Single message/post card with avatar dot and two lines | Post get/open/summarize/deconstruct/to-AI |
| F27 | Forum topic | Pink | Two overlapped post cards, visibly distinct from a single post | Topic summarize/to-AI |
| F28 | JSON schema | Blue | Braces around a simple two-level tree/blueprint | Schema, Schema Object, Schema Property |
| F29 | Metrics chart | Blue | Three unequal bars with a single rising line | Deconstruct/Combine Metrics |
| F30 | Media viewer | Blue | Rounded display frame with a prominent eye/play aperture | Image Viewer, Audio Viewer |
| F31 | Canvas report | Teal | Clipboard/page containing a two-node canvas and one check line | AI Canvas Report |
| F32 | MCP server | Gray | Compact server stack with one network port/node | SmartHopper MCP Server |
| F33 | Scattered components | Teal | Three displaced component rectangles | Tidy Up before-state |

#### 8.2 Badge-size drawings

Badge drawings occupy approximately **7–9 px**, use filled geometric silhouettes, and normally sit bottom-right. Source/type modifiers sit bottom-left. Draw every badge on the same nominal 9×9 artboard with at least a 1 px clear zone.

| ID | Drawing | Default color | Visual definition | Reused for |
| --- | --- | --- | --- | --- |
| B01 | AI spark | Green | Four-point asymmetric spark with a solid center | Generic AI-powered marker |
| B02 | To AI | Green | Short right-pointing arrow terminating in a spark | Every `toai-*` input |
| B03 | From AI | Green | Spark followed by a short right-pointing arrow | Every `aito*` output |
| B04 | Generate | Green | Spark + short outward ray; simpler than B03 | Text/image/script generation |
| B05 | List modifier | Blue | Three tiny but thick rows or paired brackets | Scalar/list differentiation |
| B06 | Markdown | Pink | Bold down-chevron joined to one stem; not tiny `MD` text | Web/File/AI to Markdown |
| B07 | Discourse source | Pink | Circular speech-bubble silhouette | Generic Discourse family |
| B08 | McNeel source | Pink | Simplified rhino-head profile or licensed `M` monogram | McNeel family |
| B09 | Ladybug source | Pink | Round beetle split by one center line and two dots | Ladybug family |
| B10 | Search | Pink | Bold magnifier | Forum search, component discovery |
| B11 | Retrieve / get | Blue | Arrow entering a shallow tray | Post Get, Get GhJSON |
| B12 | External open | Pink | Northeast arrow leaving an open corner | Forum Post Open |
| B13 | Open file | Blue | Open folder with short upward page edge | Open GhJSON/GhPatch |
| B14 | Save file | Blue | Down arrow entering a page/tray; avoid obsolete floppy detail at 8 px | Save GhJSON/GhPatch |
| B15 | Summarize | Green | Three lines contracting from long to short with a spark | AI post/topic summaries |
| B16 | Deconstruct | Blue | One stem splitting into three outward branches | Post/metrics deconstruct |
| B17 | Merge | Orange | Two arrows converging into one | JSON/GhJSON/metrics combine |
| B18 | Diff / compare | Blue | Two parallel bars with one offset segment | Diff GhJSON |
| B19 | Patch | Orange | One bold puzzle tab or broken wire bridged by a square | Apply GhPatch |
| B20 | Apply to canvas | Orange | Patch tab + short arrow into a corner frame | Apply GhPatch to Canvas |
| B21 | Place on canvas | Orange | Down/right arrow landing on a two-node baseline | Place GhJSON |
| B22 | Validate | Blue | Shield/check silhouette | Validate GhJSON |
| B23 | Filter / sort | Orange | Funnel with one downward item | AI List Filter |
| B24 | Evaluate | Green | Check and question-dot combined into one silhouette | Text/List to Boolean |
| B25 | Review | Green | Magnifier containing a check | AI Script Review |
| B26 | Edit | Orange | Short diagonal pencil | Script edit/generation where modification is possible |
| B27 | Value out | Blue | Key dot inside braces + arrow out | JSON Get Value |
| B28 | Value in | Orange | Arrow into key dot inside braces | JSON Set Value |
| B29 | Clean / sanitize | Blue | Small broom/sweep with one sparkle | JSON Sanitizer |
| B30 | Schema object | Blue | Tiny boxed braces | JSON Schema Object |
| B31 | Schema property | Blue | Tiny key/tag shape | JSON Schema Property |
| B32 | Convert to text | Blue | Short arrow ending at one thick text line | JSON Object to Text |
| B33 | Convert to list | Blue | Short arrow ending at three rows | JSON Array to Text List |
| B34 | Capture | Blue | Camera shutter formed from four bold wedges | Canvas/Viewport capture |
| B35 | View | Blue | Simple almond eye with solid pupil | Image Viewer |
| B36 | Tidy / align | Orange | Three aligned squares over one baseline | Tidy Up after-state |
| B37 | Connect | Orange | Two plugs or node endpoints joined by one curved wire | AI Smart Connect |
| B38 | Report | Green | Small clipboard/check | AI Canvas Report |
| B39 | Extra / advanced | Gray | Three slider stems with offset knobs | Extra Settings |
| B40 | Providers / registry | Gray | Three short rows with connection dots | Context Providers / AI Models |
| B41 | MCP connection | Gray | Two linked nodes with a central port | MCP Server |

#### 8.3 Runtime badges drawn by code

These are not PNG assets but should be included in the visual design handoff because they appear next to the generated icons. Keep their existing semantics and colors:

| Badge | Size | Color | Shape |
| --- | --- | --- | --- |
| Verified model | Runtime 16×16 | Green `#209848` | Filled circle + white check |
| Deprecated model | Runtime 16×16 | Purple `#9B59B6` | Filled circle + white downward arrow |
| Invalid model | Runtime 16×16 | Red `#C0392B` | Filled circle + white cross |
| Not recommended | Runtime 16×16 | Orange `#E67E22` | Filled octagon + white exclamation |
| Replaced model | Runtime 16×16 | Blue `#3498DB` | Filled circle + white refresh arrow |
| Provider identity | Runtime 16×16 | Provider brand | Official provider logo in the bottom strip; do not regenerate |

### 9. Gaps and cleanup notes

- **14 components currently ship without icons** (`Icon => null` or no override): the 4 forum `*2AI` inputs, 4 GhJSON/GhPatch file operations, `GhValidate`, `GhPatchApplyToCanvas`, `AIGhConnect`, `AIGhReport`, `CombineMetrics`, and `SmartHopperMcpServer`.
- **Eight additional components require new dedicated compositions** because they currently reuse an icon with different semantics: Boolean/Integer/Number List to AI, JSON Get Value, JSON Set Value, AI Web To Markdown, Canvas To Image, and Viewport To Image.
- `websummarize.png`, `ghjsonai.png`, `imggenerate.png`, and `toai-speech.png` are currently orphaned. Reuse a generated replacement only if its final meaning exactly matches a required composition; otherwise remove the resource during integration.
- Legacy `Resources.resx` aliases (`text2ai`, `web2ai`, `file2ai`, `audio2ai`, `img2ai`, `json2ai`, `list2ai`, `ghjson2ai`, the forum `*2ai` aliases, and `aiprompt`) should be consolidated after all code references are migrated.
- Template/junk entries `Bitmap1`, `Color1`, `Icon1`, and `Name1` should not be recreated.
- Chat UI uses an inline chart emoji for metrics; it is outside the Grasshopper component icon set but should eventually use the same F29 metrics drawing for product consistency.

### 10. Suggested generation prompts

Use one style preamble for every full-size drawing:

> "Reusable master glyph for a cohesive Grasshopper plugin icon set. Draw on a transparent 24×24 artboard; bold 14–18 px central silhouette; dark-slate outline; rounded joins; off-white negative-space details; flat color; no gradient or shadow; no fragile line below 1.5 px; readable when reduced to 16 px; no decorative background tile. Use the specified semantic palette exactly."

Use this preamble for every badge:

> "Reusable semantic badge glyph on a transparent 9×9 artboard with a 1 px clear zone; one bold filled silhouette; maximum two internal details; rounded joins; recognizable at 7 px; no text unless explicitly required; no gradient or shadow."

Compose each final icon with:

> "Transparent 24×24 icon. Main glyph: {full-size drawing ID}. Bottom-left modifier: {badge ID or none}. Bottom-right action: {badge ID or none}. Apply domain color {color} to the main glyph and behavior color {color} to the action. Preserve clear separation between main glyph and badges. Use no more than two badges and no more than two semantic colors plus slate/off-white."
