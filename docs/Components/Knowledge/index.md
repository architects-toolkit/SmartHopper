# Knowledge Components

Knowledge components handle content extraction, conversion, and summarization from various sources including files, web pages, and forum platforms.

## Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `File2MdComponent` | File2Md | Files | Converts local files to Markdown and extracts embedded images (non-AI) |
| `AIFile2MdComponent` | AIFile2Md | Files | AI-powered file-to-markdown conversion with image description |
| `Web2MdComponent` | Web2Md | Web | Converts web pages to Markdown (non-AI) |
| `AIWeb2MdComponent` | AIWeb2Md | Web | AI-powered web-to-markdown conversion with content summarization |
| `DiscourseSearchComponent` | Discourse Search | Forums | Searches Discourse forum posts and topics |
| `DiscoursePostGetComponent` | Discourse Post Get | Forums | Retrieves a specific Discourse forum post |
| `DiscoursePostOpenComponent` | Discourse Post Open | Forums | Opens a Discourse forum post in browser |
| `DiscoursePostDeconstructComponent` | Discourse Post Deconstruct | Forums | Deconstructs Discourse post data into components |
| `AIDiscoursePostSummarizeComponent` | AI Discourse Post Summarize | Forums | AI-powered summarization of Discourse posts |
| `AIDiscourseTopicSummarizeComponent` | AI Discourse Topic Summarize | Forums | AI-powered summarization of Discourse topics |
| `LadybugForumSearchComponent` | Ladybug Search | Forums | Searches Ladybug forum posts and topics |
| `LadybugForumPostGetComponent` | Ladybug Post Get | Forums | Retrieves a specific Ladybug forum post |
| `LadybugForumPostOpenComponent` | Ladybug Post Open | Forums | Opens a Ladybug forum post in browser |
| `AILadybugForumPostSummarizeComponent` | AI Ladybug Post Summarize | Forums | AI-powered summarization of Ladybug posts |
| `AILadybugForumTopicSummarizeComponent` | AI Ladybug Topic Summarize | Forums | AI-powered summarization of Ladybug topics |
| `McNeelForumSearchComponent` | McNeel Search | Forums | Searches McNeel forum posts and topics |
| `McNeelForumPostGetComponent` | McNeel Post Get | Forums | Retrieves a specific McNeel forum post |
| `McNeelForumPostOpenComponent` | McNeel Post Open | Forums | Opens a McNeel forum post in browser |
| `AIMcNeelForumPostSummarizeComponent` | AI McNeel Post Summarize | Forums | AI-powered summarization of McNeel posts |
| `AIMcNeelForumTopicSummarizeComponent` | AI McNeel Topic Summarize | Forums | AI-powered summarization of McNeel topics |

## Architecture Notes

- Knowledge components bridge external data sources with AI processing
- Non-AI variants (File2Md, Web2Md) use file converters for content extraction
- AI variants leverage AI providers for intelligent summarization and content enhancement
- Forum components integrate with Discourse, Ladybug, and McNeel community platforms
- All components support data tree processing for batch operations
