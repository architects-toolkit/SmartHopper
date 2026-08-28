# Immutability migration guide (SmartHopper AIBody / AIInteraction refactor)

Target branch: `feature/immutable-aibody`

This guide is for subagents and humans migrating the rest of the solution to the new immutable interaction/body contracts.

## What changed

- `AIMetrics` is now a `record` with `init` properties and `WithCombined(AIMetrics?)` returning a new combined instance.
- `AIInteractionBase` and all concrete interactions are `record`s with `init` properties.
- `IAIInteraction` exposes `init` properties and `WithTurnId/WithTime/WithAgent/WithMetrics`.
- `AIBody` is a `record` with `IReadOnlyList<int> InteractionsNew` and no `ResetNew()`.
- `AIBodyBuilder` is the mutable construction boundary; it uses `WithTurnId`, `Add`, `AddRange`, `Build`.
- `AIReturn.SetBody` still exists but builds an `AIBody` internally.

## Common patterns

### `AIMetrics`

Old:

```csharp
var metrics = new AIMetrics { Provider = "OpenAI", Model = "gpt-4o" };
metrics.InputTokensPrompt = 10;
metrics.OutputTokensGeneration = 5;
metrics.FinishReason = "stop";
```

New:

```csharp
var metrics = new AIMetrics
{
    Provider = "OpenAI",
    Model = "gpt-4o",
    InputTokensPrompt = 10,
    OutputTokensGeneration = 5,
    FinishReason = "stop",
};
```

If you need to add values after creation, use `with`:

```csharp
metrics = metrics with { InputTokensPrompt = 10 };
```

To combine metrics:

```csharp
combined = combined.WithCombined(other);
accumulatedMetrics = accumulatedMetrics.WithCombined(result.Metrics);
```

### `AIInteractionText`

Old:

```csharp
var text = new AIInteractionText();
text.SetResult(AIAgent.Assistant, "content", "reasoning");
text.Metrics = metrics;
text.AppendDelta(contentDelta: " world", reasoningDelta: " more", metricsDelta: deltaMetrics);
```

New (single snapshot):

```csharp
var text = new AIInteractionText
{
    Agent = AIAgent.Assistant,
    Content = "content",
    Reasoning = "reasoning",
    Metrics = metrics,
};
```

Or use fluent `WithResult`:

```csharp
var text = new AIInteractionText().WithResult(AIAgent.Assistant, "content", "reasoning");
text = text.WithDeltaMetrics(deltaMetrics);
```

New (streaming aggregation):

```csharp
var builder = new AIInteractionText.Builder()
    .WithResult(AIAgent.Assistant, "content", "reasoning")
    .AppendContent(" world")
    .AppendReasoning(" more")
    .CombineMetrics(deltaMetrics);

var text = builder.Build();
```

### `AIInteractionImage`

Old:

```csharp
var img = new AIInteractionImage();
img.CreateVisionInput("https://example.com/image.png");
img.CreateRequest("a cat", size: "1024x1024");
img.SetResult("https://example.com/out.png", imageData: null, revisedPrompt: null);
```

New:

```csharp
var img = new AIInteractionImage().WithVisionInput("https://example.com/image.png");
img = img.WithRequest("a cat", size: "1024x1024");
img = img.WithResult("https://example.com/out.png", imageData: null, revisedPrompt: null);
```

### `AIInteractionToolCall`

Old:

```csharp
var tc = new AIInteractionToolCall();
tc.Id = "id";
tc.Name = "name";
tc.Arguments = args;
```

New:

```csharp
var tc = new AIInteractionToolCall { Id = "id", Name = "name", Arguments = args };
```

If assigning later, use `with` or create a new instance.

### `AIInteractionToolResult`

Old:

```csharp
var tr = new AIInteractionToolResult();
tr.Result = result;
```

New:

```csharp
var tr = new AIInteractionToolResult { Result = result };
```

### `IAIInteraction` mutations

Old:

```csharp
interaction.TurnId = turnId;
interaction.Agent = AIAgent.Assistant;
interaction.Metrics = metrics;
interaction.Time = DateTime.UtcNow;
```

New:

```csharp
interaction = interaction.WithTurnId(turnId);
interaction = interaction.WithAgent(AIAgent.Assistant);
interaction = interaction.WithMetrics(metrics);
interaction = interaction.WithTime(DateTime.UtcNow);
```

For concrete records, you can also use `with` directly:

```csharp
if (interaction is AIInteractionText text)
{
    text = text with { TurnId = turnId, Metrics = metrics };
}
```

But prefer the `With...` interface methods when the concrete type is not known.

### `AIBodyBuilder`

Old:

```csharp
var body = AIBodyBuilder.Create();
body.Add(interaction);
body.ResetNew();
```

New:

```csharp
var body = AIBodyBuilder.Create()
    .Add(interaction)
    .Build();
```

If a builder already exists and you need to replace interactions, use builder methods such as `ReplaceLastRange` / `AddRange`.

### `InteractionUtility.EnsureTurnId`

Old:

```csharp
InteractionUtility.EnsureTurnId(interactions);
```

New:

```csharp
interactions = InteractionUtility.EnsureTurnId(interactions).ToList();
```

The method now returns a new enumerable of interactions with turn IDs assigned. Replace the original collection reference.

### `TextStreamCoalescer`

Old consumers that mutated interactions should now:

- Use `AIInteractionText.Builder` for local aggregation.
- Call `AIBodyBuilder` to produce new `AIBody` snapshots.
- Return / emit new `AIReturn` instances with `SetBody(newBody)` instead of mutating `Body`.

## Build / test

For any project:

```powershell
dotnet build src/<Project>/<Project>.csproj -p:SignAssembly=false
```

For `ProviderSdk` tests:

```powershell
dotnet test src/SmartHopper.ProviderSdk.Tests/SmartHopper.ProviderSdk.Tests.csproj -p:SignAssembly=false
```

The full solution now builds with `dotnet build SmartHopper.sln -p:SignAssembly=false`. Signed builds still require `signing.snk`; project-level builds with `-p:SignAssembly=false` are the verification target.

## What not to do

- Do not add `SetResult/AppendDelta/CreateVisionInput/CreateRequest` methods back to records.
- Do not assign `init`-only properties after construction (use `with` or create a new object).
- Do not mutate `AIBody`; use `AIBodyBuilder`.
- Do not call `AIMetrics.Combine` (removed); use `WithCombined`.
- Do not call `body.ResetNew()` (removed).

## Reference files

- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIInteractionText.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIInteractionImage.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIInteractionToolCall.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIInteractionToolResult.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIInteractionBase.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/IAIInteraction.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Interactions/AIBodyBuilder.cs`
- `src/SmartHopper.ProviderSdk/AICall/Core/Returns/AIReturn.cs`
- `src/SmartHopper.ProviderSdk/AICall/Metrics/AIMetrics.cs`
- `src/SmartHopper.Providers.OpenRouter/OpenRouterProvider.cs` (already migrated example)
