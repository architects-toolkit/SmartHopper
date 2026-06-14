# JSON Components

Pure-utility and AI-powered components for working with JSON in Grasshopper, under the `SmartHopper > JSON` category.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/JSON/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

JSON Components provide a complete toolkit for constructing, parsing, and querying JSON data within Grasshopper. They range from simple utility components for schema building and object manipulation to AI-powered components that generate structured JSON from natural language prompts.

**You should read this if:**

- You need to build or parse JSON schemas and objects in Grasshopper
- You want to use AI to generate structured JSON output from text prompts
- You are working with nested data and need dot-notation or bracket-notation path extraction
- You want to visually construct JSON schemas using Grasshopper components

---

## End-User Guide

### Components

#### AI-Powered

| Component | Nickname | Description |
| --- | --- | --- |-------------|
| `AIText2JsonComponent` | AIText2Json | Generate structured JSON from a prompt using AI, conforming to a provided JSON Schema |

#### Utility (No AI Required)

| Component | Nickname | Description |
| --- | --- | --- |-------------|
| `JsonSchemaComponent` | JsonSchema | Build a JSON Schema from property definitions; supports nested properties via dot-notation |
| `JsonObjectComponent` | JsonObject | Create a JSON object from key-value pairs |
| `JsonArrayComponent` | JsonArray | Create a JSON array from a list of items |
| `JsonArray2TextListComponent` | JsonArray2Text | Parse a JSON array string into a Grasshopper text list |
| `JsonObject2TextComponent` | Json2Text | Serialize a JSON value to a Grasshopper string (with optional pretty-print) |
| `JsonGetValueComponent` | JsonGetValue | Extract a nested value from a JSON object using dot-notation path |
| `JsonMergeComponent` | JsonMerge | Merge multiple JSON objects (shallow merge, last-wins) |

### AI Tool

The `AIText2JsonComponent` uses the `text2json` AI tool internally. See [Tools index](../../Tools/index.md) for details.

### Visual Schema Builder Components

Build schemas visually by wiring components together instead of typing format strings:

| Component | Nickname | Description |
| --- | --- | --- |-------------|
| `JsonSchemaPropComponent` | JsonSchemaProp | Property builder: Name, Description, Type, Array?, Required? → `"name:type:description:required"` string |
| `JsonSchemaObjectComponent` | JsonSchemaObj | Object builder: Name, Description, Properties → dot-prefixed property strings + Required list |

**Input order:**

- **JsonSchemaProp**: Name → Description → Type → Array? → Required?
- **JsonSchemaObject**: Name → Description → Properties
- **JsonSchemaComponent**: Title → Description → Properties → Type → Required (manual override)

**Wiring pattern (visual pipeline):**

```

JsonSchemaProp("street", "", "string", false, true)  ──┐  (Required? = true)
JsonSchemaProp("city",   "", "string", false, true)  ──┼──► JsonSchemaObject("address") ──► (Properties list)
JsonSchemaProp("zip",    "", "string", false, false) ──┘  (Required? = false)              │
                                                                                              ▼
JsonSchemaProp("name",   "", "string") ───────────────────────────────────────────►  Merge lists (Entwine/Insert)
JsonSchemaProp("age",    "", "integer") ───────────────────────────────────────────►        │
JsonSchemaProp("tags",   "", "string", true, false) ─────────────────────────────►        │  (Array? = true)
                                                                                            ▼
                                                                                   JsonSchemaComponent
                                                                                            │
                                                                                            ▼
                                                                                   AIText2JsonComponent.Schema

```

- **Array?** bool input on `JsonSchemaProp`: Set to `true` to create array properties (e.g., `tags:array[string]`)
- **Required?** bool input on `JsonSchemaProp`: Set to `true` to mark property as required; auto-bubbles up through `JsonSchemaObject` to `JsonSchemaComponent.Required`
- `JsonSchemaObject` outputs a **list** of dot-prefixed strings — merge with other properties using a GH `Entwine` or panel list
- The `Required` output of `JsonSchemaObject` gives `"address.street"` style names — connect to `JsonSchemaComponent.Required` for nested objects, or let auto-extraction handle it

