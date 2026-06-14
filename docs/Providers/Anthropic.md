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

Submit multiple requests as a batch:

```csharp
var requests = new List<AIRequestCall> { /* ... */ };
var submission = await provider.SubmitBatchAsync(requests);
var batchId = submission.BatchId;

// Poll for status
var status = await provider.GetBatchStatusAsync(batchId);
while (status.State == AIBatchState.Processing)
{
    await Task.Delay(5000);
    status = await provider.GetBatchStatusAsync(batchId);
}

```

### Streaming

Streaming is enabled by default. Responses are streamed in real-time via SSE:

```csharp
var request = new AIRequestCall
{
    EnableStreaming = true,
    // ... other settings
};

await foreach (var chunk in provider.StreamAsync(request))
{
    // Process each chunk as it arrives
}

```

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
