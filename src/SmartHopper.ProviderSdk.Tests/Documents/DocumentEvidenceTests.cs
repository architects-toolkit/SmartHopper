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

using System.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Documents;
using Xunit;

namespace SmartHopper.ProviderSdk.Tests.Documents
{
    /// <summary>Deterministic tests for the shared document-evidence codec and
    /// interaction builder (no Rhino/Grasshopper references).</summary>
    [Collection("ProviderSdk")]
    public sealed class DocumentEvidenceTests
    {
        private static DocumentEvidence SampleDocument()
            => new DocumentEvidence
            {
                DocumentId = "sha256-abc",
                SourceName = @"C:\docs\report.pdf",
                Format = "pdf",
                Pages = new System.Collections.Generic.List<DocumentPageEvidence>
                {
                    new DocumentPageEvidence
                    {
                        PageNumber = 1,
                        WidthPoints = 595,
                        HeightPoints = 842,
                        ExtractedText = "page one text",
                        Markdown = "# p1",
                        RenderImageId = "page-1-render",
                        AssetIds = new System.Collections.Generic.List<string> { "page-1-render" },
                        Warnings = new System.Collections.Generic.List<string>(),
                    },
                    new DocumentPageEvidence
                    {
                        PageNumber = 2,
                        WidthPoints = 595,
                        HeightPoints = 842,
                        ExtractedText = string.Empty,
                        RenderImageId = "page-2-render",
                        AssetIds = new System.Collections.Generic.List<string> { "page-2-render" },
                        Warnings = new System.Collections.Generic.List<string> { "scanned" },
                    },
                },
                Images = new System.Collections.Generic.List<ToolResultImage>
                {
                    new ToolResultImage
                    {
                        ImageData = "AA==",
                        MimeType = "image/png",
                        SendToModel = true,
                        SourceReference = new SourceContentReference
                        {
                            DocumentId = "sha256-abc",
                            SourceName = "report.pdf",
                            PageNumber = 1,
                            AssetId = "page-1-render",
                            Kind = SourceContentKind.Page,
                        },
                    },
                    new ToolResultImage
                    {
                        ImageData = "BB==",
                        MimeType = "image/png",
                        SendToModel = true,
                        SourceReference = new SourceContentReference
                        {
                            DocumentId = "sha256-abc",
                            SourceName = "report.pdf",
                            PageNumber = 2,
                            AssetId = "page-2-render",
                            Kind = SourceContentKind.Page,
                        },
                    },
                },
                Metadata = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["title"] = "Report",
                    ["author"] = "unit-test",
                },
                Coverage = new DocumentCoverage
                {
                    TotalPages = 4,
                    SelectedPages = new System.Collections.Generic.List<int> { 1, 2 },
                    RenderedPages = new System.Collections.Generic.List<int> { 1, 2 },
                    FailedPages = new System.Collections.Generic.List<int>(),
                    TextOnlyPages = new System.Collections.Generic.List<int>(),
                    IsComplete = true,
                },
            };

        [Fact]
        public void CodecRoundTrip_RestoresPagesCoverageAndImages()
        {
            var doc = SampleDocument();
            var encoded = DocumentEvidenceJsonCodec.Encode(doc);

            // The compact projection never carries image bytes.
            Assert.Null(encoded["images"]);
            var roundtripped = DocumentEvidenceJsonCodec.Decode(encoded, doc.Images.ToList());

            Assert.NotNull(roundtripped);
            Assert.Equal(doc.DocumentId, roundtripped.DocumentId);
            Assert.Equal(2, roundtripped.Pages.Count);
            Assert.Equal(595, roundtripped.Pages[0].WidthPoints);
            Assert.Equal("page-2-render", roundtripped.Pages[1].RenderImageId);
            Assert.Single(roundtripped.Pages[1].Warnings);
            Assert.Equal(4, roundtripped.Coverage.TotalPages);
            Assert.True(roundtripped.Coverage.IsComplete);
            Assert.Equal(2, roundtripped.Images.Count);
            Assert.Equal("AA==", roundtripped.Images[0].ImageData);
            Assert.Equal(1, roundtripped.Images[0].SourceReference.PageNumber);
            Assert.Equal("Report", roundtripped.Metadata["title"]);
            Assert.Equal("unit-test", roundtripped.Metadata["author"]);
        }

        [Fact]
        public void Builder_UrlSourceName_IsNotBasenamed()
        {
            var doc = SampleDocument() with { SourceName = "https://example.com/articles/report.pdf" };
            var interactions = DocumentEvidenceInteractionBuilder.Build(doc, null);

            var text = interactions.OfType<AIInteractionText>().First();
            Assert.Contains("https://example.com/articles/report.pdf", text.Content);
            Assert.Equal("https://example.com/articles/report.pdf", text.SourceReference.SourceName);
        }