---

### JsonSchemaComponent — Property Format

Properties are defined as strings with the format:

```

name:type
name:type:description
name:type:description:required

```

**Required suffix:** Append `:required` to automatically include the property in the schema's `required` array. This is auto-generated by `JsonSchemaProp` when **Required?** = true.

**Nested properties** use dot-notation paths:

```

address.city:string:The city name:required
address.zip:string

```

This produces a nested `address` object with `city` and `zip` Properties, where `city` is marked as required.

**Valid types:** `string`, `number`, `integer`, `boolean`, `object`, `array`

**Input order (reorganized):** Title → Description → Properties → Type → Required (manual override)

### JsonGetValueComponent — Path Examples

Supports both dot notation and bracket notation for accessing nested values:

| Path | Extracts |
| --- | --- | --- |
| `name` | Top-level `name` property |
| `address.city` | `city` inside `address` object |
| `results[0].name` | `name` property of first element in `results` array |
| `results[29].Effect` | `Effect` property of 30th element in `results` array |
| `data.tags[1]` | Second element of `tags` array inside `data` |

**Error Messages:** When a path fails to resolve, error messages clearly indicate the JSON path where the error occurred (e.g., `JSON Path: 'results[29].Effect' | Error: ...`), helping you quickly identify the problematic path.

---

## Developer Reference

The JSON components expose standard Grasshopper component patterns and can be interacted with programmatically. Below are examples of working with JSON schemas and path extraction in C#.

### Building a JSON Schema Programmatically

```csharp
using SmartHopper.Components.JSON;

// Create a schema component instance
var schemaComponent = new JsonSchemaComponent();
schemaComponent.Params.Input[0].AddVolatileDataListAtPath(new GH_Path(0), "Person");
schemaComponent.Params.Input[1].AddVolatileDataListAtPath(new GH_Path(0), "A person object");
schemaComponent.Params.Input[2].AddVolatileDataListAtPath(new GH_Path(0), "name:string:The person's name:required");
schemaComponent.Params.Input[2].AddVolatileDataListAtPath(new GH_Path(0), "age:integer");
schemaComponent.ExpireSolution(true);

// Retrieve the generated schema string
var schemaOutput = schemaComponent.Params.Output[0];
string jsonSchema = schemaOutput.VolatileData.get_FirstItem(true).Value as string;

```

### Extracting Nested Values with Dot Notation

```csharp
using SmartHopper.Components.JSON;
using Newtonsoft.Json.Linq;

// Create a JSON object and extract a nested value
var jsonObject = new JsonObjectComponent();
jsonObject.Params.Input[0].AddVolatileDataListAtPath(new GH_Path(0), "city");
jsonObject.Params.Input[1].AddVolatileDataListAtPath(new GH_Path(0), "Barcelona");
jsonObject.ExpireSolution(true);

var getValue = new JsonGetValueComponent();
getValue.Params.Input[0].AddVolatileDataListAtPath(new GH_Path(0), jsonObject.Params.Output[0].VolatileData.get_FirstItem(true).Value);
getValue.Params.Input[1].AddVolatileDataListAtPath(new GH_Path(0), "address.city");
getValue.ExpireSolution(true);

string result = getValue.Params.Output[0].VolatileData.get_FirstItem(true).Value as string;

```

---

## Architecture & Design

The JSON component suite is organized into two categories: AI-powered generation (`AIText2JsonComponent`) and pure utility components for schema and object manipulation. The visual schema builder (`JsonSchemaPropComponent`, `JsonSchemaObjectComponent`, and `JsonSchemaComponent`) enables users to construct JSON schemas through wiring rather than manual text editing, which reduces errors and improves readability in complex definitions.

The `AIText2JsonComponent` delegates to the `text2json` AI tool, which means it follows the same async execution model as other AI-powered components in SmartHopper. Utility components execute synchronously and do not require an AI provider to be configured.

All JSON utility components support standard Grasshopper data-tree semantics, allowing batch processing of multiple schemas or objects in parallel branches.

