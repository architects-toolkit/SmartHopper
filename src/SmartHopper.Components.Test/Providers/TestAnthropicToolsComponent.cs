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

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for Anthropic tool encoding and parsing.
    /// </summary>
    public class TestAnthropicToolsComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("D4E5F6A7-B8C9-0123-DEFA-456789012345");

        public TestAnthropicToolsComponent()
            : base("Test Anthropic Tools", "TEST-ANTHROPIC-TOOLS", "Tests Anthropic tool encoding and response parsing", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Anthropic");
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
            return new Worker(this, AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _encodingSuccess = new GH_Boolean(false);
            private GH_Boolean _parsingSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestAnthropicToolsComponent _parent;

            public Worker(TestAnthropicToolsComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                _parent = parent;
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
                        Arguments = JObject.Parse("{\"location\": \"San Francisco\"}")
                    });

                    // Add tool result
                    bodyBuilder.Add(new AIInteractionToolResult
                    {
                        Result = new JObject { ["content"] = "Weather in San Francisco: 70°F, Partly Cloudy" },
                        Id = "call_weather_123"
                    });

                    var call = new AIRequestCall();
                    call.Body = bodyBuilder.Build();

                    // Encode using provider from parent component
                    var provider = _parent.GetActualAIProvider();
                    var encoded = provider.Encode(call);

                    // Verify tool encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        _messages.Add(new GH_String("Encoded message is empty"));
                        _encodingSuccess = new GH_Boolean(false);
                        _parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"get_weather\""))
                    {
                        _messages.Add(new GH_String("Tool name not found in encoding"));
                        _encodingSuccess = new GH_Boolean(false);
                        _parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    encodingSuccess = true;
                    _messages.Add(new GH_String("Tool encoding successful"));
                    _messages.Add(new GH_String("- Tool name 'get_weather' encoded"));

                    // Verify parsing would work (basic structure check)
                    if (encoded.Contains("\"role\":\"assistant\"") &&
                        encoded.Contains("\"role\":\"user\""))
                    {
                        parsingSuccess = true;
                        _messages.Add(new GH_String("Tool result parsing structure valid"));
                    }
                    else
                    {
                        _messages.Add(new GH_String("Tool result parsing structure invalid"));
                    }

                    _encodingSuccess = new GH_Boolean(encodingSuccess);
                    _parsingSuccess = new GH_Boolean(parsingSuccess);
                }
                catch (Exception ex)
                {
                    _encodingSuccess = new GH_Boolean(false);
                    _parsingSuccess = new GH_Boolean(false);
                    _messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Encoding Success", _encodingSuccess, DA);
                _parent.SetPersistentOutput("Parsing Success", _parsingSuccess, DA);
                _parent.SetPersistentOutput("Messages", _messages, DA);
                message = _encodingSuccess.Value && _parsingSuccess.Value ? "Anthropic tools test passed" : "Anthropic tools test failed";
            }
        }
    }
}
