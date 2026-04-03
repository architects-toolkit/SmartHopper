# Testing Plan: SmartHopper 2.0.0 Refactor

**Branch:** `feature/2.0.0-text2json` → `dev`
**PR Date:** April 2, 2026
**Status:** 🚧 Pre-PR Testing Phase
**Estimated Testing Time:** 4-6 hours
**Testers Required:** 2 (1 Windows, 1 macOS if possible)

---

## Executive Summary

This is a **large-scale refactor** involving 208 files changed, 21,998 insertions, and 2,342 deletions. The changes span multiple major feature areas and include **breaking changes** that require migration testing.

### Critical Areas Requiring Testing

| Priority | Feature Area | Risk Level | Test Count |
|----------|--------------|------------|------------|
| 🔴 P0 | Breaking Changes - AI Tool Renames | HIGH | 15 |
| 🔴 P0 | Google Gemini Provider | HIGH | 12 |
| 🔴 P0 | Mixed-Type Data Tree Processing | HIGH | 10 |
| 🔴 P0 | Batch API Processing | HIGH | 14 |
| 🟡 P1 | Vision Input (Image-to-Text) | MEDIUM | 8 |
| 🟡 P1 | File-to-Markdown Conversion | MEDIUM | 10 |
| 🟡 P1 | Web-to-Markdown Conversion | MEDIUM | 6 |
| 🟡 P1 | AI Settings Components | MEDIUM | 8 |
| 🟡 P1 | JSON Tools & Components | MEDIUM | 12 |
| 🟢 P2 | Provider Model Updates | LOW | 6 |
| 🟢 P2 | UI/UX Improvements | LOW | 4 |

**Total Test Cases:** ~105

---

## 1. Breaking Changes Testing (🔴 P0 - CRITICAL)

### 1.1 AI Tool Renames

**Old → New Mappings:**
- `text_generate` → `text2text`
- `text_evaluate` → `text2boolean`
- `list_generate` → `text2textlist`
- `list_evaluate` → `textlist2boolean`
- `img_generate` → `text2img`
- `img_to_text` → `img2text`
- `file2md` → `file2md` (unchanged)
- `web_to_md` → `web2md` (renamed)
- `web_generic_page_read` → **REMOVED**

#### Test Cases:

- [ ] **TC-BREAK-01:** Open existing `.ghx` files with old components - verify they load without errors
- [ ] **TC-BREAK-02:** Verify `AITextGenerate` → `AIText2TextComponent` migration works
- [ ] **TC-BREAK-03:** Verify `AITextEvaluate` → `AIText2BooleanComponent` migration works
- [ ] **TC-BREAK-04:** Verify `AITextListGenerate` → `AIText2TextListComponent` migration works
- [ ] **TC-BREAK-05:** Verify `AIListEvaluate` → `AIList2BooleanComponent` migration works
- [ ] **TC-BREAK-06:** Verify `AIImgGenerateComponent` → `AIText2ImgComponent` migration works
- [ ] **TC-BREAK-07:** Verify `AIImgToTextComponent` → `AIImg2TextComponent` migration works
- [ ] **TC-BREAK-08:** Verify `WebPageReadComponent` removal doesn't crash file load
- [ ] **TC-BREAK-09:** Test AI tools are accessible by new names in chat/tool calls
- [ ] **TC-BREAK-10:** Verify old tool names return appropriate errors (not silent failures)
- [ ] **TC-BREAK-11:** Check component outputs maintain same structure after rename
- [ ] **TC-BREAK-12:** Verify Grasshopper wire connections preserved after component rename

### 1.2 Service Tier Removal

- [ ] **TC-BREAK-13:** Verify `service_tier=batch` in existing files is silently ignored
- [ ] **TC-BREAK-14:** Test that Batch input on `AISettingsComponent` works correctly
- [ ] **TC-BREAK-15:** Verify migration path documented: reconnect `Batch` input instead of `service_tier` extra

---

## 2. Google Gemini Provider Testing (🔴 P0 - CRITICAL)

### 2.1 Provider Setup & Configuration

- [ ] **TC-GEMINI-01:** Install and enable Google Gemini provider on clean install
- [ ] **TC-GEMINI-02:** Configure API key in settings - verify secure storage
- [ ] **TC-GEMINI-03:** Test provider appears in provider list with correct icon
- [ ] **TC-GEMINI-04:** Verify provider hash verification works on Windows
- [ ] **TC-GEMINI-05:** Verify provider hash verification works on macOS

