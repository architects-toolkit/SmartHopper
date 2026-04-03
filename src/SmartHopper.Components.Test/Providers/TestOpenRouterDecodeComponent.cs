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
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Providers.OpenRouter;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for OpenRouter response decoding.
    /// </summary>
    public class TestOpenRouterDecodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("F2A3B4C5-D6E7-8901-FABC-234567890123");

        public TestOpenRouterDecodeComponent()
            : base("Test OpenRouter Decode", "TEST-OPENROUTER-DEC", "Tests OpenRouter response decoding to AIReturn", "SmartHopper", "Test/Providers")
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
            private readonly TestOpenRouterDecodeComponent _parent;

            public Worker(TestOpenRouterDecodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    // Create mock OpenRouter response
                    var mockResponse = new JObject
                    {
                        ["choices"] = new JArray
                        {
                            new JObject
                            {
                                ["message"] = new JObject
                                {
                                    ["role"] = "assistant",
                                    ["content"] = "This is an OpenRouter test response"
                                },
                                ["finish_reason"] = "stop"
                            },
                        },
                        ["usage"] = new JObject
                        {
                            ["prompt_tokens"] = 18,
                            ["completion_tokens"] = 9,
                            ["total_tokens"] = 27,
                        },
                        ["model"] = "openrouter/auto",
                        ["id"] = "gen-123",
                    };

                    // Decode using OpenRouter provider
                    var provider = OpenRouterProvider.Instance;
                    var interactions = provider.Decode(mockResponse);

                    // Verify decoding
                    if (interactions == null || interactions.Count == 0)
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Decoded interactions is null or empty"));
                        await Task.Yield();
                        return;
                    }

                    var textInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault();
                    if (textInteraction == null || string.IsNullOrEmpty(textInteraction.Content))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Decoded text interaction is empty"));
                        await Task.Yield();
                        return;
                    }

                    if (!textInteraction.Content.Contains("OpenRouter test response"))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Decoded content doesn't match expected response"));
                        await Task.Yield();
                        return;
                    }

                    _success = new GH_Boolean(true);
                    _messages.Add(new GH_String("OpenRouter decoding successful"));
                    _messages.Add(new GH_String($"Decoded content: {textInteraction.Content.Substring(0, Math.Min(50, textInteraction.Content.Length))}..."));
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
                message = _success.Value ? "OpenRouter decoding test passed" : "OpenRouter decoding test failed";
            }
        }
    }
}
