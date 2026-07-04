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
 * TestSmarthopperReadmeComponent: Test component for the smarthopper_readme AI tool.
 *
 * Tool: smarthopper_readme
 *   Inputs:  topic (string, required) – one of:
 *              canvas, ghjson, selected, errors, locks, visibility,
 *              discovery, scripting, python, csharp, vb,
 *              knowledge, mcneel-forum, ladybug-forum, discourse-forum, research, web
 *   Outputs: topic (string), instructions (string)
 */

using System;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Test component for the smarthopper_readme AI tool.
    /// Returns the instruction bundle for the given topic.
    /// </summary>
    public class TestSmarthopperReadmeComponent : GH_Component
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("9BCF1E9D-0FDE-49EB-8BE6-7B0881350CBB");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSmarthopperReadmeComponent"/> class.
        /// </summary>
        public TestSmarthopperReadmeComponent()
            : base(
                "Test smarthopper_readme",
                "TEST-README",
                "Tests the smarthopper_readme AI tool. Returns detailed operational instructions for a given SmartHopper topic.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Topic",
                "T",
                "Instruction bundle topic. One of: canvas, ghjson, selected, errors, locks, visibility, " +
                "discovery, scripting, python, csharp, vb, knowledge, mcneel-forum, ladybug-forum, " +
                "discourse-forum, research, web.",
                GH_ParamAccess.item,
                "canvas");

            pManager.AddBooleanParameter(
                "Run?",
                "R",
                "Set to True to execute smarthopper_readme.",
                GH_ParamAccess.item,
                false);
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Topic",
                "T",
                "The topic that was requested.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Instructions",
                "I",
                "The instruction bundle returned for the requested topic.",
                GH_ParamAccess.item);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(1, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute smarthopper_readme.");
                return;
            }

            string topic = "canvas";
            if (!DA.GetData(0, ref topic) || string.IsNullOrWhiteSpace(topic))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Topic is required.");
                return;
            }

            try
            {
                var parameters = new JObject
                {
                    ["topic"] = topic.Trim().ToLowerInvariant(),
                };

                var toolCallInteraction = new AIInteractionToolCall
                {
                    Name = "smarthopper_readme",
                    Arguments = parameters,
                    Agent = AIAgent.Assistant,
                };

                var toolCall = new AIToolCall();
                toolCall.Endpoint = "smarthopper_readme";
                toolCall.FromToolCallInteraction(toolCallInteraction);
                toolCall.SkipMetricsValidation = true;

                var toolResult = ToolCallResult.FromAIReturn(toolCall.Exec().GetAwaiter().GetResult());

                if (toolResult.Result == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'smarthopper_readme' did not return a valid result.");
                    return;
                }

                var returnedTopic = toolResult["topic"]?.ToString() ?? string.Empty;
                var instructions = toolResult["instructions"]?.ToString() ?? string.Empty;

                DA.SetData(0, returnedTopic);
                DA.SetData(1, instructions);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
