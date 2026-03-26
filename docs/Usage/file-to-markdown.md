# File-to-Markdown Conversion

SmartHopper provides native file-to-Markdown conversion through the `FileToMdComponent` and `file_to_md` AI tool, enabling you to extract and process content from various document formats directly in Grasshopper.

## Supported Formats

| Format | Extension | Converter | Features |
|--------|-----------|-----------|----------|
| **PDF** | `.pdf` | PdfPig | Column detection, reading order, header/footer removal, heading detection, table recognition, scanned-page warnings |
| **Word** | `.docx` | DocumentFormat.OpenXml | Headings (H1-H6), bold/italic, tables, lists, images (as placeholders), metadata |
| **Excel** | `.xlsx` | DocumentFormat.OpenXml | Multi-sheet support, header rows, Markdown tables, metadata |
| **PowerPoint** | `.pptx` | DocumentFormat.OpenXml | Slide titles, body text, bullet points, speaker notes, metadata |
| **HTML** | `.html`, `.htm` | HtmlAgilityPack | Readability scoring, boilerplate removal, semantic content extraction |
| **Email** | `.eml` | MimeKit | From/To/Subject/Date, HTML or plain text body, attachment list |
| **EPUB** | `.epub` | Built-in | Chapter extraction in reading order, metadata |
| **RTF** | `.rtf` | RichTextBox (Windows) / Regex (macOS) | Plain text extraction |
| **CSV** | `.csv` | Built-in | Markdown table conversion |
| **JSON** | `.json` | Built-in | Pretty-printed fenced code block |
| **XML** | `.xml` | Built-in | Pretty-printed fenced code block |
| **Plain Text** | `.txt` | Built-in | Pass-through with line normalization |

## Using the FileToMd Component

### Basic Usage

1. Add the **FileToMd** component from the **SmartHopper → Knowledge** panel
2. Connect a file path (absolute path) to the **File Path** input
3. The component outputs:
   - **Markdown**: Converted Markdown content
   - **Format**: Detected original format (e.g., "pdf", "docx")

### Example

```
File Path: C:\Documents\report.pdf
↓
[FileToMd]
↓
Markdown: "# Annual Report\n\nThis document presents..."
Format: "pdf"
```

### Tree Support

The component supports tree inputs, allowing you to convert multiple files in parallel:

```
{0;0}: C:\docs\file1.pdf
{0;1}: C:\docs\file2.docx
{1;0}: C:\docs\file3.xlsx
```

## Using the file_to_md AI Tool

The `file_to_md` tool is automatically available in AI Chat conversations. Simply mention a file path and the AI can invoke the tool to read and process the file.

### Example Chat Interaction

```
User: Read the file at C:\reports\Q4-2025.pdf and summarize the key findings

AI: [Calls file_to_md tool with filePath: "C:\reports\Q4-2025.pdf"]
    [Receives Markdown content]
    Based on the Q4 2025 report, the key findings are:
    1. Revenue increased by 15%...
```

### Tool Parameters

- **filePath** (required): Absolute path to the file
- **preserveTableStructure** (optional, default: true): Convert tables to Markdown table format
- **removeHeadersFooters** (optional, default: true): Attempt to remove headers/footers (PDF, DOCX)
- **extractImages** (optional, default: false): Extract embedded images as base64 data (PDF, DOCX, PPTX)

## Image Extraction

When `extractImages: true` is passed to the `file_to_md` tool, embedded images are extracted as base64 data and returned alongside the Markdown content.

### Supported Formats

- **PDF** — uses PdfPig `page.GetImages()` per page; attempts PNG conversion via `TryGetPng`, falls back to raw JPEG bytes (detected via `FF D8 FF` magic bytes)
- **DOCX** — iterates `MainDocumentPart.ImageParts`; preserves the original MIME type from the part content type
- **PPTX** — iterates `SlidePart.ImageParts` per slide; tags each image with its slide number

### Tool Result Structure

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

### Notes

- Image extraction is **disabled by default** to avoid unnecessary processing overhead
- Failed individual images produce a warning rather than failing the whole conversion
- The base64 data can be passed directly to `AIBodyBuilder.AddImageInputFromBase64()` for vision AI requests (Phase 3)

## PDF Conversion Details

The PDF converter implements MinerU-inspired layout intelligence:

### Column Detection
Automatically detects multi-column layouts by finding horizontal whitespace gaps spanning the full page width.

### Reading Order
Sorts text blocks top-to-bottom within each column, then left-to-right across columns for natural reading flow.

