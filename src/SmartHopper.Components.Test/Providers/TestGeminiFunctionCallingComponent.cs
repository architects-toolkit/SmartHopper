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
using SmartHopper.Providers.GoogleGemini;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for Google Gemini function calling.
    /// </summary>
    public class TestGeminiFunctionCallingComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("8A175DAD-66A4-4153-9D28-87B46DAA6DDC");
        protected override string ComponentName => "Test Gemini Function Calling";
        protected override string ComponentDescription => "Tests Google Gemini function calling encoding and response parsing";
        protected override string ComponentCategory => "SmartHopper/Test/Providers";
        protected override string ComponentSubCategory => "Gemini";

        public TestGeminiFunctionCallingComponent()
            : base("Test Gemini Function Calling", "TEST-GEMINI-FUNC", "Tests Google Gemini function calling encoding and response parsing", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Forces the Google Gemini provider for this test component.
        /// </summary>
        protected override SmartHopper.Infrastructure.AIProviders.IAIProvider GetActualAIProvider()
        {
            return new GoogleGeminiProvider();
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Encoding Success", "ES", "Function encoding succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Parsing Success", "PS", "Function result parsing succeeded", GH_ParamAccess.item);
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
            private readonly TestGeminiFunctionCallingComponent _parent;

            public Worker(TestGeminiFunctionCallingComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create test AIRequestCall with function definitions
                    var call = new AIRequestCall();
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.Context,
                        Content = "You have access to functions."
                    });

                    // Add function call
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.ToolCall,
                        Content = "Calling calculate_sum function",
                        ToolCalls = new List<AIToolCall>
                        {
                            new AIToolCall
                            {
                                Id = "call_sum_123",
                                Name = "calculate_sum",
                                Arguments = "{\"numbers\": [1, 2, 3]}"
                            }
                        }
                    });

                    // Add function result
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.ToolResult,
                        Content = "Sum result: 6",
                        ToolCallId = "call_sum_123"
                    });

                    // Encode using Gemini provider
                    var provider = new GoogleGeminiProvider();
                    var encoded = provider.Encode(call);

                    // Verify function encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        _messages.Add(new GH_String("Encoded message is empty"));
                        _encodingSuccess = new GH_Boolean(false);
                        _parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"function_calls\"") && !encoded.Contains("\"functionCalls\""))
                    {
                        _messages.Add(new GH_String("Missing function_calls in encoding"));
                        _encodingSuccess = new GH_Boolean(false);
                        _parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"calculate_sum\""))
                    {
                        _messages.Add(new GH_String("Function name not found in encoding"));
                        _encodingSuccess = new GH_Boolean(false);
                        _parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    encodingSuccess = true;
                    _messages.Add(new GH_String("Function encoding successful"));
                    _messages.Add(new GH_String("- Function calls present"));
                    _messages.Add(new GH_String("- Function name 'calculate_sum' encoded"));

                    // Verify parsing would work (basic structure check)
                    if (encoded.Contains("\"role\":\"model\"") && 
                        encoded.Contains("\"role\":\"function\""))
                    {
                        parsingSuccess = true;
                        _messages.Add(new GH_String("Function result parsing structure valid"));
                    }
                    else
                    {
                        _messages.Add(new GH_String("Function result parsing structure invalid"));
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
                message = _encodingSuccess.Value && _parsingSuccess.Value ? "Gemini function calling test passed" : "Gemini function calling test failed";
            }
        }
    }
}
