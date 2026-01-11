# Special Turns

Special turns are a flexible system for executing AI requests with custom overrides while leveraging the regular conversation flow infrastructure.

## Overview

A **special turn** is an AI request that:
- Executes through the regular `ConversationSession` conversation flow
- Can override interactions, provider settings, tools, and context
- Controls how results are persisted to conversation history
- Maintains automatic state snapshot and restore

## Key Concepts

### Configuration

Special turns are configured via `SpecialTurnConfig`:

```csharp
var config = new SpecialTurnConfig
{
    // Request overrides
    OverrideInteractions = customInteractions,
    OverrideProvider = "openai",
    OverrideModel = "gpt-4",
    OverrideToolFilter = "-*",  // Disable all tools
    
    // Execution behavior
    ProcessTools = false,
    ForceNonStreaming = false,  // Allow streaming
    TimeoutMs = 30000,  // 30 second timeout
    
    // History persistence
    PersistenceStrategy = HistoryPersistenceStrategy.PersistResult,
    PersistenceFilter = InteractionFilter.Default,
    
    // Metadata
    TurnType = "greeting",
    Metadata = new Dictionary<string, object>
    {
        ["is_greeting"] = true
    }
};

var result = await session.ExecuteSpecialTurnAsync(
    config,
    preferStreaming: true,
    cancellationToken);
```

### History Persistence Strategies

Four strategies control how special turn results are persisted:

#### 1. PersistResult (Default)
Only persists the result interactions (typically assistant responses) to history.

**Use case:** AI-generated greetings where you don't want the generation prompt visible.

```csharp
PersistenceStrategy = HistoryPersistenceStrategy.PersistResult
```

#### 2. PersistAll
Persists all interactions (input + result) to history, filtered by `PersistenceFilter`.

**Use case:** Multi-step reasoning where intermediate steps should be visible.

```csharp
PersistenceStrategy = HistoryPersistenceStrategy.PersistAll,
PersistenceFilter = InteractionFilter.Default  // Excludes system/context
```

#### 3. Ephemeral
Executes the turn but doesn't persist anything to history.

**Use case:** Internal processing, analysis, or validation without disturbing UI/history.

```csharp
PersistenceStrategy = HistoryPersistenceStrategy.Ephemeral
```

#### 4. ReplaceAbove
Replaces all previous interactions with the special turn result, filtered by `PersistenceFilter`.

**Use case:** Conversation summarization that replaces long history with a summary.

```csharp
PersistenceStrategy = HistoryPersistenceStrategy.ReplaceAbove,
PersistenceFilter = InteractionFilter.PreserveSystemContext  // Keep system/context
```

### Interaction Filtering

`InteractionFilter` provides granular control over which interaction types are included using an allowlist/blocklist approach:

```csharp
// Allow only specific agent types
var filter = InteractionFilter.Allow(AIAgent.User, AIAgent.Assistant);

// Block specific agent types (allow all others)
var filter = InteractionFilter.Block(AIAgent.System, AIAgent.Context);

// Fluent API for complex filters
var filter = new InteractionFilter()
    .WithAllow(AIAgent.User, AIAgent.Assistant)
    .WithBlock(AIAgent.Context);

// Direct manipulation
var filter = new InteractionFilter
{
    AllowedAgents = new HashSet<AIAgent> { AIAgent.User, AIAgent.Assistant },
    BlockedAgents = new HashSet<AIAgent> { AIAgent.System }
};
```

**Filter Logic:**
- Blocklist takes precedence over allowlist
- Empty allowlist means "allow all" (unless blocked)
- Non-empty allowlist means "allow only these" (unless blocked)

**Predefined filters:**
- `InteractionFilter.Default` - Conversation only (User, Assistant, Tools)
- `InteractionFilter.PreserveSystemContext` - Blocks System/Context, allows all others
- `InteractionFilter.AllowAll` - No restrictions

**Future-proof:** Automatically supports new interaction types (images, audio, etc.) without code changes

## Built-in Special Turns

### Greeting Turn

Factory for AI-generated greetings:

```csharp
var greetingConfig = GreetingSpecialTurn.Create(
    providerName: "openai",
    systemPrompt: "You are a Grasshopper expert..."  // Optional
);

var greeting = await session.ExecuteSpecialTurnAsync(
    greetingConfig,
    preferStreaming: true,
    cancellationToken);
```

**Configuration:**
- Uses provider's default Text2Text model
- Disables all tools (`-*`)
- 30 second timeout
- `PersistResult` strategy (only greeting appears in history)

### Summarize Turn

Factory for conversation summarization when context limits are approached:

```csharp
var summarizeConfig = SummarizeSpecialTurn.Create(
    providerName: "openai",
    conversationModel: session.Request.Model,
    conversationHistory: session.GetHistoryInteractionList(),
    lastUserMessage: lastUserInteraction  // Optional, preserved after summary
);

var summary = await session.ExecuteSpecialTurnAsync(
    summarizeConfig,
    preferStreaming: false,
    cancellationToken);
```

**Configuration:**

