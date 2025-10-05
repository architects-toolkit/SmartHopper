# Interaction Identification Schema

**Author:** Generated for SmartHopper WebChat System Review  
**Date:** 2025-10-05  
**Files Reviewed:** `WebChatDialog.cs`, `WebChatObserver.cs`, `HtmlChatRenderer.cs`

---

## Overview

The WebChat system uses a sophisticated key-based identification system to manage interaction rendering, de-duplication, and streaming aggregation. This document describes how keys are generated, updated, and used across streaming and non-streaming paths.

---

## Key Types

### 1. **Stream Key** (`GetStreamKey()`)
- **Purpose:** Groups streaming chunks together for real-time aggregation
- **Stability:** Stable during streaming; does NOT include content hash
- **Usage:** Primary key for DOM upserts during streaming

### 2. **Dedup Key** (`GetDedupKey()`)
- **Purpose:** Prevents duplicate interactions in history replay
- **Stability:** Includes content/result hash for exact de-duplication
- **Usage:** Used for history replay and final rendering

### 3. **Segmented Key** (WebChatObserver internal)
- **Purpose:** Creates multiple visual bubbles for the same turn/agent
- **Format:** `{StreamKey}:seg{N}`
- **Usage:** Multi-step tool calling within a single turn

---

## Key Generation by Interaction Type

### AIInteractionText

```
GetStreamKey():
  ├─ With TurnId:    "turn:{TurnId}:{agent}"
  └─ Without TurnId: "text:{agent}"

GetDedupKey():
  └─ "{StreamKey}:{hash(turnId:agent:content)}"
```

**Example:**
- StreamKey: `"turn:abc123:assistant"`
- DedupKey: `"turn:abc123:assistant:f4a8b2c1"`

---

### AIInteractionToolCall

```
GetStreamKey():
  ├─ With TurnId:    "turn:{TurnId}:tool.call:{id}"
  └─ Without TurnId: "tool.call:{id}"

GetDedupKey():
  └─ "{StreamKey}:{hash(arguments)}"
```

**Example:**
- StreamKey: `"turn:abc123:tool.call:call_xyz"`
- DedupKey: `"turn:abc123:tool.call:call_xyz:a1b2c3d4"`

---

### AIInteractionToolResult

```
GetStreamKey():
  ├─ With TurnId:    "turn:{TurnId}:tool.result:{id}"
  └─ Without TurnId: "tool.result:{id}"

GetDedupKey():
  └─ "{StreamKey}:{hash(id:result)}"

GetFollowKey():
  └─ "turn:{TurnId}:tool.call:{id}"  (for UpsertMessageAfter)
```

**Example:**
- StreamKey: `"turn:abc123:tool.result:call_xyz"`
- DedupKey: `"turn:abc123:tool.result:call_xyz:9a7c3f12"`
- FollowKey: `"turn:abc123:tool.call:call_xyz"` (inserts after this)

---

### AIInteractionError

```
GetStreamKey():
  ├─ With TurnId:    "turn:{TurnId}:error:{hash(content)}"
  └─ Without TurnId: "error:{hash(content)}"

GetDedupKey():
  └─ "{StreamKey}" (same as stream key)
```

**Example:**
- StreamKey: `"turn:abc123:error:e8f1a3b5"`
- DedupKey: `"turn:abc123:error:e8f1a3b5"`

---

### AIInteractionImage

```
GetStreamKey():
  ├─ With TurnId:    "turn:{TurnId}:image:{url|hash(data)|prompt}"
  └─ Without TurnId: "image:{url|hash(data)|prompt}"

GetDedupKey():
  └─ "{StreamKey}:{size}:{quality}:{style}"
```

**Example:**
- StreamKey: `"turn:abc123:image:https://..."`
- DedupKey: `"turn:abc123:image:https://...:1024x1024:standard:vivid"`

---

## Segmentation System

### Purpose
Allows multiple visual bubbles for the same agent within a single turn (e.g., assistant text before tool call, then assistant text after).

### Tracked State (WebChatObserver)

```csharp
// Per base stream key (e.g., "turn:{TurnId}:assistant")
Dictionary<string, int> _textInteractionSegments;

// Per turn key (e.g., "turn:{TurnId}")
HashSet<string> _pendingNewTextSegmentTurns;
```

### Segmentation Rules

**Simplified Rule:** A new segment is created when a text interaction arrives after **ANY** completed interaction (text or non-text) in the same turn.

