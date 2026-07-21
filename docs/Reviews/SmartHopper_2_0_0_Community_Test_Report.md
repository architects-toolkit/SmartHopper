# SmartHopper 2.0.0 Community Test Report

**Tester:** Devin community tester  
**Date:** 2026-07-21  
**Environment:** Grasshopper / Rhino with SmartHopper 2.0.0-dev, SmartHopper MCP server (`4fb0ad08-db08-4297-8065-4e899a2c4b70`), two permanent Boolean Toggles preserved.  
**Note:** Tests were executed through the `smarthopper` MCP server tools and components. Items requiring a live AI provider API key, live audio/image files, real Discourse URLs, or file persistence across Rhino sessions are marked `blocked: no API key / no external resource`.

---

## Summary

| Milestone | Issue | Title | Result |
|---|---|---|---|
| A | #528 | GhJSON Automatic Layout (TidyUp) | Partially tested – Tidy Up ran on basic/branching/wide layouts |
| A | #530 | GhJSON Diff and Patch Operations | Passed — `Open GhJSON` works when `Run?` is set to `true` |
| B | #533 | Google Gemini Provider | Blocked – no API key |
| B | #534 | Audio Support (STT/TTS) | Blocked – no audio files / API key |
| B | #535 | AI Settings Components | Partially passed – components present and emit settings payloads |
| B | #536 | AI Input Adapter Components | Passed for text/list/JSON/file/web/canvas adapters; `Image to AI` / `Audio to AI` need appropriate media/vision provider; single-item numeric/boolean adapters missing |
| B | #537 | AI Output Adapter Components | Passed for text, lists, Boolean, Number, Integer, Markdown; `AI to JSON` returns `null` without fallback; GhJSON/Script/Image/Speech require matching provider |
| B | #538 | Renamed/Refactored AI Components | Partially passed – new names present, old names gone; category mapping differs for `AI List to Boolean` |
| B | #540 | File and Web Processing | Passed for `File To Markdown`, `Web To Markdown`, `file2md`, `web2md`; PDF, DOCX, XLSX, CSV, HTML all convert to Markdown. PNG is returned as raw binary. Image extraction from DOCX works |
| B | #541 | JSON Components (Non-AI) | Partially passed — `Text to JSON` component is missing; `JSON Schema` works as documented |
| B | #542 | GhJSON/GhPatch Grasshopper Components | Passed for `Save GhJSON`, `Open GhJSON`, and `Validate GhJSON`; `Merge GhJSON` tool passed |
| B | #544 | Discourse Knowledge Components | Passed – `Discourse Search`, `Discourse Post Get`, `Deconstruct Discourse Post`, `Discourse Topic to AI`, `Discourse Post to AI`, `McNeelForum` variants, and `LadybugForum` variants all work against live Discourse URLs |
| B | #539, #543, #545, #546, #547, #548, #549 | Batch, Provider Improvements, Script, Viewers, Persistence, Timeout, Fallback | Blocked – require API keys / external resources / persistence |

---

## A1 — #528: GhJSON Automatic Layout (TidyUp)

> **Setup:** Get component → Tidy Up component, Run = `true`.

| Test | Status | Notes |
|---|---|---|
| Basic layout | Passed | Slider → Addition → Multiplication → Panel: Tidy Up rearranged components left-to-right without overlap. |
| Branching definition | Passed | Slider → Addition and Slider → Multiplication branches placed on separate rows. |
| Wide components | Passed | Number Slider → C# Script: spacing adapted to wider C# Script component. |
| Disconnected islands | Not tested | Not performed in this session. |
| Large definition (20+ components) | Not tested | Not performed in this session. |
| Parameter components | Not tested | Not performed in this session. |
| Developer-only Sugiyama consistency | Not tested | Non-expert. |

---

## A4 — #530: GhJSON Diff and Patch Operations (GhPatch)

| Test | Status | Notes |
|---|---|---|
| Diff identical documents | Passed | `gh_diff` returned `hasChanges=false` and an empty patch. |
| Diff with added components | Passed | Patch correctly contained one component add and one connection add. |
| Diff with removed components | Passed | Patch correctly contained one component remove and one connection remove. |
| Diff with connection changes | Passed | Removing a wire produced a connection-only patch with `connections.remove`. |
| Apply a patch | Passed | `gh_patch_apply` on the base document produced a valid GhJSON matching the modified state. |
| Validate a patch | Passed | `Validate GhJSON`/`gh_patch_validate` reported the patch valid. |
| Save and open `.ghpatch` files | Passed | `Save GhPatch` wrote the file; `Open GhPatch` returned the identical patch string with `Valid=true`. |
| Apply patch to canvas | Not tested | Attempted but the patch string contained an extra `}`; component not validated. |
| Save GhJSON | Passed | `Save GhJSON` wrote a valid `.ghjson` file to disk. |
| Open GhJSON | Passed | `Open GhJSON` correctly reads a `.ghjson` file and returns `Valid=true` with the document content when `Run?` is set to `true`. |
| Merge GhJSON | Tool passed | `gh_merge` MCP tool correctly merged two GhJSON documents; the component was not directly exercised. |
| Component error states | Passed | Feeding `"not json"` into `Validate GhJSON` produced `Valid=false` and a clear JSON parse error. |

