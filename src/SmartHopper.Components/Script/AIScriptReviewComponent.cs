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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;

namespace SmartHopper.Components.Script
{
    /// <summary>
    /// Grasshopper component that reviews script components using AI.
    /// Performs static code analysis and AI-based review using the script_review tool.
    /// Supports reviewing multiple selected components, with questions matched first-first, second-second, etc.
    /// </summary>
    public class AIScriptReviewComponent : AISelectingStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("9C82B8C7-7F66-4E6C-9F6E-0C58C1C2A345");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.scriptreview;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "script_review" };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIScriptReviewComponent"/> class.
        /// </summary>
        public AIScriptReviewComponent()
            : base(
                  "AI Script Review",
                  "AIScriptReview",
                  "Review existing Grasshopper script components and get coded checks plus an AI review.\nSupports multiple selected components. Questions are matched to selected components in order.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Question", "Q", "Optional question or focus for the review. Multiple questions are matched to selected components in order.", GH_ParamAccess.list);
            pManager[0].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if the review succeeded. One branch per component.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Coded Issues", "C", "List of coded static issues detected in the script. One branch per component.", GH_ParamAccess.tree);
            pManager.AddTextParameter("AI Review", "R", "Full AI review text. One branch per component.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIScriptReviewWorker(this, this.AddRuntimeMessage);
        }

        /// <summary>
        /// Worker class that performs script review using the script_review AI tool.
        /// </summary>
        private sealed class AIScriptReviewWorker : AsyncWorkerBase
        {
            private readonly AIScriptReviewComponent parent;
            private List<GH_String> normalizedGuids;
            private List<GH_String> normalizedQuestions;
            private bool hasWork;
            private int iterationCount;
            private readonly GH_Structure<GH_Boolean> resultSuccess = new GH_Structure<GH_Boolean>();
            private readonly GH_Structure<GH_String> resultCodedIssues = new GH_Structure<GH_String>();
            private readonly GH_Structure<GH_String> resultAiReview = new GH_Structure<GH_String>();

            public AIScriptReviewWorker(
                AIScriptReviewComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var questions = new List<string>();
                DA.GetDataList("Question", questions);

                // Get GUIDs from selection (via selecting button)
                var guids = this.parent.SelectedObjects
                    .Select(obj => obj.InstanceGuid.ToString())
                    .ToList();

                this.hasWork = guids.Count > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No component selected. Use the selecting button to select script component(s).");
                    dataCount = 0;
                    return;
                }

                // Convert to GH_String for normalization
                // If no questions provided, use empty string as default
                var questionsAsGhString = questions.Count > 0
                    ? questions.Select(q => new GH_String(q ?? string.Empty)).ToList()
                    : new List<GH_String> { new GH_String(string.Empty) };
                var guidsAsGhString = guids.Select(g => new GH_String(g)).ToList();

                // Normalize lengths to match questions to components
                // Uses DataTreeProcessor.NormalizeBranchLengths for first-first, second-second matching
                var normalized = DataTreeProcessor.NormalizeBranchLengths(
                    new List<List<GH_String>> { questionsAsGhString, guidsAsGhString });

                this.normalizedQuestions = normalized[0];
                this.normalizedGuids = normalized[1];
                this.iterationCount = this.normalizedGuids.Count;

                dataCount = this.iterationCount;

                // Initialize progress tracking
                this.parent.InitializeProgress(this.iterationCount);
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    for (int i = 0; i < this.iterationCount; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        // Update progress (1-based)
                        this.parent.UpdateProgress(i + 1);

                        var path = new GH_Path(i);
                        var guid = this.normalizedGuids[i]?.Value ?? string.Empty;
                        var question = this.normalizedQuestions[i]?.Value ?? string.Empty;

                        Debug.WriteLine($"[AIScriptReviewWorker] Reviewing component {i + 1}/{this.iterationCount}: {guid}");

                        var parameters = new JObject
                        {
                            ["guid"] = guid,
                            ["contextFilter"] = "-*",
                        };

                        if (!string.IsNullOrWhiteSpace(question))
                        {
                            parameters["question"] = question;
                        }

                        var toolResult = await this.parent.CallAiToolAsync("script_review", parameters).ConfigureAwait(false);
                        this.StoreResult(path, toolResult);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptReviewWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            /// <summary>
            /// Stores the result from a tool call into the output trees.
            /// </summary>
            private void StoreResult(GH_Path path, JObject toolResult)
            {
                if (toolResult == null)
                {
                    this.resultSuccess.Append(new GH_Boolean(false), path);
                    this.resultAiReview.Append(new GH_String("Tool 'script_review' returned no result."), path);
                    return;
                }

                // Check for errors in result
                var hasErrors = toolResult["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                if (hasErrors)
                {
                    foreach (var text in ((JArray)toolResult["messages"])
                        .Where(msg => msg["severity"]?.ToString() == "Error")
                        .Select(msg => msg["message"]?.ToString())
                        .Where(text => !string.IsNullOrWhiteSpace(text)))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, text);
                    }

                    this.resultSuccess.Append(new GH_Boolean(false), path);
                    return;
                }

                var success = toolResult["success"]?.ToObject<bool>() ?? true;
                this.resultSuccess.Append(new GH_Boolean(success), path);

                // Extract coded issues
                if (toolResult["codedIssues"] is JArray issuesArray)
                {
                    foreach (var text in issuesArray
                        .Select(issue => issue?.ToString())
                        .Where(text => !string.IsNullOrWhiteSpace(text)))
                    {
                        this.resultCodedIssues.Append(new GH_String(text), path);
                    }
                }

                // Extract AI review
                string review = toolResult["aiReview"]?.ToString() ?? string.Empty;
                this.resultAiReview.Append(new GH_String(review), path);

                Debug.WriteLine($"[AIScriptReviewWorker] Review completed for branch {path}");
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Success", this.resultSuccess, DA);
                this.parent.SetPersistentOutput("Coded Issues", this.resultCodedIssues, DA);
                this.parent.SetPersistentOutput("AI Review", this.resultAiReview, DA);

                message = $"Reviewed {this.iterationCount} component(s)";
            }
        }
    }
}
