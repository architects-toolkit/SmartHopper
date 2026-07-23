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
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.Providers.OpenAI;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for OpenAI vision input handling.
    /// </summary>
    public class TestOpenAIVisionComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("D4E5F6A7-5678-4DEF-A012-345678901234");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public TestOpenAIVisionComponent()
            : base("Test OpenAI Vision", "TEST-OPENAI-VISION", "Tests OpenAI vision API call with image input", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("OpenAI");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Responses Success", "RS", "Responses API vision encoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Chat Completions Success", "CCS", "Chat Completions vision encoding test passed", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _responsesSuccess = new GH_Boolean(false);
            private GH_Boolean _chatCompletionsSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestOpenAIVisionComponent _parent;

            public Worker(TestOpenAIVisionComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    builder.Add(new AIInteractionText { Agent = AIAgent.User, Content = "Analyze this image" });
                    builder.Add(new AIInteractionImage { Agent = AIAgent.User, ImageData = base64Image });
                    call.Body = builder.Build();

                    var provider = AIProvider<OpenAIProvider>.Instance;

                    // ==========================================
                    // TEST 1: Responses API Vision (Default) - TESTED FIRST
                    // ==========================================
                    this._messages.Add(new GH_String("=== Test 1: Responses API Vision ==="));
                    var responsesCall = new AIRequestCall();
                    responsesCall.Body = call.Body;
                    responsesCall.Initialize("OpenAI", "gpt-5.4-mini", responsesCall.Body, "/responses", AICapability.Image2Text);
                    responsesCall = provider.PreCall(responsesCall);

                    var responsesEncoded = provider.Encode(responsesCall);
                    if (string.IsNullOrEmpty(responsesEncoded))
                    {
                        this._messages.Add(new GH_String("✗ Responses API vision encoded message is empty"));
                    }
                    else
                    {
                        var json = JObject.Parse(responsesEncoded);
                        var input = json["input"] as JArray; // Responses API uses "input"
                        var contentBlocks = input?.SelectMany(m => m["content"] as JArray ?? new JArray()).ToList() ?? new List<JToken>();

                        // Responses API uses "input_image" as the type and "image_url" is a string directly
                        var imageBlock = contentBlocks.FirstOrDefault(c => c["type"]?.ToString() == "input_image");
                        var imageUrl = imageBlock?["image_url"]?.ToString() ?? string.Empty;

                        if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("data:image/png;base64,") && imageUrl.Contains("base64"))
                        {
                            this._responsesSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Responses API vision encoding successful (uses 'input_image' and direct 'image_url' string)"));
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"✗ Responses API vision encoding invalid. ImageBlock found: {imageBlock != null}, ImageURL: {imageUrl}"));
                        }
                    }

                    // ==========================================
                    // TEST 2: Chat Completions Vision (Legacy) - TESTED SECOND
                    // ==========================================
                    this._messages.Add(new GH_String("=== Test 2: Chat Completions Vision ==="));
                    var ccCall = new AIRequestCall();
                    ccCall.Body = call.Body;
                    ccCall.Initialize("OpenAI", "gpt-5.4-mini", ccCall.Body, "/chat/completions", AICapability.Image2Text);
                    ccCall = provider.PreCall(ccCall);

                    var ccEncoded = provider.Encode(ccCall);
                    if (string.IsNullOrEmpty(ccEncoded))
                    {
                        this._messages.Add(new GH_String("✗ Chat Completions vision encoded message is empty"));
                    }
                    else
                    {
                        var json = JObject.Parse(ccEncoded);
                        var messages = json["messages"] as JArray; // Chat Completions uses "messages"
                        var contentBlocks = messages?.SelectMany(m => m["content"] as JArray ?? new JArray()).ToList() ?? new List<JToken>();

                        var imageBlock = contentBlocks.FirstOrDefault(c => c["type"]?.ToString() == "image_url");
                        var imageUrl = imageBlock?["image_url"]?["url"]?.ToString() ?? string.Empty;

                        if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("data:image/png;base64,") && imageUrl.Contains("base64"))
                        {
                            this._chatCompletionsSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Chat Completions vision encoding successful"));
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"✗ Chat Completions vision encoding invalid. ImageBlock found: {imageBlock != null}, ImageURL valid: {!string.IsNullOrEmpty(imageUrl)}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    this._responsesSuccess = new GH_Boolean(false);
                    this._chatCompletionsSuccess = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Responses Success", this._responsesSuccess, DA);
                this._parent.SetPersistentOutput("Chat Completions Success", this._chatCompletionsSuccess, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._responsesSuccess.Value && this._chatCompletionsSuccess.Value ? "OpenAI vision tests passed" : "OpenAI vision tests failed";
            }
        }
    }
}
