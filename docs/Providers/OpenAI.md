# OpenAI Provider

The OpenAI provider integrates OpenAI's GPT models into SmartHopper, supporting text generation, vision, audio, image generation, tool calling, structured outputs, reasoning, batch processing, and streaming.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/OpenAI/OpenAIProvider.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This documentation covers the OpenAI provider for SmartHopper, explaining how to configure and use OpenAI's GPT models within the ecosystem. You should read it if you plan to use OpenAI models for text generation, vision, audio, image generation, or advanced features like batch processing and reasoning.

**You should read this if you:**

- Want to use OpenAI GPT models in SmartHopper
- Need to configure the OpenAI provider settings
- Are interested in advanced features like batch processing, image generation, or reasoning

---

## End-User Guide

### Features

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

### Configuration

#### API Key

Get your API key from [OpenAI Platform](https://platform.openai.com/):

1. Visit <https://platform.openai.com/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new secret key and copy it into SmartHopper's OpenAI provider settings

#### Settings

- **API Key**: Your OpenAI API key (optional when signed in with ChatGPT)
- **Model**: Select from available OpenAI models (default resolved from registry)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000, range: 1–100000)
- **Temperature**: Controls randomness (0.0–2.0, default: 0.5)

#### Continue with ChatGPT

As an alternative to an API key, the OpenAI provider can use a paid ChatGPT plan directly through OpenAI's token-sharing flow for open-source clients:

1. Click **Continue with ChatGPT** in the provider settings — a browser opens OpenAI's sign-in page.
2. Authorize SmartHopper; the redirect returns to a local loopback listener and the grant is stored encrypted in the provider settings store.
3. The provider is then considered configured even without an API key.

Credential precedence: the ChatGPT access token is used first for eligible endpoints (`/responses` and `/models`); if OpenAI rejects it with HTTP 401/403, the call retries once with the configured API key. With no ChatGPT grant the API key is used directly; with no API key the ChatGPT token is used alone.

Constraints of plan usage: only the Responses API is supported (legacy endpoints such as `/images/generations` and audio always require an API key), requests are forced to `stream: true` + `store: false`, `max_output_tokens`/`temperature`/`top_p` are omitted, and system messages are sent as `developer` role. Tokens refresh automatically before expiry; **Disconnect** clears the stored grant.

### JSON Schema Support

OpenAI supports strict JSON schema structured outputs via `response_format` with `type: "json_schema"`. The provider automatically wraps non-object schemas into an object and enforces strict validation.

### Image Generation

For image generation, select an image-capable request or use DALL-E models:

1. The provider routes to `/images/generations` when `ImageOutput` capability is requested
2. Returns image URLs or base64-encoded data
3. Supports quality, size, and style parameters via extras

### Streaming

Streaming is enabled by default. Responses are streamed in real-time via SSE through the provider's streaming adapter:

```csharp
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Streaming;

var body = AIBodyBuilder.Create()
    .AddUser("Explain parametric design in one sentence.")
    .Build();

var request = new AIRequestCall
{
    Provider = "openai",
    Model = "gpt-4o",
    Capability = AICapability.Text2Text,
    Body = body,
    WantsStreaming = true,
};

var streamingAdapter = provider.GetStreamingAdapter();
await foreach (var chunk in streamingAdapter.StreamAsync(request, new StreamingOptions(), CancellationToken.None))
{
    var text = chunk.Body.GetLastAssistantText();
    // Process each chunk as it arrives
}
```

### Authentication

The provider uses Bearer token authentication (`Authorization: Bearer <key>`). Your API key is automatically applied from settings.

### Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct and has appropriate permissions
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available in your tier
- **Quota Exceeded**: Check your OpenAI usage limits and billing
- **Context Exceeded**: Use models with larger context windows or truncate prompts

---

## Developer Reference

### Batch Processing

Submit multiple requests as a batch through the `IAIBatchProvider` interface, then poll for completion. For a complete working example including result decoding, see `src/SmartHopper.Components.Test/Providers/TestOpenAIBatchCallComponent.cs`.

### Sending a Chat Completion

```csharp
var body = AIBodyBuilder.Create()
    .AddUser("What is the weather in Paris?")
    .Build();

var request = new AIRequestCall
{
    Provider = "openai",
    Model = "gpt-4o",
    Capability = AICapability.Text2Text,
    Body = body,
};

var response = await provider.Call(request);
var text = response.Body.GetLastAssistantText();
```

### Tool Calling

For function calling, configure the request with `AICapability.ToolChat` and a tool filter, then run a `ConversationSession` that handles the tool pass:

```csharp
using SmartHopper.Infrastructure.AICall.Sessions;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AIModels;

var body = AIBodyBuilder.Create()
    .AddText(AIAgent.System, "You are a helpful assistant.")
    .AddUser("Give me the canvas operational guidance.")
    .WithToolFilter("Knowledge.SmartHopper")
    .Build();

var request = new AIRequestCall
{
    Provider = "openai",
    Model = "gpt-4o",
    Capability = AICapability.ToolChat,
    Body = body,
};

var session = new ConversationSession(request);
var result = await session.RunToStableResult(
    new SessionOptions { ProcessTools = true, MaxTurns = 5, MaxToolPasses = 2 },
    CancellationToken.None);

var text = result.Body.GetLastAssistantText();
```

---

## Architecture & Design

The OpenAI provider implements the `AIProvider` base class and communicates with the OpenAI REST API over HTTPS. It translates SmartHopper's internal `AIRequestCall` objects into OpenAI-compatible request payloads and parses responses back into the standard SmartHopper response format.

Batch processing is handled by submitting jobs to the `/batches` endpoint and polling for completion. Image generation routes to `/images/generations` when the `ImageOutput` capability is requested. Dedicated speech capabilities route to `/audio/transcriptions` (`SpeechInput`, sent as `multipart/form-data` file uploads) and `/audio/speech` (`SpeechOutput`, returned as binary audio and normalized into typed `AIInteractionAudio` results). Multimodal audio — requests carrying `AIInteractionAudio` content parts or the `AudioInput`/`AudioOutput` capabilities — routes to `/chat/completions` using `input_audio` parts and `modalities: ["text", "audio"]`; the Responses API cannot carry audio payloads, so it is only used for ordinary text, tool, and JSON requests. Structured output support leverages OpenAI's `json_schema` strict mode by automatically wrapping schemas into valid objects.

## References

- [OpenAI API Documentation](https://platform.openai.com/docs/)
- [OpenAI Platform](https://platform.openai.com/)
