# AI, Settings, Metrics & Convenience Components

Components in `src/SmartHopper.Components/AI/`, `src/SmartHopper.Components/Mcp/`, `src/SmartHopper.Components/Misc/` (metrics), `src/SmartHopper.Components/Text/`, `src/SmartHopper.Components/List/`, and `src/SmartHopper.Components/Script/`.

## `A. AI` panel (`SmartHopper → A. AI`)

| Component | Display Name | Nickname | Exposure | Purpose |
| --- | --- | --- | --- | --- |
| `AIChatComponent` | AI Chat | `AiChat` | primary | Interactive conversational assistant bound to the component instance, with full access to SmartHopper's canvas and knowledge tools |
| `AISettingsComponent` | Settings | `Settings` | primary | Assembles `AIRequestParameters` (`Model`, `Max Tokens`, `Temperature`, `Batch`, `Timeout`, `Extras`) for any AI component's `Settings` (`S`) input |
| [`AIModelsComponent`](./AIModelsComponent.md) | AI Models | `AIModels` | primary | Lists the model names available from the selected AI provider |
| `AIExtraSettingsComponent` | Extra Settings | `Extras` | secondary | Dynamically builds provider-specific request options as a JSON object for `AISettingsComponent.Extras` |
| `AIContextProvidersComponent` | Context Providers | `CxtProviders` | secondary + obscure | Lists all registered `IAIContextProvider` IDs (`time`, `file`, `environment`, `selection`, `viewport`, `file-metadata`, etc.) |
| `AIFileMetadataComponent` | File Metadata | `FileMeta` | secondary | Registers document metadata (`Title`, `Description`, `Version`, `Author`, `Tags`) for AI context and GhJSON export |
| `SmartHopperMcpServerComponent` | SmartHopper MCP Server | `MCP` | tertiary + obscure | Starts a local HTTP Model Context Protocol server exposing SmartHopper's tools to external MCP clients |
| `CloudRuntimeConnectionComponent` | Cloud Runtime Connection | `CloudRT` | tertiary + obscure | Connects this Grasshopper instance as a headless test runtime for signed-in SmartHopper Cloud agent sessions |
| `DeconstructMetricsComponent` | Deconstruct SmartHopper Metrics | `DMetrics` | quarternary | Splits a `Metrics` JSON tree into provider, model, finish reason, completion time, context usage, iteration counts and role, plus quantities and per-currency costs as JSON objects |
| `CombineMetricsComponent` | Combine SmartHopper Metrics | `CMetrics` | quarternary + obscure | Aggregates all `Metrics` JSON items within each branch into a single combined metrics JSON object per branch |

## `I. Text & Lists` panel (`SmartHopper → I. Text & Lists`)

One-step convenience components that take plain Grasshopper inputs, call the AI provider, and output typed results:

| Component | Display Name | Nickname | Exposure | Tool used |
| --- | --- | --- | --- | --- |
| `AIText2TextComponent` | AI Text To Text | `AIText2Text` | primary | `text2text` |
| `AIText2TextListComponent` | AI Text To Text List | `AIText2TextList` | primary | `text2textlist` |
| `AIText2BooleanComponent` | AI Text To Boolean | `AIText2Boolean` | primary | `text2boolean` |
| `AIText2JsonComponent` | AI Text To JSON | `AIText2Json` | primary + obscure | `text2json` |
| `AIListFilter` | AI List Filter | `AIListFilter` | secondary | `list_filter` |
| `AIList2BooleanComponent` | AI List To Boolean | `AIList2Boolean` | secondary | `textlist2boolean` |

## `J. Script` panel (`SmartHopper → J. Script`)

| Component | Display Name | Nickname | Tool(s) used |
| --- | --- | --- | --- |
| `AIScriptGenerateComponent` | AI Script Generate | `AIScriptGen` | `script_generate`, `script_edit`, `gh_get` |
| `AIScriptReviewComponent` | AI Script Review | `AIScriptReview` | `script_review` |

## Related documentation

- [Input Components (`B. Input`)](../Input/index.md)
- [Output Components (`C. Output`)](../Output/index.md)
- [ComponentBase](../ComponentBase/index.md)
- [AIRequestParameters](../../Architecture/AIRequestParameters.md)
