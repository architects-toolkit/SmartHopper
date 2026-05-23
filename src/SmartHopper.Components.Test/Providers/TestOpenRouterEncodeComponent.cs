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
    /// Test component for OpenRouter message encoding.
    /// </summary>
    public class TestOpenRouterEncodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("04900353-70D4-4F0E-96E1-5EE2BD0C2418");

        public override GH_Exposure Exposure => GH_Exposure.octonary;

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
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestOpenRouterEncodeComponent _parent;

            public Worker(TestOpenRouterEncodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    // Create test AIRequestCall with different message types using AIBodyBuilder
                    var bodyBuilder = AIBodyBuilder.Create();

                    // Add System message (maps to system in OpenRouter)
                    bodyBuilder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.System,
                        Content = "You are a helpful assistant."
                    });

                    // Add User message (maps to user in OpenRouter)
                    bodyBuilder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "Hello, how are you?"
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
                    call.Initialize("OpenRouter", "anthropic/claude-3.5-sonnet", call.Body, "/chat/completions", AICapability.Text2Text);

                    // Encode using provider from parent component
                    var provider = this._parent.GetActualAIProvider();
                    if (provider == null)
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Provider not found"));
                        await Task.Yield();
                        return;
                    }
                    var encoded = provider.Encode(call);

                    // Verify encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Encoded message is empty"));
                        await Task.Yield();
                        return;
                    }

                    // Check for required role mappings (OpenRouter uses system, user, assistant, tool)
                    var json = JObject.Parse(encoded);
                    var messages = json["messages"] as JArray;
                    var roles = messages?.Select(m => m["role"]?.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (!roles.Contains("system"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing system role (System message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!roles.Contains("user"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing user role (User message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!roles.Contains("assistant"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing assistant role (ToolCall message)"));
                        await Task.Yield();
                        return;
                    }

                    if (!roles.Contains("tool"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Missing tool role (ToolResult message)"));
                        await Task.Yield();
                        return;
                    }

                    this._success = new GH_Boolean(true);
                    this._messages.Add(new GH_String("OpenRouter encoding successful"));
                    this._messages.Add(new GH_String($"Encoded message length: {encoded.Length}"));
                }
                catch (Exception ex)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Success", this._success, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._success.Value ? "OpenRouter encoding test passed" : "OpenRouter encoding test failed";
            }
        }
    }
}
