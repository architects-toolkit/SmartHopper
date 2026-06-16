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
    /// Test component for Google Gemini tool encoding and parsing.
    /// </summary>
    public class TestGeminiToolsComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("4A7B2C9D-1E3F-4A5B-8C6D-9E0F1A2B3C4D");

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestGeminiToolsComponent()
            : base("Test Gemini Tools", "TEST-GEMINI-TOOLS", "Tests Gemini tool encoding and response parsing", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Gemini");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Encoding Success", "ES", "Tool encoding succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Parsing Success", "PS", "Tool result parsing succeeded", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _encodingSuccess = new GH_Boolean(false);
            private GH_Boolean _parsingSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestGeminiToolsComponent _parent;

            public Worker(TestGeminiToolsComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    bool encodingSuccess = false;
                    bool parsingSuccess = false;

                    // Create test AIRequestCall with tool definitions using AIBodyBuilder
                    var bodyBuilder = AIBodyBuilder.Create();

                    bodyBuilder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.System,
                        Content = "You have access to tools.",
                    });

                    // Add tool call
                    bodyBuilder.Add(new AIInteractionToolCall
                    {
                        Id = "call_weather_123",
                        Name = "get_weather",
                        Arguments = JObject.Parse("{\"location\": \"Beijing\"}"),
                    });

                    // Add tool result
                    bodyBuilder.Add(new AIInteractionToolResult
                    {
                        Result = new JObject { ["content"] = "Weather in Beijing: 65°F, Clear" },
                        Id = "call_weather_123",
                    });

                    var call = new AIRequestCall();
                    call.Body = bodyBuilder.Build();
                    call.Initialize("Gemini", "gemini-2.0-flash", call.Body, "/models/gemini-2.0-flash:generateContent", AICapability.Text2Text, "*");

                    // Encode using provider from parent component
                    var provider = this._parent.GetActualAIProvider();
                    if (provider == null)
                    {
                        this._messages.Add(new GH_String("Provider not found"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    var encoded = provider.Encode(call);

                    // Verify tool encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        this._messages.Add(new GH_String("Encoded message is empty"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    // Parse JSON and verify Gemini tool structure
                    var encodedJson = JObject.Parse(encoded);
                    var contents = encodedJson["contents"] as JArray;
                    if (contents == null)
                    {
                        this._messages.Add(new GH_String("Missing contents array"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    bool hasFunctionCall = false;
                    bool hasFunctionResponse = false;
                    var roles = new HashSet<string>();

                    foreach (var content in contents)
                    {
                        var role = content["role"]?.ToString();
                        if (!string.IsNullOrEmpty(role))
                        {
                            roles.Add(role);
                        }

                        var parts = content["parts"] as JArray;
                        if (parts != null)
                        {
                            foreach (var part in parts)
                            {
                                // Check for functionCall in model messages
                                if (part["functionCall"] != null)
                                {
                                    hasFunctionCall = true;
                                    var functionName = part["functionCall"]?["name"]?.ToString();
                                    System.Diagnostics.Debug.WriteLine($"[TestGeminiTools] Found functionCall: {functionName}");
                                }

                                // Check for functionResponse in user messages
                                if (part["functionResponse"] != null)
                                {
                                    hasFunctionResponse = true;
                                    var responseId = part["functionResponse"]?["id"]?.ToString();
                                    System.Diagnostics.Debug.WriteLine($"[TestGeminiTools] Found functionResponse: {responseId}");
                                }
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[TestGeminiTools] Found roles: {string.Join(", ", roles)}");
                    System.Diagnostics.Debug.WriteLine($"[TestGeminiTools] Tool checks - functionCall: {hasFunctionCall}, functionResponse: {hasFunctionResponse}");

                    if (!hasFunctionCall)
                    {
                        this._messages.Add(new GH_String("Missing functionCall in encoded content"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!hasFunctionResponse)
                    {
                        this._messages.Add(new GH_String("Missing functionResponse in encoded content"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    encodingSuccess = true;
                    this._messages.Add(new GH_String("Tool encoding successful"));
                    this._messages.Add(new GH_String("- functionCall present in content"));
                    this._messages.Add(new GH_String("- functionResponse present in content"));

                    // Verify parsing would work (basic structure check)
                    bool hasModel = roles.Contains("model");
                    bool hasUser = roles.Contains("user");

                    if (hasModel && hasUser)
                    {
                        parsingSuccess = true;
                        this._messages.Add(new GH_String("Tool result parsing structure valid"));
                    }
                    else
                    {
                        this._messages.Add(new GH_String("Tool result parsing structure invalid"));
                    }

                    this._encodingSuccess = new GH_Boolean(encodingSuccess);
                    this._parsingSuccess = new GH_Boolean(parsingSuccess);
                }
                catch (Exception ex)
                {
                    this._encodingSuccess = new GH_Boolean(false);
                    this._parsingSuccess = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Encoding Success", this._encodingSuccess, DA);
                this._parent.SetPersistentOutput("Parsing Success", this._parsingSuccess, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._encodingSuccess.Value && this._parsingSuccess.Value ? "Gemini tools test passed" : "Gemini tools test failed";
            }
        }
    }
}
