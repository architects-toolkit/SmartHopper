---
description: Prepare an end-user focused changelog
---

# Changelog Simplification

When preparing changelog entries, focus on features and changes that matter to end users. Keep entries concise and avoid overly technical details.

## Guidelines

### Focus on End-User Value

**Include:**

- New AI models and capabilities
- New components or features
- UI/UX changes
- Breaking changes that affect saved files
- Performance improvements users will notice
- Deprecated features or models
- GitHub issue references in the format `[#123](https://github.com/architects-toolkit/SmartHopper/issues/123)` (keep issue IDs for compatibility with automated workflows)

**Briefly mention:**

- CI/CD improvements (one-liner)
- Infrastructure stability improvements (one-liner)
- Code quality improvements (only if significant)

**Exclude:**

- Detailed CI workflow specifications
- Concurrency settings and race condition prevention
- Auto-commit hardening specifics
- Per-provider technical minutiae
- Internal tooling details
- Implementation details unless they affect user experience

### Simplification Examples

**Before (too technical):**

```text
- ci(main-sync-to-dev): new workflow `.github/workflows/main-sync-to-dev.yml` that, on pushes to `main` (or manual dispatch), auto-opens/reuses a PR from `main` into `dev` and into every `dev-*` stabilization branch. For `dev-*` targets the diff is allow-listed to: any change under `.github/`, `.windsurf/`, `.githooks/`, `.hashes/`, plus *modifications* (not additions/renames/removals) to existing `src/SmartHopper.Providers.*/*ProviderModels.cs` files so model verification, deprecations, and provider model-list updates propagate to frozen lines. If any file outside the allow-list lands on `main`, the sync to that `dev-*` is skipped with a warning (use `patch-propagate.yml` for targeted backports). Reuses an existing open PR per target instead of creating duplicates, and skips entirely when there is no effective file diff.
```

**After (user-focused):**

```text
- **CI/CD**: Enhanced workflow automation for model verification, provider discovery, and stabilization branch management
```

**Before (too detailed):**

```text
- **AI model rebalancing**:
  - **OpenAI**: `gpt-5.4-mini` retains Rank 100 with `Default = ToolChat | Text2Json | ToolReasoningChat`; moved `Default = Text2Image | Image2Image` from `gpt-image-1-mini` to `gpt-image-2`; cleared `Default = Text2Image` from `dall-e-3` (Rank 80 → 70); demoted `gpt-image-1.5` Rank 75 → 65.
  - **Anthropic**: demoted `claude-opus-4-6` Rank 80 → 75 (superseded by `claude-opus-4-7`).
  - **DeepSeek**: cleared `Default` from `deepseek-chat` (Rank 90 → 70) and `deepseek-reasoner` (Rank 80 → 60); both aliased to `deepseek-v4-flash` per official docs.
  - **OpenRouter**: aligned mirrored OpenAI model `Default` flags with the native OpenAI provider entries — `openai/gpt-5.4-mini` and `openai/gpt-5-mini` now use `ToolChat | Text2Json | ToolReasoningChat`; cleared `Default` on `openai/gpt-5.4` to match native (no Default).
```

**After (user-focused):**

```text
- **AI model rankings**: Adjusted default models and rankings across providers based on official documentation
```

### Entry Structure

Keep each entry to 1-2 lines maximum. Use clear, simple language.

**Good format:**

```text
- **Category**: Brief description of the change
```

**Bad format:**

```text
- **Category**: Detailed technical explanation with implementation details, file paths, and internal workflow specifications that users don't need to know about
```

### When to Simplify

Simplify changelog entries when:

- The release is primarily infrastructure/CI improvements
- The changes are technical and not user-facing
- The entry exceeds 2-3 lines
- The entry contains file paths, workflow names, or implementation details

Keep detailed entries when:

- The change introduces new user-facing features
- The change is a breaking change that affects saved files
- The change deprecates features users rely on

### Workflow Compatibility

This simplification workflow is compatible with the following automated workflows:

- **chore-update-contributors.yml**: Automatically updates the contributors section under `[Unreleased]` with GitHub username links. Ensure you don't remove the contributors section when simplifying.
- **pr-update-changelog-issues.yml**: Automatically adds closed issues to the `### Fixed` section under `[Unreleased]` in the format `[#123](https://github.com/architects-toolkit/SmartHopper/issues/123)`. Always preserve these issue references when simplifying - do not remove issue IDs or links.

When simplifying the `### Fixed` section, ensure you:

- Keep all issue references in the format `[#123](link-to-issue)`
- Do not remove issue IDs even if simplifying the description
- Group related fixes by issue when possible for clarity
