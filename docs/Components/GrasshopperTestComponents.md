# Grasshopper Test Components

## Overview

Grasshopper-dependent tests cannot run in standard xUnit test projects because they require Rhino's runtime environment and Grasshopper assemblies. To solve this, we create **test components** in the `SmartHopper.Components.Test` project that run within Grasshopper's environment.

## Pattern

Test components follow the standard `StatefulComponentBase` pattern with these characteristics:

### Structure

```csharp
public class MyTypeTestComponent : StatefulComponentBase
{
    public override Guid ComponentGuid => new Guid("UNIQUE-GUID-HERE");
    protected override Bitmap Icon => null;
    public override GH_Exposure Exposure => GH_Exposure.septenary;  // Hidden from normal UI

    public MyTypeTestComponent()
        : base("Test MyType", "TEST-MYTYPE",
              "Tests MyType functionality.",
              "SmartHopper", "Testing Types")
    {
        this.RunOnlyOnInputChanges = false;
    }

    protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

    protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddBooleanParameter("Success", "S", "True if all tests pass.", GH_ParamAccess.item);
        pManager.AddTextParameter("Messages", "M", "Test result messages.", GH_ParamAccess.list);
    }

    protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
    {
        return new Worker(this, AddRuntimeMessage);
    }

    private sealed class Worker : AsyncWorkerBase
    {
        // Test implementation here
    }
}
```

### Key Features

1. **No Inputs**: Test components typically have no inputs (tests are self-contained)
2. **Success + Messages Outputs**: Always output a boolean `Success` and a list of `Messages` for test results
3. **Septenary Exposure**: Hidden from normal Grasshopper UI (only visible in test mode)
4. **Async Worker Pattern**: Use `AsyncWorkerBase` for test execution
5. **Comprehensive Logging**: Each test logs pass/fail with descriptive messages

### Test Implementation Pattern

```csharp
public override async Task DoWorkAsync(CancellationToken token)
{
    try
    {
        var testsPassed = 0;
        var testsFailed = 0;

        // Test 1: Description
        try
        {
            // Arrange
            var obj = new MyType();
            
            // Act
            var result = obj.DoSomething();
            
            // Assert
            if (result == expected)
            {
                _messages.Add(new GH_String("âœ“ Test_Description"));
                testsPassed++;
            }
            else
            {
                _messages.Add(new GH_String("âœ— Test_Description: Expected X, got Y"));
                testsFailed++;
            }
        }
        catch (Exception ex)
        {
            _messages.Add(new GH_String($"âœ— Test_Description: {ex.Message}"));
            testsFailed++;
        }

        // ... more tests ...

        _success = new GH_Boolean(testsFailed == 0);
        _messages.Insert(0, new GH_String($"Tests: {testsPassed} passed, {testsFailed} failed"));

        await Task.Yield();
    }
    catch (Exception ex)
    {
        _success = new GH_Boolean(false);
        _messages.Add(new GH_String($"Exception: {ex.Message}"));
        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
    }
}
```

## Existing Test Components

### Data Processing Tests

- `DataProcessor/` - Tests for `DataTreeProcessor` topology and behavior
- `DataProcessor/GH_StructureTestComponent.cs` - Tests for `GH_Structure`, `GH_Path`, and related types

### Type Tests

- `Misc/GH_ExtractedImageTestComponent.cs` - Tests for `GH_ExtractedImage` serialization and casting

### Badge Tests

- `Badges/` - Tests for component badge rendering

## Running Tests

1. **In Grasshopper**: Add test components to a canvas and set `Run` to true
2. **Visual Feedback**:
   - Green checkmarks (âœ“) indicate passing tests
   - Red X marks (âœ—) indicate failing tests
   - Success output shows overall pass/fail status
   - Messages output shows detailed test results

## Benefits Over xUnit Tests

| Aspect                | xUnit Tests                | Test Components           |
| -------------------- | ------------------------- | ------------------------- |
| Grasshopper Access    | âŒ No                     | âœ… Yes                    |
| Runtime Environment   | âŒ Isolated               | âœ… Full Rhino/GH          |
| Visual Feedback       | âŒ Console                | âœ… Canvas UI              |
| Serialization Tests   | âŒ Limited                | âœ… Full GH_IO support     |
| Integration Tests     | âŒ No                     | âœ… Yes                    |
| Speed                 | âœ… Fast                   | âŒ Slower                 |
| Automation            | âœ… Easy                   | âš ï¸ Manual in Grasshopper  |

