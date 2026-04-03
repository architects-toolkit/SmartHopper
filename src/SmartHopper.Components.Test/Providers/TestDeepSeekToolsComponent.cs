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
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Providers.DeepSeek;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for DeepSeek tool encoding and parsing.
    /// </summary>
    public class TestDeepSeekToolsComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("9CD3C815-439A-4F4B-B9E4-998833232858");
        protected override string ComponentName => "Test DeepSeek Tools";
        protected override string ComponentDescription => "Tests DeepSeek tool encoding and response parsing";
        protected override string ComponentCategory => "SmartHopper/Test/Providers";
        protected override string ComponentSubCategory => "DeepSeek";

        public TestDeepSeekToolsComponent()
            : base("Test DeepSeek Tools", "TEST-DEEPSEEK-TOOLS", "Tests DeepSeek tool encoding and response parsing", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Forces the DeepSeek provider for this test component.
        /// </summary>
        protected override SmartHopper.Infrastructure.AIProviders.IAIProvider GetActualAIProvider()
        {
            return new DeepSeekProvider();
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
            private readonly TestDeepSeekToolsComponent _parent;

            public Worker(TestDeepSeekToolsComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create test AIRequestCall with tool definitions
                    var call = new AIRequestCall();
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.Context,
                        Content = "You have access to tools."
                    });

                    // Add tool call
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.ToolCall,
                        Content = "Calling get_weather tool",
                        ToolCalls = new List<AIToolCall>
                        {
                            new AIToolCall
                            {
                                Id = "call_weather_123",
                                Name = "get_weather",
                                Arguments = "{\"location\": \"Beijing\"}"
                            }
                        }
                    });

                    // Add tool result
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.ToolResult,
                        Content = "Weather in Beijing: 65°F, Clear",
                        ToolCallId = "call_weather_123"
                    });

                    // Encode using DeepSeek provider
                    var provider = new DeepSeekProvider();
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

                    if (!encoded.Contains("\"tool_calls\""))
                    {
                        _messages.Add(new GH_String("Missing tool_calls array in encoding"));
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

                    if (!encoded.Contains("\"tool_call_id\""))
                    {
                        _messages.Add(new GH_String("Missing tool_call_id in tool result"));
                        _encodingSuccess = new GH_Boolean(false);
                        _parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    encodingSuccess = true;
                    _messages.Add(new GH_String("Tool encoding successful"));
                    _messages.Add(new GH_String("- Tool calls array present"));
                    _messages.Add(new GH_String("- Tool name 'get_weather' encoded"));
                    _messages.Add(new GH_String("- Tool call ID present in result"));

                    // Verify parsing would work (basic structure check)
                    if (encoded.Contains("\"role\":\"assistant\"") && 
                        encoded.Contains("\"role\":\"tool\""))
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
                message = _encodingSuccess.Value && _parsingSuccess.Value ? "DeepSeek tools test passed" : "DeepSeek tools test failed";
            }
        }
    }
}
