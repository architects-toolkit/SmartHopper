# MistralAI Provider

The MistralAI provider integrates Mistral AI models into SmartHopper, supporting text generation, vision, tool calling, structured outputs, and streaming.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/MistralAI/MistralAIProvider.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This documentation covers the MistralAI provider for SmartHopper, explaining how to configure and use Mistral AI models within the ecosystem. You should read it if you plan to use Mistral models for text generation, vision tasks, or tool calling in your SmartHopper workflows.

**You should read this if you:**

- Want to use Mistral AI models in SmartHopper
- Need to configure the MistralAI provider settings
- Are troubleshooting MistralAI integration issues

---

## End-User Guide

### Features

- **Models**: mistral-small, mistral-medium, ministral, codestral, devstral, and more
- **Text Generation**: Full support for text-to-text conversations
- **Vision**: Image input support for select models (e.g., mistral-small-2603)
- **Structured Outputs**: JSON schema support via response_format
- **Tool Calling**: Function calling with structured arguments
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)
- **Audio**: Speech-to-text (voxtral) and text-to-speech capabilities

### Configuration

#### API Key

Get your API key from [Mistral AI Console](https://console.mistral.ai/):

1. Visit <https://console.mistral.ai/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new key and copy it into SmartHopper's MistralAI provider settings

#### Settings

- **API Key**: Your MistralAI API key (required)
- **Model**: Select from available MistralAI models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (0.0–3.0, default: 0.5)

### JSON Schema Support

MistralAI supports JSON schema for structured outputs via `response_format` with `type: "json_object"`. The provider automatically wraps schemas and injects system guidance to enforce valid JSON responses.

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

MistralAI supports function calling via OpenAI-compatible `tools` and `tool_choice` parameters. Forced tool calls are supported using the `tool_choice` object format:

```json
{
    "type": "function",
    "function": { "name": "tool_name" }
}

```

### Authentication

The provider uses Bearer token authentication. Your API key is automatically applied from settings.

### Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct and active
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available in your region
- **Context Exceeded**: Use shorter prompts or models with larger context windows

---

## Developer Reference

### Provider Initialization

```csharp
public class MistralAIProvider : AIProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public MistralAIProvider(IProviderSettings settings)
    {
        _apiKey = settings.GetValue<string>("ApiKey");
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("<https://api.mistral.ai/v1/">)
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }
}

```

### Sending a Chat Completion Request

```csharp
var request = new AIRequestCall
{
    Model = "mistral-small-latest",
    Messages = new List<Message>
    {
        new Message { Role = "user", Content = "Explain quantum computing" }
    },
    Temperature = 0.7,
    MaxTokens = 1000
};

var response = await provider.ChatAsync(request);
Console.WriteLine(response.Content);

```

---

## Architecture & Design

The MistralAI provider implements the `AIProvider` base class and communicates with the Mistral AI REST API over HTTPS. It translates SmartHopper's internal `AIRequestCall` objects into Mistral-compatible request payloads and parses responses back into the standard SmartHopper response format.

Structured output support is achieved by injecting a system message that instructs the model to return valid JSON, combined with the `response_format` parameter. Streaming responses are handled by reading Server-Sent Events (SSE) from the API and yielding chunks as they arrive.

## References

- [Mistral AI API Documentation](https://docs.mistral.ai/)
- [Mistral AI Console](https://console.mistral.ai/)
