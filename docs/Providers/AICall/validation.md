# Validation Classes

Comprehensive validation framework for AI requests, tool calls, and responses with structured diagnostics.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/Validation/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document describes the pluggable validation framework for checking AI calls, tool invocations, and responses before and after execution. It covers typed validators, structured diagnostics, and composition patterns.

**You should read this if you:**

- Are implementing custom validators or integrating validation into request/response pipelines
- Need to debug validation failures, capability mismatches, or tool schema errors
- Want to understand how validation results are aggregated into runtime messages

---

## End-User Guide

The validation framework provides typed validators for checking AI calls, tool invocations, and responses before/after execution.

- **Purpose**: Provide typed validators for checking AI calls, tool invocations, and responses before/after execution
- **Architecture**: Pluggable validators implementing `IValidator<T>` interface with structured result reporting

### ValidationStrictness

Strictness levels for gating validation results:

- `InfoOrAbove` -- fail on Info, Warning, or Error
- `WarningOrAbove` -- fail on Warning or Error
- `ErrorOnly` -- fail only on Error (default)

---

## Developer Reference

### Core Interfaces and Types

#### IValidator<T>

Generic validator contract for validating specific models and returning diagnostics.

- **Type Parameter**: `T` -- type being validated
- **Properties**:
  - `FailOn` (SHRuntimeMessageSeverity) -- severity threshold at or above which validation fails (e.g., Error, Warning)
- **Methods**:
  - `ValidateAsync(instance, context, cancellationToken)` -- validates instance and returns `ValidationResult`

#### ValidationResult

Result returned by validators, carrying success flag and structured diagnostics.

- **Properties**:
  - `IsValid` (bool) -- whether validation passed considering the validator's `FailOn` threshold
  - `Messages` (List<SHRuntimeMessage>) -- collected messages emitted during validation
  - `Issues` (List<ValidationIssue>) -- optional structured issues with codes and JSON-like path hints
  - `MessagesSanitized` (bool) -- whether messages have been sanitized to avoid PII leakage
- **Computed Properties**:
  - `ErrorCount` -- count of error messages
  - `WarningCount` -- count of warning messages
  - `InfoCount` -- count of informational messages

#### ValidationIssue

Structured validation issue with optional code and location path.

- **Properties**:
  - `Code` (string, nullable) -- short code identifying the issue type
  - `Path` (string, nullable) -- JSON-like path to the location of the issue
  - `Severity` (SHRuntimeMessageSeverity) -- severity of the issue
  - `Message` (string) -- human-readable message describing the issue

#### ValidationContext

Ambient context for validators, carrying request/response, provider/model, flags and fingerprints.

- **Properties**:
  - `PolicyContext` (Policies.PolicyContext, nullable) -- optional originating policy context
  - `Request` (AIRequestCall, nullable) -- request under validation
  - `Response` (AIReturn, nullable) -- response under validation
  - `Provider` (string, computed) -- provider ID for convenience
  - `Model` (string, computed) -- model ID for convenience
  - `Capability` (AICapability, computed) -- effective capability for the call
  - `BodyFingerprint` (string, nullable) -- optional body fingerprint for cache/memoization correlation
  - `FeatureFlags` (HashSet<string>) -- feature flags to enable/disable behaviors
  - `Strictness` (ValidationStrictness) -- strictness mode for gating (default: ErrorOnly)

### Concrete Validators

#### ComponentCapabilityValidator

Validates that a component's required capability is supported by the configured provider and model.

- **File**: `ComponentCapabilityValidator.cs`
- **Type Parameter**: `AICapability`
- **FailOn**: `Error`
- **Validation Steps**:
  1. Checks provider is registered and enabled
  2. Checks model is registered for provider
  3. Checks model supports required capability
  4. Detects fallback chain (if capability mismatch triggers fallback)
- **Methods**:
  - `ValidateSync(capability)` -- synchronous validation (preferred for pre-validation in SolveInstance)
  - `ValidateAsync(capability, context, cancellationToken)` -- asynchronous validation

#### ToolExistsValidator

Validates that a tool call references an existing, registered tool.

- **File**: `ToolExistsValidator.cs`
- **Type Parameter**: `AIInteractionToolCall`
- **FailOn**: `Error`
- **Validation Steps**:
  1. Checks tool call instance is not null
  2. Checks tool name is not empty
  3. Checks tool is registered in `AIToolManager`
