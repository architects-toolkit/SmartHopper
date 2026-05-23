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
    /// Test component for Anthropic tool encoding and parsing.
    /// </summary>
    public class TestAnthropicToolsComponent : AIStatefulAsyncComponentBase
    {

        public override Guid ComponentGuid => new Guid("8C2D73B5-34DA-41A9-BECB-3F7F33766FAF");

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

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
            return new Worker(this, this.AddRuntimeMessage);
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
                        Arguments = JObject.Parse("{\"location\": \"San Francisco\"}"),
                    });

                    // Add tool result
                    bodyBuilder.Add(new AIInteractionToolResult
                    {
                        Result = new JObject { ["content"] = "Weather in San Francisco: 70°F, Partly Cloudy" },
                        Id = "call_weather_123",
                    });

                    var call = new AIRequestCall();
                    call.Body = bodyBuilder.Build();
                    call.Initialize("Anthropic", "claude-haiku-4-5", call.Body, "/v1/messages", AICapability.Text2Text, "*");

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

                    // Parse JSON and verify tool structure
                    var encodedJson = JObject.Parse(encoded);
                    var messages = encodedJson["messages"] as JArray;
                    if (messages == null)
                    {
                        this._messages.Add(new GH_String("Missing messages array"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    // Anthropic uses tool_use blocks in content arrays, not tool_calls
                    bool hasToolUse = false;
                    bool hasToolName = false;
                    bool hasToolResultId = false;
                    var roles = new HashSet<string>();

                    foreach (var message in messages)
                    {
                        var role = message["role"]?.ToString();
                        if (!string.IsNullOrEmpty(role))
                        {
                            roles.Add(role);
                        }

                        // Check for tool_use blocks in assistant messages
                        if (role == "assistant")
                        {
                            var content = message["content"] as JArray;
                            if (content != null)
                            {
                                foreach (var contentItem in content)
                                {
                                    var type = contentItem["type"]?.ToString();
                                    if (type == "tool_use")
                                    {
                                        hasToolUse = true;
                                        var toolName = contentItem["name"]?.ToString();
                                        if (toolName == "get_weather")
                                        {
                                            hasToolName = true;
                                        }
                                    }
                                }
                            }
                        }

                        // Check for tool_result_id in user messages (tool results)
                        if (role == "user")
                        {
                            var content = message["content"] as JArray;
                            if (content != null)
                            {
                                foreach (var contentItem in content)
                                {
                                    var type = contentItem["type"]?.ToString();
                                    if (type == "tool_result")
                                    {
                                        var toolUseId = contentItem["tool_use_id"]?.ToString();
                                        if (!string.IsNullOrEmpty(toolUseId))
                                        {
                                            hasToolResultId = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[TestAnthropicTools] Found roles: {string.Join(", ", roles)}");
                    System.Diagnostics.Debug.WriteLine($"[TestAnthropicTools] Tool checks - tool_use: {hasToolUse}, tool_name: {hasToolName}, tool_result_id: {hasToolResultId}");

                    if (!hasToolUse)
                    {
                        this._messages.Add(new GH_String("Missing tool_use block in encoding"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!hasToolName)
                    {
                        this._messages.Add(new GH_String("Tool name 'get_weather' not found in encoding"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    if (!hasToolResultId)
                    {
                        this._messages.Add(new GH_String("Missing tool_use_id in tool result"));
                        this._encodingSuccess = new GH_Boolean(false);
                        this._parsingSuccess = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    encodingSuccess = true;
                    this._messages.Add(new GH_String("Tool encoding successful"));
                    this._messages.Add(new GH_String("- Tool use block present"));
                    this._messages.Add(new GH_String("- Tool name 'get_weather' encoded"));
                    this._messages.Add(new GH_String("- Tool use ID present in result"));

                    // Verify parsing would work (basic structure check)
                    bool hasAssistant = roles.Contains("assistant");
                    bool hasUser = roles.Contains("user");

                    if (hasAssistant && hasUser)
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
                message = this._encodingSuccess.Value && this._parsingSuccess.Value ? "Anthropic tools test passed" : "Anthropic tools test failed";
            }
        }
    }
}