- Uses the conversation's current model (so the summarizer has the same context limit)
- Disables all tools (`-*`)
- 60 second timeout
- `ReplaceAbove` strategy with `PreserveSystemContext` filter
- Generates a concise summary preserving key topics, decisions, and context

**Automatic Usage:**

The `ConversationSession` automatically triggers summarization when:

1. Context usage exceeds 80% of the model's context limit (pre-emptive)
2. A context exceeded error is received from the provider (reactive)

**Summarization behavior:**

- Finds the **last user message** in history
- Summarizes everything **before** that user message
- Uses `ReplaceAbove` to replace old history with summary (preserves system/context messages)
- Manually appends the last user message after the summary
- **Drops all interactions after the last user message** (assistant responses, tool calls, tool results)
  - This ensures a clean conversation state and prevents token bloat from incomplete turns
  - The next provider call will be a fresh assistant response to the preserved user message

## Execution Flow

1. **Snapshot**: Current request state is captured for final persistence
2. **Clone**: An isolated `AIRequestCall` is created with config overrides applied
3. **Execute**: Request executes on isolated clone (no observer notifications)
   - Streaming mode uses provider's streaming adapter
   - Non-streaming mode uses standard execution
4. **Persist**: Result is persisted to main conversation according to strategy
   - **This is when observers are notified** (not during execution)

**Key benefit**: Special turn execution is completely isolated from the main conversation until persistence, preventing internal interactions (e.g., greeting generation prompts) from appearing in the UI.

## Observer Behavior

Special turns are **isolated from observers** during execution:

- **No deltas during execution**: Observers don't receive `OnDelta` events during special turn streaming
- **No partial notifications**: Execution happens in isolation without observer notifications
- **Single final event**: Observers receive `OnFinal` only when persistence strategy merges results into main conversation (except `Ephemeral` strategy)

**Result**: UI only shows the final persisted interactions according to the persistence strategy. Internal special turn interactions (system prompts, tool calls, intermediate reasoning) remain hidden from observers.

## Error Handling

If a special turn fails:
- Error is captured in `AIReturn` with error message
- Error is persisted to history according to `PersistenceStrategy`
- Main conversation remains unaffected (isolated execution protects against side effects)

## Use Cases

### 1. AI-Generated Greeting
```csharp
var config = GreetingSpecialTurn.Create(provider, systemPrompt);
var greeting = await session.ExecuteSpecialTurnAsync(config, preferStreaming: true);
```

### 2. Conversation Summary
```csharp
var config = new SpecialTurnConfig
{
    OverrideInteractions = new List<IAIInteraction>
    {
        new AIInteractionText { Agent = AIAgent.System, Content = "Summarize the conversation..." },
        new AIInteractionText { Agent = AIAgent.User, Content = conversationHistory }
    },
    PersistenceStrategy = HistoryPersistenceStrategy.ReplaceAbove,
    PersistenceFilter = InteractionFilter.PreserveSystemContext,
    TurnType = "summary"
};
```

### 3. Internal Validation (No Trace)
```csharp
var config = new SpecialTurnConfig
{
    OverrideInteractions = validationPrompt,
    PersistenceStrategy = HistoryPersistenceStrategy.Ephemeral,
    TurnType = "validation"
};
var validationResult = await session.ExecuteSpecialTurnAsync(config, preferStreaming: false);
// Result available but not in history
```

### 4. Image Generation (Force Non-Streaming)
```csharp
var config = new SpecialTurnConfig
{
    OverrideInteractions = imagePrompt,
    OverrideCapability = AICapability.ImageOutput,
    ForceNonStreaming = true,  // Image generation doesn't support streaming
    PersistenceStrategy = HistoryPersistenceStrategy.PersistResult,
    TurnType = "image_generation"
};
```

## Concurrency

Special turns support **parallel execution** - multiple special turns can execute simultaneously without locking.

## Architecture Benefits

- **Code reuse**: Special turns use the same infrastructure as regular turns
- **Unified streaming**: Automatic streaming support via provider adapters
- **State safety**: Automatic snapshot/restore prevents state corruption
- **Flexible persistence**: Four strategies cover most use cases
- **Observer compatibility**: Works seamlessly with existing observer pattern
- **Extensible**: Easy to create new special turn types

## Future Extensions

Possible future special turn types:
- **Translation turn**: Translate conversations to different languages
- **Refinement turn**: Improve/refine previous responses
- **Tool reflection turn**: Internal reasoning about tool execution
- **Context expansion turn**: Fetch additional context based on conversation

## Related Files

- `src/SmartHopper.Infrastructure/AICall/Sessions/SpecialTurns/SpecialTurnConfig.cs`
- `src/SmartHopper.Infrastructure/AICall/Sessions/SpecialTurns/HistoryPersistenceStrategy.cs`
- `src/SmartHopper.Infrastructure/AICall/Sessions/SpecialTurns/InteractionFilter.cs`
- `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.SpecialTurns.cs`
- `src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.ContextManagement.cs`
- `src/SmartHopper.Infrastructure/AICall/Sessions/SpecialTurns/BuiltIn/GreetingSpecialTurn.cs`
- `src/SmartHopper.Infrastructure/AICall/Sessions/SpecialTurns/BuiltIn/SummarizeSpecialTurn.cs`
