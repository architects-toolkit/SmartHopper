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
using System.Linq;
using System.Text;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.ProviderSdk.Documents
{
    /// <summary>
    /// Shared builder for the ONE ordered multimodal source payload produced from a
    /// <see cref="DocumentEvidence"/>: identity/coverage header, then each selected
    /// page's text followed by its model-visible images in page order — all under a
    /// single <c>TurnId</c> and with exact source provenance. Performs no provider call
    /// and no network while packaging; scanned pages with empty text still yield
    /// image-bearing payloads.
    /// </summary>
    public static class DocumentEvidenceInteractionBuilder
    {
        /// <summary>
        /// Builds ordered interactions from a document evidence: header, then per-page
        /// text + model-visible images in page order. For page-less content (web
        /// articles), the supplied <paramref name="markdown"/> is appended as article
        /// text followed by the model-visible images.
        /// </summary>
        /// <param name="document">The neutral document evidence.</param>
        /// <param name="markdown">Source Markdown/text used for page-less payloads or as
        /// fallback page text.</param>
        /// <returns>The ordered interaction list (empty when the document yields
        /// nothing).</returns>
        public static IReadOnlyList<IAIInteraction> Build(DocumentEvidence document, string markdown)
        {
            var interactions = new List<IAIInteraction>();
            if (document == null)
            {
                if (!string.IsNullOrWhiteSpace(markdown))
                {
                    interactions.Add(new AIInteractionText { Agent = AIAgent.User, Content = markdown });
                }

                return interactions;
            }

            var turnId = Guid.NewGuid().ToString("N");
            var displayName = DisplayName(document.SourceName);
            var coverage = document.Coverage;
            var modelImages = (document.Images ?? new List<ToolResultImage>())
                .Where(i => i != null && i.SendToModel && !string.IsNullOrEmpty(i.ImageData))
                .ToList();

            // Identity/coverage header.
            var header = new StringBuilder();
            header.Append($"Document '{displayName}' ({document.Format})");
            if (coverage != null)
            {
                header.Append($" — pages {coverage.SelectedPages.Count}/{coverage.TotalPages}, rendered [{string.Join(", ", coverage.RenderedPages)}]"
                    + (coverage.FailedPages.Count > 0 ? $", failed [{string.Join(", ", coverage.FailedPages)}]" : string.Empty)
                    + (coverage.IsComplete ? string.Empty : " (visual coverage incomplete)"));
            }

            foreach (var warning in document.Warnings ?? Enumerable.Empty<string>())
            {
                header.AppendLine().Append($"Warning: {warning}");
            }

            interactions.Add(new AIInteractionText
            {
                Agent = AIAgent.User,
                TurnId = turnId,
                Content = header.ToString(),
                SourceReference = new SourceContentReference
                {
                    DocumentId = document.DocumentId,
                    SourceName = displayName,
                },
            });

            var pages = (document.Pages ?? new List<DocumentPageEvidence>())
                .OrderBy(p => p.PageNumber)
                .ToList();
            if (pages.Count > 0)
            {
                foreach (var page in pages)
                {
                    var pageText = !string.IsNullOrWhiteSpace(page.Markdown) ? page.Markdown : page.ExtractedText;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        interactions.Add(new AIInteractionText
                        {
                            Agent = AIAgent.User,
                            TurnId = turnId,
                            Content = $"[Page {page.PageNumber}]\n{pageText}",
                            SourceReference = new SourceContentReference
                            {
                                DocumentId = document.DocumentId,
                                SourceName = displayName,
                                PageNumber = page.PageNumber,
                                Kind = SourceContentKind.Page,
                            },
                        });
                    }

                    foreach (var img in modelImages.Where(i => i.SourceReference?.PageNumber == page.PageNumber))
                    {
                        interactions.Add(new AIInteractionImage
                        {
                            Agent = AIAgent.User,
                            TurnId = turnId,
                            ImageData = img.ImageData,
                            MimeType = img.MimeType,
                            SourceReference = img.SourceReference,
                        });
                    }
                }

                // Model-visible images without a page association trail the pages.
                foreach (var img in modelImages.Where(i => i.SourceReference?.PageNumber == null))
                {
                    interactions.Add(Image(img, turnId));
                }
            }
            else
            {
                // Page-less content: article text then images.
                if (!string.IsNullOrWhiteSpace(markdown))
                {
                    interactions.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        TurnId = turnId,
                        Content = markdown,
                        SourceReference = new SourceContentReference
                        {
                            DocumentId = document.DocumentId,
                            SourceName = displayName,
                        },
                    });
                }

                foreach (var img in modelImages)
                {
                    interactions.Add(Image(img, turnId));
                }
            }

            return interactions;
        }

        private static AIInteractionImage Image(ToolResultImage img, string turnId) => new AIInteractionImage
        {
            Agent = AIAgent.User,
            TurnId = turnId,
            ImageData = img.ImageData,
            MimeType = img.MimeType,
            SourceReference = img.SourceReference,
        };

        private static string DisplayName(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return "document";
            }

            // URL sources keep their full identity — basename only applies to
            // filesystem paths, never to http(s) resource locators.
            if (sourceName.Contains("://"))
            {
                return sourceName;
            }

            try
            {
                return Path.GetFileName(sourceName);
            }
            catch
            {
                return sourceName;
            }
        }
    }
}
