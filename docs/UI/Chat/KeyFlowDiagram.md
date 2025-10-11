# WebChat Key Flow Diagram

Visual representation of how keys are generated, transformed, and used throughout the interaction lifecycle.

---

## Interaction Type → Key Generation

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        IAIKeyedInteraction                               │
│                                                                          │
│  GetStreamKey()                           GetDedupKey()                 │
│       ↓                                         ↓                        │
│  [Stable, no content hash]              [Includes content hash]         │
│       ↓                                         ↓                        │
│  Used for streaming                      Used for de-duplication        │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────┐
│ AIInteractionText   │
├─────────────────────┤
│ TurnId: "abc123"    │
│ Agent: Assistant    │
│ Content: "Hello..." │
└──────┬──────────────┘
       │
       ├─→ GetStreamKey() → "turn:abc123:assistant"
       │
       └─→ GetDedupKey()  → "turn:abc123:assistant:f4a8b2c1"
                              (includes hash of content)

┌─────────────────────┐
│ AIInteractionToolCall│
├─────────────────────┤
│ TurnId: "abc123"    │
│ Id: "call_xyz"      │
│ Name: "gh_get"      │
│ Arguments: {...}    │
└──────┬──────────────┘
       │
       ├─→ GetStreamKey() → "turn:abc123:tool.call:call_xyz"
       │
       └─→ GetDedupKey()  → "turn:abc123:tool.call:call_xyz:{'filters':...}"
                              (includes full arguments)

┌─────────────────────┐
│AIInteractionToolResult│
├─────────────────────┤
│ TurnId: "abc123"    │
│ Id: "call_xyz"      │
│ Name: "gh_get"      │
│ Result: {...}       │
└──────┬──────────────┘
       │
       ├─→ GetStreamKey()    → "turn:abc123:tool.result:call_xyz"
       │
       ├─→ GetDedupKey()     → "turn:abc123:tool.result:call_xyz:9a7c3f12"
       │                        (includes hash of result)
       │
       └─→ GetFollowKey()    → "turn:abc123:tool.call:call_xyz"
                                (for insertion positioning)
```

---

## Segmentation System

```
┌────────────────────────────────────────────────────────────────────┐
│                    WebChatObserver Segmentation                     │
└────────────────────────────────────────────────────────────────────┘

Stream Key (base):  "turn:abc123:assistant"
                           ↓
              ┌────────────┴────────────┐
              │                         │
         [Segment 1]              [Segment 2]
              │                         │
              ↓                         ↓
"turn:abc123:assistant:seg1"  "turn:abc123:assistant:seg2"


Segment Increment Triggers:
┌─────────────────────────────────────────────────────────────────┐
│ 1. Agent Change                                                  │
│    - Last text was "user" → new text is "assistant" → NEW SEG  │
│                                                                  │
│ 2. Type Boundary                                                 │
│    - Last interaction was ToolCall → new text → NEW SEG        │
│    - Last interaction was ToolResult → new text → NEW SEG       │
│    - Last interaction was Error → new text → NEW SEG            │
│                                                                  │
│ 3. Explicit Boundary Flag                                        │
│    - OnInteractionCompleted sets _pendingNewTextSegmentTurns    │
│    - Next text OnDelta/OnInteractionCompleted consumes flag     │
└─────────────────────────────────────────────────────────────────┘


