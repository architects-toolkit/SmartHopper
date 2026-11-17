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
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.DocumentProcessing.Readers;
using SmartHopper.DocumentProcessing.Rendering;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using Xunit;

namespace SmartHopper.DocumentProcessing.Tests
{
    /// <summary>
    /// Pure reader/renderer tests on the frozen fixtures — no Rhino/Grasshopper references.
    /// Ground truth: TestAssets/groundtruth.json (frozen v2 fixture; do not rederive).
    ///   page 1: vector plot, 595x842, 731 chars, 0 raster, 29 vector paths
    ///   page 2: scan,       595x842,   0 chars, 1 raster, 0 vectors
    ///   page 3: rotated/cropped visible 515x740, 80 chars, green annotation
    ///   page 4: table,      595x842, 202 chars (12.5mM / 9.0mM, E = mc^2)
    /// </summary>
    public class PdfDocumentReaderTests
    {
        // Vendored test asset (see TestAssets/README.md for provenance); copied to output.
        private static readonly string FixturePdf = FindFile(
            Path.Combine("TestAssets", "synthetic-scientific-evidence.pdf"));

        private static string FindFile(string relativeOrAbsolute, bool mustExist = true)
        {
            if (File.Exists(relativeOrAbsolute))
            {
                return relativeOrAbsolute;
            }

            // Walk up from the test binary to the worktree root.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativeOrAbsolute);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            if (mustExist)
            {
                throw new FileNotFoundException($"Test fixture not found: {relativeOrAbsolute}");
            }

            return null;
        }

        private static DocumentReadOptions RenderOptions(IReadOnlyList<int> pages = null, DocumentRegionSelection region = null, long? maxEncodedBytes = null)
        {
            return new DocumentReadOptions
            {
                RenderPages = true,
                ExtractEmbeddedImages = true,
                SelectedPages = pages,
                Region = region,
                MaxEncodedBytes = maxEncodedBytes ?? new DocumentReadOptions().MaxEncodedBytes,
            };
        }

        private static (int w, int h) PngDims(byte[] png)
        {
            return ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19],
                    (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        }

        [Fact(DisplayName = "Fixture read: page text, embedded assets, renders and coverage")]
        public async Task Fixture_Read_ProducesEvidence()
        {
            var doc = await new PdfDocumentReader().ReadAsync(FixturePdf, RenderOptions(), CancellationToken.None);

            Assert.Equal("pdf", doc.Format);
            Assert.Equal(4, doc.Coverage.TotalPages);
            Assert.Equal(new[] { 1, 2, 3, 4 }, doc.Coverage.SelectedPages.ToArray());
            Assert.StartsWith("sha256-", doc.DocumentId);
            Assert.Empty(doc.Coverage.FailedPages);

            // (b) page 1: native text (731 chars) + ZERO embedded raster yet a full render.
            var p1 = doc.Pages[0];
            Assert.Equal(1, p1.PageNumber);
            Assert.True(p1.ExtractedText.Length > 400, $"page1 text chars {p1.ExtractedText.Length}");
            Assert.NotNull(p1.RenderImageId);
            Assert.Empty(p1.AssetIds); // zero embedded raster images

            // (c) page 2: ZERO native text but image-bearing payload + scan warning.
            var p2 = doc.Pages[1];
            Assert.True(string.IsNullOrWhiteSpace(p2.ExtractedText) || p2.ExtractedText.Length < 20);
            Assert.Contains(p2.Warnings, w => w.Contains("scanned"));
            Assert.Single(p2.AssetIds); // the embedded scan image
            Assert.NotNull(p2.RenderImageId);

            // (d) page 3: rotation/crop-aware VISIBLE dims (515x740), not media box (842x595).
            var p3 = doc.Pages[2];
            Assert.Equal(515.0, p3.WidthPoints, 1);
            Assert.Equal(740.0, p3.HeightPoints, 1);
            Assert.NotNull(p3.RenderImageId);

            // (e) page 4: table values survive extraction.
            var p4 = doc.Pages[3];
            Assert.Contains("12.5", p4.ExtractedText + p4.Markdown);
            Assert.Contains("9.0", p4.ExtractedText + p4.Markdown);

            // Model-visible images: page renders + (page 2 embedded is display-only since rendered).
            var modelVisible = doc.Images.Where(i => i.SendToModel).ToList();
            Assert.True(modelVisible.Count >= 4, "at least 4 page renders must be model-visible");
            foreach (var img in modelVisible)
            {
                Assert.NotNull(img.SourceReference?.PageNumber);
                Assert.NotEmpty(img.ImageData);
            }
        }

