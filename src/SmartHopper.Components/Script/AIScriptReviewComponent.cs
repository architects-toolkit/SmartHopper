/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Script
{
    /// <summary>
    /// Grasshopper component that reviews script components using AI.
    /// Performs static code analysis and AI-based review using the script_review tool.
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
        protected override AICapability RequiredCapability => AICapability.Text2Text;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIScriptReviewComponent"/> class.
        /// </summary>
        public AIScriptReviewComponent()
            : base(
                  "AI Script Review",
                  "AIScriptReview",
                  "Review an existing Grasshopper script component and get coded checks plus an AI review.",
                  "SmartHopper",
                  "Script")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Question", "Q", "Optional question or focus for the review.", GH_ParamAccess.item, string.Empty);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if the review succeeded.", GH_ParamAccess.item);
            pManager.AddTextParameter("Coded Issues", "C", "List of coded static issues detected in the script.", GH_ParamAccess.list);
            pManager.AddTextParameter("AI Review", "R", "Full AI review text.", GH_ParamAccess.item);
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
            private string guid;
            private string question;
            private bool hasWork;
            private bool success;
            private readonly List<GH_String> codedIssues = new List<GH_String>();
            private GH_String aiReview;

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
                string localQuestion = string.Empty;
                DA.GetData("Question", ref localQuestion);

                // Get GUID from selection (via selecting button)
                var first = this.parent.SelectedObjects.FirstOrDefault();
                this.guid = first != null ? first.InstanceGuid.ToString() : string.Empty;

                this.question = localQuestion;

                this.hasWork = !string.IsNullOrWhiteSpace(this.guid);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No component selected. Use the selecting button to select a script component.");
                }

                dataCount = this.hasWork ? 1 : 0;
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
                    Debug.WriteLine($"[AIScriptReviewWorker] Reviewing component: {this.guid}");

                    var parameters = new JObject
                    {
                        ["guid"] = this.guid,
                        ["contextFilter"] = "-*",
                    };

                    if (!string.IsNullOrWhiteSpace(this.question))
                    {
                        parameters["question"] = this.question;
                    }

                    var toolResult = await this.parent.CallAiToolAsync("script_review", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'script_review' returned no result.");
                        this.success = false;
                        return;
                    }

                    // Check for errors in result
                    var hasErrors = toolResult["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                    if (hasErrors)
                    {
                        foreach (var msg in (JArray)toolResult["messages"])
                        {
                            if (msg["severity"]?.ToString() == "Error")
                            {
                                var text = msg["message"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, text);
                                }
                            }
                        }

                        this.success = false;
                        return;
                    }

                    this.success = toolResult["success"]?.ToObject<bool>() ?? true;

                    // Extract coded issues
                    if (toolResult["codedIssues"] is JArray issuesArray)
                    {
                        foreach (var issue in issuesArray)
                        {
                            var text = issue?.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                this.codedIssues.Add(new GH_String(text));
                            }
                        }
                    }

                    // Extract AI review
                    string review = toolResult["aiReview"]?.ToString() ?? string.Empty;
                    this.aiReview = new GH_String(review);

                    Debug.WriteLine($"[AIScriptReviewWorker] Review completed: {this.codedIssues.Count} coded issues, review length: {review.Length}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptReviewWorker] Error: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                    this.success = false;
                }
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                // Index-based access to match registered output parameters:
                // 0: Success (bool), 1: Coded Issues (list), 2: AI Review (text).
                DA.SetData(0, this.success);

                if (this.codedIssues.Count > 0)
                {
                    DA.SetDataList(1, this.codedIssues);
                }
                else
                {
                    DA.SetDataList(1, new List<string>());
                }

                if (this.aiReview != null)
                {
                    this.parent.SetPersistentOutput("AI Review", this.aiReview, DA);
                    message = this.success ? "Review completed" : "Review failed";
                }
                else
                {
                    message = "No review available";
                }
            }
        }
    }
}
