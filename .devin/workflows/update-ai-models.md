---
description: Check official API documentations for supported AI providers, compare with currently defined models, and suggest updates for new models, deprecated models, and parameter changes.
---

# AI Model Update Workflow

## Official API Documentation URLs
- **OpenAI:** https://developers.openai.com/api/docs/models
- **Anthropic:** https://platform.claude.com/docs/en/about-claude/models/overview
- **MistralAI:** https://docs.mistral.ai/getting-started/models
- **DeepSeek:** https://api-docs.deepseek.com/quick_start/pricing
- **OpenRouter:** https://openrouter.ai/models <- limit to the most known models

---

## Workflow Steps

### 1. Fetch Official API Models
For each provider, fetch the latest model list and capabilities from the official documentation URLs above.

**Actions:**
- Fetch the latest documentation for each provider.
- Parse the documentation to extract:
  - Model names
  - Capabilities (e.g., text input/output, function calling, streaming)
  - Default settings
  - Context limits
  - Deprecation status
- Store the fetched data in a structured format (e.g., JSON) for comparison.

---

### 2. Check Currently Defined Models
Retrieve the list of models currently defined in your codebase.

**Actions:**
- Check the `AIModelCapabilities` class in `SmartHopper.Core\Models\AIModelCapabilities.cs` to understand the structure.
- For each provider, read the corresponding `*ProviderModels.cs` file (e.g., `OpenAIProviderModels.cs`, `AnthropicProviderModels.cs`).
- Extract the list of `AIModelCapabilities` objects.
- Store this list for comparison with the official API data.

---
### 3. Compare and List New Models
Identify models not currently defined in your codebase.

**Actions:**
- Compare the official API model list with the current codebase model list.
- For each provider:
  - List models present in the official API but **not** in your codebase.
  - List models present in your codebase but **not** in the official API (potential deprecations or typos).

---
### 4. Suggest Updates
Generate a table of suggested changes, including:
- New models to add
- Models to deprecate
- Parameter changes (e.g., `Rank`, `ContextLimit`, `Default`)

**Actions:**
- For each provider:
  - **New models:** Suggest adding to the codebase, ranked by recency and cost (higher rank for cheaper, newer models).
  - **Models to deprecate:** Flag models marked as `Deprecated` in the official API or that are no longer available.
  - **Parameter changes:** Suggest updates for `Rank`, `ContextLimit`, or `Default` based on official documentation.

**Output Format:**
```markdown
   Provider     | Model Name         | Action       | Rank (new vs current) | Suggested Parameters (new vs current) | Notes                          |