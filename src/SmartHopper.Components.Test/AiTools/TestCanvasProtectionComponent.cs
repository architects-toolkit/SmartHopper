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
using SmartHopper.Core.Grasshopper.Contracts;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Test component for the canvas protection guard. This component implements
    /// <see cref="ICanvasProtectedComponent"/> so it can act as a stand-in for the
    /// SmartHopper MCP Server when testing the guard logic. Set <c>Protected?</c> to
    /// true to opt in to protection, then <c>Run?</c> to execute the test sequence.
    /// </summary>
    public class TestCanvasProtectionComponent : GH_Component, ICanvasProtectedComponent
    {
        private bool protectedInput;

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("90D18698-28D4-4978-B30B-579D99E6D6DD");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Gets a value indicating whether this component should currently be protected.
        /// Mirrors the behavior of the SmartHopper MCP Server component, which only
        /// protects itself when its Enable input is true.
        /// </summary>
        public bool IsProtected => this.protectedInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestCanvasProtectionComponent"/> class.
        /// </summary>
        public TestCanvasProtectionComponent()
            : base(
                "Test Canvas Protection",
                "TEST-CANVAS-PROT",
                "Tests the canvas protection guard that prevents AI tools from altering the enabled SmartHopper MCP Server and connected components. Set Protected?=True and Run?=True to test.",
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
                "Set to True to execute the canvas protection test sequence.",
                GH_ParamAccess.item,
                false);

            pManager.AddBooleanParameter(
                "Protected?",
                "P",
                "When true, this component acts as an enabled MCP Server and should be protected from AI tools.",
                GH_ParamAccess.item,
                false);
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Summary",
                "S",
                "High-level pass/fail summary for each test step.",
                GH_ParamAccess.list);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(0, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the test sequence.");
                DA.SetDataList(0, new List<string>());
                return;
            }

            bool protectedInput = false;
            DA.GetData(1, ref protectedInput);
            this.protectedInput = protectedInput;

            var summary = new List<string>();
            var myGuid = this.InstanceGuid.ToString();

            var protectedGuids = CanvasProtection.GetProtectedInstanceGuids();
            if (protectedInput)
            {
                if (protectedGuids.Contains(this.InstanceGuid))
                {
                    summary.Add("PASS: CanvasProtection detects this component as protected.");
                }
                else
                {
                    summary.Add("FAIL: CanvasProtection did not detect this component as protected.");
                }

                var removeResult = this.ExecuteTool(
                    "gh_remove",
                    new JObject
                    {
                        ["instanceGuids"] = new JArray(myGuid),
                    });
                var removeJson = this.ParseToolResult(removeResult);
                var removed = removeJson?["removedGuids"]?.ToObject<List<string>>() ?? new List<string>();
                var reportedProtected = removeJson?["protectedGuids"]?.ToObject<List<string>>() ?? new List<string>();

                if (removed.Contains(myGuid))
                {
                    summary.Add("FAIL: gh_remove removed the protected component.");
                }
                else if (reportedProtected.Contains(myGuid))
                {
                    summary.Add("PASS: gh_remove skipped the protected component and reported it.");
                }
                else
                {
                    summary.Add("FAIL: gh_remove did not remove the component but also did not report it as protected.");
                }
            }
            else
            {
                if (protectedGuids.Contains(this.InstanceGuid))
                {
                    summary.Add("FAIL: CanvasProtection incorrectly detects this component as protected when Protected? is false.");
                }
                else
                {
                    summary.Add("PASS: CanvasProtection does not detect this component as protected when Protected? is false.");
                }
            }

            DA.SetDataList(0, summary);
        }

        private JObject ParseToolResult(AIReturn result)
        {
            if (result?.Body?.Interactions == null)
            {
                return null;
            }

            return result.Body.Interactions
                .OfType<AIInteractionToolResult>()
                .FirstOrDefault()
                ?.Result;
        }

        private AIReturn ExecuteTool(string name, JObject arguments)
        {
            var toolCallInteraction = new AIInteractionToolCall
            {
                Name = name,
                Arguments = arguments,
                Agent = AIAgent.Assistant,
            };

            var toolCall = new AIToolCall();
            toolCall.Endpoint = name;
            toolCall.FromToolCallInteraction(toolCallInteraction);
            toolCall.SkipMetricsValidation = true;

            return toolCall.Exec().GetAwaiter().GetResult();
        }
    }
}