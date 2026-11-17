# Forums & Documents Components (`E. Forums` & `F. Documents`)

Components in `src/SmartHopper.Components/Knowledge/` for searching, reading, opening, deconstructing, and summarizing Discourse forum threads (`E. Forums`) and converting local files or web pages to Markdown (`F. Documents`).

## `E. Forums` panel (`SmartHopper → E. Forums`)

| Component | Display Name | Nickname | Purpose |
| --- | --- | --- | --- |
| `DiscourseSearchComponent` | Discourse Search | `DiscourseSearch` | Searches any Discourse forum (`Base URL`, `Query`) and outputs matching post JSON objects |
| `DiscoursePostGetComponent` | Discourse Post Get | `DiscoursePostGet` | Fetches a full Discourse post by `Base URL` and numeric `Id` |
| `DiscoursePostOpenComponent` | Discourse Post Open | `DiscoursePostOpen` | Opens a Discourse post JSON object in the default web browser |
| `DiscoursePostDeconstructComponent` | Deconstruct Discourse Post | `DeconstructDiscoursePost` | Splits post JSON into `Id`, `Username`, `Topic Id`, `Topic Title`, `Post URL`, `Date`, `Content`, `Reads`, and `Likes` |
| `AIDiscoursePostSummarizeComponent` | AI Discourse Post Summarize | `AIDiscoursePostSumm` | Generates an AI summary of one or more Discourse posts by `Post Id` |
| `AIDiscourseTopicSummarizeComponent` | AI Discourse Topic Summarize | `AIDiscourseTopicSumm` | Generates an AI summary of a Discourse topic by `Topic Id` |
| `McNeelForumSearchComponent` | McNeel Forum Search | `McNeelSearch` | Searches `discourse.mcneel.com` by `Query` |
| `McNeelForumPostGetComponent` | McNeel Forum Post Get | `McNeelPostGet` | Fetches a full post from `discourse.mcneel.com` by numeric `Id` |
| `McNeelForumPostOpenComponent` | McNeel Forum Post Open | `McNeelPostOpen` | Opens a McNeel forum post JSON object in the default web browser |
| `AIMcNeelForumPostSummarizeComponent` | AI McNeel Forum Post Summarize | `AIMcNeelPostSumm` | Generates an AI summary of one or more McNeel forum posts by `Post Id` |
| `AIMcNeelForumTopicSummarizeComponent` | AI McNeel Forum Topic Summarize | `AIMcNeelTopicSumm` | Generates an AI summary of a McNeel forum topic by `Topic Id` |
| `LadybugForumSearchComponent` | Ladybug Forum Search | `LadybugSearch` | Searches `discourse.ladybug.tools` by `Query` |
| `LadybugForumPostGetComponent` | Ladybug Forum Post Get | `LadybugPostGet` | Fetches a full post from `discourse.ladybug.tools` by numeric `Id` |
| `LadybugForumPostOpenComponent` | Ladybug Forum Post Open | `LadybugPostOpen` | Opens a Ladybug Tools forum post JSON object in the default web browser |
| `AILadybugForumPostSummarizeComponent` | AI Ladybug Forum Post Summarize | `AILadybugPostSumm` | Generates an AI summary of one or more Ladybug Tools forum posts by `Post Id` |
| `AILadybugForumTopicSummarizeComponent` | AI Ladybug Forum Topic Summarize | `AILadybugTopicSumm` | Generates an AI summary of a Ladybug Tools forum topic by `Topic Id` |

## `F. Documents` panel (`SmartHopper → F. Documents`)

| Component | Display Name | Nickname | Purpose |
| --- | --- | --- | --- |
| `File2MdComponent` | File To Markdown | `File2Md` | Converts local files (PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF…) to `Markdown`, embedded `Images` and separate `Rendered Pages` images locally (PDF renders + region crops; optional `Pages`/`Region` inputs; no AI required) |
| `AIFile2MdComponent` | AI File To Markdown | `AIFile2Md` | Converts local files to `Markdown` through the shared visual-enrichment path, appending AI-generated evidence under `## Visual Evidence (AI-Generated)`; embedded `Images` and `Rendered Pages` surfaces match File To Markdown (optional `Pages`/`Region` inputs; no `Image Mode` choice) |
| `Web2MdComponent` | Web To Markdown | `Web2Md` | Fetches a web page `URL` and converts it to `Markdown` locally (no AI required) |
| `AIWeb2MdComponent` | AI Web To Markdown | `AIWeb2Md` | Fetches a web page `URL` and converts it to `Markdown` through the shared visual-enrichment path, appending AI-generated evidence under `## Visual Evidence (AI-Generated)` (no `Image Mode` choice) |