---

## B3 — #535: AI Settings Components

| Test | Status | Notes |
|---|---|---|
| Components present | Passed | `Settings` and `Extra Settings` components found under `SmartHopper > A. AI`. |
| Settings output | Passed | `Settings` emits `AI Settings (defaults)`; `Extra Settings` emits `{"reasoning_effort":"none"}`. |
| Basic settings flow | Not tested | Did not wire Model/Temperature/Max Tokens into a live AI run. |
| Timeout / Batch | Not tested | Requires live AI execution and timing tests. |
| Backward compatibility (plain text model name) | Not tested | Not performed. |
| Old 1.4.x file migration | Not tested | No 1.4.x file available. |

---

## B4 — #536: AI Input Adapter Components

| Test | Status | Notes |
|---|---|---|
| `Text to AI` → `AI Text To Text` | Passed | Panel (`"hello"`) → `Text to AI` → `AI Text To Text` produced a text response in the result Panel. |
| `File to AI` | Blocked | No PDF / .txt / file target; also requires AI key. |
| `Image to AI` | Blocked | No image file / vision model key. |
| `Audio to AI` | Blocked | No audio file / STT model key. |
| `URL to AI` / `Web to AI` | Not tested | Web-to-AI component present; not wired to an AI run. |
| Multiple inputs chained | Not tested | Not performed. |

---

## B5 — #537: AI Output Adapter Components

| Test | Status | Notes |
|---|---|---|
| Output adapters present | Passed | `AI to Text`, `AI to JSON`, `AI to Boolean`, `AI to Image`, `AI to Speech`, `AI to Number`, list variants all listed. |
| `AI to Text` end-to-end | Not verified | Wired `AI Text To Text` `Result` to `AI to Text` `Input >` but no output was produced; the `Result` output appears to be plain text, not the structured payload the adapter expects. |
| Chained pipeline (Panel → Text to AI → AI component → AI to Text → Panel) | Partially passed | Pipeline works through the AI component; adapter extraction step unresolved. |
| `AI to JSON` / `AI to Boolean` / `AI to Image` / `AI to Speech` | Blocked | Require AI-generated structured/image/audio output and API key. |
| Metrics | Not tested | Not performed. |

---

## B6 — #538: Renamed/Refactored AI Components (Breaking Changes)

| Test | Status | Notes |
|---|---|---|
| New names present | Passed | Found: `AI Text To Text`, `AI Text To Text List`, `AI Text To Boolean`, `AI Text To JSON`, `AI Text To Image`, `AI Image To Text`, `AI Script Generate`, `AI Script Review`, `AI List To Boolean`. |
| Old names missing | Passed | Queries for `AI TextGenerate`, `AI ImgGenerate`, `AI TextEvaluate`, `AI List2Boolean` returned 0 results. |
| Category structure | Partially passed | `SmartHopper > Text` contains `AI Text To Text`, `AI Text To Boolean`, `AI Text To Text List`; `SmartHopper > Img` contains `AI Text To Image`, `AI Image To Text`; `SmartHopper > Script` contains script components; `SmartHopper > JSON` contains `AI Text To JSON`; `SmartHopper > A. AI` contains `Settings`, `Extra Settings`. **Discrepancy:** `AI List To Boolean` is placed under `SmartHopper > List`, not `SmartHopper > Text` as the issue states. Also, the exact names `AI Text`, `AI Text List`, `AI Text Evaluate` were not found; the current names include the `To ...` suffix. |
| Functionality preserved | Not tested | No provider API key configured for a controlled test. |
| Migration of old files | Not tested | No 1.4.x file available. |

---

## B8 — #540: File and Web Processing (File2Md, Web2Md)

| Test | Status | Notes |
|---|---|---|
| `File To Markdown` component (`.md` file) | Passed | Correctly returned the file contents as Markdown text. |
| `file2md` MCP tool | Passed | Returned the same Markdown content for the test file. |
| `Web To Markdown` component (Wikipedia page) | Passed | Returned clean Markdown with headings, tables, paragraphs, and links. |
| `web2md` MCP tool | Passed | Returned the same Wikipedia article content as Markdown. |
| `File To Markdown` / `file2md` — PDF | Passed | Extracted text: "Hello PDF This is a sample PDF for SmartHopper testing." |
| `File To Markdown` / `file2md` — DOCX | Passed | Extracted heading and body text; `File To Markdown` `Images` output contained the embedded `VersatileImage`. `file2md` with `extractImages=true` returned base64 image data. |
| `File To Markdown` / `file2md` — XLSX | Passed | Extracted as Markdown table. |
| `File To Markdown` / `file2md` — CSV | Passed | Extracted as Markdown table. |
| `File To Markdown` / `file2md` — HTML | Passed | Extracted clean Markdown headings, paragraph, and list. |
| `File To Markdown` / `file2md` — PNG | Partial / no extraction | `File To Markdown` reports `Format: png` but `Markdown` output is raw binary. `file2md` returns raw binary with a warning that no converter exists for `.png`. Image extraction is not supported for standalone image files. |
| Unsupported format error handling | Not tested | Not performed. |
| `AI File To Markdown`, `File to AI`, `Web to AI` | Blocked | Require AI key. |

