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
    /// Test component for Google Gemini message encoding.
    /// </summary>
    public class TestGeminiEncodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("BFA00A8F-3DA2-4261-87A6-DBCE0E861188");

        public TestGeminiEncodeComponent()
            : base("Test Gemini Encode", "TEST-GEMINI-ENC", "Tests Gemini message encoding from AIRequestCall", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Gemini");
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
            return new Worker(this, AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestGeminiEncodeComponent _parent;

            public Worker(TestGeminiEncodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    // Create test AIRequestCall with different message types using AIBodyBuilder
                    var bodyBuilder = AIBodyBuilder.Create();
                    
                    // Add Context message (maps to user in Gemini)
                    bodyBuilder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.Context,
                        Content = "You are a helpful assistant."
                    });

                    // Add ToolCall message (Gemini uses function calls)
                    bodyBuilder.Add(new AIInteractionToolCall
                    {
                        Id = "call_123",
                        Name = "test_function",
                        Arguments = JObject.Parse("{\"param\": \"value\"}")
                    });

                    // Add ToolResult message
                    bodyBuilder.Add(new AIInteractionToolResult
                    {
                        Result = new JObject { ["content"] = "Tool result" },
                        Id = "call_123"
                    });

                    var call = new AIRequestCall();
                    call.Body = bodyBuilder.Build();

                    // Encode using provider from parent component
                    var provider = _parent.GetActualAIProvider();
                    var encoded = provider.Encode(call);

                    // Verify encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Encoded message is empty"));
                        await Task.Yield();
                        return;
                    }

                    // Check for required role mappings (Gemini uses user, model, function)
                    if (!encoded.Contains("\"role\":\"user\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing user role (Context message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"role\":\"model\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing model role (ToolCall message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"role\":\"function\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing function role (ToolResult message)"));
                        await Task.Yield();
                        return;
                    }

                    _success = new GH_Boolean(true);
                    _messages.Add(new GH_String("Gemini encoding successful"));
                    _messages.Add(new GH_String($"Encoded message length: {encoded.Length}"));
                }
                catch (Exception ex)
                {
                    _success = new GH_Boolean(false);
                    _messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Success", _success, DA);
                _parent.SetPersistentOutput("Messages", _messages, DA);
                message = _success.Value ? "Gemini encoding test passed" : "Gemini encoding test failed";
            }
        }
    }
}
