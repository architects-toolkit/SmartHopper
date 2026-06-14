# Centralized JSON Formatting Implementation

Implemented a centralized JSON formatting utility (`JsonFormatHelper`) to ensure consistent minified JSON output across all JSON components and AI tools in SmartHopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core/JSON/CentralizedJsonFormatting.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document explains how SmartHopper standardizes JSON formatting across components and AI tools. It covers the helper API, the refactoring pattern, and how markdown-wrapped AI responses are automatically handled.

**You should read this if you:**

- Work with JSON components and need consistent output behavior
- Develop AI tools that return or consume JSON
- Need to validate or normalize JSON strings from external sources

---

## End-User Guide

### Problem Solved

**Inconsistency Issues:**

- `JsonSchemaComponent` output indented JSON (`Formatting.Indented`)
- `JsonObjectComponent`, `JsonMergeComponent`, `JsonGetValueComponent` output minified JSON (`Formatting.None`)
- `text2json` tool returned raw AI response without normalization
- `AIText2JsonComponent` didn't normalize AI-generated JSON

This inconsistency made it difficult to maintain and could cause downstream processing issues.

### Benefits Achieved

| Benefit | Impact |
| --- | --- |
| **Consistency** | All JSON output is minified regardless of source |
| **Maintainability** | Single point of change for JSON formatting rules |
| **Validation** | JSON is validated before output |
| **Error Handling** | Graceful fallbacks for invalid JSON |
| **Integration** | Works seamlessly with existing `JsonPathHelper` |
| **Performance** | Minified output reduces data size |

### Usage Pattern

All JSON components and tools now follow this pattern:

```csharp
// Convert JToken to minified JSON string (silent)
DA.SetData("JSON", JsonFormatHelper.JsonToString(jtoken));

// Convert JToken to minified JSON string (with error handling)
var minified = JsonFormatHelper.JsonToString(jtoken, out string error);
if (string.IsNullOrEmpty(minified))
{
    // Handle error: error contains the reason
}

// Parse JSON string to JToken (silent, auto-extracts from markdown)
var token = JsonFormatHelper.StringToJson(json);

// Parse JSON string to JToken (with error handling, auto-extracts from markdown)
var token = JsonFormatHelper.StringToJson(json, out string error);
if (token == null)
{
    // Handle error: error contains the reason
}

// Convert JSON string to minified format (auto-extracts from markdown)
var minified = JsonFormatHelper.JsonToString(response, out string jsonError);
if (string.IsNullOrEmpty(minified))
{
    // Handle error: jsonError contains the reason
}

// Validate JSON format
if (JsonFormatHelper.IsValidJson(json))
{
    // Process valid JSON
}

// Validate and get parsed token
if (JsonFormatHelper.IsValidJson(json, out var token))
{
    // Use token
}

```

---

## Developer Reference

### JsonFormatHelper.cs

**Location:** `src/SmartHopper.Infrastructure/Utilities/JsonFormatHelper.cs`

**Core Methods:**

- `JsonToString(string json, out string error)` - Convert JSON string to minified format with error handling (auto-extracts from markdown)
- `JsonToString(string json)` - Convert JSON string to minified format (silent failure, auto-extracts from markdown)
- `JsonToString(JToken token, out string error)` - Convert JToken to minified JSON string with error handling
- `JsonToString(JToken token)` - Convert JToken to minified JSON string (silent failure)
- `StringToJson(string json, out string error)` - Parse JSON string to JToken with error handling (auto-extracts from markdown)
- `StringToJson(string json)` - Parse JSON string to JToken (silent failure, auto-extracts from markdown)
- `IsValidJson(string json, out JToken parsed)` - Validate JSON format with parsed output
- `IsValidJson(string json)` - Validate JSON format without parsed output

**Markdown Code Block Extraction:**

All string-based methods (`JsonToString(string)` and `StringToJson(string)`) automatically extract JSON from markdown code blocks before processing. Supports:

- ` ```json ... ``` ` - JSON-specific code blocks
- ` ```txt ... ``` ` - Text code blocks
- ` ```text ... ``` ` - Text code blocks
- ` ``` ... ``` ` - Generic code blocks

If no code block is found, the original string is used as-is.

**Key Features:**

- Single source of truth for JSON formatting
- Graceful error handling with fallbacks
- Consistent minified output (no whitespace)
- Automatic markdown code block extraction
- Validation before output
- Integrates with existing `JsonPathHelper`
- Follows AIResponseParser patterns for consistency

### Refactored Components