### 2.2 Model Support

- [ ] **TC-GEMINI-06:** Test Gemini 2.0 Flash - text generation
- [ ] **TC-GEMINI-07:** Test Gemini 2.5 Pro - text generation with reasoning
- [ ] **TC-GEMINI-08:** Test Gemini 3.1 - text generation
- [ ] **TC-GEMINI-09:** Test image generation models (if available)
- [ ] **TC-GEMINI-10:** Verify all models appear in model selection dropdown with correct capabilities

### 2.3 Capabilities

- [ ] **TC-GEMINI-11:** Text generation with streaming
- [ ] **TC-GEMINI-12:** Structured output (JSON Schema)
- [ ] **TC-GEMINI-13:** Tool calling with function declarations
- [ ] **TC-GEMINI-14:** Extended thinking/reasoning levels
- [ ] **TC-GEMINI-15:** Context caching (verify infrastructure in place)

### 2.4 Integration

- [ ] **TC-GEMINI-16:** Test with `AIText2TextComponent`
- [ ] **TC-GEMINI-17:** Test with `AIText2BooleanComponent`
- [ ] **TC-GEMINI-18:** Test with `AIText2JsonComponent`
- [ ] **TC-GEMINI-19:** Verify batch processing works with Gemini

---

## 3. Mixed-Type Data Tree Testing (🔴 P0 - CRITICAL)

### 3.1 GHStructureConverter

- [ ] **TC-DATATREE-01:** Convert `GH_Structure<GH_String>` to `GH_Structure<IGH_Goo>`
- [ ] **TC-DATATREE-02:** Convert `GH_Structure<GH_Boolean>` to `GH_Structure<IGH_Goo>`
- [ ] **TC-DATATREE-03:** Convert `GH_Structure<GH_Integer>` to `GH_Structure<IGH_Goo>`
- [ ] **TC-DATATREE-04:** Convert `GH_Structure<GH_Number>` to `GH_Structure<IGH_Goo>`
- [ ] **TC-DATATREE-05:** Handle mixed-type trees with multiple IGH_Goo types

### 3.2 DataTreeProcessor

- [ ] **TC-DATATREE-06:** Test `groupIdenticalBranches` with `IGH_Goo` type gate
- [ ] **TC-DATATREE-07:** Verify branch grouping works with heterogeneous data
- [ ] **TC-DATATREE-08:** Test `RunAsync<T>` overload for heterogeneous output
- [ ] **TC-DATATREE-09:** Test `ExtractTypedTree<U>` helper method

### 3.3 Component Integration

- [ ] **TC-DATATREE-10:** `AIText2BooleanComponent` - mixed-type input tree with `GH_Boolean` fallback
- [ ] **TC-DATATREE-11:** `AIList2BooleanComponent` - mixed-type input tree with `GH_Boolean` fallback
- [ ] **TC-DATATREE-12:** Verify fallback values stored natively without string conversion
- [ ] **TC-DATATREE-13:** Test `ProcessingResult<IGH_Goo>.Outputs` access
- [ ] **TC-DATATREE-14:** Test `ExtractTypedTree<GH_String>()` from heterogeneous results

### 3.4 File2MdComponent

- [ ] **TC-DATATREE-15:** Test `RunProcessingAsync<GH_String>` with tree broadcasting
- [ ] **TC-DATATREE-16:** Verify `ItemGraft` path management consistency
- [ ] **TC-DATATREE-17:** Test `ComponentProcessingOptions` property behavior

---

## 4. Batch API Testing (🔴 P0 - CRITICAL)

### 4.1 OpenAI Batch

- [ ] **TC-BATCH-01:** Submit multi-request JSONL file to `/v1/files`
- [ ] **TC-BATCH-02:** Create batch via `/v1/batches`
- [ ] **TC-BATCH-03:** Poll batch status with live progress counter
- [ ] **TC-BATCH-04:** Download output from `/v1/files/{output_file_id}/content`
- [ ] **TC-BATCH-05:** Cancel batch via `/v1/batches/{id}/cancel`
- [ ] **TC-BATCH-06:** Verify `request_counts.completed` updates progress