State Tracking (per turn):
┌──────────────────────────────────────────────────────────────────┐
│ Turn: "abc123"                                                    │
├──────────────────────────────────────────────────────────────────┤
│ _lastInteractionTypeByTurn["turn:abc123"] = typeof(ToolCall)    │
│ _lastTextAgentByTurn["turn:abc123"] = AIAgent.Assistant         │
│ _pendingNewTextSegmentTurns.Contains("turn:abc123") = true      │
│ _textInteractionSegments["turn:abc123:assistant"] = 2           │
└──────────────────────────────────────────────────────────────────┘
```

---

## Complete Streaming Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         STREAMING PATH                                   │
└─────────────────────────────────────────────────────────────────────────┘

Provider
   │
   │ Emit chunk 1
   ↓
┌──────────────────────────────────┐
│ OnDelta(AIInteractionText)       │
│   TurnId: "abc123"               │
│   Content: "Hello"               │
└────────┬─────────────────────────┘
         │
         ├─→ GetStreamKey() → "turn:abc123:assistant"
         │
         ├─→ Check boundary flag → none
         │
         ├─→ GetCurrentSegmentedKey() → "turn:abc123:assistant:seg1"
         │
         ├─→ Coalesce into _streams["turn:abc123:assistant:seg1"]
         │
         └─→ UpsertMessageByKey("turn:abc123:assistant:seg1", aggregated)
             (DOM: replace/insert bubble)

   │ Emit chunk 2
   ↓
┌──────────────────────────────────┐
│ OnDelta(AIInteractionText)       │
│   TurnId: "abc123"               │
│   Content: " world"              │
└────────┬─────────────────────────┘
         │
         ├─→ Same key: "turn:abc123:assistant:seg1"
         │
         ├─→ Append to aggregated: "Hello world"
         │
         └─→ UpsertMessageByKey("turn:abc123:assistant:seg1", aggregated)
             (DOM: update same bubble)

   │ Stream complete
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(Text)     │
│   TurnId: "abc123"               │
│   Content: "Hello world"         │
│   Metrics: {...}                 │
└────────┬─────────────────────────┘
         │
         ├─→ Find streaming aggregate in _streams["turn:abc123:assistant:seg1"]
         │
         ├─→ Update metrics/time
         │
         ├─→ UpsertMessageByKey("turn:abc123:assistant:seg1", finalized)
         │
         └─→ Set boundary: _pendingNewTextSegmentTurns.Add("turn:abc123")

   │ Tool call
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(ToolCall) │
│   TurnId: "abc123"               │
│   Id: "call_1"                   │
└────────┬─────────────────────────┘
         │
         ├─→ GetStreamKey() → "turn:abc123:tool.call:call_1"
         │
         ├─→ UpsertMessageByKey("turn:abc123:tool.call:call_1", toolCall)
         │
         └─→ Set boundary: _pendingNewTextSegmentTurns.Add("turn:abc123")

   │ Tool result
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(ToolResult)│
│   TurnId: "abc123"               │
│   Id: "call_1"                   │
└────────┬─────────────────────────┘
         │
         ├─→ GetStreamKey() → "turn:abc123:tool.result:call_1"
         │
         ├─→ GetFollowKey() → "turn:abc123:tool.call:call_1"
         │
         ├─→ UpsertMessageAfter(followKey, streamKey, toolResult)
         │   (DOM: insert after tool call bubble)
         │
         └─→ Set boundary: _pendingNewTextSegmentTurns.Add("turn:abc123")

   │ Next assistant chunk
   ↓
┌──────────────────────────────────┐
│ OnDelta(AIInteractionText)       │
│   TurnId: "abc123"               │
│   Content: "Done"                │
└────────┬─────────────────────────┘
         │
         ├─→ GetStreamKey() → "turn:abc123:assistant"
         │
         ├─→ Check boundary flag → FOUND! ("turn:abc123")
         │
         ├─→ Consume flag: _pendingNewTextSegmentTurns.Remove("turn:abc123")
         │
         ├─→ Increment: _textInteractionSegments["turn:abc123:assistant"] = 2
         │
         ├─→ GetCurrentSegmentedKey() → "turn:abc123:assistant:seg2"  (NEW!)
         │
         └─→ UpsertMessageByKey("turn:abc123:assistant:seg2", newAgg)
             (DOM: new bubble appears)

   │ Final
   ↓
┌──────────────────────────────────┐
│ OnFinal(AIReturn)                │
│   Last assistant in result       │
└────────┬─────────────────────────┘
         │
         ├─→ Find aggregate: _streams["turn:abc123:assistant:seg2"]
         │
         ├─→ Merge final metrics into aggregate
         │
         ├─→ UpsertMessageByKey("turn:abc123:assistant:seg2", finalWithMetrics)
         │   (DOM: update with metrics badge)
         │
         ├─→ Mark finalized: _finalizedTextTurns.Add("turn:abc123")
         │
         ├─→ Remove thinking bubble
         │
         └─→ Clear state: _streams, _textInteractionSegments, etc.
```

