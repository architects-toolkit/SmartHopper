# Output Components (`C. Output`)

Adapter components in `src/SmartHopper.Components/Output/` that consume `AIInputPayload` (`Input >`) from [Input components](../Input/index.md), call the selected AI provider, and extract typed Grasshopper outputs.

## Component catalog (`SmartHopper → C. Output`)

| Component | Display Name | Nickname | Extra inputs | Outputs (plus `Metrics`) |
| --- | --- | --- | --- | --- |
| `AI2TextComponent` | AI to Text | `AI2Text` | — | `Text` (`T`) |
| `AI2TextListComponent` | AI to Text List | `AI2TextList` | `Fallback` (`F`) | `Text List` (`T`), `Used Fallback` (`UF`) |
| `AI2MarkdownComponent` | AI to Markdown | `AI2MD` | — | `Markdown` (`M`) |
| `AI2NumberComponent` | AI to Number | `AI2Num` | `Fallback` (`F`) | `Number` (`N`), `Used Fallback` (`UF`) |
| `AI2NumberListComponent` | AI to Number List | `AI2NumList` | `Fallback` (`F`) | `Number List` (`N`), `Used Fallback` (`UF`) |
| `AI2IntegerComponent` | AI to Integer | `AI2Int` | `Fallback` (`F`) | `Integer` (`I`), `Used Fallback` (`UF`) |
| `AI2IntegerListComponent` | AI to Integer List | `AI2IntList` | `Fallback` (`F`) | `Integer List` (`I`), `Used Fallback` (`UF`) |
| `AI2BooleanComponent` | AI to Boolean | `AI2Bool` | `Fallback` (`F`) | `Boolean` (`B`), `Used Fallback` (`UF`) |
| `AI2BooleanListComponent` | AI to Boolean List | `AI2BoolList` | `Fallback` (`F`) | `Boolean List` (`B`), `Used Fallback` (`UF`) |
| `AI2JsonComponent` | AI to JSON | `AI2JSON` | `Schema` (`S`), `Fallback` (`F`) | `JSON` (`J`), `Used Fallback` (`UF`) |
| `AI2GhJsonComponent` | AI to GhJSON | `AI2GhJSON` | — | `GhJSON` (`G`) |
| `AI2ImgComponent` | AI to Image | `AI2Img` | `Size` (`Sz`), `Quality` (`Q`), `Style` (`St`) | `Image` (`I`) |
| `AI2SpeechComponent` | AI to Speech | `AI2Speech` | `Voice` (`V`), `Speed` (`Sp`) | `Audio` (`A`) |
| `AI2ScriptComponent` | AI to Script | `AI2Script` | `Language` (`L`) | `Script` (`S`) |

## Base class (`AIOutputAdapterBase`)

All output adapters inherit from [`AIOutputAdapterBase`](../ComponentBase/AIOutputAdapterBase.md) (which extends [`AIStatefulAsyncComponentBase`](../ComponentBase/AIStatefulAsyncComponentBase.md)):

- Merges all `AIInputPayload` items within each data-tree branch into a single `AIBody` and executes one AI call per branch.
- Derives required model capabilities from both `UsingAiTools` and the modalities present in the merged payload (`ImageInput` for images, `AudioInput` for audio).
- Decodes both synchronous and batch results through the component's `GetOutputMappings()` list.

## Related documentation

- [Input Components](../Input/index.md)
- [AIOutputAdapterBase](../ComponentBase/AIOutputAdapterBase.md)
- [AIRequestParameters](../../Architecture/AIRequestParameters.md)
