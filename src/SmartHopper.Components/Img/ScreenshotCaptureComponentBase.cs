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

#pragma warning disable CA1031

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Parameters;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Shared stateful component pipeline for screenshot AITools.
    /// </summary>
    public abstract class ScreenshotCaptureComponentBase : StatefulComponentBase
    {
        private readonly string toolName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenshotCaptureComponentBase"/> class.
        /// </summary>
        protected ScreenshotCaptureComponentBase(
            string name,
            string nickname,
            string description,
            string toolName)
            : base(name, nickname, description, "SmartHopper", "Img")
        {
            this.toolName = toolName;
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            ArgumentNullException.ThrowIfNull(pManager);

            pManager.AddParameter(
                new VersatileImageParameter(),
                "Image",
                "I",
                "Captured PNG as a persistent VersatileImage.",
                GH_ParamAccess.item);
            pManager.AddIntegerParameter("Width", "W", "Captured image width in pixels.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Height", "H", "Captured image height in pixels.", GH_ParamAccess.item);
            this.RegisterCaptureOutputParams(pManager);
        }

        /// <summary>
        /// Registers outputs specific to the concrete capture tool.
        /// </summary>
        protected virtual void RegisterCaptureOutputParams(GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// Reads component inputs into the screenshot tool's arguments.
        /// </summary>
        protected abstract JObject BuildCaptureArguments(IGH_DataAccess dataAccess);

        /// <summary>
        /// Persists outputs specific to the concrete capture tool.
        /// </summary>
        protected virtual void SetCaptureOutputs(JObject result, IGH_DataAccess dataAccess)
        {
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new ScreenshotCaptureWorker(this, this.AddRuntimeMessage);
        }

        private sealed class ScreenshotCaptureWorker : AsyncWorkerBase
        {
            private readonly ScreenshotCaptureComponentBase parent;
            private JObject? arguments;
            private GH_VersatileImage? image;
            private JObject? result;

            public ScreenshotCaptureWorker(
                ScreenshotCaptureComponentBase parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this.arguments = this.parent.BuildCaptureArguments(DA);
                this.image = null;
                this.result = null;
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var interaction = new AIInteractionToolCall
                    {
                        Name = this.parent.toolName,
                        Arguments = this.arguments ?? new JObject(),
                        Agent = AIAgent.Assistant,
                    };
                    var toolCall = new AIToolCall
                    {
                        Endpoint = this.parent.toolName,
                        CancellationToken = token,
                        SkipMetricsValidation = true,
                    };
                    toolCall.FromToolCallInteraction(interaction);

                    var aiResult = await toolCall.Exec(token).ConfigureAwait(false);
                    foreach (var runtimeMessage in aiResult.Messages ?? Enumerable.Empty<SHRuntimeMessage>())
                    {
                        this.CollectMessage(runtimeMessage);
                    }

                    var toolResult = ToolCallResult.FromAIReturn(aiResult);
                    if (!toolResult.Success || toolResult.Result == null)
                    {
                        this.CollectMessage(
                            SHRuntimeMessageSeverity.Error,
                            $"Tool '{this.parent.toolName}' returned no screenshot.",
                            SHRuntimeMessageOrigin.Tool);
                        return;
                    }

                    string imageBase64 = toolResult["imageBase64"]?.ToString() ?? string.Empty;
                    string mimeType = toolResult["mimeType"]?.ToString() ?? "image/png";
                    if (string.IsNullOrWhiteSpace(imageBase64))
                    {
                        this.CollectMessage(
                            SHRuntimeMessageSeverity.Error,
                            $"Tool '{this.parent.toolName}' returned empty image data.",
                            SHRuntimeMessageOrigin.Tool);
                        return;
                    }

                    this.image = new GH_VersatileImage(VersatileImage.FromBase64(imageBase64, mimeType));
                    this.result = toolResult.Result;
                }
                catch (OperationCanceledException)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Warning, "Screenshot capture was cancelled.");
                }
                catch (Exception ex)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, $"Screenshot capture failed: {ex.Message}");
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (this.image == null || this.result == null)
                {
                    message = "Screenshot not captured";
                    return;
                }

                int width = this.result["width"]?.Value<int>() ?? 0;
                int height = this.result["height"]?.Value<int>() ?? 0;
                this.parent.SetPersistentOutput("Image", this.image, DA);
                this.parent.SetPersistentOutput("Width", width, DA);
                this.parent.SetPersistentOutput("Height", height, DA);
                this.parent.SetCaptureOutputs(this.result, DA);
                message = $"Captured {width} × {height}";
            }
        }
    }
}