### 4.2 Anthropic Batch

- [ ] **TC-BATCH-07:** Submit multiple items via `POST /v1/messages/batches`
- [ ] **TC-BATCH-08:** Poll `processing_status` on `GET /v1/messages/batches/{id}`
- [ ] **TC-BATCH-09:** Download JSONL results from `results_url`
- [ ] **TC-BATCH-10:** Cancel via `POST /v1/messages/batches/{id}/cancel`
- [ ] **TC-BATCH-11:** Verify `request_counts.succeeded` updates progress

### 4.3 MistralAI Batch

- [ ] **TC-BATCH-12:** Submit inline batching via `POST /v1/batch/jobs`
- [ ] **TC-BATCH-13:** Poll job status on `GET /v1/batch/jobs/{id}`
- [ ] **TC-BATCH-14:** Download output from `/v1/files/{output_file}/content`
- [ ] **TC-BATCH-15:** Cancel via `POST /v1/batch/jobs/{id}/cancel`
- [ ] **TC-BATCH-16:** Verify `succeeded_requests` updates progress

### 4.4 Component Integration

- [ ] **TC-BATCH-17:** Test `AITextGenerate` batch completion with `OnBatchCompleted` override
- [ ] **TC-BATCH-18:** Verify `ReconstructOutputTree<T>` replaces sentinels correctly
- [ ] **TC-BATCH-19:** Test batch state persistence across file save/reopen
- [ ] **TC-BATCH-20:** Verify `CustomIds` serialization in `Write()` / `Read()`
- [ ] **TC-BATCH-21:** Test immediate first poll when restoring batch state
- [ ] **TC-BATCH-22:** Verify batch capability validation prevents unsupported providers

### 4.5 Error Handling

- [ ] **TC-BATCH-23:** Test batch item errors surface as Grasshopper runtime messages
- [ ] **TC-BATCH-24:** Verify `AIInteractionError` detection in provider `Decode()` methods
- [ ] **TC-BATCH-25:** Test large file upload/download timeout (300s default)
- [ ] **TC-BATCH-26:** Verify progress messages show `Preparing X/X...` during data collection

---

## 5. Vision Input Testing (🟡 P1 - HIGH)

### 5.1 AIInteractionImage

- [ ] **TC-VISION-01:** Create vision input from base64 data
- [ ] **TC-VISION-02:** Create vision input from file path
- [ ] **TC-VISION-03:** Verify `MimeType` property is correctly set
- [ ] **TC-VISION-04:** Test `CreateVisionInput()` method
- [ ] **TC-VISION-05:** Test `CreateVisionInputFromBase64()` method

### 5.2 AIBodyBuilder

- [ ] **TC-VISION-06:** Test `AddImageInput()` fluent method
- [ ] **TC-VISION-07:** Test `AddImageInputFromBase64()` fluent method

### 5.3 Provider Encoding

- [ ] **TC-VISION-08:** OpenAI - verify base64 data URI encoding (`data:{mime};base64,{data}`)
- [ ] **TC-VISION-09:** Anthropic - native `image` content blocks with base64
- [ ] **TC-VISION-10:** MistralAI - OpenAI-compatible `image_url` content blocks

### 5.4 img2text Tool

- [ ] **TC-VISION-11:** Test image description with `imageUrl` parameter
- [ ] **TC-VISION-12:** Test image description with `imageBase64` + `mimeType`
- [ ] **TC-VISION-13:** Test with optional `prompt` parameter
- [ ] **TC-VISION-14:** Verify `AICapability.Image2Text` requirement enforcement

### 5.5 Components

- [ ] **TC-VISION-15:** `AIImgToTextComponent` - file path input
- [ ] **TC-VISION-16:** `AIImgToTextComponent` - URL input
- [ ] **TC-VISION-17:** `AIImgToTextComponent` - base64 input
- [ ] **TC-VISION-18:** `AIImgToTextComponent` - `GH_ExtractedImage` input (regression test for MistralAI fix)
- [ ] **TC-VISION-19:** `AIFile2MdComponent` - image mode `embed`
- [ ] **TC-VISION-20:** `AIFile2MdComponent` - image mode `describe`
- [ ] **TC-VISION-21:** `AIFile2MdComponent` - image mode `caption`

