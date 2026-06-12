# OpenAI Provider

The OpenAI provider integrates OpenAI's GPT models into SmartHopper, supporting text generation, vision, audio, image generation, tool calling, structured outputs, reasoning, batch processing, and streaming.

## Features

- **Models**: GPT-5.x, GPT-4.1, GPT-4o, o-series reasoning models, and more
- **Text Generation**: Full support for text-to-text conversations
- **Vision**: Image input support for GPT-4o and GPT-5.x models
- **Audio**: Speech-to-text and text-to-speech (gpt-audio models)
- **Image Generation**: DALL-E image generation via `/images/generations`
- **Structured Outputs**: JSON schema support with strict mode
- **Tool Calling**: Function calling with structured arguments
- **Reasoning**: Extended thinking with o-series and GPT-5.x reasoning models
- **Batch Processing**: Asynchronous batch job submission and polling
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)
- **Prompt Caching**: Automatic and manual prompt caching support

## Configuration

### API Key

Get your API key from [OpenAI Platform](https://platform.openai.com/):

1. Visit <https://platform.openai.com/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new secret key and copy it into SmartHopper's OpenAI provider settings

### Settings

- **API Key**: Your OpenAI API key (required)
- **Model**: Select from available OpenAI models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (0.0–2.0, default: 0.5)

## JSON Schema Support

OpenAI supports strict JSON schema structured outputs via `response_format` with `type: "json_schema"`. The provider automatically wraps non-object schemas into an object and enforces strict validation.

## Image Generation

For image generation, select an image-capable request or use DALL-E models:

1. The provider routes to `/images/generations` when `ImageOutput` capability is requested
2. Returns image URLs or base64-encoded data
3. Supports quality, size, and style parameters via extras

## Batch Processing

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

## Authentication

The provider uses Bearer token authentication (`Authorization: Bearer <key>`). Your API key is automatically applied from settings.

## Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct and has appropriate permissions
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available in your tier
- **Quota Exceeded**: Check your OpenAI usage limits and billing
- **Context Exceeded**: Use models with larger context windows or truncate prompts

## References

- [OpenAI API Documentation](<https://platform.openai.com/docs/>)
- [OpenAI Platform](<https://platform.openai.com/>)
