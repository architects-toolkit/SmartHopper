# OpenRouter Provider

The OpenRouter provider provides access to multiple AI models through a unified OpenAI-compatible interface, supporting text generation, vision, tool calling, structured outputs, reasoning, and streaming.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Providers.OpenRouter/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

OpenRouter lets SmartHopper users access hundreds of AI models from dozens of providers through a single API key and a unified interface. It offers automatic fallback routing, cost optimization, and prompt caching support without changing any component code.

**You should read this if you:**

- Want to use models from multiple providers (OpenAI, Anthropic, Google, Mistral, DeepSeek, etc.) with one setup
- Need automatic fallback when a model or provider is unavailable
- Want to optimize for cost, latency, or throughput with provider sorting
- Are configuring prompt caching with OpenRouter's sticky routing

---

## End-User Guide

### What Is This?

The OpenRouter provider is a SmartHopper AI provider plugin that connects to [OpenRouter](https://openrouter.ai/), a unified API gateway for hundreds of AI models. Instead of managing separate API keys for OpenAI, Anthropic, Google, and others, you configure a single OpenRouter key and gain access to all supported models.

### When to Use It

- **Multi-provider access**: You want to try or compare models from different providers without switching API keys
- **Cost optimization**: OpenRouter sorts providers by price, latency, or throughput automatically
- **Fallback reliability**: If a provider is down, OpenRouter routes to an alternative automatically
- **Vision + text in one call**: Use vision-capable models without provider-specific configuration

### Configuration

#### API Key

Get your API key from [OpenRouter](https://openrouter.ai/):

1. Visit <https://openrouter.ai/>
2. Sign up or log in
3. Navigate to API keys
4. Create a new key and copy it into SmartHopper's OpenRouter provider settings

#### Settings

| Setting | Description | Default |
| --- | --- | --- |
| **API Key** | Your OpenRouter API key (required) | â€” |
| **Model** | Select from available models (resolved from registry) | Default from registry |
| **Enable Streaming** | Allow streaming responses | Enabled |
| **Max Tokens** | Maximum output tokens | 2000 (range: 1â€“100000) |
| **Temperature** | Controls randomness | 0.5 (range: 0.0â€“2.0) |
| **Allow Fallbacks** | Allow fallback to compatible models if preferred is unavailable | true |
| **Sort Strategy** | Provider selection sort â€” `price`, `throughput`, or `latency` | price |
| **Data Collection** | Allow or deny provider data collection | deny |

### Common Questions

**Q: Why use OpenRouter instead of connecting directly to OpenAI or Anthropic?**
A: OpenRouter offers unified billing, automatic provider fallback, and the ability to switch between models from different providers without changing API keys or component configurations.

**Q: Does streaming work with all models?**
A: Streaming is supported for all OpenAI-compatible models. Some providers may have slight latency differences.

**Q: What happens if my chosen model is unavailable?**
A: If fallbacks are enabled (default), OpenRouter automatically routes to the next best available provider for the same model.

---

## Developer Reference

### API Overview

```csharp
// OpenRouter provider inherits from AIProvider and implements IAIProvider
public class OpenRouterProvider : AIProvider
{
    public override string Name => "OpenRouter";
    public override AICapability Capabilities =>
        AICapability.TextGeneration | AICapability.Vision |
        AICapability.ToolCalling | AICapability.Streaming;
}

```

### Key Types

| Type | Purpose |
| --- | --- |
| `OpenRouterProvider` | Main provider implementation |
| `OpenRouterSettings` | Provider-specific settings (API key, model, extras) |
| `OpenRouterModels` | Model registry with capability flags |

### Code Examples

#### Basic Usage

```csharp
// Get the OpenRouter provider from the manager
var provider = ProviderManager.Instance.GetProvider("OpenRouter");

// Build a simple text generation request
var input = new AIInputPayload();
input.Messages.Add(new AIMessage { Role = "user", Content = "Hello, world!" });

var parameters = new AIRequestParameters
{
    Model = "openai/gpt-4o",
    Temperature = 0.7
};

var response = await provider.GenerateAsync(input, parameters);

```

**Output**: The AI-generated text response.

#### Streaming with OpenRouter

```csharp
var request = new AIRequestCall
{
    EnableStreaming = true,
    Provider = provider,
    Parameters = parameters
};

await foreach (var chunk in provider.StreamAsync(request))
{
    // Process each chunk as it arrives
    Console.Write(chunk.Text);
}

```

#### Tool Calling

```csharp
// OpenRouter supports function calling via OpenAI-compatible tools
var tools = new List<AITool>
{
    new AITool
    {
        Name = "get_weather",
        Description = "Get current weather for a location",
        Parameters = new JsonSchema { /* ... */ }
    }
};

var response = await provider.GenerateAsync(input, parameters, tools);

```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| `Invalid API Key` | Key is missing or incorrect | Verify your API key in provider settings |
| `Rate Limiting` | Too many requests | Implement exponential backoff for retries |
| `Model Not Available` | Model unavailable on all providers | Enable fallbacks or select a different model |
| `Provider Error` | Underlying provider failure | Check the provider's status page |
| `Routing Issues` | Session sticky routing restricts options | Disable caching or wait for cache expiry |

---

## Architecture & Design

### Design Rationale

**Problem**: Users want access to multiple AI providers and models without managing separate API keys, accounts, and configurations.

**Approach**: Implement OpenRouter as a standard `AIProvider` subclass. OpenRouter uses an OpenAI-compatible API, so most request/response formatting is handled by the shared `AIProvider` base class.

**Trade-offs**:

- Single point of billing through OpenRouter (simpler) vs direct provider relationships (potentially cheaper)
- Automatic provider selection (convenient) vs explicit provider control (predictable)
- Sticky routing for cache warmth (performance) vs optimal provider selection per-request (cost)

### System Relationships

```text
SmartHopper Component â†’ AIRequestCall â†’ OpenRouterProvider â†’ OpenRouter API â†’ Underlying Provider

```

### Authentication

The provider uses Bearer token authentication. SmartHopper also sends required attribution headers:

- `X-Title`: `SmartHopper`
- `Referer`: `https://smarthopper.xyz`

### Prompt Caching Integration

When `enable_caching` is set in extras:

- Anthropic models receive `cache_control: { type: "ephemeral" }`
- A stable `session_id` is computed for sticky routing to keep caches warm

### Related Documentation

- [Prompt Caching](./PromptCaching.md)
- [Provider Manager](./ProviderManager.md)
- [AI Provider Base](./AIProvider.md)
