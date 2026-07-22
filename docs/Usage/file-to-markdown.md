# File-to-Markdown Conversion

SmartHopper provides native file-to-Markdown conversion through the `File2MdComponent` and `file2md` AI tool, enabling you to extract and process content from various document formats directly in Grasshopper.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document explains how to convert documents to Markdown within Grasshopper, which supported formats are available, and how the underlying converter architecture works. It covers both component usage and AI tool integration.

**You should read this if you:**

- Need to extract text from PDFs, Office documents, or other file formats
- Want to use AI Chat with document content automatically
- Plan to extend the converter system with a custom file format

---

## End-User Guide

### Supported Formats

| Format | Extension | Converter | Features |
| --- | --- | --- |-----------|----------|
| **PDF** | `.pdf` | PdfPig | Column detection, reading order, header/footer removal, heading detection, **hyperlink extraction**, **list detection**, **inline image positioning**, table recognition, scanned-page warnings |
| **Word** | `.docx` | DocumentFormat.OpenXml | Headings (H1-H6), bold/italic, lists, tables, hyperlinks, footnotes/endnotes, Office Math, images (as placeholders), metadata |
| **Excel** | `.xlsx` | DocumentFormat.OpenXml | Multi-sheet support, header rows, Markdown tables, cell formatting (bold/italic), metadata |
| **PowerPoint** | `.pptx` | DocumentFormat.OpenXml | Slide titles, body text, bullet points, speaker notes, hyperlinks, Office Math, metadata |
| **OpenDocument Text** | `.odt`, `.ott` | Built-in | Headings, bold/italic/underline/strikethrough, colors, highlights, lists, tables, hyperlinks, images, metadata |
| **OpenDocument Spreadsheet** | `.ods`, `.ots` | Built-in | Markdown tables, cell formatting (bold/italic), metadata |
| **OpenDocument Presentation** | `.odp`, `.otp` | Built-in | Slide pages, body text, bullet points, images, metadata |
| **HTML** | `.html`, `.htm` | HtmlAgilityPack | Readability scoring, boilerplate removal, semantic content extraction |
| **Email** | `.eml` | MimeKit | From/To/Subject/Date, HTML or plain text body, attachment list |
| **EPUB** | `.epub` | Built-in | Chapter extraction in reading order, metadata |
| **RTF** | `.rtf` | RichTextBox (Windows) / Regex (macOS) | Plain text extraction |
| **CSV** | `.csv` | Built-in | Markdown table conversion |
| **JSON** | `.json` | Built-in | Pretty-printed fenced code block |
| **XML** | `.xml` | Built-in | Pretty-printed fenced code block |
| **Plain Text** | `.txt` | Built-in | Pass-through with line normalization |

## Using the File2Md Component

#### Basic Usage

1. Add the **File2Md** component from the **SmartHopper → Knowledge** panel
2. Connect a file path (absolute path) to the **File Path** input
3. Use **Remove Headers** to toggle header/footer removal (default: `true`)
4. The component outputs:
   - **Markdown**: Converted Markdown content
   - **Images**: Extracted images (when present) as `VersatileImage` objects
   - **Format**: Detected original format (e.g., "pdf", "docx")

> **Note:** Preserve Formatting is always enabled in the `File2Md` component. The `preserveFormatting` tool parameter remains available for direct `file2md` tool calls and for AI components that wrap `file2md`.

#### Example

```

File Path: C:\Documents\report.pdf
↓
[File2Md]
↓
Markdown: "# Annual Report\n\nThis document presents..."
Format: "pdf"

```

#### Tree Support

The component supports tree inputs, allowing you to convert multiple files in parallel:

```

{0;0}: C:\docs\file1.pdf
{0;1}: C:\docs\file2.docx
{1;0}: C:\docs\file3.xlsx
```

## Using the file2md AI Tool

The `file2md` tool is automatically available in AI Chat conversations. Simply mention a file path and the AI can invoke the tool to read and process the file.

### Example Chat Interaction

```

### Using the file2md AI Tool

The `file2md` tool is automatically available in AI Chat conversations. Simply mention a file path and the AI can invoke the tool to read and process the file.

#### Example Chat Interaction

```

User: Read the file at C:\reports\Q4-2025.pdf and summarize the key findings

AI: [Calls file2md tool with filePath: "C:\reports\Q4-2025.pdf"]
    [Receives Markdown content]
    Based on the Q4 2025 report, the key findings are:

    1. Revenue increased by 15%...