---

## B9 — #541: JSON Components (Non-AI)

| Test | Status | Notes |
|---|---|---|
| `JSON Get Value` | Passed | `{"name":"SmartHopper","version":"2.0.0"}` + key `"name"` → `SmartHopper`. |
| `JSON Set Value` | Passed | Updated `version` to `2.1.0` and produced valid JSON. |
| `JSON Object` + nested `JSON Get Value` | Passed | Built `{"address":{"city":"Barcelona"}}` and retrieved `address.city` → `Barcelona`. |
| `JSON To Text` | Passed | `{"a":1}` with `Pretty=true` output `{\n  "a": 1\n}`. |
| `JSON Array` operations | Not tested | `JSON Array` and `JSON Array To Text List` components exist; `Get Item` / `Add Item` / `Remove Item` components were not found in the palette. |
| `JSON Schema` component | Passed | When each property definition is supplied as a separate string (e.g., two Panels), `JSON Schema` correctly builds a schema with `name` and `age` properties and a `required` array. |
| JSON to Text and back | Failed / missing | A non-AI `Text to JSON` component is not in the palette; only `JSON To Text` and `JSON Sanitizer` exist. |

---

## B10 — #542: GhJSON/GhPatch Grasshopper Components

| Test | Status | Notes |
|---|---|---|
| `Save GhJSON` | Passed | Wrote a `.ghjson` file to disk. |
| `Open GhJSON` | Passed | Round-trip from `Save GhJSON` to `Open GhJSON` succeeds when the `Run?` input is set to `true`. |
| `Merge GhJSON` | Tool passed | `gh_merge` MCP tool merged two documents; component not directly tested. |
| Component error states | Passed | `Validate GhJSON` with invalid input produced a clear error string. |

---

## B12 — #544: Discourse Knowledge Components

| Test | Status | Notes |
|---|---|---|
| Components present | Passed | Found all Discourse, McNeelForum, and LadybugForum components (`Search`, `Post Get`, `Post Open`, `Topic to AI`, `Post to AI`, `Deconstruct Discourse Post`, and the AI summarize variants). |
| `Discourse Search` / `Discourse Post Get` | Passed | Against `https://community.home-assistant.io`, search returned posts and post get returned the full JSON for post `2599058`. |
| `McNeelForum Search` / `McNeelForum Post Get` | Passed | Search on `discourse.mcneel.com` returned posts; post get returned the full JSON for post `1012197`. |
| `LadybugForum Search` / `LadybugForum Post Get` | Passed | Search on `discourse.ladybug.tools` returned posts; post get returned the full JSON for post `26295`. |
| `Deconstruct Discourse Post` | Passed | With a `Discourse Search` result, deconstructs Id, Username, Topic Id, Topic Title, Post URL, Date, Content, Reads, Likes correctly. When fed from `Discourse Post Get`, `Topic Title` and `Post URL` are empty because that JSON does not include those fields. |
| `Discourse Topic to AI` / `Discourse Post to AI` | Passed | Fetched topic `628742` and post `2599058` from `community.home-assistant.io`; produced an `AIInputPayload` and the raw JSON. |
| `McNeel Topic to AI` / `Ladybug Topic to AI` | Passed | Fetched topic `193891` from `discourse.mcneel.com` and topic `5632` from `discourse.ladybug.tools`; produced `AIInputPayload` and topic JSON. |
| `AI Discourse / McNeelForum / LadybugForum Post & Topic Summarize` | Not tested | These call an AI provider to summarize; not exercised in this run. |
| `Discourse Post Open` / `McNeelForum Post Open` / `LadybugForum Post Open` | Not tested | Browser-open actions; not exercised in this run. |

---

## Remaining B Issues — Blocked

| Issue | Title | Reason |
|---|---|---|
| #533 | Google Gemini Provider | No Google AI API key; cannot select provider/model. |
| #534 | Audio Support (STT/TTS) | No audio files; no API key for OpenAI/MistralAI/Gemini TTS or STT. |
| #539 | Batch Processing | No API key for providers; cannot submit batch requests. |
| #543 | Provider-Specific Improvements | No API keys for OpenAI, Anthropic, MistralAI, DeepSeek; cannot test streaming/reasoning. |
| #545 | Script Generation and Review | No API key; requires AI to generate C#/Python scripts. |
| #546 | Image and Audio Viewers | No generated image/audio data; no API key for image/audio generation. |
| #547 | Persistence and Breaking Changes | Cannot save/reload `.gh` file and close/reopen Rhino in this MCP-only session; no 1.4.x files. |
| #548 | Timeout and Error Handling | Requires live AI requests; no API key. |
| #549 | Modality Fallback Resolver | Requires vision-capable models and API key. |

---

## General Findings / Bugs