### 5.6 GH_ExtractedImage

- [ ] **TC-VISION-22:** Verify `ScriptVariable()` returns `Bitmap`
- [ ] **TC-VISION-23:** Test cast to/from `GH_String`
- [ ] **TC-VISION-24:** Test cast to/from `Bitmap`
- [ ] **TC-VISION-25:** Verify full serialization in GH files

---

## 6. File-to-Markdown Testing (🟡 P1 - HIGH)

### 6.1 Supported Formats

- [ ] **TC-F2MD-01:** PDF conversion with layout intelligence
- [ ] **TC-F2MD-02:** DOCX conversion (headings, tables, lists)
- [ ] **TC-F2MD-03:** XLSX conversion
- [ ] **TC-F2MD-04:** PPTX conversion
- [ ] **TC-F2MD-05:** HTML conversion
- [ ] **TC-F2MD-06:** CSV conversion
- [ ] **TC-F2MD-07:** JSON conversion
- [ ] **TC-F2MD-08:** XML conversion
- [ ] **TC-F2MD-09:** TXT conversion
- [ ] **TC-F2MD-10:** EML conversion
- [ ] **TC-F2MD-11:** EPUB conversion
- [ ] **TC-F2MD-12:** RTF conversion

### 6.2 PDF Enhancements

- [ ] **TC-F2MD-13:** Column detection with `RecursiveXYCut`
- [ ] **TC-F2MD-14:** Reading order preservation
- [ ] **TC-F2MD-15:** Header/footer removal (12%/88% zones)
- [ ] **TC-F2MD-16:** Heading detection
- [ ] **TC-F2MD-17:** Table detection via whitespace-gap analysis
- [ ] **TC-F2MD-18:** Inline text styling preservation (bold, italic)
- [ ] **TC-F2MD-19:** Caption detection for tables and figures

### 6.3 Image Extraction

- [ ] **TC-F2MD-20:** PDF image extraction via PdfPig
- [ ] **TC-F2MD-21:** DOCX image extraction from `ImageParts`
- [ ] **TC-F2MD-22:** PPTX image extraction from `SlidePart.ImageParts`
- [ ] **TC-F2MD-23:** PNG conversion with `TryGetPng`
- [ ] **TC-F2MD-24:** JPEG magic-byte fallback

### 6.4 Components

- [ ] **TC-F2MD-25:** `File2MdComponent` - basic conversion
- [ ] **TC-F2MD-26:** `File2MdComponent` - `Images` output with `GH_ExtractedImage`
- [ ] **TC-F2MD-27:** `AIFile2MdComponent` - AI-powered conversion
- [ ] **TC-F2MD-28:** `AIFile2MdComponent` - image modes (`embed`, `describe`, `caption`)
- [ ] **TC-F2MD-29:** `AIFile2MdComponent` - batch context persistence across save/reload

### 6.5 file2md Tool

- [ ] **TC-F2MD-30:** Test `describeImages` parameter
- [ ] **TC-F2MD-31:** Verify `images` array always returned in result
- [ ] **TC-F2MD-32:** Test `imageMode` parameter (`embed`, `describe`, `caption`)

---

## 7. Web-to-Markdown Testing (🟡 P1 - HIGH)

### 7.1 URL Converter

- [ ] **TC-W2MD-01:** Wikipedia article conversion
- [ ] **TC-W2MD-02:** GitHub file URL conversion (raw/plain/markdown)
- [ ] **TC-W2MD-03:** GitLab file URL conversion
- [ ] **TC-W2MD-04:** Discourse forum conversion
- [ ] **TC-W2MD-05:** Stack Exchange question conversion
- [ ] **TC-W2MD-06:** Generic page fallback with readability scoring

### 7.2 Readability Scoring

- [ ] **TC-W2MD-07:** Content scoring by text density
- [ ] **TC-W2MD-08:** Link density filtering
- [ ] **TC-W2MD-09:** Boilerplate removal (nav, header, footer, ads)
- [ ] **TC-W2MD-10:** Semantic container prioritization (article, main)

### 7.3 Components

- [ ] **TC-W2MD-11:** `Web2MdComponent` - basic URL conversion
- [ ] **TC-W2MD-12:** Verify `web_generic_page_read` removal - use `web2md` instead

---

