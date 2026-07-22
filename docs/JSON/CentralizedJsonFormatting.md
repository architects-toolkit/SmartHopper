# Centralized JSON Formatting Implementation

## Overview

Implemented a centralized JSON formatting utility (`JsonFormatHelper`) to ensure consistent minified JSON output across all JSON components and AI tools in SmartHopper.

## Problem Solved

**Inconsistency Issues:**

- `JsonSchemaComponent` output indented JSON (`Formatting.Indented`)
- `JsonObjectComponent`, `JsonMergeComponent`, `JsonGetValueComponent` output minified JSON (`Formatting.None`)
- `text2json` tool returned raw AI response without normalization
- `AIText2JsonComponent` didn't normalize AI-generated JSON

This inconsistency made it difficult to maintain and could cause downstream processing issues.

## Solution Architecture

### 1. JsonFormatHelper.cs

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

- âœ… Single source of truth for JSON formatting
- âœ… Graceful error handling with fallbacks
- âœ… Consistent minified output (no whitespace)
- âœ… Automatic markdown code block extraction
- âœ… Validation before output
- âœ… Integrates with existing `JsonPathHelper`
- âœ… Follows AIResponseParser patterns for consistency

### 2. Refactored Components

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

### 3. Refactored AI Tools

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

## Benefits Achieved

| Benefit | Impact |
| --- | --- |
| **Consistency** | All JSON output is minified regardless of source |
| **Maintainability** | Single point of change for JSON formatting rules |
| **Validation** | JSON is validated before output |
| **Error Handling** | Graceful fallbacks for invalid JSON |
| **Integration** | Works seamlessly with existing `JsonPathHelper` |
| **Performance** | Minified output reduces data size |

## Usage Pattern

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

## Markdown Code Block Handling

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

## Integration Points

**Existing Utilities:**

- Extends `JsonPathHelper` for consistent error messaging
- Uses `Newtonsoft.Json` (existing dependency)
- Follows SmartHopper naming conventions

**Parsing Pipeline:**

- Complements `AIResponseParser` for response extraction
- Works with `JsonSchemaService` for schema validation
- Integrates with all JSON-based AI tools

## Files Modified

| File | Changes |
| --- | --- |
| `JsonFormatHelper.cs` | âœ… Created |
| `JsonSchemaComponent.cs` | âœ… Updated to use `MinifyJson()` |
| `JsonObjectComponent.cs` | âœ… Updated to use `MinifyJson()` |
| `JsonMergeComponent.cs` | âœ… Updated to use `MinifyJson()` |
| `JsonGetValueComponent.cs` | âœ… Already consistent |
| `text2json.cs` | âœ… Updated to use `NormalizeJson()` |
| `AIText2JsonComponent.cs` | âœ… Updated to use `EnsureMinified()` |

## Testing Recommendations

1. **Schema Generation:** Verify schemas are minified
2. **Object Creation:** Verify objects are minified
3. **Merge Operations:** Verify merged objects are minified
4. **AI Generation:** Verify AI-generated JSON is normalized
5. **Value Extraction:** Verify extracted values are minified
6. **Error Cases:** Verify invalid JSON is handled gracefully

## Future Enhancements

- Add JSON schema validation using JSON Schema Draft 7
- Add pretty-printing option for debugging
- Add JSON compression utilities
- Add JSON diff/merge utilities
- Add JSON path query helpers

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about CentralizedJsonFormatting.


## End-User Guide

End-user guidance for CentralizedJsonFormatting.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for CentralizedJsonFormatting.
