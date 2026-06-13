# Output Components

Output components extract and convert AI results from `AIReturn` into Grasshopper data types. These form the adapter layer for the new AIInputPayload architecture.

## Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `AI2TextComponent` | AI→Text | Text | Generates text from AI input payloads |
| `AI2TextListComponent` | AI→TextList | Text | Generates a list of text items from AI results |
| `AI2MarkdownComponent` | AI→Markdown | Text | Generates markdown-formatted text from AI results |
| `AI2NumberComponent` | AI→Number | Numbers | Extracts numeric values from AI results |
| `AI2NumberListComponent` | AI→NumberList | Numbers | Extracts a list of numbers from AI results |
| `AI2IntegerComponent` | AI→Integer | Numbers | Extracts integer values from AI results |
| `AI2IntegerListComponent` | AI→IntegerList | Numbers | Extracts a list of integers from AI results |
| `AI2BooleanComponent` | AI→Boolean | Boolean | Extracts boolean values from AI results |
| `AI2BooleanListComponent` | AI→BooleanList | Boolean | Extracts a list of boolean values from AI results |
| `AI2ImgComponent` | AI→Img | Images | Extracts generated images from AI results |
| `AI2JsonComponent` | AI→Json | JSON | Extracts structured JSON from AI results |
| `AI2GhJsonComponent` | AI→GhJSON | Grasshopper | Converts AI results to Grasshopper JSON format |
| `AI2ScriptComponent` | AI→Script | Code | Generates Python/C# scripts from AI results |
| `AI2SpeechComponent` | AI→Speech | Audio | Generates speech audio from AI text results |

## Architecture Notes

- All output components inherit from `AIOutputAdapterBase`
- Output components are the final step in the AIInputPayload adapter architecture
- They consume `AIReturn` results from AI components and convert to Grasshopper types
- Input components prepare data → AI components process → Output components convert results
- Support for structured output (JSON schema) enables type-safe data extraction