## 8. AI Settings Components Testing (🟡 P1 - HIGH)

### 8.1 AISettingsComponent

- [ ] **TC-SETTINGS-01:** Assemble `AIRequestParameters` from inputs
- [ ] **TC-SETTINGS-02:** Test Model input (string)
- [ ] **TC-SETTINGS-03:** Test Temperature input (number)
- [ ] **TC-SETTINGS-04:** Test Max Tokens input (integer)
- [ ] **TC-SETTINGS-05:** Test Top P input (number)
- [ ] **TC-SETTINGS-06:** Test Seed input (integer)
- [ ] **TC-SETTINGS-07:** Test Extras JSON input
- [ ] **TC-SETTINGS-08:** Verify output `Settings (S)` wire connects to AI components
- [ ] **TC-SETTINGS-09:** Test Batch (B) boolean input

### 8.2 AIExtraSettingsComponent

- [ ] **TC-SETTINGS-10:** Dynamic inputs rebuild on provider change
- [ ] **TC-SETTINGS-11:** Test with OpenAI provider extras
- [ ] **TC-SETTINGS-12:** Test with Anthropic provider extras
- [ ] **TC-SETTINGS-13:** Test with MistralAI provider extras
- [ ] **TC-SETTINGS-14:** Test with DeepSeek provider extras
- [ ] **TC-SETTINGS-15:** Test with OpenRouter provider extras
- [ ] **TC-SETTINGS-16:** Verify wire connections preserved on provider change

### 8.3 AIRequestParameters

- [ ] **TC-SETTINGS-17:** Test immutable record behavior
- [ ] **TC-SETTINGS-18:** Test `AIRequestParametersBuilder` fluent methods
- [ ] **TC-SETTINGS-19:** Test `WithBatchTier()` / `ClearBatchTier()`
- [ ] **TC-SETTINGS-20:** Test serialization in `GH_AIRequestParameters`

### 8.4 Integration

- [ ] **TC-SETTINGS-21:** Backward compatibility - plain string model name
- [ ] **TC-SETTINGS-22:** Settings flow through to provider `Encode()`
- [ ] **TC-SETTINGS-23:** Per-property resolution priority (input > global settings)

---

## 9. JSON Tools & Components Testing (🟡 P1 - HIGH)

### 9.1 text2json Tool

- [ ] **TC-JSON-01:** Generate JSON from prompt with JSON Schema
- [ ] **TC-JSON-02:** Test `prompt` parameter (required)
- [ ] **TC-JSON-03:** Test `instructions` parameter (optional)
- [ ] **TC-JSON-04:** Test `jsonSchema` parameter (required)
- [ ] **TC-JSON-05:** Verify `AICapability.TextInput | JsonOutput` enforcement
- [ ] **TC-JSON-06:** Validate output conforms to provided schema

### 9.2 AIText2JsonComponent

- [ ] **TC-JSON-07:** Grasshopper component in `SmartHopper > JSON` category
- [ ] **TC-JSON-08:** Test Prompt input (tree)
- [ ] **TC-JSON-09:** Test Instructions input (tree, optional)
- [ ] **TC-JSON-10:** Test Schema input (tree)
- [ ] **TC-JSON-11:** Verify JSON output (text tree)
- [ ] **TC-JSON-12:** Test `ItemGraft + GroupIdenticalBranches` processing topology

### 9.3 JSON Helper Components

- [ ] **TC-JSON-13:** `JsonSchemaComponent` - build schema from properties
- [ ] **TC-JSON-14:** `JsonSchemaComponent` - nested properties via dot-notation
- [ ] **TC-JSON-15:** `JsonObjectComponent` - create JSON from Key+Value lists
- [ ] **TC-JSON-16:** `JsonArrayComponent` - create JSON array from items
- [ ] **TC-JSON-17:** `JsonArray2TextListComponent` - parse JSON array to GH text list
- [ ] **TC-JSON-18:** `JsonObject2TextComponent` - serialize JSON to string
- [ ] **TC-JSON-19:** `JsonGetValueComponent` - extract nested value by dot-notation
- [ ] **TC-JSON-20:** `JsonMergeComponent` - merge multiple JSON objects

### 9.4 JSON Schema Visual Builder

