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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Script
{
    public class AIScriptReviewComponent : AISelectingStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("9C82B8C7-7F66-4E6C-9F6E-0C58C1C2A345");

        // protected override Bitmap Icon => Resources.textevaluate;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Text;

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

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // TODO: use selecting component instead of guid input. Limit to just one component
            pManager.AddTextParameter("Guid", "G", "Optional GUID of the script component to review. If empty, uses the first selected script component.", GH_ParamAccess.item, string.Empty);
            pManager.AddTextParameter("Question", "Q", "Optional question or focus for the review.", GH_ParamAccess.item, string.Empty);
        }

        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if the review succeeded.", GH_ParamAccess.item);
            pManager.AddTextParameter("Coded Issues", "C", "List of coded static issues detected in the script.", GH_ParamAccess.list);
            pManager.AddTextParameter("AI Review", "R", "Full AI review text.", GH_ParamAccess.item);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIScriptReviewWorker(this, this.AddRuntimeMessage);
        }

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

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string guidInput = string.Empty;
                DA.GetData("Guid", ref guidInput);
                string localQuestion = string.Empty;
                DA.GetData("Question", ref localQuestion);

                if (string.IsNullOrWhiteSpace(guidInput))
                {
                    var first = this.parent.SelectedObjects.FirstOrDefault();
                    this.guid = first != null ? first.InstanceGuid.ToString() : string.Empty;
                }
                else
                {
                    this.guid = guidInput;
                }

                this.question = localQuestion;

                this.hasWork = !string.IsNullOrWhiteSpace(this.guid);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No GUID provided and no component selected.");
                }

                dataCount = this.hasWork ? 1 : 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var parameters = new JObject
                    {
                        ["guid"] = this.guid,
                        ["question"] = string.IsNullOrWhiteSpace(this.question) ? null : this.question,
                        ["contextFilter"] = "-*",
                    };

                    var toolResult = await this.parent.CallAiToolAsync("script_review", parameters).ConfigureAwait(false);

                    if (toolResult == null)
                    {
                        this.success = false;
                        return;
                    }

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

                    string review = toolResult["aiReview"]?.ToString() ?? string.Empty;
                    this.aiReview = new GH_String(review);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AIScriptReviewWorker] Error: {ex.Message}");
                    this.success = false;
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                DA.SetData("Success", this.success);

                if (this.codedIssues.Count > 0)
                {
                    DA.SetDataList("Coded Issues", this.codedIssues);
                }
                else
                {
                    DA.SetDataList("Coded Issues", new List<string>());
                }

                if (this.aiReview != null)
                {
                    this.parent.SetPersistentOutput("AI Review", this.aiReview, DA);
                    message = "Review completed";
                }
                else
                {
                    message = "No review available";
                }
            }
        }
    }
}
