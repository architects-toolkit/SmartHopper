# GhPatch Format Specification

**Version**: 1.0
**Status**: Draft
**Last Updated**: 2026-05-13

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Document Structure](#2-document-structure)
3. [Identity Rules](#3-identity-rules)
4. [Operations](#4-operations)
5. [Apply Semantics](#5-apply-semantics)
6. [Example](#6-example)
7. [Relationship to JSON Patch](#7-relationship-to-json-patch)

---

## 1. Introduction

### 1.1 Purpose

GhPatch is a JSON-based patch format for [GhJSON](specification.md) documents. A patch describes a set of add/remove/modify operations on components, connections, groups, and metadata at the **GhJSON-semantic level** — not at the JSON-pointer level.

GhPatch enables:

- **Version control** — produce a compact, reviewable diff between two `.ghjson` files (like `git diff`).
- **Partial edits** — apply a small change on top of a larger definition without rewriting the whole file.
- **AI-assisted editing** — let LLMs emit small, targeted edits to existing definitions.
- **Sharing snippets** — distribute reusable edits (e.g. "add a logging panel to any definition").

### 1.2 Design principles

1. **Semantic identity, not paths.** Components, connections, and groups are identified by their GhJSON identity (`instanceGuid`, `id`), not by array index or JSON pointer. Reordering the components array does not invalidate a patch.
2. **No move operations.** Component/connection/group order in the GhJSON arrays has no semantic meaning, so patches never need `move`.
3. **Structured grammar.** Each entity has a small, fixed grammar (`add` / `remove` / `modify` with nested `set` / `remove` / sub-grammars), all derived from the GhJSON schema.
4. **Conflicts at apply time, not in the patch.** A patch is a pure description of intent; the apply step is responsible for surfacing conflicts.
5. **Schema-versioned.** A patch is tied to a specific GhJSON schema version. Cross-version patching is out of scope for `1.x`.

### 1.3 File extension

GhPatch files use the `.ghpatch` extension.

### 1.4 Encoding

GhPatch files MUST be encoded in UTF-8 without BOM.

### 1.5 Relationship to GhJSON

GhPatch is a **sibling format** to GhJSON, not a profile of it. A `.ghjson` document is never also a `.ghpatch` document and vice versa. The two have separate JSON Schemas:

- `https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghjson.schema.json`
- `https://architects-toolkit.github.io/ghjson-spec/schema/v1.0/ghpatch.schema.json`

---

## 2. Document Structure

A GhPatch document is a JSON object with the following top-level structure:

```json
{
  "schema": "1.0",
  "kind": "ghpatch",
  "patch": {
    "base": { ... },
    "metadata": { ... },
    "components": { ... },
    "connections": { ... },
    "groups": { ... }
  }
}
```

### 2.1 Top-level properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `schema` | string | No | GhJSON schema version this patch targets (default: `"1.0"`). |
| `kind` | string | **Yes** | Discriminator. MUST be `"ghpatch"`. |
| `patch` | object | **Yes** | Patch body — see §4. |

### 2.2 The `patch.base` reference

`patch.base` is an optional reference to the base document the patch was generated against:

```json
{
  "base": {
    "schema": "1.0",
    "checksum": "sha256-abc123..."
  }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `schema` | string | Schema version of the base document. |
| `checksum` | string | Content checksum of the **normalised** base document. Format: `<algorithm>-<value>`. The recommended algorithm is `sha256`. |

The normalised form for checksum purposes:

1. Apply the default `Fix` operations (assign missing IDs, regenerate metadata counters, etc.) without regenerating instance GUIDs.
2. Drop volatile fields (`metadata.modified`, `metadata.componentCount`, `metadata.connectionCount`, `metadata.groupCount`, `components[].warnings`, `components[].errors`, `components[].remarks`).
3. Sort each array deterministically (`components` by `id`, `connections` by `(from.id, to.id, from.paramName, to.paramName)`, `groups` by `id`).
4. Serialise as canonical JSON (sorted object keys, no insignificant whitespace).
5. Hash with the chosen algorithm.

When `patch.base.checksum` is present, implementations MUST default to verifying it before applying and MUST refuse to apply on mismatch unless explicitly opted out (see §5).

---

## 3. Identity Rules

### 3.1 Component identity

When a patch operation references an existing component (via a `match` block or a `remove` entry), the implementation MUST resolve identity using this precedence:

1. **`instanceGuid`** — match by `instanceGuid` if present in the match block AND in the base document.
2. **`id`** — match by integer `id`.
3. **Structural fingerprint** — match by `{ componentGuid, name }` plus, when provided, `pivot` as a tie-breaker.

If no match block is provided that satisfies at least one of these rules, the operation MUST be reported as a conflict.

If multiple base components match a single match block, the operation MUST be reported as a conflict (the patch is ambiguous).

### 3.2 Connection identity

Connections are identified by their full `(from, to)` endpoint pair. Within an endpoint, `paramName` is the canonical parameter identifier; `paramIndex` is a fall-back when names are localised or have been renamed.

When diffing, implementations SHOULD canonicalise every endpoint to use `paramName` (resolving `paramIndex` against the component's parameter list if needed). Patches emitted by diff tools SHOULD always include `paramName`.

When a patch references a connection that does not exist on the base (for `remove`) or that already exists (for `add`), the operation MUST be reported as a conflict.

### 3.3 Group identity

Groups are identified by `instanceGuid` (preferred) or `id`. Same precedence rules and conflict behaviour as components.

### 3.4 New-component ID handling

For `components.add`, the new component's `id` MAY collide with an existing component on the base. When that happens:

- The implementation MUST allocate the next free `id` for the new component.
- Any subsequent operation in the same patch that references the new component by its original `id` (e.g. a `connections.add` from a freshly-added component) MUST be rewritten to use the allocated id.
- The implementation MUST report the remapping in its result.

`components.add` and `groups.add` entries MUST NOT specify `instanceGuid`. Implementations MUST reject such patches at validation time; the `instanceGuid` is generated when the component or group is placed on the canvas.

---

## 4. Operations

### 4.1 `patch.metadata`

```json
"metadata": {
  "set": { "title": "Updated", "author": "Jane" },
  "remove": ["description"]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `set` | object | Metadata fields to set or replace. Same shape as `documentMetadata` in `ghjson.schema.json`. |
| `remove` | string[] | Metadata field names to remove. |

`set` and `remove` MUST NOT both target the same field.

### 4.2 `patch.components`

```json
"components": {
  "add":    [ <full component object> ],
  "remove": [ { "instanceGuid": "..." } ],
  "modify": [ { "match": { ... }, "set": { ... }, ... } ]
}
```

#### 4.2.1 `add`

Each entry is a full component object as defined in `ghjson.schema.json#/$defs/componentData`, but MUST NOT include `instanceGuid`. The identity fields that SHOULD be present are the type identity (`name` and/or `componentGuid`) and an `id` for cross-reference by connections and groups; the `instanceGuid` is generated on placement.

#### 4.2.2 `remove`

Each entry is a `componentMatch` (see §3.1).

#### 4.2.3 `modify`

Each entry has the shape:

```json
{
  "match": { ... },
  "set":   { "pivot": "120,200", "nickName": "Add!" },
  "remove": ["warnings"],
  "componentState": {
    "set":    { "locked": true },
    "extensions": {
      "set":    { "gh.numberslider": { "value": "7<0~10>" } },
      "remove": ["gh.scribble"]
    }
  },
  "inputSettings": {
    "byParameterName": {
      "x": { "set": { "typeHint": "double" } }
    }
  },
  "outputSettings": { ... }
}
```

- `match` — `componentMatch` (required).
- `set` / `remove` — operate on top-level scalar component fields (`name`, `library`, `nickName`, `componentGuid`, `instanceGuid`, `id`, `pivot`).
- `componentState` — operations on `componentState`. The nested `extensions` sub-grammar treats each extension value (`gh.panel`, `gh.csharp`, …) as opaque: `set` replaces the whole extension object, `remove` deletes it.
- `inputSettings` / `outputSettings` — operations on the parameter settings list, keyed by `parameterName`. Each per-parameter op supports `set` and `remove` of fields inside that `parameterSettings` entry.

Implementations MAY support adding/removing entire entries to `inputSettings` / `outputSettings` in a future revision, but `1.0` keeps the surface area small and assumes parameters are not added/removed by a patch (which would imply a different component).

### 4.3 `patch.connections`

```json
"connections": {
  "add":    [ { "from": { "id": 1, "paramName": "Number" }, "to": { "id": 2, "paramName": "A" } } ],
  "remove": [ { "from": { "id": 1, "paramName": "Number" }, "to": { "id": 2, "paramName": "B" } } ]
}
```

Connections are immutable in this format: there is no `modify`. To change a connection, `remove` it and `add` the new one.

### 4.4 `patch.groups`

```json
"groups": {
  "add":    [ <full group object> ],
  "remove": [ { "instanceGuid": "..." } ],
  "modify": [
    {
      "match": { "id": 1 },
      "set":   { "name": "Inputs", "color": "argb:255,200,220,255" },
      "remove": [],
      "members": { "add": [3, 4], "remove": [9] }
    }
  ]
}
```

`members.add` / `members.remove` operate on the group's `members` integer-id list. Members SHOULD reference components that exist on the base (or are being added in the same patch); references that resolve to neither SHOULD be reported as conflicts and dropped from the resulting document.

`groups.add` entries MUST NOT specify `instanceGuid`; the `instanceGuid` is generated on placement.

---

## 5. Apply Semantics

### 5.1 Order of operations

Patch application MUST proceed in this deterministic order:

1. Verify `patch.base.checksum` if present (see §5.2).
2. Apply `patch.metadata`.
3. Apply `patch.components.modify`.
4. Apply `patch.components.remove`.
5. Apply `patch.components.add` (allocating new IDs as needed — §3.4).
6. Apply `patch.groups.*` (modify → remove → add).
7. Apply `patch.connections.remove`.
8. Apply `patch.connections.add`.
9. Run a final structural fix-up: drop connections that now reference missing components, drop group members that no longer exist, refresh metadata counters.

This ordering ensures that:

- A `modify` against a component that is later `remove`d is reported as a conflict, not silently lost.
- A `connections.add` can reference a component freshly created in `components.add`.
- The output document is structurally valid even when the patch was partially conflicting.

### 5.2 Base verification

When `patch.base.checksum` is present, the default behaviour is **strict**: refuse to apply when the checksum of the normalised base document does not match. Implementations MAY expose an "apply anyway" opt-out, but it MUST NOT be the default.

When `patch.base` is absent or has no `checksum`, no verification is performed.

### 5.3 Conflicts

A conflict is any situation where the patch cannot be applied unambiguously:

| Kind | Example |
|------|---------|
| `match_not_found` | A `modify`/`remove` references a component that is not on the base. |
| `match_ambiguous` | A `match` block matches more than one base component. |
| `id_collision` | A `components.add` has the same `id` as an existing component and id renumbering is disabled. |
| `connection_already_present` | A `connections.add` duplicates an existing connection. |
| `connection_not_found` | A `connections.remove` targets a connection that does not exist. |
| `dangling_member` | A `groups.modify.members.add` references a component that does not exist. |
| `base_checksum_mismatch` | Base verification failed. |

Implementations SHOULD report conflicts as a structured list. The default policy SHOULD be "apply what can be applied and report the rest"; "fail fast on first conflict" and "skip and report" SHOULD also be supported.

### 5.4 Idempotence

Applying the same patch twice on the same base document SHOULD produce:

- The same result document on the first apply.
- Either the same result document with all operations reported as conflicts on the second apply, or an "already applied" outcome — implementations are free to choose, but the behaviour MUST be deterministic.

---

## 6. Example

The following patch, applied to a base document containing a Number Slider (id 1) and an Addition component (id 2), would:

- Change the slider value from `5` to `7`.
- Rename the Addition component to `Add!`.
- Add a Panel (id 3) and connect the Addition's output to it.
- Lock the Addition component.

```json
{
  "schema": "1.0",
  "kind": "ghpatch",
  "patch": {
    "base": {
      "schema": "1.0",
      "checksum": "sha256-abcd1234..."
    },
    "components": {
      "add": [
        {
          "name": "Panel",
          "componentGuid": "59e0b89a-e487-49f8-bab8-b5bab16be14c",
          "id": 3,
          "pivot": "500,100"
        }
      ],
      "modify": [
        {
          "match": { "instanceGuid": "11111111-1111-1111-1111-111111111111" },
          "componentState": {
            "extensions": {
              "set": {
                "gh.numberslider": { "value": "7<0~10>" }
              }
            }
          }
        },
        {
          "match": { "id": 2 },
          "set": { "nickName": "Add!" },
          "componentState": { "set": { "locked": true } }
        }
      ]
    },
    "connections": {
      "add": [
        { "from": { "id": 2, "paramName": "Result" }, "to": { "id": 3, "paramName": "Input" } }
      ]
    }
  }
}
```

---

## 7. Relationship to JSON Patch

GhPatch is **not** [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902). The two solve overlapping problems but at different levels:

| Aspect | JSON Patch (RFC 6902) | GhPatch |
|--------|-----------------------|---------|
| Identity | JSON Pointer (`/components/3`) | GhJSON identity (`instanceGuid`, `id`, …) |
| Move support | Yes (`move` op) | No (array order is non-semantic) |
| Extension awareness | None — pure JSON tree edits | Aware of `componentState.extensions` opaque values |
| Apply order | Sequential per-op | Deterministic phase order (§5.1) |
| Conflicts | Single failure ("test" op) | Structured conflict list |

JSON Patch is appropriate when treating a GhJSON document as a generic JSON tree (for example, when piping through generic JSON tooling). GhPatch is appropriate when treating it as a GhJSON document with first-class semantics.
