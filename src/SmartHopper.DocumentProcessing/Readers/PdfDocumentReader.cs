/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

/*
 * This code uses PdfPig for PDF text extraction and layout analysis:
 * https://github.com/UglyToad/PdfPig
 * Apache License 2.0
 *
 * Layout analysis improvements inspired by:
 * - MinerU (AGPL-3.0, https://github.com/opendatalab/MinerU):
 *   xy-cut reading order, header/footer removal, heading detection by font size
 * - Camelot (MIT, https://github.com/camelot-dev/camelot):
 *   Stream-mode table detection via whitespace column-alignment clustering
 * - Tabula (MIT, https://github.com/tabulapdf/tabula):
 *   Column centroid clustering for table cell extraction
 * - PyMuPDF / MuPDF (AGPL-3.0, https://pymupdf.readthedocs.io/):
 *   Lattice-mode table detection via PDF vector graphics (page.Paths) as primary signal
 * All algorithms are independently reimplemented in C#; no source code was copied.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.DocumentProcessing.Rendering;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace SmartHopper.DocumentProcessing.Readers
{
    /// <summary>
    /// Rhino-free PDF reader producing <see cref="DocumentEvidence"/>: extracted text and
    /// Markdown per page, embedded assets, full-page renders and detail crops through the
    /// isolated native renderer, with warnings and explicit coverage. One bounded byte
    /// snapshot feeds text extraction, document identity and rendering.
    /// </summary>
    public sealed class PdfDocumentReader : IDocumentReader
    {
        /// <summary>Canonical format id feeding evidence and renderer dispatch.</summary>
        private const string FormatIdValue = "pdf";

        /// <summary>Minimum number of data rows required to classify a block as a table.</summary>
        private const int MinTableRows = 3;

        /// <summary>Minimum page count for a repeated text to be removed as header/footer.</summary>
        private const int MinHeaderFooterRepeat = 3;

        /// <summary>A column separator gap must be at least this multiple of the block's median inter-word spacing.</summary>
        private const double ColumnGapMultiplier = 2.5;

        /// <summary>Absolute minimum width (PDF units) for a column separator gap.</summary>
        private const double MinAbsoluteColumnGap = 10.0;

        /// <summary>Cluster radius (PDF units) when grouping separator midpoints across lines.</summary>
        private const double ColumnSeparatorTolerance = 20.0;

        /// <summary>Maximum fraction of cells allowed to be empty before the block is rejected as a table.</summary>
        private const double MaxEmptyCellFraction = 0.5;

        /// <summary>Lower gap multiplier for table blocks forced by a TABLE caption.</summary>
        private const double ColumnGapMultiplierForced = 1.5;

        /// <summary>Maximum word count for a block tracked as a candidate running header/footer.</summary>
        private const int MaxShortRepeatBlockWords = 5;

        private static readonly Regex SectionArtifactPattern = new Regex(@"^#\s*\d+$", RegexOptions.Compiled);
        private static readonly Regex TableCaptionPattern = new Regex(@"^(?:TABLE|Table)\s+(?:\d+|[A-Z]|[a-z]|[IVXivx]+)", RegexOptions.Compiled);
        private static readonly Regex FigureCaptionPattern = new Regex(@"^(?:FIGURE|Figure|Fig\.)\s+(?:\d+|[A-Z]|[a-z]|[IVXivx]+)", RegexOptions.Compiled);
        private static readonly Regex NumericOnlyPattern = new Regex(@"^\d{1,4}$", RegexOptions.Compiled);
        private static readonly Regex BulletListPattern = new Regex(@"^(?:[•‣◦○▪▫►→▸▹›»]|\s*[\-–—])\s+", RegexOptions.Compiled);
        private static readonly Regex NumberedListPattern = new Regex(@"^(?:(\d+)[.)]\s+|[a-zA-Z][.)]\s+|[ivxIVX]+[.)]\s+)", RegexOptions.Compiled);

        /// <inheritdoc/>
        public string FormatId => FormatIdValue;

        /// <inheritdoc/>
        public System.Collections.Generic.IEnumerable<string> SupportedExtensions => new[] { ".pdf" };

        /// <summary>
        /// Reads a PDF file into a neutral <see cref="DocumentEvidence"/>.
        /// </summary>
        /// <param name="path">Path to the PDF file.</param>
        /// <param name="options">Extraction/render options and limits.</param>
        /// <param name="ct">Cancellation token (cooperative; checked around native work).</param>
        /// <returns>The document evidence.</returns>
        public async Task<DocumentEvidence> ReadAsync(string path, DocumentReadOptions options, CancellationToken ct)
        {
            options ??= new DocumentReadOptions();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"PDF file not found: '{path}'.", path);
            }

            var info = new FileInfo(path);
            if (info.Length == 0)
            {
                throw new InvalidDataException($"PDF file '{path}' is empty.");
            }

            if (info.Length > options.MaxFileBytes)
            {
                throw new InvalidDataException(
                    $"PDF file '{path}' is {info.Length / (1024 * 1024)} MiB, exceeding the {options.MaxFileBytes / (1024 * 1024)} MiB limit. Extract text from a smaller document or split the file.");
            }

            // One bounded snapshot feeds text extraction, identity and rendering — read at
            // most MaxFileBytes+1 bytes so a file that grows after the size check still fails.
            byte[] pdfBytes;
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true))
            {
                pdfBytes = new byte[Math.Min(stream.Length, options.MaxFileBytes + 1)];
                var offset = 0;
                while (offset < pdfBytes.Length)
                {
                    var read = await stream.ReadAsync(pdfBytes, offset, pdfBytes.Length - offset, ct).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    offset += read;
                }

                if (offset != pdfBytes.Length)
                {
                    Array.Resize(ref pdfBytes, offset);
                }
            }

            if (pdfBytes.Length > options.MaxFileBytes)
            {
                throw new InvalidDataException(
                    $"PDF file '{path}' exceeds the {options.MaxFileBytes / (1024 * 1024)} MiB limit while reading.");
            }
            ct.ThrowIfCancellationRequested();

            if (pdfBytes.Length < 5 || pdfBytes[0] != (byte)'%' || pdfBytes[1] != (byte)'P' || pdfBytes[2] != (byte)'D' || pdfBytes[3] != (byte)'F' || pdfBytes[4] != (byte)'-')
            {
                throw new InvalidDataException($"File '{path}' is not a PDF (missing %PDF- header).");
            }

            var documentId = ComputeDocumentId(pdfBytes);
            var sourceName = Path.GetFileName(path);

            return await Task.Run(() => ReadDocument(pdfBytes, sourceName, documentId, options, warnings, ct), ct).ConfigureAwait(false);
        }

        private static DocumentEvidence ReadDocument(byte[] pdfBytes, string sourceName, string documentId, DocumentReadOptions options, List<string> warnings, CancellationToken ct)
        {
            using var document = PdfDocument.Open(pdfBytes);
            var totalPages = document.NumberOfPages;

            // ---- Selection validation (empty is invalid, unlike null) ----
            var selected = ResolveSelection(options.SelectedPages, totalPages);
            var region = options.Region;
            if (region != null)
            {
                ValidateRegion(region, selected);
            }

            if (options.RenderPages && selected.Count > options.MaxRenderPages)
            {
                throw new InvalidDataException(
                    $"Rendering {selected.Count} pages exceeds the {options.MaxRenderPages}-page limit. Narrow the 'pages' selection or set extractImages=false for text-only extraction.");
            }

            // ---- Renderer ----
            IsolatedDocumentRenderer renderer = null;
            double[] nativeSizes = null;
            if (options.RenderPages)
            {
                renderer = IsolatedDocumentRenderer.Create(options.RendererDirectory, options.RendererTrustVerifier);
                if (!renderer.IsAvailable)
                {
                    warnings.Add($"PDF page rendering unavailable: {renderer.UnavailableReason} Text and embedded assets are still extracted.");
                    renderer = null;
                }
                else
                {
                    try
                    {
                        // Native visible page sizes already account rotation and crop boxes;
                        // never use unrotated MediaBox dimensions as visible-page dimensions.
                        nativeSizes = renderer.GetPageSizes(FormatIdValue, pdfBytes, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Native page size probe failed ({ex.Message}); falling back to rotation/crop-aware PdfPig dimensions.");
                    }
                }
            }

            // ---- Text extraction ----
            var metadata = new Dictionary<string, string>();
            var info = document.Information;
            if (!string.IsNullOrWhiteSpace(info.Title)) metadata["title"] = info.Title;
            if (!string.IsNullOrWhiteSpace(info.Author)) metadata["author"] = info.Author;
            if (!string.IsNullOrWhiteSpace(info.CreationDate)) metadata["created"] = info.CreationDate;

            var allPageData = CollectPageData(document, selected);
            var headersFooters = options.PreserveHeaderFooter
                ? new HashSet<string>()
                : DetectHeadersFooters(allPageData);

            var pages = new List<DocumentPageEvidence>();
            var images = new List<ToolResultImage>();
            var rendered = new List<int>();
            var failed = new List<int>();
            var textOnly = new List<int>();
            long encodedBytes = 0;
            var imageIndex = 0;

            foreach (var (page, blocks) in allPageData)
            {
                ct.ThrowIfCancellationRequested();
                var pageNumber = page.Number;
                var pageWarnings = new List<string>();

                var extractedText = (page.Text ?? string.Empty).Trim();
                if (blocks.Count == 0 || blocks.Sum(b => GetBlockText(b).Length) < 5)
                {
                    pageWarnings.Add($"Page {pageNumber} appears to be scanned or image-only; native text may be missing.");
                }

                // ---- Embedded assets (kept; SendToModel=false when a page render exists) ----
                var assetIds = new List<string>();
                if (options.ExtractEmbeddedImages)
                {
                    imageIndex = ExtractPageImages(page, pageNumber, imageIndex, images, assetIds, pageWarnings, documentId, sourceName, extractedText);
                    foreach (var img in images.Skip(assetIds.Count == 0 ? images.Count : images.Count - assetIds.Count))
                    {
                        encodedBytes += img.ImageData.Length;
                        if (encodedBytes > options.MaxEncodedBytes)
                        {
                            throw new InvalidDataException(
                                $"Encoded visual evidence exceeded the {options.MaxEncodedBytes / (1024 * 1024)} MiB budget. Narrow the 'pages' selection or disable image extraction.");
                        }
                    }
                }

                // ---- Markdown projection ----
                var markdown = BuildPageMarkdown(page, blocks, headersFooters, options, pageNumber);

                // ---- Page render ----
                string renderId = null;
                if (renderer != null)
                {
                    try
                    {
                        var png = renderer.Render(FormatIdValue, pdfBytes, pageNumber - 1, options.RenderDpi, options.MaxRenderSide, null, ct);
                        if (png != null && png.Length > 0)
                        {
                            encodedBytes += ((png.Length + 2) / 3) * 4;
                            if (encodedBytes > options.MaxEncodedBytes)
                            {
                                throw new InvalidDataException(
                                    $"Encoded visual evidence exceeded the {options.MaxEncodedBytes / (1024 * 1024)} MiB budget. Narrow the 'pages' selection or disable image extraction.");
                            }

                            renderId = $"page-{pageNumber}-render";
                            var (pw, ph) = PngDims(png);
                            images.Add(new ToolResultImage
                            {
                                ImageData = Convert.ToBase64String(png),
                                MimeType = "image/png",
                                SendToModel = true,
                                Width = pw > 0 ? pw : (int?)null,
                                Height = ph > 0 ? ph : (int?)null,
                                SourceReference = new SourceContentReference
                                {
                                    DocumentId = documentId,
                                    SourceName = sourceName,
                                    PageNumber = pageNumber,
                                    AssetId = renderId,
                                    Kind = SourceContentKind.Page,
                                    ContextText = extractedText,
                                },
                            });
                            rendered.Add(pageNumber);
                        }
                        else
                        {
                            failed.Add(pageNumber);
                            pageWarnings.Add($"Page {pageNumber}: renderer produced no output.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (InvalidDataException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed.Add(pageNumber);
                        pageWarnings.Add($"Page {pageNumber}: render failed: {ex.Message}");
                    }
                }

                // An embedded asset already visible inside a rendered page is display-only.
                if (renderId != null)
                {
                    foreach (var id in assetIds)
                    {
                        var idx = images.FindIndex(i => i.SourceReference?.AssetId == id);
                        if (idx >= 0)
                        {
                            images[idx] = images[idx] with { SendToModel = false };
                        }
                    }
                }

                var (visW, visH) = VisiblePageSize(page, pageNumber, nativeSizes);
                pages.Add(new DocumentPageEvidence
                {
                    PageNumber = pageNumber,
                    WidthPoints = visW,
                    HeightPoints = visH,
                    ExtractedText = extractedText,
                    Markdown = markdown,
                    RenderImageId = renderId,
                    AssetIds = assetIds,
                    Warnings = pageWarnings,
                });
            }

            // ---- Detail crop ----
            if (region != null && renderer != null)
            {
                var png = renderer.Render(FormatIdValue, pdfBytes, region.Page - 1, options.RenderDpi, options.MaxCropSide, new[] { region.X, region.Y, region.Width, region.Height }, ct);
                if (png != null && png.Length > 0)
                {
                    encodedBytes += ((png.Length + 2) / 3) * 4;
                    if (encodedBytes > options.MaxEncodedBytes)
                    {
                        throw new InvalidDataException(
                            $"Encoded visual evidence exceeded the {options.MaxEncodedBytes / (1024 * 1024)} MiB budget. Narrow the 'pages' or 'region' selection.");
                    }

                    var cropId = $"page-{region.Page}-crop";
                    var cropPageText = pages.FirstOrDefault(p => p.PageNumber == region.Page)?.ExtractedText;
                    var (cw, ch) = PngDims(png);
                    images.Add(new ToolResultImage
                    {
                        ImageData = Convert.ToBase64String(png),
                        MimeType = "image/png",
                        SendToModel = true,
                        Width = cw > 0 ? cw : (int?)null,
                        Height = ch > 0 ? ch : (int?)null,
                        SourceReference = new SourceContentReference
                        {
                            DocumentId = documentId,
                            SourceName = sourceName,
                            PageNumber = region.Page,
                            AssetId = cropId,
                            Kind = SourceContentKind.Crop,
                            Region = new SourceContentRegion { X = region.X, Y = region.Y, Width = region.Width, Height = region.Height },
                            ContextText = cropPageText,
                        },
                    });
                    var idx = pages.FindIndex(p => p.PageNumber == region.Page);
                    if (idx >= 0)
                    {
                        pages[idx] = pages[idx] with { AssetIds = pages[idx].AssetIds.Concat(new[] { cropId }).ToList() };
                    }
                }
                else
                {
                    warnings.Add($"Detail region on page {region.Page}: renderer produced no output.");
                }
            }
            else if (region != null)
            {
                warnings.Add("Detail region requested but the page renderer is unavailable; no crop was produced.");
            }

            var renderedSet = new HashSet<int>(rendered);
            var failedSet = new HashSet<int>(failed);
            textOnly = selected.Where(p => !renderedSet.Contains(p) && !failedSet.Contains(p)).ToList();

            // Visual completeness = every selected page rendered AND every requested crop
            // produced. Text-only projections and missing-renderer paths are never complete.
            var cropOk = region == null || images.Any(i => i.SourceReference?.Kind == SourceContentKind.Crop);
            var isComplete = options.RenderPages
                && failed.Count == 0
                && renderedSet.IsSupersetOf(new HashSet<int>(selected))
                && cropOk;

            return new DocumentEvidence
            {
                DocumentId = documentId,
                SourceName = sourceName,
                Format = "pdf",
                Metadata = metadata,
                Pages = pages,
                Images = images,
                Warnings = warnings,
                Coverage = new DocumentCoverage
                {
                    TotalPages = totalPages,
                    SelectedPages = selected,
                    RenderedPages = rendered,
                    FailedPages = failed,
                    TextOnlyPages = textOnly,
                    IsComplete = isComplete && failed.Count == 0,
                },
            };
        }

        private static List<int> ResolveSelection(IReadOnlyList<int> selectedPages, int totalPages)
        {
            if (selectedPages == null)
            {
                return Enumerable.Range(1, totalPages).ToList();
            }

            if (selectedPages.Count == 0)
            {
                throw new InvalidDataException("The 'pages' selection is empty; supply unique 1-based page numbers or omit 'pages' entirely.");
            }

            var seen = new HashSet<int>();
            foreach (var page in selectedPages)
            {
                if (page < 1 || page > totalPages)
                {
                    throw new InvalidDataException($"Page {page} is out of range; the document has {totalPages} page(s). Supply 1-based page numbers between 1 and {totalPages}.");
                }

                if (!seen.Add(page))
                {
                    throw new InvalidDataException($"Duplicate page {page} in the 'pages' selection; each page may appear at most once.");
                }
            }

            return selectedPages.Distinct().OrderBy(p => p).ToList();
        }

        private static void ValidateRegion(DocumentRegionSelection region, IReadOnlyList<int> selected)
        {
            if (!selected.Contains(region.Page))
            {
                throw new InvalidDataException($"Detail region targets page {region.Page}, which is not a selected page ({string.Join(",", selected)}).");
            }

            if (!double.IsFinite(region.X) || !double.IsFinite(region.Y) || !double.IsFinite(region.Width) || !double.IsFinite(region.Height)
                || region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0
                || (region.X + region.Width) > 1.000001 || (region.Y + region.Height) > 1.000001)
            {
                throw new InvalidDataException(
                    $"Detail region [x={region.X}, y={region.Y}, w={region.Width}, h={region.Height}] is invalid; coordinates must be finite, normalized to [0,1], and lie entirely inside the page.");
            }
        }

        /// <summary>
        /// Visible page dimensions in points: native renderer sizes first (rotation and crop
        /// boxes already applied), else PdfPig CropBox with 90/270 rotation swap — never the
        /// unrotated MediaBox.
        /// </summary>
        private static (double W, double H) VisiblePageSize(Page page, int pageNumber, double[] nativeSizes)
        {
            var idx = (pageNumber - 1) * 2;
            if (nativeSizes != null && nativeSizes.Length > idx + 1
                && nativeSizes[idx] > 0 && double.IsFinite(nativeSizes[idx])
                && nativeSizes[idx + 1] > 0 && double.IsFinite(nativeSizes[idx + 1]))
            {
                return (nativeSizes[idx], nativeSizes[idx + 1]);
            }

            try
            {
                var cropW = page.CropBox.Bounds.Width;
                var cropH = page.CropBox.Bounds.Height;
                if (cropW <= 0 || cropH <= 0)
                {
                    cropW = page.Width;
                    cropH = page.Height;
                }

                var rotation = ((int)page.Rotation.Value) % 360;
                if (rotation == 90 || rotation == 270)
                {
                    return (cropH, cropW);
                }

                return (cropW, cropH);
            }
            catch
            {
                return (page.Width, page.Height);
            }
        }

        private static string ComputeDocumentId(byte[] pdfBytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(pdfBytes);
            return "sha256-" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>PNG IHDR width/height; returns (0,0) for malformed buffers.</summary>
        private static (int W, int H) PngDims(byte[] png)
        {
            if (png == null || png.Length < 24)
            {
                return (0, 0);
            }

            return ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19],
                    (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        }

        private static int ExtractPageImages(
            Page page,
            int pageNumber,
            int imageIndex,
            List<ToolResultImage> images,
            List<string> assetIds,
            List<string> pageWarnings,
            string documentId,
            string sourceName,
            string contextText)
        {
            try
            {
                foreach (var image in page.GetImages())
                {
                    imageIndex++;
                    string base64Data = null;
                    string mimeType = "image/png";

                    if (image.TryGetPng(out var pngBytes))
                    {
                        base64Data = Convert.ToBase64String(pngBytes);
                        mimeType = "image/png";
                    }
                    else
                    {
                        var rawBytes = image.RawBytes.ToArray();
                        if (rawBytes.Length > 0)
                        {
                            if (rawBytes.Length >= 3 && rawBytes[0] == 0xFF && rawBytes[1] == 0xD8 && rawBytes[2] == 0xFF)
                            {
                                base64Data = Convert.ToBase64String(rawBytes);
                                mimeType = "image/jpeg";
                            }
                            else
                            {
                                // Unknown encoding: keep the asset for the record but never
                                // send it as model input.
                                pageWarnings.Add($"Page {pageNumber}, image {imageIndex + 1}: unrecognized image encoding; retained as display-only asset.");
                                mimeType = null;
                            }
                        }
                    }

                    var assetId = $"img-{imageIndex}";
                    if (!string.IsNullOrEmpty(base64Data) && mimeType != null)
                    {
                        assetIds.Add(assetId);
                        images.Add(new ToolResultImage
                        {
                            ImageData = base64Data,
                            MimeType = mimeType,
                            SendToModel = true,
                            SourceReference = new SourceContentReference
                            {
                                DocumentId = documentId,
                                SourceName = sourceName,
                                PageNumber = pageNumber,
                                AssetId = assetId,
                                Kind = SourceContentKind.Image,
                                ContextText = contextText,
                            },
                        });
                    }
                    else if (mimeType == null)
                    {
                        assetIds.Add(assetId);
                        images.Add(new ToolResultImage
                        {
                            ImageData = base64Data,
                            MimeType = "application/octet-stream",
                            SendToModel = false,
                            SourceReference = new SourceContentReference
                            {
                                DocumentId = documentId,
                                SourceName = sourceName,
                                PageNumber = pageNumber,
                                AssetId = assetId,
                                Kind = SourceContentKind.Image,
                                ContextText = contextText,
                            },
                        });
                    }
                    else
                    {
                        pageWarnings.Add($"Page {pageNumber}, image {imageIndex}: could not extract image data.");
                    }
                }
            }
            catch (Exception ex)
            {
                pageWarnings.Add($"Page {pageNumber}: image extraction failed: {ex.Message}");
            }

            return imageIndex;
        }

        private static string BuildPageMarkdown(
            Page page,
            List<TextBlock> blocks,
            HashSet<string> headersFooters,
            DocumentReadOptions options,
            int pageNumber)
        {
            var markdown = new StringBuilder();
            if (blocks.Count == 0 || blocks.Sum(b => GetBlockText(b).Length) < 5)
            {
                return string.Empty;
            }

            var ordered = new UnsupervisedReadingOrderDetector(
                spatialReasoningRule: UnsupervisedReadingOrderDetector.SpatialReasoningRules.RowWise,
                useRenderingOrder: false).Get(blocks);
            var content = ordered
                .Where(b => !headersFooters.Contains(GetBlockText(b)))
                .ToList();

            var hyperlinks = options.PreserveLinks
                ? page.GetHyperlinks()
                : Array.Empty<Hyperlink>();

            double medianFontSize = GetMedianFontSize(content);
            double headingThreshold = medianFontSize * 1.3;

            bool nextBlockForceTable = false;
            var listLeftMarginLevels = new List<double>();

            for (int bi = 0; bi < content.Count; bi++)
            {
                var block = content[bi];
                string text = GetBlockText(block);
                if (string.IsNullOrWhiteSpace(text))
                {
                    nextBlockForceTable = false;
                    continue;
                }

                if (SectionArtifactPattern.IsMatch(text)) continue;
                if (text.IndexOf("continued on next page", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                if (NumericOnlyPattern.IsMatch(text)
                    && GetBlockFontSize(block) > headingThreshold
                    && (block.BoundingBox.Bottom > page.Height * 0.88
                        || block.BoundingBox.Top < page.Height * 0.12))
                {
                    continue;
                }

                bool isTableCaption = TableCaptionPattern.IsMatch(text);
                int wordCount = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                bool isFigureCaption = !isTableCaption
                    && FigureCaptionPattern.IsMatch(text)
                    && wordCount <= 25;

                bool forceTable = nextBlockForceTable;
                nextBlockForceTable = isTableCaption;

                if (options.PreserveTables && !isTableCaption && !isFigureCaption)
                {
                    bool isLattice = HasLatticeLines(page, block);
                    bool forcedRender = forceTable || isLattice;

                    if (forcedRender || IsTableBlock(block))
                    {
                        string table = RenderMarkdownTable(block, forcedRender);
                        if (!string.IsNullOrWhiteSpace(table))
                        {
                            markdown.AppendLine(table);
                            listLeftMarginLevels.Clear();
                            continue;
                        }

                        if (forceTable)
                        {
                            markdown.AppendLine("```text");
                            markdown.AppendLine(GetBlockText(block));
                            markdown.AppendLine("```");
                            listLeftMarginLevels.Clear();
                            continue;
                        }
                    }
                }

                double fontSize = GetBlockFontSize(block);

                if (isTableCaption || isFigureCaption)
                {
                    listLeftMarginLevels.Clear();
                    string styledCaption = GetBlockTextWithStylingAndLinks(block, hyperlinks, options.PreserveFormatting).Replace("|", "\\|");
                    if (styledCaption.Length >= 4 && styledCaption.StartsWith("**", StringComparison.Ordinal) && styledCaption.EndsWith("**", StringComparison.Ordinal))
                    {
                        styledCaption = styledCaption.Substring(2, styledCaption.Length - 4);
                    }

                    markdown.Append("**").Append(styledCaption).AppendLine("**");
                }
                else if (options.PreserveHeadings && fontSize > headingThreshold)
                {
                    listLeftMarginLevels.Clear();
                    int level = GetHeadingLevel(fontSize, medianFontSize);
                    markdown.Append(new string('#', level)).Append(' ').AppendLine(text);
                }
                else if (TryDetectListItem(text, out string listMarker, out string listContent))
                {
                    int indentLevel = GetListIndentLevel(block.BoundingBox.Left, listLeftMarginLevels);
                    markdown.Append(new string(' ', indentLevel * 2))
                        .Append(listMarker).Append(' ').AppendLine(listContent);
                }
                else
                {
                    listLeftMarginLevels.Clear();
                    string styledText = GetBlockTextWithStylingAndLinks(block, hyperlinks, options.PreserveFormatting);
                    markdown.AppendLine(styledText);
                }

                markdown.AppendLine();
            }

            return markdown.ToString().Trim();
        }

        // ------------------------------------------------------------------
        // Ported layout/styling/table helpers — verbatim algorithms from the
        // previous Core.Grasshopper PdfConverter (no logic changes).
        // ------------------------------------------------------------------

        private static List<(Page Page, List<TextBlock> Blocks)> CollectPageData(PdfDocument document, IReadOnlyList<int> selectedPages)
        {
            var pageData = new List<(Page, List<TextBlock>)>();
            var wanted = new HashSet<int>(selectedPages);

            foreach (var page in document.GetPages())
            {
                if (!wanted.Contains(page.Number))
                {
                    continue;
                }

                var letters = page.Letters;
                if (letters.Count == 0)
                {
                    pageData.Add((page, new List<TextBlock>()));
                    continue;
                }

                var words = NearestNeighbourWordExtractor.Instance.GetWords(letters).ToList();
                if (words.Count == 0)
                {
                    pageData.Add((page, new List<TextBlock>()));
                    continue;
                }

                var segmenter = new RecursiveXYCut(new RecursiveXYCut.RecursiveXYCutOptions
                {
                    MinimumWidth = page.Width / 3.0,
                });

                var blocks = segmenter.GetBlocks(words).ToList();
                pageData.Add((page, blocks));
            }

            return pageData;
        }

        private static HashSet<string> DetectHeadersFooters(
            List<(Page Page, List<TextBlock> Blocks)> allPageData)
        {
            if (allPageData.Count < MinHeaderFooterRepeat)
            {
                return new HashSet<string>();
            }

            var headerCounts = new Dictionary<string, int>();
            var footerCounts = new Dictionary<string, int>();
            var shortTextCounts = new Dictionary<string, int>();

            foreach (var (page, blocks) in allPageData)
            {
                double headerLine = page.Height * 0.88;
                double footerLine = page.Height * 0.12;

                foreach (var block in blocks)
                {
                    string text = GetBlockText(block);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var bb = block.BoundingBox;
                    if (bb.Bottom > headerLine)
                    {
                        headerCounts.TryGetValue(text, out int c);
                        headerCounts[text] = c + 1;
                    }
                    else if (bb.Top < footerLine)
                    {
                        footerCounts.TryGetValue(text, out int c);
                        footerCounts[text] = c + 1;
                    }

                    int words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (words <= MaxShortRepeatBlockWords)
                    {
                        shortTextCounts.TryGetValue(text, out int sc);
                        shortTextCounts[text] = sc + 1;
                    }
                }
            }

            var result = new HashSet<string>();
            foreach (var kvp in headerCounts.Where(k => k.Value >= MinHeaderFooterRepeat))
            {
                result.Add(kvp.Key);
            }

            foreach (var kvp in footerCounts.Where(k => k.Value >= MinHeaderFooterRepeat))
            {
                result.Add(kvp.Key);
            }

            foreach (var kvp in shortTextCounts.Where(k => k.Value >= MinHeaderFooterRepeat))
            {
                result.Add(kvp.Key);
            }

            return result;
        }

        private static string GetBlockText(TextBlock block)
        {
            return string.Join(" ", block.TextLines
                .Select(l => string.Join(" ", l.Words.Select(w => w.Text)))).Trim();
        }

        private static Hyperlink FindLinkForWord(Word word, IReadOnlyList<Hyperlink> hyperlinks)
        {
            var wb = word.BoundingBox;
            foreach (var link in hyperlinks)
            {
                var lb = link.Bounds;
                if (wb.Left < lb.Right && wb.Right > lb.Left
                    && wb.Bottom < lb.Top && wb.Top > lb.Bottom)
                {
                    return link;
                }
            }

            return null;
        }

        private static string GetBlockTextWithStylingAndLinks(TextBlock block, IReadOnlyList<Hyperlink> hyperlinks, bool preserveFormatting)
        {
            var lines = block.TextLines.ToList();
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (int li = 0; li < lines.Count; li++)
            {
                var line = lines[li];
                if (li > 0)
                {
                    sb.Append(' ');
                }

                var words = line.Words.ToList();
                int i = 0;
                while (i < words.Count)
                {
                    var word = words[i];
                    var link = FindLinkForWord(word, hyperlinks);

                    if (link != null)
                    {
                        int j = i + 1;
                        while (j < words.Count && FindLinkForWord(words[j], hyperlinks) == link)
                        {
                            j++;
                        }

                        if (sb.Length > 0 && sb[^1] != ' ')
                        {
                            sb.Append(' ');
                        }

                        string linkText = string.Join(" ", words.GetRange(i, j - i).Select(w => EscapeMarkdownChars(w.Text)));
                        string linkUrl = string.IsNullOrWhiteSpace(link.Uri) ? string.Empty : EscapeMarkdownUrl(link.Uri);
                        if (string.IsNullOrEmpty(linkUrl))
                        {
                            sb.Append(linkText);
                        }
                        else
                        {
                            sb.Append($"[{linkText}]({linkUrl})");
                        }

                        i = j;
                    }
                    else
                    {
                        var (isBold, isItalic) = preserveFormatting ? GetWordStyle(word) : (false, false);

                        int j = i + 1;
                        while (j < words.Count)
                        {
                            var (nb, ni) = preserveFormatting ? GetWordStyle(words[j]) : (false, false);
                            if (nb != isBold || ni != isItalic || FindLinkForWord(words[j], hyperlinks) != null)
                            {
                                break;
                            }

                            j++;
                        }

                        string runText = string.Join(" ", words.GetRange(i, j - i).Select(w => EscapeMarkdownChars(w.Text)));
                        if (sb.Length > 0 && sb[^1] != ' ')
                        {
                            sb.Append(' ');
                        }

                        if (isBold && isItalic)
                        {
                            sb.Append("***").Append(runText).Append("***");
                        }
                        else if (isBold)
                        {
                            sb.Append("**").Append(runText).Append("**");
                        }
                        else if (isItalic)
                        {
                            sb.Append('*').Append(runText).Append('*');
                        }
                        else
                        {
                            sb.Append(runText);
                        }

                        i = j;
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private static (bool IsBold, bool IsItalic) GetWordStyle(Word word)
        {
            if (!word.Letters.Any()) return (false, false);

            string fontName = word.Letters.First().FontName ?? string.Empty;

            bool isBold = fontName.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0
                       || fontName.IndexOf("-B", StringComparison.Ordinal) >= 0
                       || fontName.IndexOf(",Bold", StringComparison.OrdinalIgnoreCase) >= 0;

            bool isItalic = fontName.IndexOf("Italic", StringComparison.OrdinalIgnoreCase) >= 0
                         || fontName.IndexOf("Oblique", StringComparison.OrdinalIgnoreCase) >= 0
                         || fontName.IndexOf("-I", StringComparison.Ordinal) >= 0
                         || fontName.IndexOf(",Italic", StringComparison.OrdinalIgnoreCase) >= 0;

            return (isBold, isItalic);
        }

        private static string EscapeMarkdownChars(string text)
        {
            return text.Replace("*", "\\*").Replace("_", "\\_");
        }

        private static string EscapeMarkdownUrl(string url)
        {
            return url.Replace("(", "%28").Replace(")", "%29").Replace(" ", "%20");
        }

        private static bool TryDetectListItem(string text, out string marker, out string content)
        {
            var bulletMatch = BulletListPattern.Match(text);
            if (bulletMatch.Success)
            {
                marker = "-";
                content = text.Substring(bulletMatch.Length).TrimStart();
                return true;
            }

            var numMatch = NumberedListPattern.Match(text);
            if (numMatch.Success)
            {
                string number = numMatch.Groups[1].Success ? numMatch.Groups[1].Value : null;
                marker = string.IsNullOrEmpty(number) ? "1." : $"{number}.";
                content = text.Substring(numMatch.Length).TrimStart();
                return true;
            }

            marker = string.Empty;
            content = string.Empty;
            return false;
        }

        private static int GetListIndentLevel(double leftMargin, List<double> leftMarginLevels, double tolerance = 18.0)
        {
            for (int i = 0; i < leftMarginLevels.Count; i++)
            {
                if (Math.Abs(leftMargin - leftMarginLevels[i]) < tolerance)
                {
                    return i;
                }
            }

            leftMarginLevels.Add(leftMargin);
            leftMarginLevels.Sort();
            return leftMarginLevels.Count - 1;
        }

        private static double GetBlockFontSize(TextBlock block)
        {
            var letters = block.TextLines.SelectMany(l => l.Words).SelectMany(w => w.Letters);
            return letters.Any() ? letters.Average(l => l.FontSize) : 12.0;
        }

        private static double GetMedianFontSize(IEnumerable<TextBlock> blocks)
        {
            var sizes = blocks.Select(GetBlockFontSize).OrderBy(s => s).ToList();
            if (sizes.Count == 0) return 12.0;
            int mid = sizes.Count / 2;
            return sizes.Count % 2 == 0 ? (sizes[mid - 1] + sizes[mid]) / 2.0 : sizes[mid];
        }

        private static int GetHeadingLevel(double fontSize, double medianFontSize)
        {
            double ratio = fontSize / medianFontSize;
            if (ratio >= 2.0) return 1;
            if (ratio >= 1.7) return 2;
            if (ratio >= 1.5) return 3;
            if (ratio >= 1.4) return 4;
            if (ratio >= 1.3) return 5;
            return 6;
        }

        private static bool IsTableBlock(TextBlock block)
        {
            var lines = block.TextLines.ToList();
            if (lines.Count < MinTableRows) return false;

            double avgWords = lines.Average(l => l.Words.Count);
            if (avgWords < 1.5) return false;

            return FindConsistentColumnSeparators(lines).Count >= 1;
        }

        private static bool HasLatticeLines(Page page, TextBlock block)
        {
            if (page == null || block == null) return false;
            try
            {
                var bbox = block.BoundingBox;
                const double tolerance = 5.0;
                const double maxLineThickness = 3.0;
                const double minLineLength = 15.0;

                int hLineCount = 0;
                int vLineCount = 0;

                foreach (var path in page.Paths)
                {
                    if (!path.IsStroked) continue;

                    var rect = path.GetBoundingRectangle();
                    if (rect == null) continue;

                    var r = rect.Value;

                    if (r.Right < bbox.Left - tolerance || r.Left > bbox.Right + tolerance) continue;
                    if (r.Top < bbox.Bottom - tolerance || r.Bottom > bbox.Top + tolerance) continue;

                    double w = r.Width;
                    double h = r.Height;

                    if (h <= maxLineThickness && w >= minLineLength) hLineCount++;
                    else if (w <= maxLineThickness && h >= minLineLength) vLineCount++;
                }

                return hLineCount >= 3 && vLineCount >= 1;
            }
            catch
            {
                return false;
            }
        }

        private static List<double> FindConsistentColumnSeparators(List<TextLine> lines, double gapMultiplier = ColumnGapMultiplier)
        {
            var allGapSizes = new List<double>();
            var lineGapData = new List<List<(double Mid, double Size)>>();

            foreach (var line in lines)
            {
                var words = line.Words.OrderBy(w => w.BoundingBox.Left).ToList();
                var lineGaps = new List<(double Mid, double Size)>();

                for (int i = 1; i < words.Count; i++)
                {
                    double gap = words[i].BoundingBox.Left - words[i - 1].BoundingBox.Right;
                    if (gap > 0)
                    {
                        double mid = (words[i - 1].BoundingBox.Right + words[i].BoundingBox.Left) / 2.0;
                        lineGaps.Add((mid, gap));
                        allGapSizes.Add(gap);
                    }
                }

                lineGapData.Add(lineGaps);
            }

            if (allGapSizes.Count == 0) return new List<double>();

            allGapSizes.Sort();
            double medianGap = allGapSizes[allGapSizes.Count / 2];
            double largeGapThreshold = Math.Max(medianGap * gapMultiplier, MinAbsoluteColumnGap);

            var largeMids = lineGapData
                .SelectMany(lg => lg.Where(g => g.Size >= largeGapThreshold).Select(g => g.Mid))
                .OrderBy(x => x)
                .ToList();

            if (largeMids.Count == 0) return new List<double>();

            var clusters = new List<(double Centroid, int Count)> { (largeMids[0], 1) };
            foreach (var x in largeMids.Skip(1))
            {
                int nearest = -1;
                double nearestDist = ColumnSeparatorTolerance;
                for (int i = 0; i < clusters.Count; i++)
                {
                    double d = Math.Abs(x - clusters[i].Centroid);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = i;
                    }
                }

                if (nearest >= 0)
                {
                    var (c, n) = clusters[nearest];
                    clusters[nearest] = ((c * n + x) / (n + 1), n + 1);
                }
                else
                {
                    clusters.Add((x, 1));
                }
            }

            int minAppearances = Math.Max(2, lines.Count / 2);
            return clusters
                .Where(cl => lineGapData.Count(lg =>
                    lg.Any(g => g.Size >= largeGapThreshold &&
                                Math.Abs(g.Mid - cl.Centroid) <= ColumnSeparatorTolerance))
                    >= minAppearances)
                .OrderBy(cl => cl.Centroid)
                .Select(cl => cl.Centroid)
                .ToList();
        }

        private static string RenderMarkdownTable(TextBlock block, bool forcedByCaption = false)
        {
            var lines = block.TextLines.ToList();
            double gapMult = forcedByCaption ? ColumnGapMultiplierForced : ColumnGapMultiplier;
            var separators = FindConsistentColumnSeparators(lines, gapMult);
            if (separators.Count < 1) return string.Empty;

            int columnCount = separators.Count + 1;
            var rows = new List<string[]>();

            foreach (var line in lines)
            {
                var cells = new string[columnCount];
                for (int i = 0; i < cells.Length; i++) cells[i] = string.Empty;

                foreach (var word in line.Words)
                {
                    int col = GetCellIndex(word.BoundingBox.Left, separators);
                    string w = word.Text.Replace("|", "\\|").Replace("*", "\\*").Replace("_", "\\_");
                    cells[col] = cells[col].Length == 0 ? w : cells[col] + " " + w;
                }

                rows.Add(cells);
            }

            if (rows.Count == 0) return string.Empty;

            int totalCells = rows.Count * columnCount;
            int emptyCells = rows.Sum(r => r.Count(string.IsNullOrWhiteSpace));
            if (emptyCells > totalCells * MaxEmptyCellFraction) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("| ").Append(string.Join(" | ", rows[0])).AppendLine(" |");
            sb.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", columnCount))).AppendLine(" |");
            foreach (var row in rows.Skip(1))
            {
                sb.Append("| ").Append(string.Join(" | ", row)).AppendLine(" |");
            }

            return sb.ToString();
        }

        private static int GetCellIndex(double wordLeft, List<double> separators)
        {
            for (int i = 0; i < separators.Count; i++)
            {
                if (wordLeft < separators[i]) return i;
            }

            return separators.Count;
        }
    }
}