```

#### Tool Parameters

- **filePath** (required): Absolute path to the file
- **removeHeadersFooters** (optional, default: true): Attempt to remove headers/footers (PDF, DOCX)
- **extractImages** (optional, default: false): Extract embedded images as base64 data (PDF, DOCX, PPTX, ODF)
- **preserveFormatting** (optional, default: true): Preserve inline text formatting. DOCX and ODF text documents preserve colors, highlights, bold, italic, underline, and strikethrough; XLSX, ODS, and PPTX preserve bold and italic
- **preserveComments** (optional, default: true): Preserve comments in DOCX files
- **preserveFootnotes** (optional, default: true): Expand footnote references and append note text (DOCX)
- **preserveEndnotes** (optional, default: true): Expand endnote references and append note text (DOCX)
- **describeImages** (optional, default: false): Use AI to describe each extracted image and embed the results in the markdown output
- **imageMode** (optional, default: `"embed"`): Controls how described images appear in the markdown (`"embed"`, `"describe"`, `"caption"`)
- **imageDescriptionPrompt** (optional): Custom prompt for AI image description
- **HTMLreadabilityMode** (optional, default: `"auto"`): HTML main-content extraction strategy (`"auto"`, `"smartreader"`, `"heuristic"`, `"off"`)
- **includeLinks** (optional, default: true): Keep hyperlinks in the Markdown output for HTML sources
- **includeImages** (optional, default: true): Keep inline image references in the Markdown output for HTML sources

> Table structure, hyperlinks, and Office Math are now always preserved and are no longer exposed as individual parameters.

When `extractImages: true` is passed to the `file2md` tool, embedded images are extracted as base64 data and returned alongside the Markdown content.

AI file components (`AIFile2Md`, `File2AI`) expose an **Image Mode** input that controls how images are handled after `file2md` extracts them:

| Mode | Behavior | Needs AI provider |
| --- | --- | --- |
| `embed` (AIFile2Md default) | Embed the image as a base64 data URI with a short AI caption | Yes |
| `describe` | Replace the image with a long AI text description | Yes |
| `caption` | Replace the image with a short AI-generated title | Yes |
| `skip` (File2AI default) | Do not extract images; only convert the document text | No |

`File2AI` maps `skip` to `extractImages=false`; all other modes extract images and then use `img2text` for AI descriptions.

### Image Extraction

When `extractImages: true` is passed to the `file2md` tool, embedded images are extracted as base64 data and returned alongside the Markdown content.

#### Supported Formats

- **PDF** — uses PdfPig `page.GetImages()` per page; attempts PNG conversion via `TryGetPng`, falls back to raw JPEG bytes (detected via `FF D8 FF` magic bytes)
- **DOCX** — iterates `MainDocumentPart.ImageParts`; preserves the original MIME type from the part content type
- **PPTX** — iterates `SlidePart.ImageParts` per slide; tags each image with its slide number
- **ODF** — finds `draw:image` elements in `content.xml`, reads the referenced file from the `Pictures/` package folder, and infers the MIME type from magic bytes or extension

#### Tool Result Structure

When images are extracted, the tool result includes two extra fields:

```json
{
  "content": "# My Document\n...",
  "originalFormat": "pdf",
  "imageCount": 2,
  "images": [
    {
      "id": "img-1",
      "mimeType": "image/png",
      "context": "Page 1",
      "pageOrSlide": 1,
      "base64Data": "iVBORw0KGgoAAAANSUhEUgAA..."
    },
    {
      "id": "img-2",
      "mimeType": "image/jpeg",
      "context": "Page 3",
      "pageOrSlide": 3,
      "base64Data": "/9j/4AAQSkZJRgABAQEA..."
    }
  ]
}

```

#### Notes

- Image extraction is **disabled by default** to avoid unnecessary processing overhead
- Failed individual images produce a warning rather than failing the whole conversion
- The base64 data can be passed directly to `AIBodyBuilder.AddImageInputFromBase64()` for vision AI requests (Phase 3)

### PDF Conversion Details

The PDF converter implements MinerU-inspired layout intelligence:

#### Column Detection

Automatically detects multi-column layouts by finding horizontal whitespace gaps spanning the full page width.

#### Reading Order

Sorts text blocks top-to-bottom within each column, then left-to-right across columns for natural reading flow.

#### Header/Footer Removal

Identifies and removes text blocks that:

- Appear in the top 8% or bottom 8% of pages
- Repeat across 3 or more pages

#### Heading Detection

Detects headings by font size relative to body text:

- Font size > median × 2.0 → `#` (H1)
- Font size > median × 1.7 → `##` (H2)
- Font size > median × 1.5 → `###` (H3)
- Font size > median × 1.4 → `####` (H4)
- Font size > median × 1.3 → `#####` (H5)

#### Hyperlink Extraction

