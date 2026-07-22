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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for MistralAI tool encoding and parsing.
    /// </summary>
    public class TestMistralAIToolsComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("5160C10D-CB11-48EA-9CAB-099F56D560AC");

        public TestMistralAIToolsComponent()
            : base("Test MistralAI Tools", "TEST-MISTRAL-TOOLS", "Tests MistralAI tool encoding and response parsing", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("MistralAI");
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
            private readonly TestMistralAIToolsComponent _parent;

            public Worker(TestMistralAIToolsComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                        Content = "You have access to tools."
                    });

                    // Add tool call
                    bodyBuilder.Add(new AIInteractionToolCall
                    {
                        Id = "call_weather_123",
                        Name = "get_weather",
                        Arguments = JObject.Parse("{\"location\": \"Paris\"}")
                    });

                    // Add tool result
                    bodyBuilder.Add(new AIInteractionToolResult
                    {
                        Result = new JObject { ["content"] = "Weather in Paris: 68°F, Cloudy" },
                        Id = "call_weather_123"
                    });

                    var call = new AIRequestCall();
                    call.Body = bodyBuilder.Build();

                    // Encode using provider from parent component
                    var provider = this._parent.GetActualAIProvider();
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

                    if (!encoded.Contains("\"tool_calls\""))
                    {
                        this._messages.Add(new GH_String("Missing tool_calls array in encoding"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"get_weather\""))
                    {
                        this._messages.Add(new GH_String("Tool name not found in encoding"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"tool_call_id\""))
                    {
                        this._messages.Add(new GH_String("Missing tool_call_id in tool result"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    encodingSuccess = true;
                    this._messages.Add(new GH_String("Tool encoding successful"));
                    this._messages.Add(new GH_String("- Tool calls array present"));
                    this._messages.Add(new GH_String("- Tool name 'get_weather' encoded"));
                    this._messages.Add(new GH_String("- Tool call ID present in result"));

                    // Verify parsing would work (basic structure check)
                    if (encoded.Contains("\"role\":\"assistant\"") &&
                        encoded.Contains("\"role\":\"tool\""))
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
                message = this._encodingSuccess.Value && this._parsingSuccess.Value ? "MistralAI tools test passed" : "MistralAI tools test failed";
            }
        }
    }
}
