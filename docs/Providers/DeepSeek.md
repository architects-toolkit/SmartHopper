# DeepSeek Provider

The DeepSeek provider integrates DeepSeek AI models into SmartHopper, supporting text generation, reasoning, tool calling, structured outputs, and streaming.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/DeepSeek/DeepSeekProvider.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document explains how to configure and use the DeepSeek provider to access DeepSeek models within SmartHopper. It covers setup, streaming, reasoning content extraction, and tool calling with DeepSeek-specific requirements.

**You should read this if you:**

- Want to use DeepSeek models (deepseek-v4, deepseek-chat, or deepseek-reasoner) inside SmartHopper
- Need to handle reasoning content separately from main responses
- Plan to use tool calling with DeepSeek's strict message requirements
- Are enabling streaming or structured JSON outputs

---

## End-User Guide

### Features

- **Models**: deepseek-v4 and legacy deepseek-chat/reasoner
- **Text Generation**: Full support for text-to-text conversations
- **Reasoning**: Extended reasoning with reasoning_content extraction
- **Structured Outputs**: JSON schema support via response_format
- **Tool Calling**: Function calling with structured arguments
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)
- **Prompt Caching**: KV cache hit metrics for cached tokens

### Configuration

#### API Key

Get your API key from [DeepSeek Platform](https://platform.deepseek.com/):

1. Visit <https://platform.deepseek.com/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new key and copy it into SmartHopper's DeepSeek provider settings

#### Settings

- **API Key**: Your DeepSeek API key (required)
- **Model**: Select from available DeepSeek models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (default: 0.5)

### JSON Schema Support

DeepSeek supports JSON schema for structured outputs via `response_format` with `type: "json_object"`. The provider automatically wraps schemas and injects a system message with the schema definition to guide the model.

### Reasoning Content

DeepSeek reasoning models return `reasoning_content` separately from the main `content`. The provider extracts and surfaces reasoning through the `AIInteractionText.Reasoning` property. Important notes:

- `reasoning_content` is **not** echoed back on plain assistant messages in subsequent requests
- `reasoning_content` **is** required on assistant messages with `tool_calls`

### Authentication

The provider uses Bearer token authentication. Your API key is automatically applied from settings.

### Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available
- **Context Exceeded**: DeepSeek models support up to 1M tokens (v4)
- **Malformed JSON Response**: The provider includes automatic cleanup for malformed array responses

### References

- [DeepSeek API Documentation](https://api-docs.deepseek.com/)
- [DeepSeek Platform](https://platform.deepseek.com/)

---

## Developer Reference

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

DeepSeek supports function calling via OpenAI-compatible `tools` and `tool_choice` parameters. The provider merges consecutive assistant messages carrying `tool_calls` into a single message to comply with DeepSeek's strict requirements.

Forced tool calls are supported using:

```json
{
    "type": "function",
    "function": { "name": "tool_name" }
}

```

### Structured Output Example

Configure a request for JSON schema structured output:

```csharp
var request = new AIRequestCall
{
    Model = "deepseek-v4",
    Messages = new List<IAIInteraction>
    {
        new AIInteractionText { Role = "user", Text = "Generate a user profile" }
    },
    ResponseFormat = new AIResponseFormat
    {
        Type = "json_object",
        Schema = /* your JSON schema */
    }
};

var response = await provider.Call(request);

```

---

## Architecture & Design

The DeepSeek provider implements the `IAIProvider` interface and translates SmartHopper's generic request/response model into DeepSeek's API format, which is OpenAI-compatible with some important differences:

- **Reasoning Content Extraction**: The provider parses `reasoning_content` from responses and maps it to `AIInteractionText.Reasoning`, ensuring reasoning is preserved without being echoed back in follow-up messages unless tool calls are present.
- **Message Merging**: Consecutive assistant messages carrying `tool_calls` are merged into a single message to comply with DeepSeek's strict message ordering requirements.
- **Schema Injection**: For structured outputs, the provider injects a system message containing the JSON schema definition rather than relying solely on `response_format`.
- **Malformed Response Cleanup**: The provider includes automatic cleanup for malformed array responses, improving robustness against edge-case API behavior.