#### JsonSchemaComponent.cs

```csharp
// Before: DA.SetData("Schema", schema.ToString(Newtonsoft.Json.Formatting.Indented));
// After:
DA.SetData("Schema", JsonFormatHelper.JsonToString(schema));

```

#### JsonObjectComponent.cs

```csharp
// Before: DA.SetData("JSON", obj.ToString(Newtonsoft.Json.Formatting.None));
// After:
DA.SetData("JSON", JsonFormatHelper.JsonToString(obj));

```

#### JsonMergeComponent.cs

```csharp
// Before: DA.SetData("JSON", merged.ToString(Newtonsoft.Json.Formatting.None));
// After:
DA.SetData("JSON", JsonFormatHelper.JsonToString(merged));

```

#### JsonGetValueComponent.cs

- Already uses minified output via `TokenToString()` method
- No changes needed

### Refactored AI Tools

#### text2json.cs

```csharp
// Normalize JSON output to minified format
var normalizedJson = JsonFormatHelper.JsonToString(response, out string jsonError);
if (string.IsNullOrWhiteSpace(normalizedJson))
{
    output.CreateToolError($"AI response is not valid JSON: {jsonError}");
    return output;
}

var toolResult = new JObject();
toolResult.Add("json", normalizedJson);

```

#### AIText2JsonComponent.cs

```csharp
string json = toolResult["json"]?.ToString() ?? string.Empty;
// Ensure JSON is minified for consistency
string normalizedJson = JsonFormatHelper.JsonToString(json);
outputs["JSON"].Add(new GH_String(normalizedJson));

```

### Markdown Code Block Handling

When AI responses contain JSON wrapped in markdown code blocks, the helper automatically extracts the content:

```csharp
// Input: "```json\n{\"key\": \"value\"}\n```"
var minified = JsonFormatHelper.JsonToString(response);
// Output: "{\"key\":\"value\"}"

// Input: "```\n[1, 2, 3]\n```"
var token = JsonFormatHelper.StringToJson(response);
// Output: JArray with [1, 2, 3]

```

This follows the same pattern as `AIResponseParser.ExtractFromMarkdownCodeBlock()` for consistency across the codebase.

---

## Architecture & Design

### Solution Architecture

#### 1. JsonFormatHelper.cs

Central utility that consolidates JSON string operations:

- **Formatting**: All outputs are minified via `Formatting.None`
- **Extraction**: Strips markdown code fences before parsing
- **Validation**: Every path validates JSON before returning it
- **Dual API**: Every operation has a silent overload and an `out string error` overload

#### 2. Refactored Components

Existing JSON Grasshopper components were updated to call `JsonFormatHelper.JsonToString()` instead of calling `JToken.ToString()` directly. This guarantees identical behavior regardless of which component produced the JSON.

#### 3. Refactored AI Tools

AI tools that emit JSON (`text2json`) or consume AI-generated JSON (`AIText2JsonComponent`) now normalize through `JsonFormatHelper` so downstream components receive predictable, minified strings.

### Integration Points

**Existing Utilities:**

- Extends `JsonPathHelper` for consistent error messaging
- Uses `Newtonsoft.Json` (existing dependency)
- Follows SmartHopper naming conventions

**Parsing Pipeline:**

- Complements `AIResponseParser` for response extraction
- Works with `JsonSchemaService` for schema validation
- Integrates with all JSON-based AI tools

### Files Modified

| File | Changes |
| --- | --- |
| `JsonFormatHelper.cs` | Created |
| `JsonSchemaComponent.cs` | Updated to use `JsonToString()` |
| `JsonObjectComponent.cs` | Updated to use `JsonToString()` |
| `JsonMergeComponent.cs` | Updated to use `JsonToString()` |
| `JsonGetValueComponent.cs` | Already consistent |
| `text2json.cs` | Updated to use `JsonToString()` |
| `AIText2JsonComponent.cs` | Updated to use `JsonToString()` |

### Testing Recommendations

1. **Schema Generation:** Verify schemas are minified
2. **Object Creation:** Verify objects are minified
3. **Merge Operations:** Verify merged objects are minified
4. **AI Generation:** Verify AI-generated JSON is normalized
5. **Value Extraction:** Verify extracted values are minified
6. **Error Cases:** Verify invalid JSON is handled gracefully

### Future Enhancements

- Add JSON schema validation using JSON Schema Draft 7
- Add pretty-printing option for debugging
- Add JSON compression utilities
- Add JSON diff/merge utilities
- Add JSON path query helpers
