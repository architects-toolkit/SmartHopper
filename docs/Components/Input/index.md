# Input Components

Input components wrap various data types and external sources into `AIInputPayload` for AI processing. These form the adapter layer for the new AIInputPayload architecture.

## Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `Text2AIComponent` | Text2AI | Text | Wraps text input into an AIInputPayload for AI processing |
| `TextList2AIComponent` | TextList2AI | Text | Wraps a list of text items into an AIInputPayload |
| `NumberList2AIComponent` | NumberList2AI | Numbers | Wraps a list of numbers into an AIInputPayload |
| `IntegerList2AIComponent` | IntegerList2AI | Numbers | Wraps a list of integers into an AIInputPayload |
| `BooleanList2AIComponent` | BooleanList2AI | Boolean | Wraps a list of boolean values into an AIInputPayload |
| `Img2AIComponent` | Img2AI | Images | Wraps image files or URLs into an AIInputPayload for vision processing |
| `Audio2AIComponent` | Audio2AI | Audio | Wraps audio files into an AIInputPayload for speech-to-text processing |
| `File2AIComponent` | File2AI | Files | Converts local files to markdown and wraps into an AIInputPayload |
| `Web2AIComponent` | Web2AI | Web | Fetches web content and wraps into an AIInputPayload |
| `Json2AIComponent` | Json2AI | JSON | Wraps JSON data into an AIInputPayload |
| `GhJSON2AIComponent` | GhJSON2AI | Grasshopper | Wraps Grasshopper JSON into an AIInputPayload |
| `AIPromptComponent` | AI Prompt | Prompts | Creates a structured AI prompt with system/user roles |
| `AIContextComponent` | AI Context | Context | Injects AI context data into an AIInputPayload |
| `DiscoursePost2AIComponent` | Discourse Post2AI | Forums | Wraps Discourse forum posts into an AIInputPayload |
| `DiscourseTopic2AIComponent` | Discourse Topic2AI | Forums | Wraps Discourse forum topics into an AIInputPayload |
| `LadybugPost2AIComponent` | Ladybug Post2AI | Forums | Wraps Ladybug forum posts into an AIInputPayload |
| `LadybugTopic2AIComponent` | Ladybug Topic2AI | Forums | Wraps Ladybug forum topics into an AIInputPayload |
| `McNeelPost2AIComponent` | McNeel Post2AI | Forums | Wraps McNeel forum posts into an AIInputPayload |
| `McNeelTopic2AIComponent` | McNeel Topic2AI | Forums | Wraps McNeel forum topics into an AIInputPayload |

## Architecture Notes

- All input components inherit from `AIInputAdapterBase`
- Input components are the first step in the AIInputPayload adapter architecture
- They convert Grasshopper data types and external sources into a unified `AIInputPayload` format
- Payloads are then passed to AI components (e.g., `AICallComponent`) for processing
- Output components consume the results and convert back to Grasshopper types
