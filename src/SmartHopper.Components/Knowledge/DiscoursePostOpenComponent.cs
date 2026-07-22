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
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Opens the Discourse forum page for a given post JSON in the default browser.
    /// Works with any Discourse instance.
    /// </summary>
    public class DiscoursePostOpenComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("D5EB98E2-27BF-4692-BEC7-DA1CE1CE49DB");

        protected override Bitmap Icon => Resources.discoursepostopen;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public DiscoursePostOpenComponent()
            : base(
                  "Discourse Post Open",
                  "DiscoursePostOpen",
                  "Open a Discourse forum post in the default web browser by its numeric ID from any Discourse instance.",
                  "SmartHopper",
                  "Knowledge")
        {
            // Set RunOnlyOnInputChanges to false to ensure the component always runs when the Run parameter is true
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Base URL",
                "U",
                "Base URL of the Discourse forum (e.g., https://discourse.mcneel.com or https://discourse.ladybug.tools). If empty, will try to extract from post JSON.",
                GH_ParamAccess.item,
                string.Empty);
            pManager.AddTextParameter(
                "Discourse Post",
                "P",
                "JSON object representing the full forum post (e.g. from DiscoursePostGetComponent).",
                GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new DiscoursePostOpenWorker(this, this.AddRuntimeMessage);
        }

        private sealed class DiscoursePostOpenWorker : AsyncWorkerBase
        {
            private readonly DiscoursePostOpenComponent parent;
            private string baseUrl;
            private string postJson;
            private bool hasWork;

            private string resultUrl;

            public DiscoursePostOpenWorker(
                DiscoursePostOpenComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string localBaseUrl = string.Empty;
                DA.GetData(0, ref localBaseUrl);

                string localJson = null;
                DA.GetData(1, ref localJson);

                this.baseUrl = localBaseUrl ?? string.Empty;
                this.postJson = localJson ?? string.Empty;
                this.hasWork = !string.IsNullOrWhiteSpace(this.postJson);
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Discourse Post is required.");
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

                    // Use provided base URL or try to extract from post data
                    string effectiveBaseUrl = this.baseUrl;
                    if (string.IsNullOrWhiteSpace(effectiveBaseUrl))
                    {
                        // Try to get from post_url or use default
                        string rawPostUrl = obj["post_url"]?.Value<string>() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(rawPostUrl))
                        {
                            if (rawPostUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                // Extract base URL from full URL
                                try
                                {
                                    var uri = new Uri(rawPostUrl);
                                    effectiveBaseUrl = $"{uri.Scheme}://{uri.Host}";
                                }
                                catch
                                {
                                    effectiveBaseUrl = string.Empty;
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(effectiveBaseUrl))
                        {
                            this.CollectMessage(SHRuntimeMessageSeverity.Error, "Could not determine base URL. Please provide Base URL input.");
                            this.resultUrl = string.Empty;
                            return;
                        }
                    }

                    // Remove trailing slash
                    effectiveBaseUrl = effectiveBaseUrl.TrimEnd('/');

                    if (topicId > 0)
                    {
                        if (postNumber.HasValue && postNumber.Value > 0)
                        {
                            postUrl = $"{effectiveBaseUrl}/t/{topicId}/{postNumber.Value}";
                        }
                        else
                        {
                            postUrl = $"{effectiveBaseUrl}/t/{topicId}";
                        }
                    }
                    else
                    {
                        // Fallback to post_url if available
                        string rawPostUrl = obj["post_url"]?.Value<string>() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(rawPostUrl))
                        {
                            if (rawPostUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                postUrl = rawPostUrl;
                            }
                            else
                            {
                                postUrl = $"{effectiveBaseUrl}{(rawPostUrl.StartsWith("/") ? string.Empty : "/")}{rawPostUrl}";
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
                        Debug.WriteLine($"[DiscoursePostOpenWorker] Error opening browser: {ex.Message}");
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                        this.resultUrl = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscoursePostOpenWorker] Error parsing JSON: {ex.Message}");
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
