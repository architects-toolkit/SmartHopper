# Prompt Caching

How SmartHopper enables provider-side prompt caching, and the conditions required for cache hits.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Providers.*` (provider-specific encoding) |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Prompt caching reduces API costs and latency by reusing previously computed prompt prefixes across multiple requests. Understanding when and how caching works helps you decide whether to enable it and how to verify it is effective.

**You should read this if you:**

- Want to reduce API costs for repeated or batch AI operations
- Need to understand why cache hits are not occurring
- Are comparing costs between providers that support caching
- Want to optimize multi-turn conversations or batch workflows

---

## End-User Guide

### What Is This?

Prompt caching is a provider-side feature that stores the computed representation of prompt prefixes (system prompts, tools, and earlier messages) so subsequent requests with the same prefix do not need to reprocess them from scratch. When a cache hit occurs, you pay a fraction of the input token cost.

### When to Use It

- **Batch processing**: Running many requests with the same tools and system prompt but different user inputs
- **Multi-turn chat**: Conversations where the message history grows but the early turns remain stable
- **Cost-sensitive workflows**: Any scenario where the same prefix is reused across multiple calls

### Enabling

Set the extra parameter `enable_caching = true` (exposed in the provider's extras descriptors). Supported by the **Anthropic** and **OpenRouter** providers. Other providers (OpenAI, DeepSeek, Gemini 2.5, Grok, Groq via OpenRouter) cache automatically with no request changes needed.

### Verifying Cache Effectiveness

- **Anthropic**: Check `cache_creation_input_tokens` and `cache_read_input_tokens` in the response `usage`. Both 0 means no caching occurred (likely below the minimum prompt length or a changed prefix).
- **OpenRouter**: Check `prompt_tokens_details.cached_tokens` and `cache_write_tokens` in the usage object, or the `cache_discount` field.

### Common Questions

**Q: Why am I not seeing cache hits?**
A: Ensure the prefix (tools + system + cached messages) is 100% identical across requests. Even small changes like timestamps or dynamic context invalidate the cache. Also check that your prompt meets the minimum token threshold (1,024–4,096 tokens depending on model).

**Q: Does caching cost more if I never reuse the prefix?**
A: Yes. Cache writes cost 1.25x the base input price. Only enable caching when you expect repeated prefixes.

---

## Developer Reference

### API Overview

```csharp
// Caching is enabled via the provider's extras dictionary
var parameters = new AIRequestParameters
{
    Extras = new Dictionary<string, object>
    {
        { "enable_caching", true }
    }
};

```

### Key Concepts

| Concept | Description |
| --- | --- |
| **System breakpoint** | Explicit `cache_control` placed on the merged system block for Anthropic requests |
| **Top-level caching** | `cache_control` at the request root for automatic breakpoint advancement |
| **Sticky routing** | OpenRouter uses a stable `session_id` to route requests to the same provider endpoint |
| **Cache TTL** | 5 minutes for Anthropic; refreshed on each read |

### Code Examples

#### Enabling Caching for Anthropic

```csharp
var provider = ProviderManager.Instance.GetProvider("Anthropic");

var parameters = new AIRequestParameters
{
    Model = "claude-sonnet-4-20250514",
    Extras = new Dictionary<string, object>
    {
        { "enable_caching", true }
    }
};

var response = await provider.GenerateAsync(input, parameters);

// Check cache usage in response
var usage = response.Usage;
Console.WriteLine($"Cache creation: {usage.CacheCreationInputTokens}");
Console.WriteLine($"Cache read: {usage.CacheReadInputTokens}");

```

#### Enabling Caching for OpenRouter

```csharp
var provider = ProviderManager.Instance.GetProvider("OpenRouter");

var parameters = new AIRequestParameters
{
    Model = "anthropic/claude-sonnet-4",
    Extras = new Dictionary<string, object>
    {
        { "enable_caching", true }
    }
};

var response = await provider.GenerateAsync(input, parameters);

// Verify sticky routing is active
var sessionId = OpenRouterProvider.ComputeSessionId(parameters.Model, input.SystemMessage);
Console.WriteLine($"Session ID for sticky routing: {sessionId}");

```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| No cache hits | Prefix changed between requests | Ensure tools, system prompt, and message ordering are identical |
| No cache hits | Prompt too short | Minimum 1,024–4,096 tokens required depending on model |
| Higher costs | Cache writes without reuse | Disable caching if prefixes are not reused |
| OpenRouter routing issues | `cache_control` sent to non-Anthropic model | Only `anthropic/*` models receive explicit `cache_control` via OpenRouter |

---

## Architecture & Design

### Design Rationale

**Problem**: Repeated AI requests with the same system prompt and tools waste tokens and increase latency because providers recompute the same prefix every time.

**Approach**: Add an optional `enable_caching` extra parameter. Each provider encodes the request to include provider-specific caching hints (e.g., `cache_control` for Anthropic, `session_id` for OpenRouter sticky routing).

**Trade-offs**:

- Reduced cost and latency on repeated prefixes (benefit) vs higher cost on unique prefixes due to cache write fees (cost)
- Automatic provider routing (convenience) vs potential routing restrictions with sticky sessions (limitation)

### Provider-Specific Implementation

**Anthropic**

- Emits explicit system breakpoint + top-level automatic caching
- Benefits both single-shot/batch and multi-turn conversations

**OpenRouter**

- Emits top-level `cache_control` only for `anthropic/*` models
- Uses SHA-256-based `session_id` for provider sticky routing
- Implicit caching for OpenAI, DeepSeek, Gemini 2.5, Grok, Groq

### Not Supported

- Block-level explicit `cache_control` breakpoints inside message content (required for Gemini explicit caching and Alibaba Qwen via OpenRouter) are not emitted.

### Related Documentation

- [OpenRouter Provider](./OpenRouter.md)
- [Anthropic Provider](./Anthropic.md)
