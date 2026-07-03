# Knowledge Components

Knowledge components handle content extraction, conversion, and summarization from various sources including files, web pages, and forum platforms.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/Knowledge/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Knowledge components bridge external data sources — such as local files, web pages, and community forums — with Grasshopper workflows. They enable both direct content extraction and AI-enhanced summarization, making it easy to bring outside information into your parametric models.

**You should read this if:**

- You need to extract text or markdown from files or web pages
- You want to summarize forum discussions or web content using AI
- You are integrating Discourse, Ladybug, or McNeel forum data into your workflow
- You need batch processing of external knowledge sources

---

## End-User Guide

### Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `File2MdComponent` | File2Md | Files | Converts local files to Markdown and extracts embedded images (non-AI) |
| `AIFile2MdComponent` | AIFile2Md | Files | AI-powered file-to-markdown conversion with image description |
| `Web2MdComponent` | Web2Md | Web | Converts web pages to Markdown; AI is used only for image descriptions when Image Mode is not `link` |
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

---

## Developer Reference

The following examples demonstrate how to interact with knowledge components programmatically and how to process forum data in custom scripts.

### Using File-to-Markdown Components in Code

```csharp
using SmartHopper.Components.Knowledge;

// Instantiate the non-AI file converter
var file2Md = new File2MdComponent();
file2Md.Params.Input[0].AddVolatileDataListAtPath(
    new GH_Path(0),
    @"C:\Documents\report.pdf");
file2Md.ExpireSolution(true);

// Retrieve the markdown output
string markdown = file2Md.Params.Output[0]
    .VolatileData.get_FirstItem(true).Value as string;

```

### Processing Discourse Forum Search Results

```csharp
using SmartHopper.Components.Knowledge;

// Search for topics on a Discourse instance
var search = new DiscourseSearchComponent();
search.Params.Input[0].AddVolatileDataListAtPath(
    new GH_Path(0), "<https://forum.example.com">);
search.Params.Input[1].AddVolatileDataListAtPath(
    new GH_Path(0), "grasshopper plugin");
search.ExpireSolution(true);

// Access results as a data tree
var results = search.Params.Output[0].VolatileData;
foreach (var path in results.Paths)
{
    foreach (var item in results.get_Branch(path))
    {
        Console.WriteLine(item.Value);
    }
}

```

---

## Architecture & Design

- Knowledge components bridge external data sources with AI processing
- `File2Md` uses file converters for content extraction (non-AI)
- `Web2Md` fetches web content with a local tool; AI is used only for image descriptions when Image Mode is not `link`
- `AIFile2Md` uses AI providers for file content and image description
- `AIWeb2Md` leverages AI providers for intelligent summarization and content enhancement
- Forum components integrate with Discourse, Ladybug, and McNeel community platforms
- All components support data tree processing for batch operations

