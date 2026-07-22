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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Providers.DeepSeek;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for DeepSeek vision input handling.
    /// </summary>
    public class TestDeepSeekVisionComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("43A8D588-79AD-4F81-B92A-2FC8CEA04682");

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestDeepSeekVisionComponent()
            : base("Test DeepSeek Vision", "TEST-DEEPSEEK-VISION", "Tests DeepSeek vision API call with image input", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("DeepSeek");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "Test passed", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestDeepSeekVisionComponent _parent;

            public Worker(TestDeepSeekVisionComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    // Create test AIRequestCall with image content
                    var call = new AIRequestCall();

                    // Hardcoded minimal base64 PNG (1x1 transparent pixel)
                    const string base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwAhgGAWjR9awAAAABJRU5ErkJggg==";

                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.Context,
                        Content = "Analyze this image"
                    });
                    builder.Add(new AIInteractionImage
                    {
                        ImageData = base64Image
                    });
                    call.Body = builder.Build();

                    // Encode using DeepSeek provider
                    var provider = AIProvider<DeepSeekProvider>.Instance;
                    var encoded = provider.Encode(call);

                    // Verify encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Encoded message is empty"));
                        await Task.Yield();
                        return;
                    }

                    // Check for image content in encoding
                    if (!encoded.Contains("\"url\"") && !encoded.Contains("\"image_url\""))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing image URL or image_url in encoding"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("image/png") && !encoded.Contains("image/jpeg"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing image MIME type in encoding"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("base64"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing base64 encoding marker"));
                        await Task.Yield();
                        return;
                    }

                    this._success = new GH_Boolean(true);
                    this._messages.Add(new GH_String("DeepSeek vision encoding successful"));
                    this._messages.Add(new GH_String("- Image URL present"));
                    this._messages.Add(new GH_String("- MIME type correctly set"));
                    this._messages.Add(new GH_String("- Base64 encoding marker present"));
                }
                catch (Exception ex)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Success", this._success, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._success.Value ? "DeepSeek vision test passed" : "DeepSeek vision test failed";
            }
        }
    }
}