### Header/Footer Removal
Identifies and removes text blocks that:
- Appear in the top 8% or bottom 8% of pages
- Repeat across 3 or more pages

### Heading Detection
Detects headings by font size relative to body text:
- Font size > median × 2.0 → `#` (H1)
- Font size > median × 1.7 → `##` (H2)
- Font size > median × 1.5 → `###` (H3)
- Font size > median × 1.4 → `####` (H4)
- Font size > median × 1.3 → `#####` (H5)

### Scanned Page Detection
Warns when a page contains fewer than 5 characters, indicating it may be a scanned image requiring OCR.

## HTML Conversion Details

The HTML converter uses magic-html-inspired readability scoring:

### Content Scoring
Scores each container (`<div>`, `<section>`, `<article>`) by:
```
score = text_length / (link_count + 1) + paragraph_bonus + semantic_bonus + content_class_bonus
```

### Boilerplate Removal
Automatically removes:
- Navigation elements (`<nav>`, `<header>`, `<footer>`, `<aside>`)
- Elements with classes/IDs matching: `ad`, `advertisement`, `cookie`, `sidebar`, `menu`, `banner`, `social`, `share`

### Semantic Prioritization
Prioritizes content in:
- `<article>` tags (+100 score bonus)
- `<main>` tags (+80 score bonus)
- Elements with content-indicating classes: `content`, `main`, `article`, `post`, `entry`, `body` (+50 bonus)

## Architecture

### Converter Framework

The file-to-Markdown system uses an extensible plugin architecture:

```
IFileConverter (interface)
├── SupportedExtensions: string[]
└── ConvertAsync(filePath, options): FileConversionResult

FileConverterRegistry (dispatcher)
├── Register(converter)
├── IsSupported(extension)
└── ConvertAsync(filePath, options)

ExtractedImage
├── Id: string             (e.g., "img-1")
├── Base64Data: string     (base64-encoded image bytes)
├── MimeType: string       (e.g., "image/png", "image/jpeg")
├── Context: string        (e.g., "Page 3", "Slide 2", "Document body")
└── PageOrSlide: int       (1-based; 0 if not applicable)

FileConversionResult
├── MarkdownContent: string
├── DetectedFormat: string
├── Metadata: Dictionary<string, string>
├── Warnings: List<string>
└── Images: List<ExtractedImage>
```

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

2. Register in `file_to_md.cs`:
```csharp
registry.Register(new MyConverter());
```

## Dependencies

All converters use native .NET libraries with MIT or Apache 2.0 licenses:

- **UglyToad.PdfPig** (Apache 2.0): PDF text extraction with coordinates
- **DocumentFormat.OpenXml** (MIT): Office document parsing
- **HtmlAgilityPack** (MIT): HTML parsing
- **MimeKit** (MIT): Email parsing

No Python or external runtime dependencies required.

## Limitations

- **OCR**: Scanned PDFs require external OCR processing (not included)
- **Image Descriptions**: Images are extracted as raw base64 data; comprehensive descriptions require the `img_to_text` tool
- **Complex Tables**: Very complex table layouts may not convert perfectly
- **Formulas**: Mathematical formulas in PDFs are extracted as plain text
- **Macros**: Office document macros are not executed or extracted

## Best Practices

1. **Use Absolute Paths**: Always provide full absolute paths to files
2. **Check Warnings**: Review the warnings output for scanned pages or conversion issues
3. **Validate Output**: For critical documents, verify the Markdown output matches the source
4. **Batch Processing**: Use tree inputs to process multiple files efficiently
5. **Metadata**: Check the metadata dictionary for document properties (title, author, dates)

## Troubleshooting

### "File not found" Error
- Verify the file path is absolute (e.g., `C:\docs\file.pdf`, not `docs\file.pdf`)
- Check that the file exists and is accessible

### "Unsupported file format" Error
- Verify the file extension is in the supported formats list
- Check that the file is not corrupted

### Empty or Incomplete Output
- For PDFs: Check warnings for scanned page notifications
- For Office docs: Ensure the file is not password-protected
- For HTML: The content may be heavily JavaScript-dependent

### Poor Table Formatting
- Set `preserveTableStructure: false` to get plain text instead of Markdown tables
- Complex nested tables may require manual cleanup

## Related Components

- **Web Page Read**: For fetching and converting web pages (URLs)
- **AI Chat**: Uses `file_to_md` tool automatically when you mention file paths
