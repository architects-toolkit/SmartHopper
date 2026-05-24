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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for OpenAI message encoding.
    /// </summary>
    public class TestOpenAIEncodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("AD538781-65B9-4123-B4EE-874D03BD6FC3");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public TestOpenAIEncodeComponent()
            : base("Test OpenAI Encode", "TEST-OPENAI-ENC", "Tests OpenAI message encoding from AIRequestCall", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("OpenAI");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Responses Success", "RS", "Responses API encoding test passed", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Chat Completions Success", "CCS", "Chat Completions encoding test passed", GH_ParamAccess.item);
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
            private readonly TestOpenAIEncodeComponent _parent;

            public Worker(TestOpenAIEncodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var provider = this._parent.GetActualAIProvider();
                    if (provider == null)
                    {
                        this._responsesSuccess = new GH_Boolean(false);
                        this._chatCompletionsSuccess = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Provider not found"));
                        await Task.Yield();
                        return;
                    }

                    // Create test interactions with different message types using AIBodyBuilder
                    var bodyBuilder = AIBodyBuilder.Create();
                    bodyBuilder.Add(new AIInteractionText { Agent = AIAgent.System, Content = "You are a helpful assistant." });
                    bodyBuilder.Add(new AIInteractionText { Agent = AIAgent.User, Content = "Hello, how are you?" });
                    bodyBuilder.Add(new AIInteractionToolCall { Id = "call_123", Name = "test_tool", Arguments = JObject.Parse("{\"param\": \"value\"}") });
                    bodyBuilder.Add(new AIInteractionToolResult { Result = new JObject { ["content"] = "Tool result" }, Id = "call_123" });
                    var body = bodyBuilder.Build();

                    // ==========================================
                    // TEST 1: Responses API encoding (Default)
                    // ==========================================
                    this._messages.Add(new GH_String("=== Test 1: Responses API Encoding ==="));
                    var responsesCall = new AIRequestCall();
                    responsesCall.Body = body;
                    responsesCall.Initialize("OpenAI", "gpt-5.4-mini", responsesCall.Body, "/responses", AICapability.Text2Text);
                    responsesCall = provider.PreCall(responsesCall);

                    var responsesEncoded = provider.Encode(responsesCall);
                    if (string.IsNullOrEmpty(responsesEncoded))
                    {
                        this._messages.Add(new GH_String("✗ Responses API encoded message is empty"));
                    }
                    else
                    {
                        var json = JObject.Parse(responsesEncoded);
                        var input = json["input"] as JArray; // Responses API uses "input" instead of "messages"
                        var roles = input?.Select(m => m["role"]?.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

                        if (input != null && roles.Contains("system") && roles.Contains("user") && roles.Contains("assistant") && roles.Contains("tool"))
                        {
                            this._responsesSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Responses API encoding successful (uses 'input' array and contains correct roles)"));
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"✗ Responses API encoding invalid. Input present: {input != null}, Roles found: {string.Join(", ", roles)}"));
                        }
                    }

                    // ==========================================
                    // TEST 2: Chat Completions encoding (Legacy)
                    // ==========================================
                    this._messages.Add(new GH_String("=== Test 2: Chat Completions Encoding ==="));
                    var ccCall = new AIRequestCall();
                    ccCall.Body = body;
                    ccCall.Initialize("OpenAI", "gpt-5.4-mini", ccCall.Body, "/chat/completions", AICapability.Text2Text);
                    ccCall = provider.PreCall(ccCall);

                    var ccEncoded = provider.Encode(ccCall);
                    if (string.IsNullOrEmpty(ccEncoded))
                    {
                        this._messages.Add(new GH_String("✗ Chat Completions encoded message is empty"));
                    }
                    else
                    {
                        var json = JObject.Parse(ccEncoded);
                        var messages = json["messages"] as JArray; // Chat Completions uses "messages"
                        var roles = messages?.Select(m => m["role"]?.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

                        if (messages != null && roles.Contains("system") && roles.Contains("user") && roles.Contains("assistant") && roles.Contains("tool"))
                        {
                            this._chatCompletionsSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Chat Completions encoding successful (uses 'messages' array and contains correct roles)"));
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"✗ Chat Completions encoding invalid. Messages present: {messages != null}, Roles found: {string.Join(", ", roles)}"));
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
                message = this._responsesSuccess.Value && this._chatCompletionsSuccess.Value ? "OpenAI encoding tests passed" : "OpenAI encoding tests failed";
            }
        }
    }
}