        [Fact]
        public void Builder_PosixSourceName_IsBasenamed()
        {
            var doc = SampleDocument() with { SourceName = "/Users/name/docs/report.pdf" };
            var interactions = DocumentEvidenceInteractionBuilder.Build(doc, null);

            var text = interactions.OfType<AIInteractionText>().First();
            Assert.Contains("report.pdf", text.Content);
            Assert.DoesNotContain("/Users/name", text.Content); // basename, never the absolute path
        }

        [Fact]
        public void Builder_EmitsPagesInPageNumberOrder()
        {
            var doc = SampleDocument() with
            {
                Pages = new System.Collections.Generic.List<DocumentPageEvidence>
                {
                    SampleDocument().Pages[1],
                    SampleDocument().Pages[0],
                },
            };
            var interactions = DocumentEvidenceInteractionBuilder.Build(doc, null);
            var pageTexts = interactions.OfType<AIInteractionText>()
                .Where(t => t.SourceReference?.PageNumber != null)
                .ToList();
            Assert.Equal(new int?[] { 1 }, pageTexts.Select(t => t.SourceReference.PageNumber).ToArray());
        }

        [Fact]
        public void Builder_EmitsHeaderThenOrderedPagesWithSharedTurnAndSources()
        {
            var interactions = DocumentEvidenceInteractionBuilder.Build(SampleDocument(), "# source markdown");

            var texts = interactions.OfType<AIInteractionText>().ToList();
            var images = interactions.OfType<AIInteractionImage>().ToList();
            Assert.Equal(2, texts.Count);   // header + p1 text (p2 has no text; markdown unused for page documents)
            Assert.Equal(2, images.Count);
            var turnIds = interactions.Select(i => i.TurnId).Distinct().ToList();
            Assert.Single(turnIds);
            Assert.Contains("report.pdf", texts[0].Content);
            Assert.DoesNotContain("C:\\docs", texts[0].Content); // basename, never the absolute path
            Assert.Equal(SourceContentKind.Page, texts[1].SourceReference.Kind);
            Assert.Equal(1, texts[1].SourceReference.PageNumber);

            // Ordering: header, page-1 text, page-1 image, page-2 image.
            Assert.IsType<AIInteractionText>(interactions[0]);
            Assert.IsType<AIInteractionText>(interactions[1]);
            Assert.IsType<AIInteractionImage>(interactions[2]);
            Assert.IsType<AIInteractionImage>(interactions[3]);
            Assert.Equal("sha256-abc", images[0].SourceReference.DocumentId);
        }

        [Fact]
        public void Builder_PagelessDocument_AppendsArticleThenImages()
        {
            var doc = SampleDocument() with { Pages = new System.Collections.Generic.List<DocumentPageEvidence>() };
            var interactions = DocumentEvidenceInteractionBuilder.Build(doc, "article body");

            Assert.Equal("article body", interactions.OfType<AIInteractionText>().Last().Content);
            Assert.True(interactions.Last() is AIInteractionImage);
        }

        [Fact]
        public void Codec_CompactEncode_OmitsContextTextButKeepsIdentity()
        {
            var source = new SourceContentReference
            {
                DocumentId = "sha256-abc",
                SourceName = "https://example.com/article",
                PageNumber = 2,
                AssetId = "img-3",
                Caption = "Figure 2",
                ContextText = "a very long article body that must not repeat per image",
                Kind = SourceContentKind.Image,
                Region = new SourceContentRegion { X = 0.1, Y = 0.2, Width = 0.5, Height = 0.5 },
            };

            var compact = SourceContentReferenceJsonCodec.Encode(source, includeContextText: false);
            Assert.Null((string)compact["contextText"]);
            Assert.Equal("sha256-abc", (string)compact["documentId"]);
            Assert.Equal("https://example.com/article", (string)compact["sourceName"]);
            Assert.Equal(2, (int)compact["pageNumber"]);
            Assert.Equal("img-3", (string)compact["assetId"]);
            Assert.Equal("Figure 2", (string)compact["caption"]);
            Assert.Equal(0.5, (double)compact["region"]["width"]);

            // The default one-arg overload stays full-fidelity.
            var full = SourceContentReferenceJsonCodec.Encode(source);
            Assert.Equal(source.ContextText, (string)full["contextText"]);
            var roundtrip = SourceContentReferenceJsonCodec.Decode((Newtonsoft.Json.Linq.JObject)full);
            Assert.Equal(source.ContextText, roundtrip.ContextText);
            Assert.Equal(source.DocumentId, roundtrip.DocumentId);
        }

        [Fact]
        public void Builder_DisplayOnlyImages_AreExcluded()
        {
            var doc = SampleDocument();
            doc = doc with
            {
                Images = new System.Collections.Generic.List<ToolResultImage>
                {
                    doc.Images[0] with { SendToModel = false },
                    doc.Images[1],
                },
            };
            var interactions = DocumentEvidenceInteractionBuilder.Build(doc, null);

            Assert.DoesNotContain(interactions.OfType<AIInteractionImage>(), i => i.ImageData == "AA==");
        }
    }
}