Uses PdfPig's `page.GetHyperlinks()` to extract PDF link annotations. Any text that falls inside a hyperlink's bounding box is wrapped in Markdown link syntax (`[text](url)`). Respects the `preserveHyperlinks` option.

#### List Detection

Detects bullet and numbered list items by pattern-matching the start of each text block:

- **Bullets**: `•`, `‣`, `◦`, `○`, `▪`, `▫`, `►`, `→`, `-`, `–`, `—`
- **Numbered**: `1.`, `1)`, `a.`, `a)`, `i.`, `ii.`, etc.

Indentation levels are inferred from the visual left margin of each list item relative to others on the same page.

> Letter (`a)`) and Roman-numeral (`i.`) markers have no CommonMark equivalent and are normalized to a repeated `1.` marker per item. The final `MarkdownListRenumberer` cleanup pass (applied to every converter's output, see [Format Converters](#format-converters)) rewrites these into consecutive integers so the raw Markdown reads correctly.

#### Inline Image Positioning

When `extractImages` is enabled, extracted images are positioned inline based on their vertical location on the page rather than appended at the end. Images are interleaved with text blocks in top-to-bottom reading order.

#### Scanned Page Detection

Warns when a page contains fewer than 5 characters, indicating it may be a scanned image requiring OCR.

### HTML Conversion Details

The HTML converter uses magic-html-inspired readability scoring:

#### Content Scoring

Scores each container (`<div>`, `<section>`, `<article>`) by:

```

score = text_length / (link_count + 1) + paragraph_bonus + semantic_bonus + content_class_bonus

```

#### Boilerplate Removal

Automatically removes:

- Navigation elements (`<nav>`, `<header>`, `<footer>`, `<aside>`)
- Elements with classes/IDs matching: `ad`, `advertisement`, `cookie`, `sidebar`, `menu`, `banner`, `social`, `share`

#### Semantic Prioritization

Prioritizes content in:

- `<article>` tags (+100 score bonus)
- `<main>` tags (+80 score bonus)
- Elements with content-indicating classes: `content`, `main`, `article`, `post`, `entry`, `body` (+50 bonus)

### OpenXML Converter Details

DOCX, XLSX, and PPTX share the same OpenXML foundation, but each format applies Markdown output differently.

#### Bold/Italic Handling

- DOCX and PPTX emit run-level formatting as Markdown inline markers:
  - Bold run → `**text**`
  - Italic run → `*text*`
  - Bold + italic run → `***text***`
- If every run in a paragraph or table row is uniformly bold or italic, the markers are omitted to avoid noisy Markdown.

#### Hyperlinks

- When `PreserveHyperlinks` is enabled (default: `true`), external hyperlinks in DOCX and PPTX are converted to `[text](url)`.
- The visible text is taken from the link runs; the URL is resolved from the document's hyperlink relationships.

#### Footnotes & Endnotes

- DOCX only.
- When `PreserveFootnotes` / `PreserveEndnotes` are enabled (default: `true`), references are expanded to `[^fn1]` and `[^en1]` inline.
- The footnote/endnote text is appended at the end of the document as a Markdown footnote list.

#### Office Math

- DOCX and PPTX.
- When `PreserveMath` is enabled (default: `true`), OMML equations are converted to inline LaTeX `$...$`.
- Conversion is best-effort; very complex equation layouts may require manual review.

#### Lists

- DOCX ordered and unordered lists are supported.
- Indentation and numbering are preserved by translating each list level to the corresponding Markdown indentation/numbering.

#### XLSX Cell Formatting

- When converting sheets to Markdown tables, per-cell formatting is applied:
  - Bold cells → `**value**`
  - Italic cells → `*value*`
  - Bold + italic cells → `***value***`
- If an entire row is uniformly bold or italic, the markers are omitted for that row to avoid over-escaping the output.

### Dependencies

All converters use native .NET libraries with MIT or Apache 2.0 licenses:

- **UglyToad.PdfPig** (Apache 2.0): PDF text extraction with coordinates
- **DocumentFormat.OpenXml** (MIT): Office document parsing
- **HtmlAgilityPack** (MIT): HTML parsing
- **MimeKit** (MIT): Email parsing

No Python or external runtime dependencies required.

### Limitations

- **OCR**: Scanned PDFs require external OCR processing (not included)
- **Image Descriptions**: Images are extracted as raw base64 data; comprehensive descriptions require the `img_to_text` tool
- **Complex Tables**: Very complex table layouts may not convert perfectly
- **Formulas**: Mathematical formulas in PDFs are extracted as plain text
- **Macros**: Office document macros are not executed or extracted

### Best Practices

1. **Use Absolute Paths**: Always provide full absolute paths to files
2. **Check Warnings**: Review the warnings output for scanned pages or conversion issues
3. **Validate Output**: For critical documents, verify the Markdown output matches the source
4. **Batch Processing**: Use tree inputs to process multiple files efficiently
5. **Metadata**: Check the metadata dictionary for document properties (title, author, dates)

### Troubleshooting

#### "File not found" Error


- Verify the file path is absolute (e.g., `C:\docs\file.pdf`, not `docs\file.pdf`)
- Check that the file exists and is accessible

#### "Unsupported file format" Error


- Verify the file extension is in the supported formats list
- Check that the file is not corrupted

#### Empty or Incomplete Output


- For PDFs: Check warnings for scanned page notifications
- For Office docs: Ensure the file is not password-protected
- For HTML: The content may be heavily JavaScript-dependent

#### Poor Table Formatting


- Table structure is always preserved as Markdown tables; complex nested tables may require manual cleanup

### Related Components

- **Web Page Read**: For fetching and converting web pages (URLs)
- **AI Chat**: Uses `file2md` tool automatically when you mention file paths

---

## Developer Reference

### Core Types

#### IFileConverter (interface)

- Contract for file-to-markdown converters
- Each converter handles one or more file formats
- Properties:
  - `SupportedExtensions` — file extensions supported (e.g., ".pdf", ".docx")
- Methods:
  - `ConvertAsync(filePath, options)` — converts file to Markdown asynchronously

#### FileConverterRegistry (dispatcher)

- Central registry for all file converters
- Extension-based converter routing
- Methods:
  - `Register(converter)` — registers a single converter
  - `RegisterAll(converters)` — registers multiple converters
  - `IsSupported(extension)` — checks if extension is supported
  - `ConvertAsync(filePath, options)` — dispatches to appropriate converter, then applies `MarkdownListRenumberer.Renumber()` and `MarkdownStyleCleanup.Cleanup()` to the successful result before returning
  - `SupportedExtensions` — returns all registered extensions

#### MarkdownListRenumberer (post-processing)

- Static utility applied by `FileConverterRegistry.ConvertAsync` to every successful conversion result, regardless of format
- Rewrites consecutive ordered-list items (`1.`, `2.`, ...) to increasing integers, tracked per indentation level
- Needed because converters normalize non-CommonMark list markers (lettered `a)`, Roman `i.`) to a repeated `1.` per item; this pass restores visually correct sequential numbering
- A list at a given indentation ends on non-blank, non-list content, or is reset by a shallower-indented list item (which also clears deeper nested counters)

#### MarkdownStyleCleanup (post-processing)

- Static utility applied by `FileConverterRegistry.ConvertAsync` after `MarkdownListRenumberer`, to every successful conversion result
- Trims trailing whitespace per line (2+ trailing spaces are a CommonMark hard line break and are usually unintentional artifacts of styled-text joins)
- Ensures a blank line surrounds every ATX heading (`#` through `######`) so strict CommonMark parsers recognize them
- Collapses runs of 2+ blank lines into a single blank line
- Trims leading/trailing blank lines from the whole document

#### FileConversionOptions (configuration)

- Configures conversion behavior
- Properties:
  - `PreserveTableStructure` (bool, default: true) — convert tables to Markdown format
  - `RemoveHeadersFooters` (bool, default: true) — attempt to remove headers/footers
  - `ExtractImages` (bool, default: false) — extract embedded images as base64
  - `PreserveHyperlinks` (bool, default: true) — convert links to `[text](url)` (DOCX, PPTX)
  - `PreserveFootnotes` (bool, default: true) — expand footnotes and append note text (DOCX)
  - `PreserveEndnotes` (bool, default: true) — expand endnotes and append note text (DOCX)
  - `PreserveMath` (bool, default: true) — convert OMML equations to LaTeX `$...$` (DOCX, PPTX)
  - `PreserveFormatting` (bool, default: true) — preserve inline formatting: colors, highlights, bold, and italic (DOCX); bold and italic (XLSX, PPTX)

#### FileConversionResult (output)

- Result of a file-to-markdown conversion
- Properties:
  - `MarkdownContent` — extracted Markdown content (string)
  - `DetectedFormat` — detected original format (string, e.g., "pdf", "docx")
  - `Metadata` — extracted metadata (Dictionary, title, author, etc.)
  - `Warnings` — warnings during conversion (List, e.g., "Page 3 appears to be scanned")
  - `Images` — extracted images (List, when ExtractImages enabled)
  - `IsSuccess` — whether conversion succeeded (bool)
- Factory Methods:
  - `Success(markdownContent, detectedFormat)` — creates successful result
  - `Failure(detectedFormat, warningMessage)` — creates failed result

### Format Converters

All converters implement `IFileConverter` and are registered in `FileConverterRegistry`:

| Converter | File | Extensions | Library | Features |
| --- | --- | --- | --- | --- |
| `PdfConverter` | `PdfConverter.cs` | `.pdf` | UglyToad.PdfPig | Column detection, reading order, header/footer removal, heading detection, **hyperlink extraction**, **list detection**, **inline image positioning**, table recognition, scanned-page warnings |
| `DocxConverter` | `DocxConverter.cs` | `.docx` | DocumentFormat.OpenXml | Headings (H1-H6), bold/italic, tables, lists, images, metadata |
| `XlsxConverter` | `XlsxConverter.cs` | `.xlsx` | DocumentFormat.OpenXml | Multi-sheet support, header rows, Markdown tables, metadata |
| `PptxConverter` | `PptxConverter.cs` | `.pptx` | DocumentFormat.OpenXml | Slide titles, body text, bullet points, speaker notes, metadata |
| `HtmlConverter` | `HtmlConverter.cs` | `.html`, `.htm` | HtmlAgilityPack | Readability scoring, boilerplate removal, semantic content extraction |
| `EmlConverter` | `EmlConverter.cs` | `.eml` | MimeKit | From/To/Subject/Date, HTML or plain text body, attachment list |
| `EpubConverter` | `EpubConverter.cs` | `.epub` | Built-in | Chapter extraction in reading order, metadata |
| `RtfConverter` | `RtfConverter.cs` | `.rtf` | RichTextBox (Windows) / Regex (macOS) | Plain text extraction |
| `CsvConverter` | `CsvConverter.cs` | `.csv` | Built-in | Markdown table conversion |
| `JsonConverter` | `JsonConverter.cs` | `.json` | Built-in | Pretty-printed fenced code block |
| `XmlConverter` | `XmlConverter.cs` | `.xml` | Built-in | Pretty-printed fenced code block |
| `TxtConverter` | `TxtConverter.cs` | `.txt` | Built-in | Pass-through with line normalization |
| `UrlConverter` | `UrlConverter.cs` | (HTTP/HTTPS URLs) | HtmlAgilityPack | Fetches and converts web pages |

### Helper Classes

- **HeuristicExtractor** — base class for content extraction heuristics
- **HtmlReadabilityHelper** — HTML readability scoring and boilerplate removal
- **ReadabilityExtractor** — Readability-based HTML content extraction
- **SmartReaderExtractor** — Smart reader-based HTML content extraction
- **GHStructureConverter** — Converts Grasshopper structures to Markdown
- **StringConverter** — Converts strings to Markdown
- **IntConverter** — Converts integers to Markdown

### Adding Custom Converters

To add support for a new file format:

1. Implement `IFileConverter`:

```csharp
public class MyConverter : IFileConverter
{
    public IEnumerable<string> SupportedExtensions => new[] { ".myext" };
    
    public async Task<FileConversionResult> ConvertAsync(
        string filePath,
        FileConversionOptions options)
    {
        // Your conversion logic
        return FileConversionResult.Success(markdown, "myext");
    }
}

```

2. Register in `file2md.cs`:
```csharp
registry.Register(new MyConverter());

```

---

## Architecture & Design

### File Converters Subsystem

The file-to-Markdown system uses an extensible plugin architecture for converting various file formats to Markdown.

#### Converter Framework Diagram

```

IFileConverter (interface)
├── SupportedExtensions: string[]
└── ConvertAsync(filePath, options): FileConversionResult

FileConverterRegistry (dispatcher)
├── Register(converter)
├── IsSupported(extension)
└── ConvertAsync(filePath, options)

FileConversionOptions
├── PreserveTableStructure: bool
├── RemoveHeadersFooters: bool
├── ExtractImages: bool
├── PreserveHyperlinks: bool
├── PreserveFootnotes: bool
├── PreserveEndnotes: bool
├── PreserveMath: bool
└── PreserveFormatting: bool

FileConversionResult
├── MarkdownContent: string
├── DetectedFormat: string
├── Metadata: Dictionary<string, string>
├── Warnings: List<string>
├── Images: List<VersatileImage>
└── IsSuccess: bool

```

#### Design Principles

### Poor Table Formatting
- Set `preserveTableStructure: false` to get plain text instead of Markdown tables
- Complex nested tables may require manual cleanup

## Related Components

- **Web Page Read**: For fetching and converting web pages (URLs)
- **AI Chat**: Uses `file2md` tool automatically when you mention file paths