- **Emits**: `ToolValidationError` code on failure

#### ToolCapabilityValidator

Validates that the selected provider/model supports the capabilities required by the tool.

- **File**: `ToolCapabilityValidator.cs`
- **Type Parameter**: `AIInteractionToolCall`
- **FailOn**: `Error`
- **Validation Steps**:
  1. Checks tool call instance is not null
  2. Checks provider/model context is available
  3. Checks tool exists
  4. Checks tool's required capabilities are supported by provider/model
- **Defers**: Unknown tool handling to `ToolExistsValidator`
- **Emits**: `ToolValidationError` code on failure

#### ToolJsonSchemaValidator

Validates a tool call's arguments against the tool's JSON parameters schema.

- **File**: `ToolJsonSchemaValidator.cs`
- **Type Parameter**: `AIInteractionToolCall`
- **FailOn**: `Error`
- **Validation Steps**:
  1. Checks tool call instance is not null
  2. Checks tool exists
  3. Parses tool's parameter schema
  4. Validates required parameters are present
  5. Validates argument types match schema
- **Defers**: Unknown tool handling to `ToolExistsValidator`
- **Emits**: `ToolValidationError` code on schema violations

#### JsonSchemaResponseValidator

Validates provider response content against the request's JSON schema.

- **File**: `JsonSchemaResponseValidator.cs`
- **Type Parameter**: `AIReturn`
- **FailOn**: `Error`
- **Validation Steps**:
  1. Checks request has JSON output schema requirement
  2. Extracts latest assistant text interaction
  3. Validates content against schema
  4. Reports schema violations with path hints
- **Emits**: `ReturnInvalid` code on schema violations

### Usage Patterns

#### In Request Policies

Validators are composed in `AIToolValidationRequestPolicy` to validate pending tool calls before provider execution:

```csharp
var validators = new IValidator<AIInteractionToolCall>[]
{
    new ToolExistsValidator(),
    new ToolCapabilityValidator(provider, model),
    new ToolJsonSchemaValidator(),
};

foreach (var validator in validators)
{
    var result = await validator.ValidateAsync(toolCall, context, cancellationToken);
    if (!result.IsValid)
    {
        // Add messages to request
        request.AddRuntimeMessages(result.Messages);
    }
}

```

#### In Response Policies

Response validators check provider output against request constraints:

```csharp
var validator = new JsonSchemaResponseValidator();
var result = await validator.ValidateAsync(response, context, cancellationToken);
if (!result.IsValid)
{
    response.AddRuntimeMessages(result.Messages);
}

```

#### In Components

Pre-validation in component `SolveInstance`:

```csharp
var capValidator = new ComponentCapabilityValidator(provider, model);
var result = capValidator.ValidateSync(AICapability.TextGeneration);
if (!result.IsValid)
{
    AddRuntimeMessage(result.Messages[0]);
    return;
}

```

---

## Architecture & Design

- Location: `src/SmartHopper.Infrastructure/AICall/Validation/`

### Message Codes

Validators emit structured message codes for programmatic checks:

| Code | Validator | Meaning |
| --- | --- | --- |---------|
| `ProviderMissing` | ComponentCapabilityValidator | Provider not registered or enabled |
| `UnknownModel` | ComponentCapabilityValidator | Model not registered for provider |
| `NoCapableModel` | ComponentCapabilityValidator | No model supports required capability |
| `CapabilityMismatch` | ComponentCapabilityValidator | Model lacks capability; fallback applied |
| `ToolValidationError` | ToolExistsValidator, ToolCapabilityValidator, ToolJsonSchemaValidator | Tool validation failed |
| `ReturnInvalid` | JsonSchemaResponseValidator | Response invalid or missing |

### Design Principles

1. **Composability**: Validators are independent and can be composed in pipelines
2. **Structured Diagnostics**: Messages carry severity, origin, and machine-readable codes
3. **Async-First**: All validators support async I/O for network-dependent checks
4. **Deduplication**: Validators avoid double-reporting by deferring to specialized validators
5. **Context-Aware**: Validators receive ambient context (provider, model, request, response)
6. **Strictness Levels**: Configurable failure thresholds allow different validation modes
7. **PII Safety**: Validators can sanitize messages to avoid leaking sensitive data
