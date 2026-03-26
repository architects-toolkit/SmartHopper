# JSON Components

Pure-utility and AI-powered components for working with JSON in Grasshopper, under the `SmartHopper > JSON` category.

## Components

### AI-Powered

| Component | Nickname | Description |
|-----------|----------|-------------|
| `AIText2JsonComponent` | AIText2Json | Generate structured JSON from a prompt using AI, conforming to a provided JSON Schema |

### Utility (No AI Required)

| Component | Nickname | Description |
|-----------|----------|-------------|
| `JsonSchemaComponent` | JsonSchema | Build a JSON Schema from property definitions; supports nested properties via dot-notation |
| `JsonObjectComponent` | JsonObject | Create a JSON object from key-value pairs |
| `JsonArrayComponent` | JsonArray | Create a JSON array from a list of items |
| `JsonArray2TextListComponent` | JsonArray2Text | Parse a JSON array string into a Grasshopper text list |
| `JsonObject2TextComponent` | Json2Text | Serialize a JSON value to a Grasshopper string (with optional pretty-print) |
| `JsonGetValueComponent` | JsonGetValue | Extract a nested value from a JSON object using dot-notation path |
| `JsonMergeComponent` | JsonMerge | Merge multiple JSON objects (shallow merge, last-wins) |

## AI Tool

The `AIText2JsonComponent` uses the `text2json` AI tool internally. See [Tools index](../../Tools/index.md) for details.

### Visual Schema Builder Components

Build schemas visually by wiring components together instead of typing format strings:

| Component | Nickname | Description |
|-----------|----------|-------------|
| `JsonSchemaPropComponent` | JsonSchemaProp | Scalar property: Name + Type + Description → `"name:type:description"` string |
| `JsonSchemaPropObjectComponent` | JsonSchemaPropObj | Object property: Name + Sub-Properties list → dot-prefixed property strings |
| `JsonSchemaPropArrayComponent` | JsonSchemaPropArr | Array property: Name + Items Type + Description → `"name:array[itemsType]:description"` string |

**Wiring pattern (visual pipeline):**

```
JsonSchemaProp("street", "string")  ──┐
JsonSchemaProp("city",   "string")  ──┼──► JsonSchemaPropObj("address") ──► (Properties list)
JsonSchemaProp("zip",    "string")  ──┘                                         │
                                                                                 ▼
JsonSchemaProp("name",   "string") ─────────────────────────────►  Merge lists (Entwine/Insert)
JsonSchemaProp("age",    "integer") ────────────────────────────►         │
JsonSchemaPropArr("tags", "string") ────────────────────────────►         │
                                                                           ▼
                                                                  JsonSchemaComponent
                                                                           │
                                                                           ▼
                                                                  AIText2JsonComponent.Schema
```

- `JsonSchemaPropObj` outputs a **list** of dot-prefixed strings — merge with other properties using a GH `Entwine` or panel list
- The `Required Names` output of `JsonSchemaPropObj` gives `"address.street"` style names — connect to `JsonSchemaComponent.Required`

---

## JsonSchemaComponent — Property Format

Properties are defined as strings with the format:

```
name:type
name:type:description
```

**Nested properties** use dot-notation paths:

```
address.city:string:The city name
address.zip:string
```

This produces a nested `address` object with `city` and `zip` sub-properties.

**Valid types:** `string`, `number`, `integer`, `boolean`, `object`, `array`

## JsonGetValueComponent — Path Examples

| Path | Extracts |
|------|---------|
| `name` | Top-level `name` property |
| `address.city` | `city` inside `address` object |
| `items.0` | First element of `items` array |
| `data.tags.1` | Second element of `tags` array inside `data` |
