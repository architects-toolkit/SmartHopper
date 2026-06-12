# MistralAI Provider

The MistralAI provider integrates Mistral AI models into SmartHopper, supporting text generation, vision, tool calling, structured outputs, and streaming.

## Features

- **Models**: mistral-small, mistral-medium, ministral, codestral, devstral, and more
- **Text Generation**: Full support for text-to-text conversations
- **Vision**: Image input support for select models (e.g., mistral-small-2603)
- **Structured Outputs**: JSON schema support via response_format
- **Tool Calling**: Function calling with structured arguments
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)
- **Audio**: Speech-to-text (voxtral) and text-to-speech capabilities

## Configuration

### API Key

Get your API key from [Mistral AI Console](https://console.mistral.ai/):

1. Visit <https://console.mistral.ai/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new key and copy it into SmartHopper's MistralAI provider settings

### Settings

- **API Key**: Your MistralAI API key (required)
- **Model**: Select from available MistralAI models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (0.0–3.0, default: 0.5)

## JSON Schema Support

MistralAI supports JSON schema for structured outputs via `response_format` with `type: "json_object"`. The provider automatically wraps schemas and injects system guidance to enforce valid JSON responses.

## Streaming

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

## Tool Calling

MistralAI supports function calling via OpenAI-compatible `tools` and `tool_choice` parameters. Forced tool calls are supported using the `tool_choice` object format:

```json
{
    "type": "function",
    "function": { "name": "tool_name" }
}
```

## Authentication

The provider uses Bearer token authentication. Your API key is automatically applied from settings.

## Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct and active
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available in your region
- **Context Exceeded**: Use shorter prompts or models with larger context windows

## References

- [Mistral AI API Documentation](<https://docs.mistral.ai/>)
- [Mistral AI Console](<https://console.mistral.ai/>)