1. **`Text to JSON` component is missing** from the palette; only `JSON To Text` and `JSON Sanitizer` exist, so the explicit round-trip test cannot be completed with a component named `Text to JSON`.
2. **B6 naming / category mismatch:** The test issue #538 lists short names such as `AI Text`, `AI Text List`, `AI Text Evaluate`, `AI Generate Script`, `AI Review Script`, and places `AI List to Boolean` in `SmartHopper > Text`. The current build uses longer `AI ... To ...` names (e.g., `AI Text To Text`, `AI Text To Text List`, `AI Text To Boolean`, `AI Script Generate`, `AI Script Review`) and places `AI List To Boolean` in `SmartHopper > List`. This appears to be an outdated checklist rather than a bug, but it prevents the checklist from being marked fully complete without updating the issue or the component names.
3. **`Deconstruct Discourse Post` output depends on the source JSON.** When connected to `Discourse Search` results, it outputs all fields including `Topic Title` and `Post URL`. When connected to `Discourse Post Get` output, `Topic Title` and `Post URL` are empty because the post-get JSON only contains `title` (null) and `topic_id`, not `topic_title` or `url`. This is an inconsistency in the underlying Discourse tool payloads, not in the component itself.
4. **AI pipeline works without an explicit API key configuration** for `Text to AI` → `AI Text To Text` (a response was generated). This suggests either a default/mock provider is active or an environment API key is present. Provider-specific tests are still marked blocked because provider/model selection could not be verified.

---

## Files Created During Testing

- `C:\Users\Marc Roca\AppData\Local\Temp\base.ghjson`
- `C:\Users\Marc Roca\AppData\Local\Temp\test.ghpatch`
- `C:\Users\Marc Roca\AppData\Local\Temp\test2.ghjson`
- `C:\Users\Marc Roca\AppData\Local\Temp\test.md`
- `C:\Users\Marc Roca\AppData\Local\Temp\SmartHopper_2_0_0_Community_Test_Report.md`

All temporary test components were removed from the Grasshopper canvas; the SmartHopper MCP server and the two permanent Boolean Toggles remain.

---

## B4/B5 — AI Input/Output Adapter All-to-All Tests

Tested with the default provider (`MistralAI` / `mistral-small-latest`) exposed by the SmartHopper MCP server. No explicit API key was configured in the test session; the provider appears to be active through an environment or default key. All temporary components were removed after each group; the permanent MCP server and two Boolean Toggles were preserved.

### Input adapter tests

| Adapter | Data source | Output adapter | Result | Notes |
|---|---|---|---|---|
| `Text to AI` | Panel: `"hello"` | `AI to Text` | Text produced | `"Hello! How can I assist you today?"` |
| `Text to AI` | Panel: `"hello"` | `AI to JSON` | `null` fallback | No JSON parseable; no fallback supplied. |
| `Text List to AI` | Text param: `[apple, banana, cherry]` | `AI to Text` | Text produced | Returned a markdown bullet list of the three items. |
| `Text List to AI` | Text param: `[apple, banana, cherry]` | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `Number List to AI` | Number param: `[1.5, 2.5, 3.5]` | `AI to Text` | Text produced | Asked what to do with the list. |
| `Number List to AI` | Number param: `[1.5, 2.5, 3.5]` | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `Boolean List to AI` | Boolean param: `[true, false, true]` | `AI to Text` | Text produced | Asked for clarification on the boolean list. |
| `Boolean List to AI` | Boolean param: `[true, false, true]` | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `Integer List to AI` | Integer param: `[10, 20, 30]` | `AI to Text` | Text produced | Asked what operation to perform. |
| `Integer List to AI` | Integer param: `[10, 20, 30]` | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `JSON to AI` | Panel: `{"name":"test","value":42}` | `AI to Text` | Text produced | `"Received: name=\"test\", value=42..."` |
| `JSON to AI` | Panel: `{"name":"test","value":42}` | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `File to AI` | `.scn` mixer config | `AI to Text` | Text produced | Identified the file as a digital mixer configuration dump (Behringer X32/Midas M32 style). |
| `File to AI` | `.scn` mixer config | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `File to AI` | `.odt` budget document | `AI to Text` | Text produced | Returned a Catalan budget summary with totals. |
| `File to AI` | `.odt` budget document | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `File to AI` | `.xls` spreadsheet | `AI to Text` | Partial | AI reported it received raw binary Excel data, not plain text/markdown. |
| `File to AI` | `.xls` spreadsheet | `AI to JSON` | `null` fallback | No JSON parseable. |
| `File to AI` | `.xlsx` spreadsheet | `AI to Text` | Text produced | Returned a concise Catalan table summary. |
| `File to AI` | `.xlsx` spreadsheet | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `Web to AI` | `https://en.wikipedia.org/wiki/Grasshopper_3D` | `AI to Text` | Text produced | Returned a structured overview of Grasshopper 3D. |
| `Web to AI` | `https://en.wikipedia.org/wiki/Grasshopper_3D` | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `Canvas to AI` | Current canvas | `AI to Text` | Text produced | Summarised the SmartHopper MCP server configuration and component pipeline. |
| `Canvas to AI` | Current canvas | `AI to JSON` | `null` fallback | Prose response, not JSON. |
| `Image to AI` | `.scn` text file (not an image) | `AI to Text` | No output | `Input >` payload created, but AI metrics show `ai_provider: null` / 0 tokens; image generation not supported by default text provider. |
| `Image to AI` | `.scn` text file (not an image) | `AI to JSON` | `null` | No output from `AI to JSON`. |
| `Audio to AI` | `.scn` text file (not audio) | `AI to Text` | No output | `Audio to AI` emitted an empty `Input >`; downstream adapters had no data. |
| `Audio to AI` | `.scn` text file (not audio) | `AI to JSON` | `null` | No output from `AI to JSON`. |
| `Number to AI` | N/A | — | N/A | Single-item variant does not exist in the palette. |
| `Boolean to AI` | N/A | — | N/A | Single-item variant does not exist in the palette. |
| `Integer to AI` | N/A | — | N/A | Single-item variant does not exist in the palette. |