## When to Use Test Components

Use test components for:

- âœ… Testing Grasshopper types (GH_String, GH_Structure, etc.)
- âœ… Testing GH_IO serialization/deserialization
- âœ… Testing components that interact with Grasshopper document
- âœ… Integration tests with Grasshopper canvas

Use xUnit tests for:

- âœ… Pure logic tests (no Grasshopper dependencies)
- âœ… Fast unit tests
- âœ… CI/CD automated testing
- âœ… Tests that don't need Rhino runtime

## Migration from xUnit to Test Components

When converting xUnit tests to test components:

1. **Identify Grasshopper Dependencies**: Find tests that fail due to missing Grasshopper assemblies
2. **Create Test Component**: Create a new component class in `SmartHopper.Components.Test`
3. **Convert Test Logic**: Translate xUnit assertions to message logging
4. **Track Results**: Use `testsPassed`/`testsFailed` counters
5. **Output Results**: Set `Success` boolean and `Messages` list
6. **Remove xUnit Tests**: Delete the original xUnit test methods

## Example: GH_ExtractedImage Tests

The `GH_ExtractedImageTestComponent` demonstrates testing a Grasshopper type:

- Tests constructor validation
- Tests casting (CastFrom/CastTo)
- Tests serialization (Write/Read round-trip)
- Tests script variable conversion
- Provides detailed pass/fail messages for each test

## Example: GH_Structure Tests

The `GH_StructureTestComponent` demonstrates testing Grasshopper data structures:

- Tests structure creation with different types
- Tests path equality and string representation
- Tests multiple items in same/different paths
- Tests tree operations (Flatten, Graft)
- Validates path and data counts

## Suitable Tests for Grasshopper Test Components

Based on analysis of existing test components, the following tests from the test matrix are suitable for implementation as Grasshopper test components. **All test data is hardcoded internally** - users only need to toggle Run=true.

### ðŸ”´ P0 - Breaking Changes (2 tests)

- **TC-BREAK-09**: Test AI tools are accessible by new names in chat/tool calls
  - Hardcoded: Use AIToolCall with new tool names (e.g., "text_generate")
  - Verify: Tool call object is created successfully (no API execution)
  - Success: Tool name is recognized and call structure is valid

- **TC-BREAK-10**: Verify old tool names return appropriate errors
  - Hardcoded: Use AIToolCall with old tool names (e.g., "text_generate_legacy")
  - Verify: Error is returned (no silent failure, no crash)
  - Success: ToolManager returns expected "tool not found" error

### ðŸ”´ P0 - Mixed-Type Data Trees (5 tests)

- **TC-DATATREE-05**: Handle mixed-type trees with multiple IGH_Goo types
  - Hardcoded: Create GH_Structure<IGH_Goo> with GH_String, GH_Integer, GH_Number, GH_Boolean
  - Verify: Structure maintains type information, correct PathCount and DataCount
  - Success: All types coexist in same structure without data loss

- **TC-DATATREE-06**: Test `groupIdenticalBranches` with IGH_Goo type gate
  - Hardcoded: Create tree with 3 paths where 2 have identical GH_Integer content [1,2]
  - Verify: Function is called only twice (identical branches grouped)
  - Success: Results appear at all 3 paths, optimization counter shows 2 calls

- **TC-DATATREE-07**: Verify branch grouping works with heterogeneous data
  - Hardcoded: Create tree with mixed types (GH_String "1", GH_Integer 1) - not identical despite same value
  - Verify: Different types are NOT grouped even if string representations match
  - Success: Function called for each unique type combination

- **TC-DATATREE-12**: Verify fallback values stored natively without string conversion
  - Hardcoded: Create DataTreeProcessor with mixed GH_String/GH_Boolean tree
  - Verify: When converting GH_String "true" to fallback GH_Boolean, it's stored as native boolean
  - Success: Result type is GH_Boolean, not GH_String containing "true"

- **TC-DATATREE-13**: Test `ProcessingResult<IGH_Goo>.Outputs` access
  - Hardcoded: Execute DataTreeProcessor with multiple outputs (Sum, Difference)
  - Verify: Outputs dictionary accessible and contains both results
  - Success: Dictionary has expected keys with correct GH_Structure values