**Mechanism:**
1. **Set Boundary:** After any `OnInteractionCompleted` call, set boundary flag for the turn
2. **Consume Boundary:** When next text interaction arrives (OnDelta or OnInteractionCompleted), consume flag and increment segment

### Segment Key Format

```
Base Stream Key:  "turn:abc123:assistant"
Segmented Keys:   "turn:abc123:assistant:seg1"
                  "turn:abc123:assistant:seg2"
                  "turn:abc123:assistant:seg3"
```

### When Segments Increment

```
Event Flow:
┌─────────────────────────────────────┐
│ OnInteractionCompleted(AnyInteraction) │
│   ├─ Render interaction            │
│   └─ SetBoundaryFlag(turnKey)      │
└─────────────────────────────────────┘
            ↓
┌─────────────────────────────────────┐
│ OnDelta/OnInteractionCompleted(TextInteraction) │
│   ├─ ConsumeBoundaryAndIncrement   │
│   │   ├─ Check boundary flag       │
│   │   ├─ If set: increment segment │
│   │   └─ Clear boundary flag       │
│   └─ Render text in new segment    │
└─────────────────────────────────────┘
```

### Example: Multi-Step Tool Calling

```
Turn abc123:
  1. Assistant text      → turn:abc123:assistant:seg1
  2. Tool call gh_get    → turn:abc123:tool.call:call_1
  3. Tool result gh_get  → turn:abc123:tool.result:call_1
  4. Assistant text      → turn:abc123:assistant:seg2  (NEW segment)
  5. Tool call gh_put    → turn:abc123:tool.call:call_2
  6. Tool result gh_put  → turn:abc123:tool.result:call_2
  7. Assistant text      → turn:abc123:assistant:seg3  (NEW segment)
```

---

## Streaming Path

### Flow Diagram

```
Provider Emits Chunk
       ↓
┌──────────────────────────────────────┐
│ OnDelta(AIInteraction partial)       │
│   ├─ For AIInteractionText:          │
│   │   ├─ Get base stream key         │
│   │   ├─ Check/consume boundary flag │
│   │   ├─ Get current segmented key   │
│   │   ├─ Coalesce into StreamState   │
│   │   └─ Throttled upsert (10ms)    │
│   └─ For other types: (skipped)      │
└──────────────────────────────────────┘
       ↓
┌──────────────────────────────────────┐
│ OnInteractionCompleted(persisted)    │
│   ├─ For AIInteractionText:          │
│   │   ├─ Finalize streaming agg      │
│   │   ├─ Update metrics/time         │
│   │   ├─ Upsert segmented key        │
│   │   └─ Set pending boundary flag   │
│   ├─ For AIInteractionToolCall:      │
│   │   ├─ Upsert by stream key        │
│   │   └─ Set pending boundary flag   │
│   └─ For AIInteractionToolResult:    │
│       ├─ UpsertMessageAfter followKey│
│       └─ Set pending boundary flag   │
└──────────────────────────────────────┘
       ↓
┌──────────────────────────────────────┐
│ OnFinal(AIReturn result)             │
│   ├─ Find final assistant interaction│
│   ├─ Locate streaming aggregate      │
│   ├─ Merge final metrics into agg    │
│   ├─ Prefer segmented key if exists  │
│   ├─ Final upsert (replaces stream)  │
│   ├─ Mark turn finalized             │
│   ├─ Remove thinking bubble          │
│   └─ Clear streaming state           │
└──────────────────────────────────────┘
```

### Key Usage in Streaming

| Event | Interaction Type | Key Used | DOM Operation |
|-------|-----------------|----------|---------------|
| **OnDelta** | AIInteractionText | Segmented key | UpsertMessageByKey (throttled) |
| **OnInteractionCompleted** | AIInteractionText | Segmented key | UpsertMessageByKey (final) |
| **OnInteractionCompleted** | AIInteractionToolCall | Stream key | UpsertMessageByKey |
| **OnInteractionCompleted** | AIInteractionToolResult | Stream key + Follow key | UpsertMessageAfter |
| **OnFinal** | AIInteractionText | Segmented or Dedup key | UpsertMessageByKey (with metrics) |

---

## Non-Streaming Path

### Flow Diagram

