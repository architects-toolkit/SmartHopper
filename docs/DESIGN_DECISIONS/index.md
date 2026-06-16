# Design Decisions

This section documents the key architectural decisions behind SmartHopper, explaining the reasoning, trade-offs, and alternatives considered for each major design choice.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Understanding design decisions helps you extend the system without fighting its grain, evaluate trade-offs when proposing changes, and onboard faster by understanding the "why" behind the code.

**You should read this if you:**

- Want to extend or modify SmartHopper components
- Need to evaluate trade-offs when proposing architectural changes
- Are onboarding and want to understand the reasoning behind the codebase
- Are contributing a new provider, component, or policy

---

## End-User Guide

### How to Add a Design Decision

When making a significant architectural choice:

1. Copy the template below into this file.
2. Fill in all sections.
3. Link to related documentation.
4. Update the metadata date.

### Template

```markdown

### N. [Decision Title]

**Context**: [What situation led to this decision?]

**Decision**: [What was decided?]

**Rationale**:

- [Reason 1]
- [Reason 2]

**Trade-offs**:

- [Benefit] -- [Cost]

**Related**: [Links to related docs]

```

---

## Developer Reference

### Composable Adapter Pattern

```csharp
// Example: wiring an input adapter to an output adapter
var textInput = new Text2AI("Generate a parametric facade");
var payload = textInput.ToPayload();

var output = new AI2Text(payload, provider: "OpenAI", model: "gpt-4");
string result = await output.ComputeAsync();

```

### Immutable Record with Builder

```csharp
// Example: constructing an immutable AI request body
var body = new AIBodyBuilder()
    .WithPrompt("Design a parametric roof")
    .WithModel("gpt-4")
    .WithMaxTokens(500)
    .Build();

```

### Policy Pipeline Execution

```csharp
// Example: running request and response policies
var pipeline = new PolicyPipeline();
pipeline.AddRequestPolicy(new TimeoutNormalizationPolicy());
pipeline.AddRequestPolicy(new ContextInjectionPolicy());
pipeline.AddResponsePolicy(new SchemaValidationPolicy());

var result = await pipeline.ExecuteAsync(request, provider);

```

### Capability Flag Check

```csharp
// Example: checking model capabilities before sending a request
var required = AICapability.Text | AICapability.Vision;
if ((model.Capabilities & required) == required)
{
    await model.SendAsync(request);
}

```

---

## Architecture & Design

### 1. Composable Input/Output Adapters over Monolithic Components

**Context**: Early SmartHopper versions used monolithic components (e.g., `AIText2Text`) that combined input preparation, AI calling, and output extraction in one component.

**Decision**: Introduce a composable adapter pattern: separate Input and Output components connected by the `AIInputPayload` wire type. Output components handle both the AI call and the result extraction.

**Rationale**:

- Combinatorial explosion: N input types x M output types would require N*M monolithic components
- Single-responsibility: input adapters prepare data, output adapters call AI and extract results
- User flexibility: any input can connect to any output

**Trade-offs**:

- More components on the canvas (2 instead of 1)
- Slightly steeper learning curve for new users
- Legacy monolithic components kept for backward compatibility

**Related**: [AIInputPayload](../Architecture/AIInputPayload.md), [Input Components](../Components/Input/index.md), [Output Components](../Components/Output/index.md)

---

### 2. Immutable Records for AI Request/Response Data

**Context**: The AI call pipeline passes data through multiple stages (policies, encoding, provider, decoding). Mutable objects caused subtle bugs when policies modified shared state.

**Decision**: Use immutable records (`AIBody`, `AIReturn`, `AIMetrics`) with builder patterns (`AIBodyBuilder`) for construction.

**Rationale**:

- Thread safety: immutable data can be shared across async operations safely
- Predictability: policies cannot accidentally corrupt data for downstream stages
- Debuggability: each pipeline stage produces a new, inspectable snapshot

**Trade-offs**:

- More allocations (new record per stage)
- Builder pattern adds ceremony for construction
- Requires discipline: all mutation goes through builders

**Related**: [AIBody & AIBodyBuilder](../Providers/AICall/body-metrics-status.md)

---

### 3. Policy Pipeline for Request/Response Processing

**Context**: Cross-cutting concerns (timeout normalization, context injection, schema validation, tool validation) were scattered across provider implementations.

**Decision**: Centralize these concerns in a `PolicyPipeline` that runs ordered request and response policies for every AI call.

**Rationale**:

- Separation of concerns: providers focus on HTTP/API specifics
- Consistency: all providers get the same validation, context injection, etc.
- Extensibility: new policies can be added without modifying providers

**Trade-offs**:

- Indirection: debugging requires understanding the pipeline order
- Performance: each policy adds processing overhead (minimal in practice)

**Related**: [Policy Pipeline](../Providers/AICall/policy-pipeline.md)

---

### 4. Capability Flags for Model Selection

**Context**: Different AI models support different modalities (text, image, audio, tools, reasoning). Components need to know what a model can do before sending a request.

**Decision**: Use a `[Flags]` enum (`AICapability`) with bitwise composition for expressing model capabilities.

**Rationale**:

- Efficient: capability checks are single bitwise operations
- Composable: complex capability sets are simple OR combinations
- Hierarchical: audio inherits from speech automatically

**Trade-offs**:

- Limited to 32/64 flags (sufficient for current needs)
- Adding new capabilities requires enum changes

**Related**: [AICapability](../Providers/AICapability.md)

---

### 5. Secure Provider Plugin Loading

**Context**: SmartHopper loads external provider DLLs at runtime. Malicious or tampered DLLs could compromise user systems.

**Decision**: Require triple verification (Authenticode signature + strong-name public key token + online hash validation) before enabling any provider assembly.

**Rationale**:

- Supply-chain safety: prevents loading unsigned or tampered assemblies
- User control: first-run trust prompt with persisted decisions
- Defense in depth: three independent verification mechanisms
- Online hash validation: assembly hash is checked against a published registry, catching tampered binaries even if both signatures are valid

**Trade-offs**:

- Developer friction: providers must be properly signed and their hashes published
- Complexity: signing infrastructure and online registry required for builds
- Network dependency: online validation requires connectivity (with graceful fallback)

**Related**: [Architecture -- Provider Discovery](../Architecture.md), [Authenticode Signing](../Development/authenticode-signing.md)

---

### 6. Layered Component Base Hierarchy

**Context**: Grasshopper components need varying combinations of features: async execution, state management, provider selection, canvas selection, AI orchestration.

**Decision**: Build a layered hierarchy where each base class adds exactly one orthogonal concern.

**Rationale**:

- Single responsibility: each layer does one thing
- Opt-in complexity: simple components use simple bases
- No duplication: shared logic lives in `Core` helpers

**Trade-offs**:

- Deep inheritance (up to 5 levels)
- Learning curve for understanding the hierarchy

**Related**: [ComponentBase](../Components/ComponentBase/index.md)