        [Fact(DisplayName = "Selected pages [1,3] only, with ordering")]
        public async Task Selection_SubsetPages()
        {
            var doc = await new PdfDocumentReader().ReadAsync(
                FixturePdf, RenderOptions(pages: new[] { 1, 3 }), CancellationToken.None);

            Assert.Equal(4, doc.Coverage.TotalPages);
            Assert.Equal(new[] { 1, 3 }, doc.Coverage.SelectedPages.ToArray());
            Assert.Equal(2, doc.Pages.Count);
            Assert.Equal(3, doc.Pages[1].PageNumber);
            Assert.Equal(2, doc.Images.Count(i => i.SendToModel));
        }

        [Theory(DisplayName = "Invalid page selections reject")]
        [InlineData(new int[0])]
        [InlineData(new[] { 1, 1 })]
        [InlineData(new[] { 5 })]
        [InlineData(new[] { 0 })]
        [InlineData(new[] { -2 })]
        public async Task Selection_Invalid_Throws(int[] pages)
        {
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf, RenderOptions(pages: pages), CancellationToken.None));
        }

        [Fact(DisplayName = "Detail crop: normalized sampling increases density toward 4096px")]
        public async Task Crop_HighDensityRender()
        {
            // Half of the rotated page's visible width => ~258pt => ~1400px at 144dpi, but
            // the crop must upsample so the longest side reaches the 4096 detail cap.
            var region = new DocumentRegionSelection { Page = 3, X = 0.0, Y = 0.0, Width = 0.5, Height = 1.0 };
            var doc = await new PdfDocumentReader().ReadAsync(
                FixturePdf, RenderOptions(pages: new[] { 3 }, region: region), CancellationToken.None);

            var crop = doc.Images.FirstOrDefault(i => i.SourceReference?.Kind == SourceContentKind.Crop);
            Assert.NotNull(crop);
            Assert.True(crop.SendToModel);
            var (w, h) = PngDims(Convert.FromBase64String(crop.ImageData));
            Assert.Equal(4096, Math.Max(w, h));
        }

        [Fact(DisplayName = "Invalid regions reject")]
        public async Task Region_Invalid_Throws()
        {
            // NaN width
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    RenderOptions(region: new DocumentRegionSelection { Page = 1, X = 0, Y = 0, Width = double.NaN, Height = 0.5 }), CancellationToken.None));
            // Out of bounds
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    RenderOptions(region: new DocumentRegionSelection { Page = 1, X = 0.8, Y = 0, Width = 0.5, Height = 0.5 }), CancellationToken.None));
            // Region on a non-selected page
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    RenderOptions(pages: new[] { 1 }, region: new DocumentRegionSelection { Page = 3, X = 0, Y = 0, Width = 0.5, Height = 0.5 }), CancellationToken.None));
        }

        [Fact(DisplayName = "Limits: max render pages, file size and encoded budget enforce")]
        public async Task Limits_Enforced()
        {
            // >MaxRenderPages in one call: actionable error, text-only remains available.
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    new DocumentReadOptions { MaxRenderPages = 1 }, CancellationToken.None));

            // Text-only extraction (no rendering) still works beyond render limits.
            var textDoc = await new PdfDocumentReader().ReadAsync(FixturePdf,
                new DocumentReadOptions { RenderPages = false, ExtractEmbeddedImages = false, MaxRenderPages = 1 },
                CancellationToken.None);
            Assert.Equal(4, textDoc.Coverage.SelectedPages.Count);
            Assert.Equal(4, textDoc.Coverage.TextOnlyPages.Count);

            // File-size cap.
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    new DocumentReadOptions { MaxFileBytes = 10 }, CancellationToken.None));

            // Encoded budget cap during rendering.
            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    new DocumentReadOptions { MaxEncodedBytes = 100 }, CancellationToken.None));
        }

        [Fact(DisplayName = "Encoded budget counts base64 length, not raw bytes")]
        public async Task Limits_EncodedBudget_CountsBase64Length()
        {
            // The accounting unit is the base64 string length (~4/3 of the raw byte
            // count): a cap between the two must fire even though the raw payload fits.
            var doc = await new PdfDocumentReader().ReadAsync(
                FixturePdf, RenderOptions(pages: new[] { 1 }), CancellationToken.None);
            var encoded = doc.Images.Sum(i => i.ImageData?.Length ?? 0);
            var raw = doc.Images.Sum(i => Convert.FromBase64String(i.ImageData).Length);
            Assert.True(encoded > raw, $"encoded={encoded} raw={raw}");

            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                new PdfDocumentReader().ReadAsync(FixturePdf,
                    RenderOptions(pages: new[] { 1 }, maxEncodedBytes: raw), CancellationToken.None));

            var fit = await new PdfDocumentReader().ReadAsync(FixturePdf,
                RenderOptions(pages: new[] { 1 }, maxEncodedBytes: encoded), CancellationToken.None);
            Assert.Equal(encoded, fit.Images.Sum(i => i.ImageData?.Length ?? 0));
        }

        [Fact(DisplayName = "Missing renderer yields partial coverage, not failure")]
        public async Task MissingRenderer_PartialCoverage()
        {
            var doc = await new PdfDocumentReader().ReadAsync(FixturePdf,
                new DocumentReadOptions { RendererDirectory = Path.Combine(Path.GetTempPath(), "definitely-missing-sh-renderer-dir-xyz") },
                CancellationToken.None);

            Assert.NotEmpty(doc.Warnings);
            Assert.Empty(doc.Coverage.RenderedPages);
            Assert.Equal(4, doc.Pages.Count);
            Assert.All(doc.Pages, p => Assert.Null(p.RenderImageId));
        }

        [Fact(DisplayName = "Custom renderer dir without trust verifier is refused")]
        public async Task CustomRendererDir_NoVerifier_Refused()
        {
            var renderer = IsolatedDocumentRenderer.Create(Path.GetTempPath());
            Assert.False(renderer.IsAvailable);
            Assert.Contains("trust", renderer.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        }

        [Theory(DisplayName = "Render boundary rejects malformed region arrays before native entry")]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void Render_NonFourRegionLength_Rejected(int length)
        {
            var renderer = IsolatedDocumentRenderer.Create(Path.GetTempPath());
            Assert.False(renderer.IsAvailable);
            Assert.Throws<ArgumentException>(() =>
                renderer.Render("pdf", new byte[] { 1, 2, 3 }, 0, 96, 4096, new double[length], CancellationToken.None));
        }

        [Fact(DisplayName = "Render boundary rejects non-finite region coordinates")]
        public void Render_NonFiniteRegion_Rejected()
        {
            var renderer = IsolatedDocumentRenderer.Create(Path.GetTempPath());
            Assert.Throws<ArgumentException>(() =>
                renderer.Render("pdf", new byte[] { 1, 2, 3 }, 0, 96, 4096, new[] { 0.0, 0.0, double.NaN, 0.5 }, CancellationToken.None));
            Assert.Throws<ArgumentException>(() =>
                renderer.Render("pdf", new byte[] { 1, 2, 3 }, 0, 96, 4096, new[] { 0.0, 0.0, double.PositiveInfinity, 0.5 }, CancellationToken.None));
        }

        [Fact(DisplayName = "Render boundary accepts null and empty region as full page")]
        public void Render_NullOrEmptyRegion_IsFullPage()
        {
            var renderer = IsolatedDocumentRenderer.Create(Path.GetTempPath());
            Assert.False(renderer.IsAvailable);

            // Valid shapes pass validation and fail on availability instead.
            Assert.Throws<InvalidDataException>(() =>
                renderer.Render("pdf", new byte[] { 1, 2, 3 }, 0, 96, 4096, null, CancellationToken.None));
            Assert.Throws<InvalidDataException>(() =>
                renderer.Render("pdf", new byte[] { 1, 2, 3 }, 0, 96, 4096, Array.Empty<double>(), CancellationToken.None));
        }

        [Fact(DisplayName = "Renderer deps never bind into the default load context")]
        public async Task Renderer_StaysIsolated()
        {
            var doc = await new PdfDocumentReader().ReadAsync(FixturePdf, RenderOptions(), CancellationToken.None);
            Assert.NotNull(doc);

            var defaultAssemblies = AssemblyLoadContext.Default.Assemblies
                .Select(a => a.GetName().Name).ToList();
            Assert.DoesNotContain(defaultAssemblies, n =>
                n.StartsWith("SkiaSharp", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("PDFtoImage", StringComparison.OrdinalIgnoreCase));
        }

        [Fact(DisplayName = "Missing-renderer coverage is incomplete, not IsComplete=true")]
        public async Task MissingRenderer_IsCompleteFalse()
        {
            var doc = await new PdfDocumentReader().ReadAsync(FixturePdf,
                new DocumentReadOptions { RendererDirectory = Path.Combine(Path.GetTempPath(), "definitely-missing-sh-renderer-dir-xyz") },
                CancellationToken.None);

            Assert.NotEmpty(doc.Warnings);
            Assert.False(doc.Coverage.IsComplete);
            Assert.Equal(4, doc.Pages.Count);
        }
    }
}