```
Provider Returns Complete Result
       ↓
┌──────────────────────────────────────┐
│ OnInteractionCompleted(complete)     │
│   ├─ For AIInteractionText:          │
│   │   ├─ Check/consume boundary flag │
│   │   ├─ Initialize segment if needed│
│   │   ├─ Get current segmented key   │
│   │   ├─ Store in StreamState        │
│   │   ├─ Upsert segmented key        │
│   │   └─ Set pending boundary flag   │
│   ├─ For AIInteractionToolCall:      │
│   │   ├─ Upsert by stream key        │
│   │   └─ Set pending boundary flag   │
│   └─ For AIInteractionToolResult:    │
│       ├─ UpsertMessageAfter followKey│
│       └─ Set pending boundary flag   │
└──────────────────────────────────────┘
       ↓
┌──────────────────────────────────────┐
│ OnFinal(AIReturn result)             │
│   ├─ Same logic as streaming path    │
│   └─ May use dedup key if no stream  │
└──────────────────────────────────────┘
```

### Key Difference from Streaming

- **No OnDelta calls** (no incremental updates)
- **OnInteractionCompleted** receives complete interactions
- **Segmentation still applies** (multi-step tool calls)
- **OnFinal** may use dedup key if no streaming aggregate exists

---

## Key State Lifecycle

### Per-Run State (Cleared on OnStart/OnFinal)

```csharp
_streams                          // Active streaming aggregates by segmented key
_textInteractionSegments          // Current segment number per base stream key
_pendingNewTextSegmentTurns       // Flags indicating next text should start new segment
_finalizedTextTurns               // Turns marked final (prevent late delta overrides)
_lastUpsertAt                     // Throttling timestamps per key
```

### Key Generation Timeline

```
STREAMING PATH:
┌─────────────────────────────────────────────────────────────┐
│ Time  │ Event              │ Key Generated         │ Action │
├───────┼────────────────────┼──────────────────────┼────────┤
│ T0    │ OnStart            │ -                     │ Reset  │
│ T1    │ OnDelta (chunk 1)  │ turn:123:assistant:seg1 │ Upsert │
│ T2    │ OnDelta (chunk 2)  │ turn:123:assistant:seg1 │ Upsert │
│ T3    │ OnInteractionComp  │ turn:123:assistant:seg1 │ Finalize│
│ T4    │ OnInteractionComp  │ turn:123:tool.call:c1  │ Upsert │
│ T5    │ OnInteractionComp  │ turn:123:tool.result:c1│ UpsertAfter│
│ T6    │ OnDelta (chunk 3)  │ turn:123:assistant:seg2 │ Upsert │  (NEW seg)
│ T7    │ OnInteractionComp  │ turn:123:assistant:seg2 │ Finalize│
│ T8    │ OnFinal            │ turn:123:assistant:seg2 │ Final+Metrics│
└─────────────────────────────────────────────────────────────┘

NON-STREAMING PATH:
┌─────────────────────────────────────────────────────────────┐
│ Time  │ Event              │ Key Generated         │ Action │
├───────┼────────────────────┼──────────────────────┼────────┤
│ T0    │ OnStart            │ -                     │ Reset  │
│ T1    │ OnInteractionComp  │ turn:123:assistant:seg1 │ Upsert │
│ T2    │ OnInteractionComp  │ turn:123:tool.call:c1  │ Upsert │
│ T3    │ OnInteractionComp  │ turn:123:tool.result:c1│ UpsertAfter│
│ T4    │ OnInteractionComp  │ turn:123:assistant:seg2 │ Upsert │  (NEW seg)
│ T5    │ OnFinal            │ turn:123:assistant:seg2 │ Final+Metrics│
└─────────────────────────────────────────────────────────────┘
```

---

## History Replay (WebChatDialog.ReplayFullHistoryToWebView)

### Purpose
When WebView initializes or resets, replay all persisted interactions from `ConversationSession.GetHistoryInteractionList()`.

### Key Used
**Dedup Key** (from `GetDedupKey()`)

### Why Dedup Key?
- More stable than segmented keys (segments are runtime state, not persisted)
- Prevents duplicate rendering if same interaction replayed
- Includes content hash to differentiate similar messages

### Replay Flow

```
WebView Initialized
       ↓
┌──────────────────────────────────────┐
│ ReplayFullHistoryToWebView()         │
│   ├─ Get history interactions        │
│   └─ For each interaction:           │
│       ├─ Cast to IAIKeyedInteraction │
│       ├─ Get dedup key               │
│       └─ UpsertMessageByKey(dedupKey)│
└──────────────────────────────────────┘
```

