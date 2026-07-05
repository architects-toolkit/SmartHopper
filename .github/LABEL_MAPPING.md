# SmartHopper Label Mapping

This document describes how repository folders map to GitHub labels and how the automated labeling system works.

## Label Categories

| Prefix | Granularity | Source of truth | Color |
| ------ | ----------- | --------------- | ----- |
| `component:` | Per **file** | `src/SmartHopper.Components/**` | `1B2638` |
| `scope:` | Per **folder** | `src/SmartHopper.Core/**`, `src/SmartHopper.Infrastructure/**` | `000000` |
| `provider:` | Per **folder** | `src/SmartHopper.Providers.<Name>/` | `1A3636` |

All other label categories (`status:`, `priority:`, `close:`, `automated`, `ci`, `documentation`, etc.) are managed manually and preserved by the automation scripts.

## component: Naming Rule

For a component file like `AIText2TextComponent.cs`:

1. Trim `.cs` → `AIText2TextComponent`
2. Trim `Component` or `Components` suffix → `AIText2Text`
3. If the result starts with `AI`, insert a space after `AI` → **`AI Text2Text`**

Examples:

| File | Label |
| ---- | ----- |
| `AIChatComponent.cs` | `component: AI Chat` |
| `GhGetComponents.cs` | `component: GhGet` |
| `DeconstructMetricsComponent.cs` | `component: Deconstruct Metrics` |
| `AIImg2TextComponent.cs` | `component: AI Img2Text` |

## scope: Naming Rule

For a folder like `src/SmartHopper.Core/AIContext/`:

- Folder name in Title Case → **`scope: AIContext`**

Example: `src/SmartHopper.Infrastructure/Mcp/` → `scope: Mcp` (also auto-tagged when issue/PR content mentions MCP, McpServer, or SmartHopperMcpServer).

## provider: Naming Rule

For a folder like `src/SmartHopper.Providers.OpenAI/`:

- Provider name from folder → **`provider: OpenAI`**

## AI Tool Propagation

When an AI tool file under `src/SmartHopper.Core.Grasshopper/AITools/` is edited, the PR auto-label workflow automatically applies the `component:` labels of all components that declare that tool in their `UsingAiTools` property.

Example: editing `text2text.cs` triggers the `component: AI Text2Text` label because `AIText2TextComponent.cs` declares:

```csharp
protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };
```

## Automation Workflows

| Workflow | Trigger | Purpose |
| -------- | ------- | ------- |
| `github-pr-auto-label.yml` | PR open/sync | Path-based labels + AI tool propagation |
| `pre-commit.ps1` | Every commit | Regenerate `labels.yml` and `labeler.yml` component mappings |
| `one-time-label-sync.yml` | Manual | Rename/delete/create labels on GitHub and migrate issues/PRs |
| `github-labels-sync.yml` | Push to labels.yml | Keep GitHub labels in sync with `labels.yml` definitions |

## PowerShell Scripts

| Script | Purpose |
| ------ | ------- |
| `tools/Update-GitHubLabels.ps1` | Scan project structure and update both `labels.yml` and `labeler.yml` |
| `tools/Get-ComponentAiTools.ps1` | Extract `UsingAiTools` from a component file |
| `tools/Find-ComponentsUsingAiTool.ps1` | Find all components that use a given AI tool |

## Single Source of Truth

- `.github/labels.yml` — canonical list of all label names, colors, and descriptions.
- `.github/labeler.yml` — canonical mapping of file paths to labels for PR auto-labeling.

Both files are updated automatically by the workflows described above.