---

## Non-Streaming Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       NON-STREAMING PATH                                 │
└─────────────────────────────────────────────────────────────────────────┘

Provider
   │
   │ Return complete result
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(Text)     │
│   TurnId: "abc123"               │
│   Content: "Hello world"         │
└────────┬─────────────────────────┘
         │
         ├─→ GetStreamKey() → "turn:abc123:assistant"
         │
         ├─→ Check boundary flag → none
         │
         ├─→ GetCurrentSegmentedKey() → "turn:abc123:assistant:seg1"
         │
         ├─→ Store in _streams["turn:abc123:assistant:seg1"]
         │
         ├─→ UpsertMessageByKey("turn:abc123:assistant:seg1", text)
         │
         └─→ Set boundary: _pendingNewTextSegmentTurns.Add("turn:abc123")

   │
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(ToolCall) │
└────────┬─────────────────────────┘
         │
         └─→ (same as streaming)

   │
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(ToolResult)│
└────────┬─────────────────────────┘
         │
         └─→ (same as streaming)

   │
   ↓
┌──────────────────────────────────┐
│ OnInteractionCompleted(Text)     │
│   TurnId: "abc123"               │
│   Content: "Done"                │
└────────┬─────────────────────────┘
         │
         ├─→ GetStreamKey() → "turn:abc123:assistant"
         │
         ├─→ Check boundary flag → FOUND!
         │
         ├─→ Increment segment → 2
         │
         ├─→ GetCurrentSegmentedKey() → "turn:abc123:assistant:seg2"
         │
         └─→ UpsertMessageByKey("turn:abc123:assistant:seg2", text)

   │
   ↓
┌──────────────────────────────────┐
│ OnFinal(AIReturn)                │
└────────┬─────────────────────────┘
         │
         └─→ (same as streaming)
```

---

## History Replay Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         HISTORY REPLAY                                   │
│                    (WebView initialization)                              │
└─────────────────────────────────────────────────────────────────────────┘

WebView Document Loaded
   │
   ↓
┌──────────────────────────────────┐
│ ReplayFullHistoryToWebView()     │
└────────┬─────────────────────────┘
         │
         ├─→ Get ConversationSession.GetHistoryInteractionList()
         │
         └─→ For each interaction:

   ┌────────────────────────────────────┐
   │ Interaction 1: User text           │
   │   TurnId: "abc123"                 │
   └────────┬───────────────────────────┘
            │
            ├─→ Cast to IAIKeyedInteraction
            │
            ├─→ GetDedupKey() → "turn:abc123:user:a1b2c3d4"
            │
            └─→ UpsertMessageByKey("turn:abc123:user:a1b2c3d4", interaction)

   ┌────────────────────────────────────┐
   │ Interaction 2: Assistant text      │
   │   TurnId: "abc123"                 │
   └────────┬───────────────────────────┘
            │
            ├─→ GetDedupKey() → "turn:abc123:assistant:e5f6g7h8"
            │
            └─→ UpsertMessageByKey("turn:abc123:assistant:e5f6g7h8", interaction)
                (Note: NOT segmented key! Segments are runtime-only)

   ┌────────────────────────────────────┐
   │ Interaction 3: Tool call           │
   └────────┬───────────────────────────┘
            │
            └─→ GetDedupKey() → "turn:abc123:tool.call:call_1:{args}"

   ┌────────────────────────────────────┐
   │ Interaction 4: Tool result         │
   └────────┬───────────────────────────┘
            │
            └─→ GetDedupKey() → "turn:abc123:tool.result:call_1:9a7c3f12"

   ┌────────────────────────────────────┐
   │ Interaction 5: Assistant text      │
   │   TurnId: "abc123"                 │
   └────────┬───────────────────────────┘
            │
            └─→ GetDedupKey() → "turn:abc123:assistant:m3n4o5p6"
                (Different hash from interaction 2 due to different content)
```

**Key Observation:** History replay uses **dedup keys**, not segmented keys. This is because:
1. Segments are UI rendering state, not persisted in history
2. Multiple assistant texts in the same turn will have different dedup keys (different content hashes)
3. DOM idempotency still works via the `_lastDomHtmlByKey` cache

