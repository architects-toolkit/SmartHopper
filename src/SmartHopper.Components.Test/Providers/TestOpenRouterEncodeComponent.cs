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
    /// Test component for OpenRouter message encoding.
    /// </summary>
    public class TestOpenRouterEncodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("E1F2A3B4-C5D6-7890-EFAB-123456789012");

        public TestOpenRouterEncodeComponent()
            : base("Test OpenRouter Encode", "TEST-OPENROUTER-ENC", "Tests OpenRouter message encoding from AIRequestCall", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("OpenRouter");
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
            private readonly TestOpenRouterEncodeComponent _parent;

            public Worker(TestOpenRouterEncodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    
                    // Add Context message
                    bodyBuilder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.System,
                        Content = "You are a helpful assistant."
                    });

                    // Add ToolCall message
                    bodyBuilder.Add(new AIInteractionToolCall
                    {
                        Id = "call_123",
                        Name = "test_tool",
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

                    // Check for required role mappings (OpenRouter uses user, assistant, tool)
                    if (!encoded.Contains("\"role\":\"user\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing user role (Context message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"role\":\"assistant\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing assistant role (ToolCall message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("\"role\":\"tool\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing tool role (ToolResult message)"));
                        await Task.Yield();
                        return;
                    }

                    _success = new GH_Boolean(true);
                    _messages.Add(new GH_String("OpenRouter encoding successful"));
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
                message = _success.Value ? "OpenRouter encoding test passed" : "OpenRouter encoding test failed";
            }
        }
    }
}