### ðŸŸ¡ P1 - Vision Input (2 tests)

- **TC-VISION-17**: `AIImgToTextComponent` - base64 input validation
  - Hardcoded: Create base64-encoded 1x1 PNG image (stored as constant string)
  - Verify: Component accepts base64 string, validates format, converts to internal representation
  - Success: No API call needed - just validate input parsing and type conversion

- **TC-VISION-25**: Verify full GH_IO serialization round-trip
  - Hardcoded: Create GH_ExtractedImage with test data (id, base64, mimeType, pageNumber)
  - Verify: Write to GH_IWriter, read from GH_IReader, compare properties
  - Success: All properties match after round-trip (no file save/load needed)

### ðŸŸ¡ P1 - File-to-Markdown (1 test)

- **TC-F2MD-26**: `File2MdComponent` - Images output extraction
  - Hardcoded: Store sample PDF bytes internally (minimal test PDF or mock)
  - Verify: File2Md extracts images and returns GH_ExtractedImage objects
  - Success: Images list is non-empty, each item has valid base64 data

### ðŸŸ¡ P1 - AI Settings Components (4 tests)

- **TC-SETTINGS-01**: Assemble AIRequestParameters from inputs
  - Hardcoded: Create AISettingsComponent with test values
  - Verify: Component assembles AIRequestParameters correctly (Model, Temp, MaxTokens, TopP, Seed)
  - Success: Output parameters match inputs exactly

- **TC-SETTINGS-09**: Test Batch (B) boolean input
  - Hardcoded: Create AISettingsComponent with Batch=true
  - Verify: Batch flag is set in AIRequestParameters, service_tier="batch" present
  - Success: Parameters object reflects batch mode correctly

- **TC-SETTINGS-20**: Test serialization in GH_AIRequestParameters
  - Hardcoded: Create GH_AIRequestParameters with various settings
  - Verify: Write to GH_IWriter, read from GH_IReader
  - Success: All values preserved after round-trip

- **TC-SETTINGS-21**: Backward compatibility - plain string model name
  - Hardcoded: Create GH_String with model name, cast to GH_AIRequestParameters
  - Verify: CastFrom string works, creates valid parameters with model set
  - Success: Backward compatibility maintained

### ðŸŸ¡ P1 - JSON Tools (6 tests)

- **TC-JSON-17**: `JsonArray2TextListComponent` - parse JSON array to GH text list
  - Hardcoded: JSON string `["item1","item2","item3"]`
  - Verify: Component parses to GH_Structure<GH_String>
  - Success: Output has 3 items with correct values

- **TC-JSON-18**: `JsonObject2TextComponent` - serialize JSON to string
  - Hardcoded: JObject with nested structure
  - Verify: Component serializes to valid JSON string
  - Success: Output string is valid JSON matching input structure

- **TC-JSON-19**: `JsonGetValueComponent` - extract nested value by dot-notation
  - Hardcoded: JSON with `{ "user": { "profile": { "name": "Test" } } }`
  - Verify: Extract using "user.profile.name" returns "Test"
  - Success: Dot-notation navigation works correctly

- **TC-JSON-20**: `JsonMergeComponent` - merge multiple JSON objects
  - Hardcoded: Two JObjects `{ "a": 1 }` and `{ "b": 2 }`
  - Verify: Merged result contains both properties
  - Success: Output has `a=1, b=2`

- **TC-JSON-21**: `JsonSchemaPropComponent` - scalar property definition
  - Hardcoded: Property name "age", type "integer", required true
  - Verify: Generated schema fragment is correct
  - Success: Output matches expected JSON schema structure

- **TC-JSON-22**: `JsonSchemaPropObjectComponent` - object property with sub-properties
  - Hardcoded: Property "address" with sub-properties "street", "city"
  - Verify: Generated schema has type="object" with properties map
  - Success: Nested schema structure is correct

### ðŸŸ¢ P2 - UI/UX (1 test)

- **TC-UI-07**: Tool results inherit TurnId from ToolCall
  - Hardcoded: Create simulated tool call with TurnId=123
  - Verify: Tool result message has same TurnId
  - Success: TurnId propagation works correctly (no actual tool execution needed)

---

## Provider-Specific Test Components

