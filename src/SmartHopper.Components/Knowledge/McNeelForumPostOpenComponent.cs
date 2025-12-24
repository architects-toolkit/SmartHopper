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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Opens the McNeelForum page for a given post JSON in the default browser.
    /// </summary>
    public class McNeelForumPostOpenComponent : StatefulComponentBaseV2
    {
        public override Guid ComponentGuid => new Guid("1B7A2E6C-4F0B-4B1C-9D19-6B3A2C8F9012");

        protected override Bitmap Icon => Resources.mcneelpostopen;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public McNeelForumPostOpenComponent()
            : base(
                  "McNeelForum Post Open",
                  "McNeelPostOpen",
                  "Open the McNeel Discourse webpage for a forum post JSON in the default browser.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "McNeelForum Post",
                "McP",
                "JSON object representing the full forum post (e.g. from McNeelForumPostGetComponent).",
                GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new McNeelForumPostOpenWorker(this, this.AddRuntimeMessage);
        }

        private sealed class McNeelForumPostOpenWorker : AsyncWorkerBase
        {
            private readonly McNeelForumPostOpenComponent parent;
            private string postJson;
            private bool hasWork;

            private string resultUrl;

            public McNeelForumPostOpenWorker(
                McNeelForumPostOpenComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localJson = null;
                DA.GetData(0, ref localJson);

                this.postJson = localJson ?? string.Empty;
                this.hasWork = !string.IsNullOrWhiteSpace(this.postJson);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "McNeelForum Post is required.");
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
                    // Parse JSON
                    var obj = JObject.Parse(this.postJson);

                    int topicId = obj["topic_id"]?.Value<int?>() ?? 0;
                    int? postNumber = obj["post_number"]?.Value<int?>();
                    string postUrl = string.Empty;

                    if (topicId > 0)
                    {
                        if (postNumber.HasValue && postNumber.Value > 0)
                        {
                            postUrl = $"https://discourse.mcneel.com/t/{topicId}/{postNumber.Value}";
                        }
                        else
                        {
                            postUrl = $"https://discourse.mcneel.com/t/{topicId}";
                        }
                    }
                    else
                    {
                        string rawPostUrl = obj["post_url"]?.Value<string>() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(rawPostUrl))
                        {
                            if (rawPostUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                postUrl = rawPostUrl;
                            }
                            else
                            {
                                postUrl = $"https://discourse.mcneel.com{(rawPostUrl.StartsWith("/") ? string.Empty : "/")}{rawPostUrl}";
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(postUrl))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not determine post URL from JSON.");
                        this.resultUrl = string.Empty;
                        return;
                    }

                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = postUrl,
                            UseShellExecute = true,
                        };

                        Process.Start(psi);
                        this.resultUrl = postUrl;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[McNeelForumPostOpenWorker] Error opening browser: {ex.Message}");
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                        this.resultUrl = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[McNeelForumPostOpenWorker] Error parsing JSON: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                    this.resultUrl = string.Empty;
                }

                await Task.CompletedTask;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = string.IsNullOrWhiteSpace(this.resultUrl) ? "No URL opened" : "Post opened";
            }
        }
    }
}
