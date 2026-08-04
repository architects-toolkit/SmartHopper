# Google Gemini Provider

The Google Gemini provider integrates Google's Gemini AI models into SmartHopper, supporting text generation, image generation, structured outputs, tool calling, and batch processing.

## Features

- **Models**: Gemini 3.1, Gemini 2.5, Gemini 2.0, and Gemini 1.5 models
- **Text Generation**: Full support for text-to-text conversations
- **Image Generation**: Generate images using Gemini image models
- **Structured Outputs**: JSON schema support with Gemini's JSON Schema subset
- **Tool Calling**: Function calling with structured arguments
- **Thinking/Reasoning**: Extended thinking with configurable thinking levels
- **Batch Processing**: Asynchronous batch job submission and status polling
- **Streaming**: Real-time response streaming via Server-Sent Events (SSE)

## Configuration

### API Key

Get your API key from [Google AI Studio](https://ai.google.dev/):

1. Visit https://ai.google.dev/
2. Click "Get API key"
3. Create a new API key
4. Copy the key into SmartHopper's Google provider settings

### Settings

- **API Key**: Your Google AI API key (required)
- **Model**: Select from available Gemini models (default: `gemini-2.5-flash`)
- **Enable Streaming**: Allow streaming responses (default: enabled)
- **Max Tokens**: Maximum output tokens (default: 2000)
- **Temperature**: Controls randomness (0.0â€“2.0, default: 1.0)

### Extra Parameters

- **Thinking Level**: Gemini 3 (`minimal`/`low`/`medium`/`high`) or Gemini 2.5 (integer budget)
- **Batch Priority**: Priority for batch jobs (0 = default)
- **Image Aspect Ratio**: For image generation (e.g., `16:9`, `1:1`)
- **Image Size**: For image generation (e.g., `1K`, `2K`, `4K`)
- **Top-K Sampling**: Top-K parameter for sampling
- **Top-P Sampling**: Top-P (nucleus) parameter
- **Random Seed**: For deterministic outputs
- **Safety Level**: Content safety filter level

## Model Capabilities

### Gemini 3.1 (Preview)

- **gemini-3.1-pro-preview**: Full-featured flagship model
- **gemini-3.1-flash-preview**: Fast, efficient model
- **gemini-3-pro-image-preview**: Image generation with reasoning
- **gemini-3.1-flash-image-preview**: Fast image generation

### Gemini 2.5 (Stable)

- **gemini-2.5-pro**: High-quality text generation
- **gemini-2.5-flash**: Recommended default for most tasks
- **gemini-2.5-flash-image**: Image generation
- **gemini-2.5-flash-lite**: Lightweight, fast model

### Gemini 2.0 & 1.5 (Stable/Deprecated)

- **gemini-2.0-flash**: Stable, verified model
- **gemini-1.5-pro**: Deprecated but available
- **gemini-1.5-flash**: Deprecated but available

## JSON Schema Support

Gemini supports a subset of JSON Schema for structured outputs:

**Supported types**: `string`, `number`, `integer`, `boolean`, `object`, `array`, `null`

**Supported keywords**: `title`, `description`, `properties`, `required`, `additionalProperties`, `enum`, `format`, `minimum`, `maximum`, `items`, `prefixItems`, `minItems`, `maxItems`

**Note**: Gemini 2.0 models require `propertyOrdering` array for proper structure (auto-injected by the provider).

## Thinking/Reasoning

Configure thinking levels for extended reasoning:

- **Gemini 3**: Use `thinking_level` extra parameter with values: `minimal`, `low`, `medium`, `high`
- **Gemini 2.5**: Use `thinking_level` as integer string (e.g., `"8192"` for 8K token budget, `"0"` to disable)

## Image Generation

For image generation models:

1. Select an image model (e.g., `gemini-2.5-flash-image`)
2. Set request capability to `ImageOutput`
3. Optionally configure `image_aspect_ratio` and `image_size` in extras
4. Images are returned as base64-encoded data in the response

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

// Access results
foreach (var result in status.Results.Values)
{
    // Process result
}
```

Optional: Set `batch_priority` in extras (0 = default, higher = higher priority).

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

The provider uses Google's `x-goog-api-key` authentication header. Your API key is automatically applied from the settings.

## Error Handling

Common errors:

- **Invalid API Key**: Verify your API key is correct and has appropriate permissions
- **Rate Limiting**: Implement exponential backoff for retries
- **Model Not Available**: Check that the selected model is available in your region
- **Quota Exceeded**: Check your Google Cloud project quotas

## References

- [Google AI API Documentation](https://ai.google.dev/api?hl=en)
- [Gemini 3 Developer Guide](https://ai.google.dev/gemini-api/docs/gemini-3)
- [Image Generation Guide](https://ai.google.dev/gemini-api/docs/image-generation)
- [Batch Processing Guide](https://ai.google.dev/gemini-api/docs/batch-processing)

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about Gemini.


## End-User Guide

End-user guidance for Gemini.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for Gemini.
