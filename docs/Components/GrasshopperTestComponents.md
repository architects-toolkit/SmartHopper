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
                _messages.Add(new GH_String("✓ Test_Description"));
                testsPassed++;
            }
            else
            {
                _messages.Add(new GH_String("✗ Test_Description: Expected X, got Y"));
                testsFailed++;
            }
        }
        catch (Exception ex)
        {
            _messages.Add(new GH_String($"✗ Test_Description: {ex.Message}"));
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
   - Green checkmarks (✓) indicate passing tests
   - Red X marks (✗) indicate failing tests
   - Success output shows overall pass/fail status
   - Messages output shows detailed test results

## Benefits Over xUnit Tests

| Aspect                | xUnit Tests                | Test Components           |
| -------------------- | ------------------------- | ------------------------- |
| Grasshopper Access    | ❌ No                     | ✅ Yes                    |
| Runtime Environment   | ❌ Isolated               | ✅ Full Rhino/GH          |
| Visual Feedback       | ❌ Console                | ✅ Canvas UI              |
| Serialization Tests   | ❌ Limited                | ✅ Full GH_IO support     |
| Integration Tests     | ❌ No                     | ✅ Yes                    |
| Speed                 | ✅ Fast                   | ❌ Slower                 |
| Automation            | ✅ Easy                   | ⚠️ Manual in Grasshopper  |

## When to Use Test Components

Use test components for:

- ✅ Testing Grasshopper types (GH_String, GH_Structure, etc.)
- ✅ Testing GH_IO serialization/deserialization
- ✅ Testing components that interact with Grasshopper document
- ✅ Integration tests with Grasshopper canvas

Use xUnit tests for:

- ✅ Pure logic tests (no Grasshopper dependencies)
- ✅ Fast unit tests
- ✅ CI/CD automated testing
- ✅ Tests that don't need Rhino runtime

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

Based on analysis of existing test components, the following tests from the test matrix are suitable for implementation as Grasshopper test components:

### 🔴 P0 - Breaking Changes (3 tests)
- **TC-BREAK-02**: Verify `AITextGenerate` → `AIText2TextComponent` migration works
  - Create old component, verify it loads and has expected inputs/outputs
  - Create new component, verify inputs/outputs match structure
  - Test wire connections work with both

- **TC-BREAK-09**: Test AI tools are accessible by new names in chat/tool calls
  - Create test component that calls tools by new names via AIToolCall
  - Verify tool execution succeeds with new names

- **TC-BREAK-10**: Verify old tool names return appropriate errors (not silent failures)
  - Create test component that calls tools by old names
  - Verify error is returned (not silent failure or crash)

### 🔴 P0 - Mixed-Type Data Trees (5 tests)
- **TC-DATATREE-05**: Handle mixed-type trees with multiple IGH_Goo types
  - Create heterogeneous GH_Structure<IGH_Goo> with different types
  - Verify structure maintains type information
  - Verify data count and path count correct

- **TC-DATATREE-06**: Test `groupIdenticalBranches` with `IGH_Goo` type gate
  - Create mixed-type tree with identical branches
  - Verify grouping works correctly with heterogeneous types
  - Verify function call count matches expected optimization

- **TC-DATATREE-07**: Verify branch grouping works with heterogeneous data
  - Similar to TC-DATATREE-06 but with more complex type combinations

- **TC-DATATREE-12**: Verify fallback values stored natively without string conversion
  - Create AIText2BooleanComponent with mixed-type input tree
  - Verify fallback GH_Boolean values are stored natively (not as strings)

- **TC-DATATREE-13**: Test `ProcessingResult<IGH_Goo>.Outputs` access
  - Execute DataTreeProcessor with mixed-type output
  - Verify Outputs dictionary is accessible and contains correct data

### 🟡 P1 - Vision Input (4 tests)
- **TC-VISION-15**: `AIImgToTextComponent` - file path input
  - Create test image file
  - Pass file path to component
  - Verify component accepts path and processes correctly

- **TC-VISION-17**: `AIImgToTextComponent` - base64 input
  - Create base64-encoded image
  - Pass to component
  - Verify component processes correctly

- **TC-VISION-18**: `AIImgToTextComponent` - `GH_ExtractedImage` input (regression test)
  - Create GH_ExtractedImage instance
  - Pass to component
  - Verify component handles it correctly (MistralAI regression fix)

- **TC-VISION-25**: Verify full serialization in GH files
  - Create GH_ExtractedImage
  - Serialize to GH file format
  - Deserialize and verify data integrity

### 🟡 P1 - File-to-Markdown (2 tests)
- **TC-F2MD-25**: `File2MdComponent` - basic conversion
  - Create test file (PDF, CSV, JSON, TXT, etc.)
  - Pass to component
  - Verify markdown output is generated

- **TC-F2MD-26**: `File2MdComponent` - `Images` output with `GH_ExtractedImage`
  - Create test file with images
  - Verify Images output contains GH_ExtractedImage objects
  - Verify image data is correctly extracted

### 🟡 P1 - AI Settings Components (4 tests)
- **TC-SETTINGS-01**: Assemble `AIRequestParameters` from inputs
  - Create AISettingsComponent
  - Set Model, Temperature, MaxTokens, TopP, Seed inputs
  - Verify AIRequestParameters output is correctly assembled

- **TC-SETTINGS-09**: Test Batch (B) boolean input
  - Create AISettingsComponent with Batch=true
  - Verify batch flag is set in AIRequestParameters
  - Verify service_tier=batch is applied

- **TC-SETTINGS-20**: Test serialization in `GH_AIRequestParameters`
  - Create GH_AIRequestParameters with various settings
  - Serialize to string
  - Deserialize and verify all values preserved

- **TC-SETTINGS-21**: Backward compatibility - plain string model name
  - Create GH_AIRequestParameters with plain string model name
  - Verify it casts correctly to AIRequestParameters
  - Verify backward compatibility works

### 🟡 P1 - JSON Tools (6 tests)
- **TC-JSON-17**: `JsonArray2TextListComponent` - parse JSON array to GH text list
  - Create JSON array string
  - Pass to component
  - Verify output is GH_String list

- **TC-JSON-18**: `JsonObject2TextComponent` - serialize JSON to string
  - Create JSON object
  - Pass to component
  - Verify output is serialized string

- **TC-JSON-19**: `JsonGetValueComponent` - extract nested value by dot-notation
  - Create JSON object with nested structure
  - Extract value using dot-notation path
  - Verify correct value returned

- **TC-JSON-20**: `JsonMergeComponent` - merge multiple JSON objects
  - Create multiple JSON objects
  - Merge them
  - Verify result contains all properties

- **TC-JSON-21**: `JsonSchemaPropComponent` - scalar property definition
  - Create scalar property via component
  - Verify schema is correctly generated

- **TC-JSON-22**: `JsonSchemaPropObjectComponent` - object property with sub-properties
  - Create nested object property
  - Verify schema structure is correct

### 🟢 P2 - UI/UX (1 test)
- **TC-UI-07**: Tool results inherit TurnId from ToolCall
  - Create test component that executes tool call
  - Verify tool result has same TurnId as tool call
  - Verify metrics aggregation works correctly

---

## Tests NOT Suitable for Grasshopper Components

These require external APIs, mocking, or manual verification:

### 🔴 P0 - Google Gemini Provider (19 tests)
- Requires actual API credentials and provider DLL
- Better suited for: xUnit integration tests with mocked HTTP

### 🔴 P0 - Batch API (most tests)
- Require API mocking or live endpoints
- Better suited for: `SmartHopper.Infrastructure.Tests` with mocked HTTP

### 🟡 P1 - Web-to-Markdown (12 tests)
- Require live HTTP requests
- Better suited for: xUnit integration tests with mocked HTTP

### 🟢 P2 - Provider Model Updates (11 tests)
- Require live API calls to verify model availability
- Better suited for: xUnit tests with provider mocking

### Manual Verification Tests (not suitable)
- TC-BREAK-11: Component outputs maintain same structure (visual inspection)
- TC-BREAK-12: Grasshopper wire connections preserved (visual inspection)
- TC-BREAK-13: `service_tier=batch` silently ignored (visual inspection)
- TC-BREAK-14: Batch input on AISettingsComponent works (visual inspection)
- TC-BREAK-15: Migration path documented (documentation review)
- TC-VISION-16: URL input (requires live image URL)
- TC-W2MD-01 to TC-W2MD-12: Web-to-Markdown (requires live HTTP)
- TC-UI-01 to TC-UI-06: Progress counter display (visual inspection)
- TC-UI-08: Metrics aggregation (visual inspection)

---

## Summary

**Total Suitable Grasshopper Test Components: 25 tests**

- P0 Breaking Changes: 3 tests
- P0 Mixed-Type Data Trees: 5 tests
- P1 Vision Input: 4 tests
- P1 File-to-Markdown: 2 tests
- P1 AI Settings Components: 4 tests
- P1 JSON Tools: 6 tests
- P2 UI/UX: 1 test

These tests focus on:
- ✅ Grasshopper type behavior (GH_Structure, GH_ExtractedImage, etc.)
- ✅ Serialization/deserialization with GH_IO
- ✅ Component integration and data flow
- ✅ Programmatic true/false success verification
- ❌ No manual visual inspection required
- ❌ No external API calls needed
- ❌ No live HTTP requests

## Future Improvements

- [ ] Batch test running across multiple components
- [ ] Test result persistence to file
- [ ] Integration with CI/CD pipeline
- [ ] Performance benchmarking components
