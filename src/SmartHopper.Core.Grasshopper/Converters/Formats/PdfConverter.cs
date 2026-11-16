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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Core.Types;
using SmartHopper.DocumentProcessing;
using SmartHopper.DocumentProcessing.Rendering;
using SmartHopper.ProviderSdk.Documents;

namespace SmartHopper.Core.Grasshopper.Converters.Formats
{
    /// <summary>
    /// Thin adapter for PDF files (.pdf): delegates extraction, layout analysis and page
    /// rendering to <see cref="PdfDocumentReader"/> and projects the neutral
    /// <see cref="DocumentEvidence"/> onto the legacy <see cref="FileConversionResult"/>
    /// shape. All algorithms live in SmartHopper.DocumentProcessing.
    /// </summary>
    public sealed class PdfConverter : IFileConverter
    {
        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new[] { ".pdf" };

        /// <inheritdoc/>
        public async Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            var readOptions = new DocumentReadOptions
            {
                ExtractEmbeddedImages = options.ExtractImages,
                RenderPages = options.ExtractImages,
                PreserveHeaderFooter = !options.RemoveHeadersFooters,
                PreserveTables = options.PreserveTableStructure,
                PreserveLinks = options.PreserveHyperlinks,
                PreserveHeadings = options.DetectHeadings,
                PreserveFormatting = options.PreserveFormatting,
                SelectedPages = options.Pages,
                Region = options.Region,
                RendererTrustVerifier = VerifyRendererAssembly,
            };

            DocumentEvidence document;
            try
            {
                document = await DocumentReaders.GetReader(filePath).ReadAsync(filePath, readOptions, options.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return FileConversionResult.Failure("pdf", $"Failed to convert PDF: {ex.Message}");
            }

            return ProjectResult(document);
        }

        /// <summary>
        /// Projects neutral document evidence onto the legacy result: per-page Markdown
        /// joined with image placeholders in document order, plus the compatibility
        /// <see cref="FileConversionResult.Images"/> list as <see cref="VersatileImage"/>.
        /// </summary>
        private static FileConversionResult ProjectResult(DocumentEvidence document)
        {
            var markdown = new StringBuilder();
            var images = new List<VersatileImage>();

            foreach (var image in document.Images)
            {
                var number = images.Count + 1;
                var source = image.SourceReference;
                images.Add(VersatileImage.FromExtractedDocument(
                    base64Data: image.ImageData,
                    mimeType: image.MimeType,
                    id: source?.AssetId ?? $"img-{number}",
                    context: source?.PageNumber != null ? $"Page {source.PageNumber}" : "Document",
                    pageOrSlide: source?.PageNumber ?? 0,
                    sourceDocument: document.SourceName,
                    source: source));
            }

            foreach (var page in document.Pages)
            {
                if (!string.IsNullOrWhiteSpace(page.Markdown))
                {
                    markdown.AppendLine(page.Markdown);
                    markdown.AppendLine();
                }

                // Image placeholders reference the compatibility Images list by number.
                for (var i = 0; i < document.Images.Count; i++)
                {
                    var src = document.Images[i].SourceReference;
                    if (src?.PageNumber == page.PageNumber)
                    {
                        markdown.AppendLine($"[image {i + 1}]");
                        markdown.AppendLine();
                    }
                }
            }

            var result = FileConversionResult.Success(markdown.ToString().Trim(), "pdf");
            result.Document = document;
            foreach (var kv in document.Metadata)
            {
                result.Metadata[kv.Key] = kv.Value;
            }

            foreach (var warning in document.Warnings)
            {
                result.Warnings.Add(warning);
            }

            foreach (var page in document.Pages)
            {
                foreach (var warning in page.Warnings)
                {
                    result.Warnings.Add(warning);
                }
            }

            result.Images = images;
            return result;
        }

        /// <summary>
        /// Production trust check for the isolated renderer assembly via the strict
        /// <see cref="SmartHopper.Infrastructure.Security.RendererIntegrityGate"/>
        /// predicate: Official + strong-name + (Windows Authenticode | hash-manifest
        /// match). The DEBUG packaged-folder exception applies only to the exact
        /// packaged install path, never to caller-supplied directories.
        /// </summary>
        private static bool VerifyRendererAssembly(string assemblyPath)
        {
            var result = SmartHopper.Infrastructure.AIProviders.ProviderClassifier
                .ClassifyAsync(assemblyPath).ConfigureAwait(false).GetAwaiter().GetResult();

            var isPackaged = false;
            try
            {
                isPackaged = !string.IsNullOrWhiteSpace(IsolatedDocumentRenderer.PackagedDirectory)
                    && string.Equals(
                        Path.GetFullPath(assemblyPath),
                        Path.GetFullPath(Path.Combine(
                            IsolatedDocumentRenderer.PackagedDirectory,
                            "SmartHopper.DocumentRendering.dll")),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Path failures keep the packaged exception off.
            }

            return SmartHopper.Infrastructure.Security.RendererIntegrityGate
                .IsTrusted(result, OperatingSystem.IsWindows(), isPackaged);
        }
    }
}