- [ ] **TC-JSON-21:** `JsonSchemaPropComponent` - scalar property definition
- [ ] **TC-JSON-22:** `JsonSchemaPropObjectComponent` - object property with sub-properties
- [ ] **TC-JSON-23:** `JsonSchemaPropArrayComponent` - array property with items type
- [ ] **TC-JSON-24:** Verify components wire together correctly

---

## 10. Provider Model Updates (🟢 P2 - MEDIUM)

### 10.1 OpenAI

- [ ] **TC-MODEL-01:** GPT-5.4 series models available
- [ ] **TC-MODEL-02:** GPT-5.4-mini model works
- [ ] **TC-MODEL-03:** GPT-5.4-nano model works
- [ ] **TC-MODEL-04:** gpt-image-1.5 for image generation

### 10.2 MistralAI

- [ ] **TC-MODEL-05:** Versioned model aliases available
- [ ] **TC-MODEL-06:** mistral-small-4-0-26-03 works
- [ ] **TC-MODEL-07:** mistral-large-3-25-12 works
- [ ] **TC-MODEL-08:** ministral models work
- [ ] **TC-MODEL-09:** codestral-25-08 works

### 10.3 OpenRouter

- [ ] **TC-MODEL-10:** GPT-5.4 series available
- [ ] **TC-MODEL-11:** Claude 4.6 series available

---

## 11. UI/UX Testing (🟢 P2 - MEDIUM)

### 11.1 Batch Progress UI

- [ ] **TC-UI-01:** Live progress counter shows `Processing batch (0/XX)...`
- [ ] **TC-UI-02:** Counter updates during polling `(YY/XX)...`
- [ ] **TC-UI-03:** `Preparing X/X...` shown during data collection
- [ ] **TC-UI-04:** Stale progress reset on new run
- [ ] **TC-UI-05:** Terminal states show base message (not stale progress)

### 11.2 Chat UI

- [ ] **TC-UI-06:** User messages appear correctly (no dedup issues)
- [ ] **TC-UI-07:** Tool results inherit TurnId from ToolCall
- [ ] **TC-UI-08:** Metrics aggregation per turn works

---

## Test Execution Checklist

### Pre-Test Setup

- [ ] Fresh Rhino 8 installation (or clean YAK package)
- [ ] All AI provider API keys configured (OpenAI, Anthropic, MistralAI, DeepSeek, OpenRouter, Google Gemini)
- [ ] Test files downloaded from test repository
- [ ] macOS test environment ready (if available)

### Windows Testing

- [ ] All 🔴 P0 tests executed
- [ ] All 🟡 P1 tests executed
- [ ] Critical tests pass (95%+)
- [ ] No crashes or data loss

### macOS Testing (if available)

- [ ] Provider hash verification works
- [ ] File-to-Markdown works
- [ ] Batch processing works
- [ ] No macOS-specific crashes

### Regression Testing

- [ ] Existing `.ghx` files load correctly
- [ ] Existing AI components work
- [ ] Chat functionality works
- [ ] GhJSON tools work

### Documentation Review

- [ ] CHANGELOG.md accurately reflects changes
- [ ] Breaking changes clearly documented
- [ ] Migration guide exists for renamed components
- [ ] New features documented

---

## Sign-Off Criteria

| Criteria | Requirement | Status |
|----------|-------------|--------|
| P0 Tests | 100% pass rate | ⬜ |
| P1 Tests | 90% pass rate | ⬜ |
| P2 Tests | 80% pass rate | ⬜ |
| No Critical Bugs | 0 blocking issues | ⬜ |
| macOS Compatible | Basic functionality works | ⬜ |
| Migration Path Clear | Breaking changes documented | ⬜ |

---

## Test Notes Template

```markdown
### Test Session [DATE]

**Tester:** [Name]
**Platform:** [Windows/macOS] [Version]
**Rhino Version:** [e.g., 8.15]
**SmartHopper Version:** [Branch/Commit]

#### Tests Executed:
- TC-XXXX-XX: [PASS/FAIL] - Notes
- TC-XXXX-XX: [PASS/FAIL] - Notes

#### Issues Found:
1. [Issue description] - [Severity] - [Link to issue if filed]

#### Overall Assessment:
[GO/NO-GO recommendation]
```

---

*Document generated: April 2, 2026*
*For PR: feature/2.0.0-text2json → dev*
