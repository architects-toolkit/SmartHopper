# AI Components

Components specific to AI provider/model management and interactions.

## Provider & Model Management

- [AIModelsComponent](./AIModelsComponent.md) — Lists available AI models for the selected provider using dynamic API retrieval with static fallback.

## Input Adapters

Input adapters convert Grasshopper data into `AIInputPayload` wire types for composition with output components.

| Component | File | Purpose |
| --- | --- | --- |
| Text2AI | `src/SmartHopper.Components/Input/Text2AIComponent.cs` | Converts text input to AI payload |
| TextList2AI | `src/SmartHopper.Components/Input/TextList2AIComponent.cs` | Converts text list input to AI payload |
| AIContext | `src/SmartHopper.Components/Input/AIContextComponent.cs` | Provides context filters for AI operations |

## Output Adapters (AI Response Conversion)

Output adapters consume `AIInputPayload` inputs and convert AI responses to Grasshopper data types.

| Component | File | Output Type | Purpose |
| --- | --- | --- | --- |
| AI2Text | `src/SmartHopper.Components/Output/AI2TextComponent.cs` | Text | Converts AI response to single text value |
| AI2TextList | `src/SmartHopper.Components/Output/AI2TextListComponent.cs` | Text List | Converts AI response to text list |
| AI2Boolean | `src/SmartHopper.Components/Output/AI2BooleanComponent.cs` | Boolean | Converts AI response to boolean |
| AI2BooleanList | `src/SmartHopper.Components/Output/AI2BooleanListComponent.cs` | Boolean List | Converts AI response to boolean list |
| AI2Number | `src/SmartHopper.Components/Output/AI2NumberComponent.cs` | Number | Converts AI response to numeric value |
| AI2NumberList | `src/SmartHopper.Components/Output/AI2NumberListComponent.cs` | Number List | Converts AI response to number list |
| AI2Integer | `src/SmartHopper.Components/Output/AI2IntegerComponent.cs` | Integer | Converts AI response to integer |
| AI2IntegerList | `src/SmartHopper.Components/Output/AI2IntegerListComponent.cs` | Integer List | Converts AI response to integer list |
| AI2Json | `src/SmartHopper.Components/Output/AI2JsonComponent.cs` | JSON Object | Converts AI response to JSON object |
| AI2GhJson | `src/SmartHopper.Components/Output/AI2GhJsonComponent.cs` | Grasshopper JSON | Converts AI response to Grasshopper-native JSON |
| AI2Img | `src/SmartHopper.Components/Output/AI2ImgComponent.cs` | Image | Converts AI response to image |
| AI2Markdown | `src/SmartHopper.Components/Output/AI2MarkdownComponent.cs` | Markdown | Converts AI response to markdown text |
| AI2Script | `src/SmartHopper.Components/Output/AI2ScriptComponent.cs` | Script | Converts AI response to executable script |
| AI2Speech | `src/SmartHopper.Components/Output/AI2SpeechComponent.cs` | Speech/Audio | Converts AI response to speech audio |

## Legacy Components (Monolithic)

These components combine input and output in a single component. For new workflows, prefer the composable Input/Output adapter pattern above.

| Component | File | Purpose |
| --- | --- | --- |
| AIText2Text | `src/SmartHopper.Components/Text/AIText2TextComponent.cs` | Generates text from text input |
| AIText2Boolean | `src/SmartHopper.Components/Text/AIText2BooleanComponent.cs` | Evaluates text to boolean |
| AIText2TextList | `src/SmartHopper.Components/Text/AIText2TextListComponent.cs` | Generates text list from text input |
| AIText2Json | `src/SmartHopper.Components/JSON/AIText2JsonComponent.cs` | Generates JSON from text input |
| AIText2Img | `src/SmartHopper.Components/Img/AIText2ImgComponent.cs` | Generates image from text input |
| AIImg2Text | `src/SmartHopper.Components/Img/AIImg2TextComponent.cs` | Analyzes image to text |
| JsonObject2Text | `src/SmartHopper.Components/JSON/JsonObject2TextComponent.cs` | Converts JSON object to text |
| JsonArray2TextList | `src/SmartHopper.Components/JSON/JsonArray2TextListComponent.cs` | Converts JSON array to text list |
