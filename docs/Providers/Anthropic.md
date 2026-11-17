# Anthropic Provider

The Anthropic provider integrates Claude models into SmartHopper via the Messages API, supporting text generation, vision, tool calling, structured outputs, reasoning, batch processing, and streaming.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/Anthropic/AnthropicProvider.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document explains how to configure and use the Anthropic provider to access Claude models within SmartHopper. It covers everything from basic API key setup to advanced features like batch processing, streaming, and structured JSON outputs.

**You should read this if you:**

- Want to use Claude models (Sonnet, Opus, Haiku) inside SmartHopper
- Need to enable streaming, tool calling, or structured outputs
- Plan to submit batch jobs or use prompt caching
- Are integrating vision capabilities with image inputs

---

## End-User Guide

### Features

- **Models**: Claude Sonnet, Opus, and Haiku families
- **Text Generation**: Full support for text-to-text conversations
- **Vision**: Image input support across all major models
- **Structured Outputs**: JSON schema support with Anthropic schema wrapping
- **Tool Calling**: Function calling with structured arguments
- **Reasoning**: Extended thinking capabilities
- **Batch Processing**: Asynchronous batch job submission and status polling
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)
- **Prompt Caching**: Automatic prompt caching with cache control

### Configuration

#### API Key

Get your API key from [Anthropic Console](https://console.anthropic.com/):

1. Visit <https://console.anthropic.com/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new key and copy it into SmartHopper's Anthropic provider settings

#### Settings

- **API Key**: Your Anthropic API key (required)
- **Model**: Select from available Claude models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (0.0–2.0, default: 0.5)

### JSON Schema Support

Anthropic supports JSON schema for structured outputs. The provider automatically wraps schemas and manages content block sorting (text blocks must precede tool_use blocks).

### Authentication

The provider uses `x-api-key` header authentication. Your API key is automatically applied from settings. The `anthropic-version` header defaults to `2023-06-01`.

### Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct and has appropriate permissions
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available in your region
- **Context Exceeded**: Claude models support up to 1M tokens (latest) or 200K (legacy)
- **Content Block Ordering**: The provider automatically sorts content blocks per Anthropic requirements

### References

- [Anthropic API Documentation](https://docs.anthropic.com/)
- [Anthropic Console](https://console.anthropic.com/)

---

## Developer Reference

### Batch Processing

Submit multiple requests as a batch through `IAIBatchProvider.SubmitBatchAsync` and poll with `GetBatchStatusAsync`. For a complete Anthropic batch example, see `src/SmartHopper.Components.Test/Providers/TestAnthropicBatchCallComponent.cs`.

### Streaming

Streaming is enabled by default. Responses are streamed in real-time via SSE. For a concrete example, see `docs/Providers/AICall/Streaming.md` and `src/SmartHopper.Providers.Anthropic/AnthropicProvider.Streaming.cs`.

### Reasoning / Extended Thinking

Request encoding emits a `thinking` field whenever `ReasoningEffort` resolves non-empty (per-request `effort` extra overrides the provider setting; default `medium`). The field shape depends on the model family:

- **Adaptive family** — all 5.x lines, `fable`/`mythos`, and every 4.6+ model: `thinking: {"type":"adaptive","display":"summarized"}`. Without the field these models never think (4.6–4.8) or think invisibly (5.x defaults `display` to `omitted`, billing thinking tokens that return empty). `output_config.effort` is still sent alongside and steers depth.
- **Extended family** — 4.5-era and 3.7 models: `thinking: {"type":"enabled","budget_tokens":N}` with effort mapped to a budget (low→1024, medium→4096, high→8192, xhigh→16384, max→32768, clamped strictly below `max_tokens`). The API then requires `temperature=1` and no `top_p`/`top_k`, which the encoder enforces. `output_config.effort` is not emitted for these models.
- **Older models** (3.5 and below): no `thinking` field — they reject it.

Thinking blocks decode onto `AIInteractionText.Reasoning`; each block's `signature` (encrypted reasoning state) is preserved verbatim in `IAIInteraction.ProviderData["thinking"]` and re-emitted ahead of the text block when assistant history is replayed — Anthropic requires thinking blocks echoed back unchanged on continuation turns, and tool-use loops that strip them fail. Streaming handles `thinking_delta`, `signature_delta` and `redacted_thinking` events the same way. Reported `usage.output_tokens_details.thinking_tokens` maps to `internal_reasoning` metrics (already included in `output_tokens`, never added twice).

### Tool Calling

Anthropic supports function calling via native `tools` parameters. Forced tool calls are supported using the `tool_choice` object format:

```json
{
    "type": "tool",
    "name": "tool_name"
}

```

---

## Architecture & Design

The Anthropic provider implements the `IAIProvider` interface and translates SmartHopper's generic request/response model into Anthropic's Messages API format. Key architectural decisions include:

- **Content Block Sorting**: The provider enforces Anthropic's requirement that text blocks precede `tool_use` blocks by automatically sorting content blocks before submission.
- **Schema Wrapping**: JSON schemas are automatically wrapped in Anthropic's expected format for structured outputs.
- **Prompt Caching**: Cache control is applied automatically where supported to reduce token costs on repeated prompts.
- **Version Header Management**: The `anthropic-version` header is managed internally and defaults to `2023-06-01`.
