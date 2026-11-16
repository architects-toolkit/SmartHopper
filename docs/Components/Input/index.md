# Input Components (`B. Input`)

Adapter components in `src/SmartHopper.Components/Input/` that package Grasshopper data, media, files, web pages, forum posts, and system/context instructions into `AIInputPayload` (`Input >`) for any [Output component](../Output/index.md).

## Component catalog (`SmartHopper → B. Input`)

| Component | Display Name | Nickname | What it packages |
| --- | --- | --- | --- |
| `Text2AIComponent` | Text to AI | `Text2AI` | Single text prompt or message |
| `TextList2AIComponent` | Text List to AI | `TextList2AI` | List of strings as one JSON array per branch |
| `Number2AIComponent` | Number to AI | `Num2AI` | Single number |
| `NumberList2AIComponent` | Number List to AI | `NumList2AI` | List of numbers as one JSON array per branch |
| `Integer2AIComponent` | Integer to AI | `Int2AI` | Single integer |
| `IntegerList2AIComponent` | Integer List to AI | `IntList2AI` | List of integers as one JSON array per branch |
| `Boolean2AIComponent` | Boolean to AI | `Bool2AI` | Single boolean |
| `BooleanList2AIComponent` | Boolean List to AI | `BoolList2AI` | List of booleans as one JSON array per branch |
| `Json2AIComponent` | JSON to AI | `Json2AI` | Validated JSON string |
| `Img2AIComponent` | Image to AI | `Img2AI` | Image (`VersatileImage`, Bitmap, file path, URL, data URI, or base64) |
| `Audio2AIComponent` | Audio to AI | `Audio2AI` | Local audio file (`.mp3`, `.wav`, `.m4a`, `.ogg`, `.flac`) |
| `File2AIComponent` | File to AI | `File2AI` | Local file packaged as one ordered multimodal payload (PDF: rendered pages + extracted text + embedded assets; optional `Pages`/`Region` JSON inputs; no provider call while packaging); also outputs `Markdown` and `Format` |
| `Web2AIComponent` | Web to AI | `Web2AI` | Web page URL packaged as one ordered multimodal payload (article text + approved materialized images; no provider call while packaging); also outputs `Markdown` |
| `GhJSON2AIComponent` | Canvas to AI | `GhJSON2AI` | Active canvas (or selected/filtered components) exported as GhJSON; also outputs `GhJSON` |
| `AIPromptComponent` | AI Prompt | `AIPrompt` | System prompt (`AIAgent.System`) guiding AI behavior |
| `AIContextComponent` | AI Context | `AICtx` | Live context from a registered context provider ID (`time`, `file`, `environment`, `selection`, `viewport`, etc.); also outputs `Display` |
| `DiscoursePost2AIComponent` | Discourse Post to AI | `DiscoursePost2AI` | Discourse forum post by `Base URL` and `Post ID` |
| `DiscourseTopic2AIComponent` | Discourse Topic to AI | `DiscourseTopic2AI` | Discourse forum topic by `Base URL`, `Topic ID`, and `Max Posts` |
| `LadybugPost2AIComponent` | Ladybug Post to AI | `LadybugPost2AI` | Ladybug Tools forum post by `Post ID` |
| `LadybugTopic2AIComponent` | Ladybug Topic to AI | `LadybugTopic2AI` | Ladybug Tools forum topic by `Topic ID` and `Max Posts` |
| `McNeelPost2AIComponent` | McNeel Post to AI | `McNeelPost2AI` | McNeel Discourse forum post by `Post ID` |
| `McNeelTopic2AIComponent` | McNeel Topic to AI | `McNeelTopic2AI` | McNeel Discourse forum topic by `Topic ID` and `Max Posts` |

## Base classes

- Synchronous adapters derive from [`AIInputAdapterBase`](../ComponentBase/AIInputAdapterBase.md), which seals `RegisterOutputParams` to expose `Input >` (`>`) at index 0 and provides `CreateTextPayload`, `CreateImagePayload`, `CreateAudioPayload`, `CreateContextPayload`, and `WrapPayload`.
- Asynchronous adapters that fetch or convert external resources (`File2AIComponent`, `Web2AIComponent`, and the forum/canvas adapters) derive from [`StatefulComponentBase`](../ComponentBase/StatefulComponentBase.md) or [`AIStatefulAsyncComponentBase`](../ComponentBase/AIStatefulAsyncComponentBase.md). File/Web-to-AI packaging makes no provider call — visual-evidence compatibility is resolved later by the consuming output component's request preparation.

## Related documentation

- [AIInputPayload](../../Architecture/AIInputPayload.md)
- [Output Components](../Output/index.md)
- [ComponentBase](../ComponentBase/index.md)
