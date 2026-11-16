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
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.DocumentProcessing;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Documents;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Manual test component for the file2md Pages/Region selection contract and the
    /// typed image/payload wiring used by File2Md, AIFile2Md, File2AI and Web2AI.
    /// Run=True executes the checks; results are reported in the Summary output.
    /// </summary>
    public class TestFile2MdSelectionComponent : GH_Component
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("7B53C9D2-6A31-4B3E-9F6E-3D4C9B72E3D1");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFile2MdSelectionComponent"/> class.
        /// </summary>
        public TestFile2MdSelectionComponent()
            : base(
                "Test file2md selection + typed images",
                "TEST-FILE2MD-SEL",
                "Tests the optional Pages/Region selection contract, typed Images output "
                + "and single-payload evidence wiring. Set Run=True to execute.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter(
                "Run?",
                "R",
                "Set to True to execute the test sequence.",
                GH_ParamAccess.item,
                false);
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Summary",
                "S",
                "High-level pass/fail summary for each check.",
                GH_ParamAccess.list);

            pManager.AddBooleanParameter(
                "Success",
                "OK",
                "True when every check passed.",
                GH_ParamAccess.item);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(0, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the test sequence.");
                return;
            }

            var checks = new List<(string Name, bool Ok, string Detail)>();
            void Report(string name, bool ok, string detail = null)
                => checks.Add((name, ok, detail ?? string.Empty));

            // ---- 1. Optional unconnected Pages/Region: empty tokens must parse to null. ----
            Report(
                "pages: absent token stays optional",
                DocumentSelectionJsonParser.TryParsePages(null, out var absentPages, out _)
                    && absentPages == null);
            Report(
                "region: absent token stays optional",
                DocumentSelectionJsonParser.TryParseRegion(null, out var absentRegion, out _)
                    && absentRegion == null);

            // ---- 2. Strict selection: valid parses, oversized/invalid rejected with error. ----
            var pagesOk = DocumentSelectionJsonParser.TryParsePages(JToken.Parse("[3,1]"), out var pages, out _);
            Report(
                "pages: [3,1] parses and sorts",
                pagesOk && pages != null && pages.SequenceEqual(new[] { 1, 3 }));

            var overflowOk = DocumentSelectionJsonParser.TryParsePages(JToken.Parse("[99999999999]"), out _, out var overflowError);
            Report(
                "pages: int32 overflow rejected with error",
                !overflowOk && !string.IsNullOrWhiteSpace(overflowError));

            var regionOk = DocumentSelectionJsonParser.TryParseRegion(
                JToken.Parse(@"{""page"":2,""x"":0.25,""y"":0.25,""width"":0.5,""height"":0.5}"),
                out var region,
                out _);
            Report(
                "region: valid object parses",
                regionOk && region != null && region.Page == 2 && Math.Abs(region.Width - 0.5) < 1e-9);

            var regionOverflowOk = DocumentSelectionJsonParser.TryParseRegion(
                JToken.Parse(@"{""page"":99999999999,""x"":0,""y"":0,""width"":0.5,""height"":0.5}"),
                out _,
                out var regionError);
            Report(
                "region: page int32 overflow rejected with error",
                !regionOverflowOk && !string.IsNullOrWhiteSpace(regionError));

            // ---- 2b. Production ports: real components expose optional Pages/Region inputs. ----
            CheckOptionalInput(new SmartHopper.Components.Knowledge.File2MdComponent(), "File2Md");
            CheckOptionalInput(new SmartHopper.Components.Knowledge.AIFile2MdComponent(), "AIFile2Md");
            CheckOptionalInput(new SmartHopper.Components.Input.File2AIComponent(), "File2AI");

            void CheckOptionalInput(GH_Component component, string label)
            {
                foreach (var name in new[] { "Pages", "Region" })
                {
                    var param = component.Params.Input.FirstOrDefault(p =>
                        string.Equals(p.Name, name, StringComparison.Ordinal));
                    Report(
                        $"{label}: {name} input exists, optional, and carries no required data",
                        param != null && param.Optional && param.VolatileData.IsEmpty,
                        param == null ? "input port missing" : $"Optional={param.Optional}");
                }
            }

            // ---- 3. Typed result wiring: bytes and provenance come from ToolCallResult.Images. ----
            var typedImage = new ToolResultImage
            {
                ImageData = "aW1hZ2VkYXRh",
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
            };
            var toolResult = new ToolCallResult(
                success: true,
                result: new JObject
                {
                    ["content"] = "# Report markdown",
                    ["originalFormat"] = "pdf",
                },
                images: new List<ToolResultImage> { typedImage });

            var parsed = File2MdToolResult.Parse(toolResult, "report.pdf");
            Report(
                "typed parse: images surface with bytes + provenance",
                parsed != null
                    && parsed.TypedImages.Count == 1
                    && parsed.TypedImages[0].ImageData == "aW1hZ2VkYXRh"
                    && parsed.TypedImages[0].SourceReference?.AssetId == "page-1-render"
                    && parsed.Images.Count == 1);
            Report(
                "typed parse: no image bytes in the raw JSON payload",
                parsed != null && parsed.Raw != null
                    && !parsed.Raw.ToString(Newtonsoft.Json.Formatting.None).Contains("aW1hZ2VkYXRh"));

            // ---- 4. One mixed payload: codec roundtrip + interaction builder share one TurnId. ----
            var document = new DocumentEvidence
            {
                DocumentId = "sha256-abc",
                SourceName = "report.pdf",
                Images = new List<ToolResultImage> { typedImage },
                Coverage = new DocumentCoverage { TotalPages = 1, IsComplete = true },
            };
            var encoded = DocumentEvidenceJsonCodec.Encode(document);
            Report(
                "codec: compact projection carries no image bytes",
                encoded != null && !encoded.ToString(Newtonsoft.Json.Formatting.None).Contains("aW1hZ2VkYXRh"));

            var interactions = DocumentEvidenceInteractionBuilder.Build(document, "markdown body");
            var imageInteractions = interactions?.OfType<AIInteractionImage>().ToList() ?? new List<AIInteractionImage>();
            Report(
                "payload: image interaction carries source bytes",
                imageInteractions.Count == 1 && imageInteractions[0].ImageData == "aW1hZ2VkYXRh");
            Report(
                "payload: every interaction shares one TurnId",
                interactions != null
                    && interactions.Count > 0
                    && interactions.Select(i => i.TurnId).Distinct(StringComparer.Ordinal).Count() == 1);

            var failed = checks.Where(c => !c.Ok).ToList();
            var summary = checks
                .Select(c => $"{(c.Ok ? "PASS" : "FAIL")}: {c.Name}{(string.IsNullOrEmpty(c.Detail) ? string.Empty : " — " + c.Detail)}")
                .ToList();

            DA.SetDataList(0, summary);
            DA.SetData(1, failed.Count == 0);

            if (failed.Count > 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"{failed.Count} check(s) failed: {string.Join("; ", failed.Select(f => f.Name))}");
            }
        }
    }
}