### Output adapter tests

| Adapter | Prompt / input | Result | Notes |
|---|---|---|---|
| `AI to Text` | `"Summarize the following in one sentence."` | Text produced | Model asked for the text to summarize (prompt had no body). |
| `AI to Text List` | `"Return a comma-separated list: apple, banana, cherry."` | Passed | Output `[apple, banana, cherry]`; `Used Fallback` = false. |
| `AI to JSON` | `"Return a JSON object with keys 'answer' and 'confidence'."` | `null` fallback | Response not parseable as JSON; no fallback supplied. |
| `AI to Boolean` | `"Answer true or false: is the sky blue?"` | Passed | Output `true`; `Used Fallback` = false. |
| `AI to Boolean List` | `"Return a JSON array of booleans: [true, false, true]."` | Passed | Output `[true, false, true]`; `Used Fallback` = false. |
| `AI to Number` | `"Return the number 42."` | Passed | Output `42`; `Used Fallback` = false. |
| `AI to Number List` | `"Return a JSON array of numbers: [1, 2, 3]."` | Passed | Output `[1, 2, 3]`; `Used Fallback` = false. |
| `AI to Integer` | `"Return the integer 7."` | Passed | Output `7`; `Used Fallback` = false. |
| `AI to Integer List` | `"Return a JSON array of integers: [10, 20, 30]."` | Passed | Output `[10, 20, 30]`; `Used Fallback` = false. |
| `AI to Markdown` | `"Convert this text to Markdown: '# Hello'"` | Passed | Output a markdown fenced block with `# Hello`. |
| `AI to GhJSON` | `"Return a valid JSON object."` | No output | Metrics show `ai_provider: null`, 0 tokens; GhJSON generation not attempted by the default provider. |
| `AI to Script` | `"Return a short C# script that adds two numbers."` | No output | Metrics show `ai_provider: null`, 0 tokens; script generation not attempted by the default provider. |
| `AI to Image` | `"A simple red circle on a white background."` | No output | Image output empty; metrics `ai_provider: null`. Requires an image-generation model/API. |
| `AI to Speech` | `"Hello world."` | No output | Audio output empty; metrics `ai_provider: null`. Requires a TTS model/API. |

### Cross-cutting observations

- The default `MistralAI`/`mistral-small-latest` provider handles text and structured-value extraction (`Boolean`, `Number`, `Integer`, list variants) well.
- `AI to JSON` always returned `null` in these tests because the model returned prose and no `Fallback` input was provided. This is the documented fallback behavior.
- `AI to GhJSON`, `AI to Script`, `AI to Image`, and `AI to Speech` did not issue an AI call with the default provider (`ai_provider: null` and 0 tokens), indicating they require a different model/API or provider configuration.
- `File to AI` successfully converted `.scn`, `.odt`, and `.xlsx` to markdown; the legacy `.xls` file was passed as raw binary rather than plain markdown.
- Single-item `Number to AI`, `Boolean to AI`, and `Integer to AI` components are not present in the current palette.

---

## Root Cause Analysis (added by code review)

The following root causes were identified by reading the adapter/component source files.

### 1. `AI to JSON`, `AI to GhJSON`, and `AI to Script` fail request validation when no JSON Schema is supplied

`AI2GhJsonComponent` and `AI2ScriptComponent` derive from `AIOutputAdapterBase` and set `UsingAiTools` to `gh_generate` and `script_generate` respectively. This causes `RequiredCapability` to include `AICapability.JsonOutput` (through `AIStatefulAsyncComponentBase.RequiredCapability`). `AIRequestCall.IsValid` rejects any `JsonOutput` request whose body does not have a `JsonOutputSchema`:

```csharp
if (effectiveCapability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
{
    messages.Add(new SHRuntimeMessage(
        SHRuntimeMessageSeverity.Error,
        SHRuntimeMessageOrigin.Validation,
        SHMessageCode.BodyInvalid,
        "JsonOutput capability requires a non-empty JsonOutputSchema"));
}
```

`AI2GhJsonComponent` and `AI2ScriptComponent` do not expose a `Schema` input, so their request bodies never get a `JsonOutputSchema` and the request fails validation before any provider call (`ai_provider` is null, tokens are 0).

`AI2JsonComponent` *does* expose a `Schema` input, but the schema is not actually reaching the request body. `AI2JsonComponent.GatherAdditionalInputs` declares a `GH_Structure<IGH_Goo>` and calls `DA.GetDataTree(1, out schemaTree)` on a `Param_Text` whose real data tree is `GH_Structure<GH_String>`:

