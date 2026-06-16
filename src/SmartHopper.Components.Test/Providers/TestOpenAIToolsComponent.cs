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
    /// Test component for OpenAI tool encoding and parsing.
    /// </summary>
    public class TestOpenAIToolsComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("63FEAE05-B951-4642-8BC4-8D5F1A1FAAD5");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public TestOpenAIToolsComponent()
            : base("Test OpenAI Tools", "TEST-OPENAI-TOOLS", "Tests OpenAI tool encoding and response parsing", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("OpenAI");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Responses Tool Encoding Success", "RTES", "Tool encoding on Responses API succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Responses Tool Parsing Success", "RTPS", "Tool result parsing on Responses API succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Chat Completions Tool Encoding Success", "CCTES", "Tool encoding on Chat Completions succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Chat Completions Tool Parsing Success", "CCTPS", "Tool result parsing on Chat Completions succeeded", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _responsesEncodingSuccess = new GH_Boolean(false);
            private GH_Boolean _responsesParsingSuccess = new GH_Boolean(false);
            private GH_Boolean _ccEncodingSuccess = new GH_Boolean(false);
            private GH_Boolean _ccParsingSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestOpenAIToolsComponent _parent;

            public Worker(TestOpenAIToolsComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                        this._messages.Add(new GH_String("Provider not found"));
                        await Task.Yield();
                        return;
                    }

                    // Create test interactions with tool definitions using AIBodyBuilder
                    var bodyBuilder = AIBodyBuilder.Create();
                    bodyBuilder.Add(new AIInteractionText { Agent = AIAgent.System, Content = "You have access to tools." });
                    bodyBuilder.Add(new AIInteractionToolCall { Id = "call_weather_123", Name = "get_weather", Arguments = JObject.Parse("{\"location\": \"New York\"}") });
                    bodyBuilder.Add(new AIInteractionToolResult { Result = new JObject { ["content"] = "Weather in New York: 72°F, Sunny" }, Id = "call_weather_123" });
                    var body = bodyBuilder.Build();

                    // ==========================================
                    // TEST 1: Responses API Tools (Default) - TESTED FIRST
                    // ==========================================
                    this._messages.Add(new GH_String("=== Test 1: Responses API Tools ==="));
                    var responsesCall = new AIRequestCall();
                    responsesCall.Body = body;
                    responsesCall.Initialize("OpenAI", "gpt-5.4-mini", responsesCall.Body, "/responses", AICapability.Text2Text, "*");
                    responsesCall = provider.PreCall(responsesCall);

                    var responsesEncoded = provider.Encode(responsesCall);
                    if (string.IsNullOrEmpty(responsesEncoded))
                    {
                        this._messages.Add(new GH_String("✗ Responses API tool encoded message is empty"));
                    }
                    else
                    {
                        var json = JObject.Parse(responsesEncoded);
                        var input = json["input"] as JArray; // Responses API uses "input"
                        
                        bool hasToolCalls = false;
                        bool hasToolName = false;
                        bool hasToolCallId = false;
                        var roles = new HashSet<string>();

                        if (input != null)
                        {
                            foreach (var message in input)
                            {
                                var role = message["role"]?.ToString();
                                if (!string.IsNullOrEmpty(role)) roles.Add(role);

                                if (role == "assistant")
                                {
                                    var toolCalls = message["tool_calls"] as JArray;
                                    if (toolCalls != null && toolCalls.Any())
                                    {
                                        hasToolCalls = true;
                                        foreach (var tc in toolCalls)
                                        {
                                            if (tc["function"]?["name"]?.ToString() == "get_weather") hasToolName = true;
                                        }
                                    }
                                }
                                if (role == "tool" && !string.IsNullOrEmpty(message["tool_call_id"]?.ToString()))
                                {
                                    hasToolCallId = true;
                                }
                            }
                        }

                        if (hasToolCalls && hasToolName && hasToolCallId)
                        {
                            this._responsesEncodingSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Responses API Tool encoding successful"));
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"✗ Responses API Tool encoding failed (tool_calls={hasToolCalls}, name={hasToolName}, call_id={hasToolCallId})"));
                        }

                        if (roles.Contains("assistant") && roles.Contains("tool"))
                        {
                            this._responsesParsingSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Responses API Tool result parsing structure valid"));
                        }
                    }

                    // ==========================================
                    // TEST 2: Chat Completions Tools (Legacy) - TESTED SECOND
                    // ==========================================
                    this._messages.Add(new GH_String("=== Test 2: Chat Completions Tools ==="));
                    var ccCall = new AIRequestCall();
                    ccCall.Body = body;
                    ccCall.Initialize("OpenAI", "gpt-5.4-mini", ccCall.Body, "/chat/completions", AICapability.Text2Text, "*");
                    ccCall = provider.PreCall(ccCall);

                    var ccEncoded = provider.Encode(ccCall);
                    if (string.IsNullOrEmpty(ccEncoded))
                    {
                        this._messages.Add(new GH_String("✗ Chat Completions tool encoded message is empty"));
                    }
                    else
                    {
                        var json = JObject.Parse(ccEncoded);
                        var messages = json["messages"] as JArray; // Chat Completions uses "messages"
                        
                        bool hasToolCalls = false;
                        bool hasToolName = false;
                        bool hasToolCallId = false;
                        var roles = new HashSet<string>();

                        if (messages != null)
                        {
                            foreach (var message in messages)
                            {
                                var role = message["role"]?.ToString();
                                if (!string.IsNullOrEmpty(role)) roles.Add(role);

                                if (role == "assistant")
                                {
                                    var toolCalls = message["tool_calls"] as JArray;
                                    if (toolCalls != null && toolCalls.Any())
                                    {
                                        hasToolCalls = true;
                                        foreach (var tc in toolCalls)
                                        {
                                            if (tc["function"]?["name"]?.ToString() == "get_weather") hasToolName = true;
                                        }
                                    }
                                }
                                if (role == "tool" && !string.IsNullOrEmpty(message["tool_call_id"]?.ToString()))
                                {
                                    hasToolCallId = true;
                                }
                            }
                        }

                        if (hasToolCalls && hasToolName && hasToolCallId)
                        {
                            this._ccEncodingSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Chat Completions Tool encoding successful"));
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"✗ Chat Completions Tool encoding failed (tool_calls={hasToolCalls}, name={hasToolName}, call_id={hasToolCallId})"));
                        }

                        if (roles.Contains("assistant") && roles.Contains("tool"))
                        {
                            this._ccParsingSuccess = new GH_Boolean(true);
                            this._messages.Add(new GH_String("✓ Chat Completions Tool result parsing structure valid"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    this._responsesEncodingSuccess = new GH_Boolean(false);
                    this._responsesParsingSuccess = new GH_Boolean(false);
                    this._ccEncodingSuccess = new GH_Boolean(false);
                    this._ccParsingSuccess = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Responses Tool Encoding Success", this._responsesEncodingSuccess, DA);
                this._parent.SetPersistentOutput("Responses Tool Parsing Success", this._responsesParsingSuccess, DA);
                this._parent.SetPersistentOutput("Chat Completions Tool Encoding Success", this._ccEncodingSuccess, DA);
                this._parent.SetPersistentOutput("Chat Completions Tool Parsing Success", this._ccParsingSuccess, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                
                bool allPassed = this._responsesEncodingSuccess.Value && this._responsesParsingSuccess.Value &&
                                 this._ccEncodingSuccess.Value && this._ccParsingSuccess.Value;
                message = allPassed ? "OpenAI tools tests passed" : "OpenAI tools tests failed";
            }
        }
    }
}
