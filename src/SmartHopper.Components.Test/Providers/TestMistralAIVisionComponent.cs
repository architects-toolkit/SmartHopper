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
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.Providers.MistralAI;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for MistralAI vision input handling.
    /// </summary>
    public class TestMistralAIVisionComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("B2C7D4E8-1234-4ABC-9DEF-123456789ABC");

        public TestMistralAIVisionComponent()
            : base("Test MistralAI Vision", "TEST-MISTRAL-VISION", "Tests MistralAI vision API call with image input", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("MistralAI");
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
            private readonly TestMistralAIVisionComponent _parent;

            public Worker(TestMistralAIVisionComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Encode using MistralAI provider
                    var provider = AIProvider<MistralAIProvider>.Instance;
                    var encoded = provider.Encode(call);

                    // Verify encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Encoded message is empty"));
                        await Task.Yield();
                        return;
                    }

                    // Check for image content in encoding by parsing JSON
                    var json = JObject.Parse(encoded);
                    var messages = json["messages"] as JArray;
                    var contentBlocks = messages?.SelectMany(m => m["content"] as JArray ?? new JArray()).ToList() ?? new List<JToken>();

                    var imageBlock = contentBlocks.FirstOrDefault(c => c["type"]?.ToString() == "image_url");
                    if (imageBlock == null)
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing image_url content block"));
                        await Task.Yield();
                        return;
                    }

                    var imageUrl = imageBlock["image_url"]?["url"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing image URL in image_url block"));
                        await Task.Yield();
                        return;
                    }

                    if (!imageUrl.StartsWith("data:image/png;base64,") && !imageUrl.StartsWith("data:image/jpeg;base64,"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Image URL is not a data URI with expected MIME type"));
                        await Task.Yield();
                        return;
                    }

                    if (!imageUrl.Contains("base64"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing base64 encoding marker in image URL"));
                        await Task.Yield();
                        return;
                    }

                    this._success = new GH_Boolean(true);
                    this._messages.Add(new GH_String("MistralAI vision encoding successful"));
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
                message = this._success.Value ? "MistralAI vision test passed" : "MistralAI vision test failed";
            }
        }
    }
}