These test components validate provider functionality using actual API credentials and runtime settings available in Grasshopper. Each provider has multiple components testing individual features.

### ðŸ”´ P0 - OpenAI Provider Tests (5 components)

- **TC-PROVIDER-OPENAI-01**: Encode AIRequestCall to OpenAI message format
  - Hardcoded: Create AIRequestCall with Context, ToolCall, ToolResult messages
  - Verify: Encoded JSON has correct role mapping (system, assistant, tool)
  - Success: Message structure matches OpenAI API requirements

- **TC-PROVIDER-OPENAI-02**: Decode OpenAI response to AIReturn
  - Hardcoded: Create mock OpenAI response JSON with choices, usage tokens
  - Verify: Decoded AIReturn has correct structure and content
  - Success: Response parsing works correctly

- **TC-PROVIDER-OPENAI-03**: Standard API call with metrics validation
  - Runtime: Uses stored OpenAI API key from settings
  - Verify: (1) Call succeeds and returns valid response, (2) Metrics have correct structure
  - Success: Both success flags true - call works AND metrics valid (input_tokens > 0, output_tokens > 0)

- **TC-PROVIDER-OPENAI-04**: Batch API call with metrics validation
  - Runtime: Uses stored OpenAI API key, batch-enabled model
  - Verify: (1) Batch call succeeds with service_tier=batch, (2) Metrics correctly parsed
  - Success: Both success flags true - batch call works AND metrics valid

- **TC-PROVIDER-OPENAI-05**: Tool calls encoding and response parsing
  - Hardcoded: Create AIRequestCall with tool definitions and tool calls
  - Verify: (1) Tools encoded correctly in request, (2) Tool results decoded from response
  - Success: Both success flags true - tool encoding works AND tool result parsing works

### ðŸ”´ P0 - MistralAI Provider Tests (5 components)

- **TC-PROVIDER-MISTRAL-01**: Encode AIRequestCall to MistralAI message format
  - Hardcoded: Create AIRequestCall with Context, ToolCall, ToolResult messages
  - Verify: Encoded JSON has correct role mapping (user, assistant, tool)
  - Success: Message structure matches MistralAI API requirements

- **TC-PROVIDER-MISTRAL-02**: Decode MistralAI response to AIReturn
  - Hardcoded: Create mock MistralAI response JSON with choices, usage tokens
  - Verify: Decoded AIReturn has correct structure and content
  - Success: Response parsing works correctly

- **TC-PROVIDER-MISTRAL-03**: Standard API call with metrics validation
  - Runtime: Uses stored MistralAI API key from settings
  - Verify: (1) Call succeeds and returns valid response, (2) Metrics have correct structure
  - Success: Both success flags true - call works AND metrics valid (input_tokens > 0, output_tokens > 0)

- **TC-PROVIDER-MISTRAL-04**: Batch API call with metrics validation
  - Runtime: Uses stored MistralAI API key, batch-enabled model
  - Verify: (1) Batch call succeeds with service_tier=batch, (2) Metrics correctly parsed
  - Success: Both success flags true - batch call works AND metrics valid

- **TC-PROVIDER-MISTRAL-05**: Tool calls encoding and response parsing
  - Hardcoded: Create AIRequestCall with tool definitions and tool calls
  - Verify: (1) Tools encoded correctly in request, (2) Tool results decoded from response
  - Success: Both success flags true - tool encoding works AND tool result parsing works

### ðŸ”´ P0 - DeepSeek Provider Tests (5 components)

- **TC-PROVIDER-DEEPSEEK-01**: Encode AIRequestCall to DeepSeek message format
  - Hardcoded: Create AIRequestCall with Context, ToolCall, ToolResult messages
  - Verify: Encoded JSON has correct role mapping (user, assistant, tool)
  - Success: Message structure matches DeepSeek API requirements

- **TC-PROVIDER-DEEPSEEK-02**: Decode DeepSeek response to AIReturn
  - Hardcoded: Create mock DeepSeek response JSON with choices, usage tokens
  - Verify: Decoded AIReturn has correct structure and content
  - Success: Response parsing works correctly

- **TC-PROVIDER-DEEPSEEK-03**: Standard API call with metrics validation
  - Runtime: Uses stored DeepSeek API key from settings
  - Verify: (1) Call succeeds and returns valid response, (2) Metrics have correct structure
  - Success: Both success flags true - call works AND metrics valid (input_tokens > 0, output_tokens > 0)

