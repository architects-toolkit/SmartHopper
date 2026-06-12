# OpenRouter Provider

The OpenRouter provider provides access to multiple AI models through a unified OpenAI-compatible interface, supporting text generation, vision, tool calling, structured outputs, reasoning, and streaming.

## Features

- **Models**: Access to models from OpenAI, Anthropic, Google, Mistral, DeepSeek, and more
- **Text Generation**: Full support for text-to-text conversations
- **Vision**: Image input support for compatible underlying models
- **Structured Outputs**: JSON schema support via `json_schema` response format
- **Tool Calling**: Function calling with structured arguments
- **Reasoning**: Reasoning content extraction from compatible models
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)
- **Provider Routing**: Smart provider selection with fallback support
- **Session Sticky Routing**: Consistent endpoint routing for prompt cache warmth

## Configuration

### API Key

Get your API key from [OpenRouter](https://openrouter.ai/):

1. Visit <https://openrouter.ai/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new key and copy it into SmartHopper's OpenRouter provider settings

### Settings

- **API Key**: Your OpenRouter API key (required)
- **Model**: Select from available models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (0.0–2.0, default: 0.5)
- **Allow Fallbacks**: Allow fallback to compatible models if preferred is unavailable (default: true)
- **Sort Strategy**: Provider selection sort — `price`, `throughput`, or `latency` (default: price)
- **Data Collection**: Allow or deny provider data collection — `allow` or `deny` (default: deny)

## Provider Selection

OpenRouter automatically routes requests to underlying providers. SmartHopper configures:

- **Fallbacks**: Enabled by default for reliability
- **Sorting**: By price for cost optimization
- **Data Collection**: Denied by default for privacy

When `enable_caching` is set in extras:

- Anthropic models receive `cache_control: { type: "ephemeral" }`
- A stable `session_id` is computed for sticky routing to keep caches warm

## JSON Schema Support

OpenRouter supports JSON schema structured outputs via `response_format` with `type: "json_schema"`. The provider sets `structured_outputs: true` to hint structured output requirements.

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

OpenRouter supports function calling via OpenAI-compatible `tools` and `tool_choice` parameters across all underlying providers that support it.

Forced tool calls use:

```json
{
    "type": "function",
    "function": { "name": "tool_name" }
}
```

## Authentication

The provider uses Bearer token authentication. SmartHopper also sends required attribution headers:

- `X-Title`: `SmartHopper`
- `Referer`: `https://smarthopper.xyz`

## Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: The model may be unavailable on all providers; enable fallbacks
- **Provider Error**: Check the underlying provider's status
- **Routing Issues**: Session sticky routing may restrict provider options

## Extra Parameters

OpenRouter accepts many extra parameters via the `Extras` dictionary:

- `seed`: Integer for deterministic outputs
- `top_p`, `top_k`: Sampling parameters
- `frequency_penalty`, `presence_penalty`, `repetition_penalty`: Repetition control
- `min_p`, `top_a`: Advanced sampling
- `logprobs`, `top_logprobs`: Log probability output
- `enable_caching`: Enable prompt caching with sticky routing

## References

- [OpenRouter Documentation](<https://openrouter.ai/docs>)
- [OpenRouter API Reference](<https://openrouter.ai/docs/api>)
