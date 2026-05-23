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
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Opens the LadybugForum page for a given post JSON in the default browser.
    /// </summary>
    public class LadybugForumPostOpenComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("C4D5E6F7-A8B9-4C0D-1E2F-3A4B5C6D7E8F");

        protected override Bitmap Icon => Resources.ladybugpostopen;

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public LadybugForumPostOpenComponent()
            : base(
                  "LadybugForum Post Open",
                  "LadybugPostOpen",
                  "Open a Ladybug Tools Discourse forum post in the default web browser by its numeric ID.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "LadybugForum Post",
                "LbP",
                "JSON object representing the full forum post (e.g. from LadybugForumPostGetComponent).",
                GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new LadybugForumPostOpenWorker(this, this.AddRuntimeMessage);
        }

        private sealed class LadybugForumPostOpenWorker : AsyncWorkerBase
        {
            private readonly LadybugForumPostOpenComponent parent;
            private string postJson;
            private bool hasWork;

            private string resultUrl;

            public LadybugForumPostOpenWorker(
                LadybugForumPostOpenComponent parent,
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
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "LadybugForum Post is required.");
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
                            postUrl = $"https://discourse.ladybug.tools/t/{topicId}/{postNumber.Value}";
                        }
                        else
                        {
                            postUrl = $"https://discourse.ladybug.tools/t/{topicId}";
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
                                postUrl = $"https://discourse.ladybug.tools{(rawPostUrl.StartsWith("/") ? string.Empty : "/")}{rawPostUrl}";
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(postUrl))
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, "Could not determine post URL from JSON.");
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
                        Debug.WriteLine($"[LadybugForumPostOpenWorker] Error opening browser: {ex.Message}");
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                        this.resultUrl = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LadybugForumPostOpenWorker] Error parsing JSON: {ex.Message}");
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                    this.resultUrl = string.Empty;
                }

                await Task.CompletedTask.ConfigureAwait(false);
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                message = string.IsNullOrWhiteSpace(this.resultUrl) ? "No URL opened" : "Post opened";
            }
        }
    }
}