- **TC-PROVIDER-DEEPSEEK-04**: Batch API call with metrics validation
  - Runtime: Uses stored DeepSeek API key, batch-enabled model
  - Verify: (1) Batch call succeeds with service_tier=batch, (2) Metrics correctly parsed
  - Success: Both success flags true - batch call works AND metrics valid

- **TC-PROVIDER-DEEPSEEK-05**: Tool calls encoding and response parsing
  - Hardcoded: Create AIRequestCall with tool definitions and tool calls
  - Verify: (1) Tools encoded correctly in request, (2) Tool results decoded from response
  - Success: Both success flags true - tool encoding works AND tool result parsing works

### ðŸŸ¡ P1 - Google Gemini Provider Tests (5 components)

- **TC-PROVIDER-GEMINI-01**: Encode AIRequestCall to Gemini message format
  - Hardcoded: Create AIRequestCall with Context, ToolCall, ToolResult messages
  - Verify: Encoded JSON has correct role mapping (user, model, function)
  - Success: Message structure matches Gemini API requirements

- **TC-PROVIDER-GEMINI-02**: Decode Gemini response to AIReturn
  - Hardcoded: Create mock Gemini response JSON with candidates, usage tokens
  - Verify: Decoded AIReturn has correct structure and content
  - Success: Response parsing works correctly

- **TC-PROVIDER-GEMINI-03**: Standard API call with metrics validation
  - Runtime: Uses stored Gemini API key from settings
  - Verify: (1) Call succeeds and returns valid response, (2) Metrics have correct structure
  - Success: Both success flags true - call works AND metrics valid (input_tokens > 0, output_tokens > 0)

- **TC-PROVIDER-GEMINI-04**: Function calling encoding and response parsing
  - Hardcoded: Create AIRequestCall with function definitions and function calls
  - Verify: (1) Functions encoded correctly in request, (2) Function results decoded from response
  - Success: Both success flags true - function encoding works AND function result parsing works

- **TC-PROVIDER-GEMINI-05**: Vision input handling (image base64)
  - Hardcoded: Create AIRequestCall with image content (base64 PNG)
  - Verify: Image encoded correctly in Gemini format
  - Success: Vision content structure is valid

### ðŸŸ¡ P1 - Anthropic Provider Tests (5 components)

- **TC-PROVIDER-ANTHROPIC-01**: Encode AIRequestCall to Anthropic message format
  - Hardcoded: Create AIRequestCall with Context, ToolCall, ToolResult messages
  - Verify: Encoded JSON has correct role mapping (user, assistant)
  - Success: Message structure matches Anthropic API requirements

- **TC-PROVIDER-ANTHROPIC-02**: Decode Anthropic response to AIReturn
  - Hardcoded: Create mock Anthropic response JSON with content blocks, usage tokens
  - Verify: Decoded AIReturn has correct structure and content
  - Success: Response parsing works correctly

- **TC-PROVIDER-ANTHROPIC-03**: Standard API call with metrics validation
  - Runtime: Uses stored Anthropic API key from settings
  - Verify: (1) Call succeeds and returns valid response, (2) Metrics have correct structure
  - Success: Both success flags true - call works AND metrics valid (input_tokens > 0, output_tokens > 0)

- **TC-PROVIDER-ANTHROPIC-04**: Batch API call with metrics validation
  - Runtime: Uses stored Anthropic API key, batch-enabled model
  - Verify: (1) Batch call succeeds with service_tier=batch, (2) Metrics correctly parsed
  - Success: Both success flags true - batch call works AND metrics valid

- **TC-PROVIDER-ANTHROPIC-05**: Tool calls encoding and response parsing
  - Hardcoded: Create AIRequestCall with tool definitions and tool calls
  - Verify: (1) Tools encoded correctly in request, (2) Tool results decoded from response
  - Success: Both success flags true - tool encoding works AND tool result parsing works

### ðŸŸ¢ P2 - OpenRouter Provider Tests (5 components)

- **TC-PROVIDER-OPENROUTER-01**: Encode AIRequestCall to OpenRouter message format
  - Hardcoded: Create AIRequestCall with Context, ToolCall, ToolResult messages
  - Verify: Encoded JSON has correct role mapping (user, assistant, tool)
  - Success: Message structure matches OpenRouter API requirements