---

## Key Transformation Pipeline

```
┌────────────────────────────────────────────────────────────────────┐
│                     From Interaction to DOM                         │
└────────────────────────────────────────────────────────────────────┘

[Provider Emits Interaction]
         │
         ↓
    ┌─────────────────┐
    │ IAIInteraction  │
    │   • TurnId      │
    │   • Agent       │
    │   • Content     │
    └────────┬────────┘
             │
             ↓
    ┌─────────────────────────────┐
    │ IAIKeyedInteraction         │
    │   • GetStreamKey()          │  ← For streaming
    │   • GetDedupKey()           │  ← For de-duplication
    └────────┬────────────────────┘
             │
             ↓
    ┌─────────────────────────────┐
    │ WebChatObserver             │
    │   • Checks segmentation     │
    │   • Applies boundaries      │
    │   • Generates segmented key │
    └────────┬────────────────────┘
             │
             ↓
    ┌─────────────────────────────┐
    │ Segmented Key (if text)     │
    │ "turn:abc:assistant:seg2"   │
    │                             │
    │ OR                          │
    │                             │
    │ Stream Key (if non-text)    │
    │ "turn:abc:tool.call:c1"     │
    └────────┬────────────────────┘
             │
             ↓
    ┌─────────────────────────────┐
    │ WebChatDialog               │
    │   • UpsertMessageByKey()    │
    │   • Checks idempotency      │
    │   • Renders HTML            │
    └────────┬────────────────────┘
             │
             ↓
    ┌─────────────────────────────┐
    │ DOM Element                 │
    │   data-key="turn:..."       │
    │   <div class="message">     │
    └─────────────────────────────┘
```

---

## State Cleanup Timeline

```
┌────────────────────────────────────────────────────────────────────┐
│                       State Lifecycle                               │
└────────────────────────────────────────────────────────────────────┘

[OnStart]
   │
   ├─→ Clear _streams
   ├─→ Clear _textInteractionSegments
   ├─→ Clear _lastInteractionTypeByTurn
   ├─→ Clear _lastTextAgentByTurn
   ├─→ Clear _pendingNewTextSegmentTurns
   ├─→ Clear _finalizedTextTurns
   └─→ Reset _lastUpsertAt
   
   │
   │ ... streaming/interaction processing ...
   │
   
[OnFinal]
   │
   ├─→ Final render with metrics
   ├─→ Mark turn finalized
   ├─→ Clear _streams
   ├─→ Clear _textInteractionSegments
   ├─→ Clear _lastInteractionTypeByTurn
   ├─→ Clear _lastTextAgentByTurn
   └─→ (Keep _finalizedTextTurns until next OnStart)


[WebView DocumentLoaded]
   │
   └─→ Clear _lastDomHtmlByKey (idempotency cache reset)


[ClearChat]
   │
   ├─→ Clear _lastDomHtmlByKey
   └─→ (Observer state cleared on next OnStart)
```

---

## Key Decision Tree

```
┌──────────────────────────────────────────────────────────────────┐
│              Which key should I use?                              │
└──────────────────────────────────────────────────────────────────┘

START: I need to identify an interaction for DOM rendering
   │
   ├─→ Is this for streaming aggregation (OnDelta)?
   │   └─→ YES: Use GetCurrentSegmentedKey(GetStreamKey())
   │
   ├─→ Is this for persisted interaction (OnInteractionCompleted)?
   │   ├─→ Is it text?
   │   │   └─→ YES: Use GetCurrentSegmentedKey(GetStreamKey())
   │   └─→ Is it non-text?
   │       └─→ YES: Use GetStreamKey() directly
   │
   ├─→ Is this for final rendering (OnFinal)?
   │   ├─→ Does a streaming aggregate exist?
   │   │   └─→ YES: Use the segmented key from _streams
   │   └─→ No aggregate?
   │       └─→ Use GetDedupKey() (fallback for non-streamed)
   │
   ├─→ Is this for history replay (ReplayFullHistoryToWebView)?
   │   └─→ YES: Use GetDedupKey()
   │
   └─→ Is this for de-duplication check?
       └─→ YES: Use GetDedupKey()
```

---

**End of Diagram**