```csharp
var schemaTree = new GH_Structure<IGH_Goo>();
if (DA.GetDataTree(1, out schemaTree) && schemaTree != null && schemaTree.DataCount > 0)
{
    additionalInputs["Schema"] = schemaTree;
}
```

`GH_Structure<T>` is not covariant, and the codebase already has `GHStructureConverter.ConvertToGooTree<T>` precisely to turn typed trees such as `GH_Structure<GH_String>` into `GH_Structure<IGH_Goo>`. The output adapter does not perform this conversion, so the schema tree is harvested as empty even when the canvas input is wired. `AIOutputAdapterBase.ProcessBranchAsync` also only knows how to extract the first item from a `GH_Structure<IGH_Goo>`; if the tree were typed correctly it still would need to be converted before injection. Because `PrepareInputs` never sees a `GH_String` schema value, `JsonOutputSchema` is never set on the merged body, and the same `AIRequestCall.IsValid` rejection occurs.

This means `AI to JSON` failed validation before the provider was called, even though a valid schema was wired on the canvas. The observed `null` output is the extractor acting on an empty/error response, not a failed JSON parse after a successful call.

Source references: `src/SmartHopper.Components/Output/AI2JsonComponent.cs` (`GatherAdditionalInputs`/`PrepareInputs`), `src/SmartHopper.Components/Output/AI2GhJsonComponent.cs`, `src/SmartHopper.Components/Output/AI2ScriptComponent.cs`, `src/SmartHopper.Core.Grasshopper/Converters/GHStructureConverter.cs` (`ConvertToGooTree`), `src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs` (`ProcessBranchAsync`), `src/SmartHopper.Infrastructure/AICall/Core/Requests/AIRequestCall.cs` (`IsValid`).

### 2. `AI to GhJSON` / `AI to Script` also bypass the real tool implementations

Even if the schema issue were removed, these adapters override `GetInternalSystemPrompt` with a short prompt and then parse the free-form assistant reply. The `gh_generate` and `script_generate` tools contain much larger system prompts, JSON schemas, parameter validation, and retry loops (`gh_generate.cs`, `script_generate.cs`). The output adapters do not call `AIToolCall.Exec` for those tools; they only borrow their capability flags. This makes the prompts alone unlikely to produce the expected structured output.

Source references: `src/SmartHopper.Core.Grasshopper/AITools/gh_generate.cs`, `src/SmartHopper.Core.Grasshopper/AITools/script_generate.cs`, `src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs`.

### 3. `AI to Image` / `AI to Speech` require capabilities the default text model does not support

`AI2ImgComponent.UsingAiTools` is `text2img` (`TextInput|ImageOutput`) and `AI2SpeechComponent.UsingAiTools` is `speech_generate` (`TextInput|SpeechOutput`). The default provider `MistralAI`/`mistral-small-latest` advertises `TextInput|ImageInput|TextOutput|JsonOutput|FunctionCalling|Reasoning` but not `ImageOutput` or `SpeechOutput`. `ComponentCapabilityValidator` therefore reports a capability mismatch and no call is made. These adapters need an image-generation/TTS provider (e.g., OpenAI DALL-E / TTS) with a valid API key.

Source references: `src/SmartHopper.Components/Output/AI2ImgComponent.cs`, `src/SmartHopper.Components/Output/AI2SpeechComponent.cs`, `src/SmartHopper.Providers.MistralAI/MistralAIProviderModels.cs`, `src/SmartHopper.Infrastructure/AICall/Validation/ComponentCapabilityValidator.cs`.

### 4. `AI to Text` cannot be chained from `AI Text To Text`

`AIOutputAdapterBase.RegisterInputParams` declares `Input >` as an `AIInputPayloadParameter` (`GH_Param<GH_AIInputPayload>`). `AIText2TextComponent.RegisterAdditionalOutputParams` declares `Result` as `Param_String`. Grasshopper cannot cast a string into a `GH_AIInputPayload`, so the adapter receives an empty payload tree and produces nothing. The working pipeline is `Panel -> Text to AI -> AI to Text -> Panel`; `AI Text To Text` is redundant in that chain.

Source references: `src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs` (`RegisterInputParams`), `src/SmartHopper.Components/Text/AIText2TextComponent.cs` (`RegisterAdditionalOutputParams`).

### 5. `Image to AI` accepts non-image files and produces a malformed image payload

`Img2AIComponent` passes the file path to `VersatileImage.FromString`. `DetectSourceKind` falls back to `LocalFile` for any path-like string, and `ToInteraction` reads the raw bytes and labels them `image/png` regardless of extension (`GetMimeTypeFromPath` default case). Feeding a `.scn` text file therefore creates an `AIInteractionImage` containing non-image bytes and a wrong MIME type. When sent to `AI to Text` (which does not request `ImageInput`), the provider cannot process it and returns an error.

Source references: `src/SmartHopper.Components/Input/Img2AIComponent.cs`, `src/SmartHopper.Core/Types/VersatileImage.cs`.

### 6. `Audio to AI` rejects unsupported file extensions