- **TC-PROVIDER-OPENROUTER-02**: Decode OpenRouter response to AIReturn
  - Hardcoded: Create mock OpenRouter response JSON with choices, usage tokens
  - Verify: Decoded AIReturn has correct structure and content
  - Success: Response parsing works correctly

- **TC-PROVIDER-OPENROUTER-03**: Standard API call with metrics validation
  - Runtime: Uses stored OpenRouter API key from settings
  - Verify: (1) Call succeeds and returns valid response, (2) Metrics have correct structure
  - Success: Both success flags true - call works AND metrics valid (input_tokens > 0, output_tokens > 0)

- **TC-PROVIDER-OPENROUTER-04**: Batch API call with metrics validation
  - Runtime: Uses stored OpenRouter API key, batch-enabled model
  - Verify: (1) Batch call succeeds with service_tier=batch, (2) Metrics correctly parsed
  - Success: Both success flags true - batch call works AND metrics valid

- **TC-PROVIDER-OPENROUTER-05**: Tool calls encoding and response parsing
  - Hardcoded: Create AIRequestCall with tool definitions and tool calls
  - Verify: (1) Tools encoded correctly in request, (2) Tool results decoded from response
  - Success: Both success flags true - tool encoding works AND tool result parsing works

### Manual Verification Tests (not suitable)

- TC-BREAK-11: Component outputs maintain same structure (visual inspection)
- TC-BREAK-12: Grasshopper wire connections preserved (visual inspection)
- TC-BREAK-13: `service_tier=batch` silently ignored (visual inspection)
- TC-BREAK-14: Batch input on AISettingsComponent works (visual inspection)
- TC-BREAK-15: Migration path documented (documentation review)
- TC-VISION-16: URL input (requires live image URL)
- TC-W2MD-01 to TC-W2MD-12: Web-to-Markdown (requires live HTTP)
- TC-UI-01 to TC-UI-06: Progress counter display (visual inspection)
- TC-UI-08: Metrics aggregation (covered by provider tests above)

---

## Summary

### Total Suitable Grasshopper Test Components: 50 tests

**Core Functionality Tests (20 tests):**

- P0 Breaking Changes: 2 tests
- P0 Mixed-Type Data Trees: 5 tests
- P1 Vision Input: 2 tests
- P1 File-to-Markdown: 1 test
- P1 AI Settings Components: 4 tests
- P1 JSON Tools: 6 tests
- P2 UI/UX: 1 test

**Provider-Specific Tests (30 tests):**

- OpenAI Provider: 5 tests (Encode, Decode, Standard Call, Batch Call, Tools)
- MistralAI Provider: 5 tests (Encode, Decode, Standard Call, Batch Call, Tools)
- DeepSeek Provider: 5 tests (Encode, Decode, Standard Call, Batch Call, Tools)
- Google Gemini Provider: 5 tests (Encode, Decode, Standard Call, Function Calling, Vision)
- Anthropic Provider: 5 tests (Encode, Decode, Standard Call, Batch Call, Tools)
- OpenRouter Provider: 5 tests (Encode, Decode, Standard Call, Batch Call, Tools)

**Test Characteristics:**

Core tests focus on:

- Grasshopper type behavior (GH_Structure, GH_ExtractedImage, etc.)
- Serialization/deserialization with GH_IO
- Component integration and data flow
- Programmatic true/false success verification

Provider tests focus on:

- Message encoding (AIRequestCall â†’ provider format)
- Response decoding (provider format â†’ AIReturn)
- Standard API calls with metrics validation (input_tokens > 0, output_tokens > 0)
- Batch API calls with service_tier=batch
- Tool/Function call encoding and response parsing
- Vision input handling (base64 images)

**All tests:**

- Use hardcoded internal test data (no user setup required)
- Return dual success outputs: one for functionality, one for metrics validation
- Use actual API credentials from runtime Grasshopper settings
- Make real API calls (not mocked)
- Validate metrics structure and token counts

## Future Improvements

- [ ] Batch test running across multiple components
- [ ] Test result persistence to file
- [ ] Integration with CI/CD pipeline
- [ ] Performance benchmarking components

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about GrasshopperTestComponents.


## End-User Guide

End-user guidance for GrasshopperTestComponents.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for GrasshopperTestComponents.
