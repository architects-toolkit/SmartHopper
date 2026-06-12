# Prompt caching

How SmartHopper enables provider-side prompt caching, and the conditions required for cache hits.

## Enabling

Set the extra parameter `enable_caching = true` (exposed in the provider's extras descriptors). Supported by the **Anthropic** and **OpenRouter** providers. Other providers (OpenAI, DeepSeek, Gemini 2.5, Grok, Groq via OpenRouter) cache automatically with no request changes needed.

## Anthropic provider

When `enable_caching` is on, `AnthropicProvider.Encode()` emits two complementary mechanisms:

- **Explicit system breakpoint**: the `system` field is sent as a content-block array with `cache_control: {"type": "ephemeral"}` on the (single, merged) system block. This caches the stable prefix (`tools` + `system`), so single-shot and batch requests sharing the same tools and system prompt get cache reads even when the user message varies per request.
- **Top-level automatic caching**: `cache_control: {"type": "ephemeral"}` at the request root. Anthropic automatically advances the breakpoint over the growing message history, benefiting multi-turn conversations (chat, agentic tool loops). This is a no-op when the last block already carries the same explicit breakpoint.

### Conditions for cache hits (Anthropic)

- The prefix (`tools` → `system` → cached `messages`) must be **100% identical** across requests, including tool definitions and ordering.
- Minimum cacheable prompt length: 1,024–4,096 tokens depending on model. Shorter prompts are silently not cached.
- 5-minute TTL, refreshed on each read.
- A cache entry only becomes available after the first response begins — fully parallel requests miss each other's writes.
- Dynamic content injected into the system prompt (e.g. timestamps from context providers) invalidates the system cache on every request.

### Costs

- Cache writes: 1.25x base input price (5-minute TTL).
- Cache reads: 0.1x base input price.
- If the prefix never repeats, enabling caching costs more than disabling it.

## OpenRouter provider

When `enable_caching` is on, `OpenRouterProvider.Encode()`:

- Emits top-level `cache_control` **only for `anthropic/*` models** (Anthropic automatic caching). For other models the field is omitted: OpenAI, DeepSeek, Grok, Groq, and Gemini 2.5 cache implicitly, and sending top-level `cache_control` for them is undocumented and restricts OpenRouter routing.
- Sends a stable `session_id` (SHA-256 of model + first system text, see `ComputeSessionId`) to activate **provider sticky routing** from the first request, so subsequent requests with the same stable prefix are routed to the same provider endpoint and the cache stays warm.

### Not supported

- Block-level explicit `cache_control` breakpoints inside message content (required for Gemini explicit caching and Alibaba Qwen via OpenRouter) are not emitted.

## Verifying cache effectiveness

- Anthropic: check `cache_creation_input_tokens` and `cache_read_input_tokens` in the response `usage`. Both 0 means no caching occurred (likely below the minimum prompt length or a changed prefix).
- OpenRouter: check `prompt_tokens_details.cached_tokens` and `cache_write_tokens` in the usage object, or the `cache_discount` field.