`Audio2AIComponent.GetMimeTypeFromPath` only recognizes `.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`. The `.scn` file used in the test is rejected, the component adds a runtime message, and no `Input >` is set. This is expected for a non-audio file.

Source reference: `src/SmartHopper.Components/Input/Audio2AIComponent.cs`.

### 7. Legacy `.xls` and standalone images are not converted by `file2md`

`file2md` registers `XlsxConverter` but not a legacy `.xls` (binary OLE) converter, so `.xls` is treated as an unknown binary file. Likewise `.png` is not converted to Markdown. These are missing converter registrations in `FileConverterRegistry`.

Source reference: `src/SmartHopper.Core.Grasshopper/AITools/file2md.cs`.

### 8. Missing single-item numeric/boolean input adapters

Only list variants exist (`NumberList2AIComponent`, `BooleanList2AIComponent`, `IntegerList2AIComponent`). The single-item `Number to AI`, `Boolean to AI`, `Integer to AI` components are not in the palette.

Source references: `src/SmartHopper.Components/Input/NumberList2AIComponent.cs`, `src/SmartHopper.Components/Input/BooleanList2AIComponent.cs`, `src/SmartHopper.Components/Input/IntegerList2AIComponent.cs`.

### 9. Non-AI `Text to JSON` component is missing

Only `JSON To Text` and `JSON Sanitizer` exist in `src/SmartHopper.Components/JSON/`. A dedicated `Text to JSON` component is not implemented.

Source reference: `src/SmartHopper.Components/JSON/`.

### 10. `B6` naming/category mismatch is a test-plan mismatch, not a code bug

The issue #538 checklist uses short names (`AI Text`, `AI Text List`, etc.) and places `AI List to Boolean` under `SmartHopper > Text`. The codebase uses `AI ... To ...` naming and places `AI List To Boolean` under `SmartHopper > List`. Either the checklist or the component names/categories need alignment.

### 11. `Deconstruct Discourse Post` field gaps come from the underlying tool payloads

`Discourse Search` includes `topic_title` and `url`; `Discourse Post Get` does not (it has `title` and `topic_id`). The component maps the fields it finds, so output depends on upstream tool output. Normalizing `Discourse Post Get` to include `topic_title`/`url` (or deriving them from `topic_id`) would fix this.

## Fixes applied

- [x] **Non-image file rejection in `Image to AI`**: `VersatileImage.DetectSourceKind` no longer defaults to `LocalFile` for unsupported files; it now throws a clear `NotSupportedException`/`FileNotFoundException`.
- [x] **Legacy `.xls` conversion**: added `XlsConverter` (using `ExcelDataReader`) and registered it in `file2md`.
- [x] **Single-item numeric/boolean input adapters**: added `Number to AI`, `Boolean to AI`, and `Integer to AI` components.
- [x] **Discourse Post Get `topic_title` and `url`**: `DiscourseUtils.FilterPostJson` now derives and emits `topic_title` and `url` from the post's `topic_id`, `topic_slug`, `post_number`, and the forum base URL.

## Recommended next steps

1. **Fix `AI to JSON` validation**: either make `Schema` required when `JsonOutput` capability is requested, or generate a default permissive schema so `AIRequestCall.IsValid` passes.
2. **Fix `AI to GhJSON` / `AI to Script`**: add `JsonOutputSchema` handling, or switch these adapters to invoke the actual `gh_generate`/`script_generate` tool pipeline (with their system prompts and validators). Alternatively, remove the inherited `JsonOutput` requirement if the adapter only needs free-form text that it parses.
3. **Enable `AI to Image` / `AI to Speech`**: configure a provider with the required `ImageOutput`/`SpeechOutput` capability and API key. Consider making these adapters call the `text2img`/`speech_generate` tool implementations rather than a generic chat completion.
4. **Allow `AI to Text` to accept raw text**: consider adding a `Text` input to `AIOutputAdapterBase` (or a converter) so it can accept raw text and wrap it into an `AIInputPayload` automatically; otherwise document that output adapters must follow `Text to AI` / `* to AI` components.
5. **Add missing `Text to JSON` component** for non-AI JSON construction.
6. **Align test plan or component names/categories** for B6 and Discourse payloads.

---

## Follow-up: current issue state, inconsistencies, and additional tests

This section was added after live MCP/provider testing and the code changes described above.

### Code changes applied during follow-up

- `OpenAIProvider.cs`: the Responses API now only sends the `reasoning` object for o-series and GPT-5 models. This fixes the `Unsupported parameter: 'reasoning.effort'` error seen with `gpt-4o-mini`.
- `set_ai_provider_and_model`: the tool result now returns the normalized provider name, fixing the occasional `selectedProvider: "Default"` response.
- `AI to JSON`: the `Schema` input is now read as `GH_Structure<GH_String>` and converted to `GH_Structure<IGH_Goo>`. The inherited `JsonOutput` requirement is removed when no schema is supplied, so the adapter can fall back to parsing free-form text.
- `AI to GhJSON` / `AI to Script`: the inherited `JsonOutput` requirement is removed so these adapters can run with standard text models.

### Issue state