### Example

```
History:
  1. User text         → dedup: "turn:123:user:a1b2c3d4"
  2. Assistant seg1    → dedup: "turn:123:assistant:e5f6g7h8"
  3. Tool call         → dedup: "turn:123:tool.call:c1:{args}"
  4. Tool result       → dedup: "turn:123:tool.result:c1:9i0j1k2l"
  5. Assistant seg2    → dedup: "turn:123:assistant:m3n4o5p6"

Replay uses dedup keys for idempotent upserts.
```

---

## Idempotency Cache (WebChatDialog)

### Purpose
Prevent redundant DOM updates when the same HTML would be rendered twice.

### Implementation

```csharp
Dictionary<string, string> _lastDomHtmlByKey;

// Before upsert:
if (_lastDomHtmlByKey.TryGetValue(domKey, out var last) && 
    string.Equals(last, html, StringComparison.Ordinal))
{
    // Skip DOM update
    return;
}

// After upsert:
_lastDomHtmlByKey[domKey] = html;
```

### Cleared On
- Document reload
- ClearChat() command

---

## Finalization Protection

### Purpose
Prevent late `OnDelta` or `OnInteractionCompleted` calls from overriding final metrics/timestamp.

### Implementation

```csharp
HashSet<string> _finalizedTextTurns;

// In OnFinal:
var turnKey = GetTurnBaseKey(finalAssistant?.TurnId);
_finalizedTextTurns.Add(turnKey);

// In OnDelta/OnInteractionCompleted:
if (_finalizedTextTurns.Contains(turnKey))
{
    return; // Ignore late updates
}
```

---

## DOM Operations

### UpsertMessageByKey(domKey, interaction)
- **Purpose:** Insert or replace message with given key
- **JavaScript:** `upsertMessage(key, html)`
- **Idempotency:** Checks `_lastDomHtmlByKey` cache before executing

### UpsertMessageAfter(followKey, domKey, interaction)
- **Purpose:** Insert message immediately after another message
- **JavaScript:** `upsertMessageAfter(followKey, key, html)`
- **Usage:** Tool results follow their tool calls

### AddInteractionToWebView(interaction)
- **Purpose:** Append message without key (rare, for keyless fallback)
- **JavaScript:** `addMessage(html)`
- **Usage:** Only when interaction doesn't implement `IAIKeyedInteraction`

---

## Key Design Principles

1. **Turn-Based Stability:** Keys include `TurnId` when available for stable grouping
2. **Agent Scoping:** Keys include agent to avoid collisions (assistant vs user vs tool)
3. **Content Independence (Stream Keys):** Stream keys exclude content for stable streaming
4. **Content Dependence (Dedup Keys):** Dedup keys include hash for exact de-duplication
5. **Segmentation Transparency:** Segments are UI-only; not visible to providers or session
6. **Type Safety:** Interface-first design (`IAIKeyedInteraction`) avoids type switches

---

## Future Considerations

### Potential Issues
1. **Segment State Not Persisted:** If session is serialized/restored, segments won't match original render
2. **Key Length:** Long tool arguments or URLs may create large keys
3. **Hash Collisions:** 16-char hash (64-bit) has ~1 in 2^64 collision risk

### Possible Enhancements
1. **Persist Segment Numbers:** Store segment in interaction metadata for replay fidelity
2. **Key Compression:** Use full SHA256 hash instead of 16-char prefix
3. **Segment Reset Logic:** Allow explicit segment reset commands for complex workflows

---

## Summary Table

| Concept | Purpose | Generated By | Used By | Lifetime |
|---------|---------|-------------|---------|----------|
| **Stream Key** | Group streaming chunks | `GetStreamKey()` | OnDelta, OnInteractionCompleted | Per-run |
| **Dedup Key** | Prevent duplicates | `GetDedupKey()` | ReplayFullHistory, OnFinal (fallback) | Persistent |
| **Segmented Key** | Multiple bubbles per turn/agent | `GetCurrentSegmentedKey()` | OnDelta, OnInteractionCompleted (text) | Per-run |
| **Turn Key** | Group turn state | `GetTurnBaseKey()` | Segmentation logic | Per-run |
| **Follow Key** | Position tool result after call | `GetFollowKeyForToolResult()` | UpsertMessageAfter | Per-operation |

---

**End of Schema**