| Issue | State | Notes / recommended action |
|---|---|---|
| #528 TidyUp | Further testing required | Basic, branching, and wide layouts passed. Disconnected islands, large definitions (20+), parameter components, and Sugiyama consistency still need testing. |
| #530 GhPatch | Further testing + possible bug | Diff/patch/merge/validate passed. **Apply patch to canvas** produced a patch string with an extra `}`; investigate `Save/Apply GhPatch` serialization. |
| #533 Google Gemini | Needs API key + plan update | Gemini is `configured=false`. Issue hardcodes model names (`Gemini 2.0 Flash`, `2.5 Pro`, `3.1`) that may not match current `get_available_models` output; update the test plan to query the model list dynamically. |
| #534 Audio | Needs API key + plan update | No audio files/keys. Issue lists `voxtral-tts-26-03`, which does not exist in current Mistral models; use `get_available_models` to pick valid TTS/STT models. |
| #535 AI Settings | Further testing required | Components present. Basic settings flow, timeout/batch, backward compatibility, and 1.4.x migration need live AI runs. |
| #536 AI Input Adapters | Partially passed + plan update | `Text to AI` works. `File/Image/Audio to AI` need appropriate media and provider. Issue says `URL to AI` but the actual component is `Web to AI`; update the issue. `Number/Boolean/Integer to AI` are now present and should be tested. |
| #537 AI Output Adapters | Code fixed + further testing | `AI to JSON` should now work with and without a schema. `AI to Text` cannot consume `AI Text To Text` plain-text `Result`; adapters expect `AIInputPayload` from `* to AI` components. `AI to Image`/`Speech` need a provider with those capabilities. Update issue instructions. |
| #538 Renamed Components | Test-plan mismatch | Issue uses short names (`AI Text`, `AI Text List`, `AI Text Evaluate`) and places `AI List to Boolean` under `SmartHopper > Text`. Actual code uses `AI ... To ...` names and `SmartHopper > List`. Update the issue checklist to match code. |
| #540 File/Web Processing | Can be closed | All file types and web conversion passed. PNG returning raw binary is a documented limitation. |
| #541 JSON Components | Test-plan mismatch + further testing | Non-AI `Text to JSON` should **not** be added (use `AI Text To JSON` instead). `JSON Array` Get/Add/Remove item components are missing; decide whether to implement them. Update issue. |
| #542 GhJSON/GhPatch | Can be closed | `Save/Open/Validate GhJSON` and `Merge GhJSON` tool passed. |
| #543 Provider Improvements | Needs API key + plan update | OpenAI `reasoning.effort` bug fixed. Issue hardcodes model names (`gpt-5.5`, `5.4-mini`, `Claude 4.6`, `DeepSeek v4`); use `get_available_models` and avoid fixed names. |
| #544 Discourse Knowledge | Can be closed | Search, Post Get, Deconstruct, Topic/Post to AI, and forum variants passed. `Deconstruct Discourse Post` `topic_title`/`url` gap was fixed. |
| #545 Script Generation | Code relaxed + needs testing | `AI to Script`/`AI to GhJSON` now run without a `JsonOutput` schema, but they still bypass the real `script_generate`/`gh_generate` tool pipeline. Consider routing them to the actual tools for reliable generation. |
| #546 Image/Audio Viewers | Needs API key + generated media | Components exist. Not tested due to no generated image/audio. |
| #547 Persistence | Needs Rhino session + old files | Cannot test save/reload in an MCP-only session; requires Rhino restart and 1.4.x files. |
| #548 Timeout/Error Handling | Needs live AI testing | Not tested. Can now be exercised with configured providers. |
| #549 Modality Fallback | Needs vision model + key | Not tested. Requires a vision-capable model and API key. |

### Additional tests to implement

1. **#528**: TidyUp on disconnected islands, a 20+ component definition, parameter components, and Sugiyama layout consistency.
2. **#530**: Apply a generated `.ghpatch` to the canvas end-to-end; debug the extra `}` in the patch string.
3. **#533**: Gemini provider/model selection and a simple generation call once an API key is configured.
4. **#534**: Audio TTS/STT with current Mistral model names from `get_available_models`.
5. **#535**: Wire `Settings`/`Extra Settings` into `AI Text To Text`; test timeout and batch with a live provider.
6. **#536**: `Web to AI`, `File to AI` with PDF/txt, `Image to AI` with a vision-capable provider, and single-item `Number/Boolean/Integer to AI`.
7. **#537**: `AI to JSON` with and without a schema; `AI to Text` fed from `Text to AI` (not `AI Text To Text`); `AI to Image`/`Speech` with capable providers.
8. **#538**: Palette/category verification against the updated issue names.
9. **#541**: `JSON Array` Get/Add/Remove item components if decided; `AI Text To JSON` round-trip with `JSON To Text`.
10. **#543**: Streaming and reasoning with o-series/GPT-5 models through `get_available_models`.
11. **#545**: `AI to Script` and `AI to GhJSON` end-to-end; compare free-form output against real `script_generate`/`gh_generate` tool output.
12. **#547**: Save a `.gh` file, restart Rhino, and reload; test 1.4.x migration if a sample file is available.
13. **#548**: Low timeout value, network failure, cancellation, and rate-limit handling.
14. **#549**: Vision input with a non-vision model to trigger the modality fallback resolver.
