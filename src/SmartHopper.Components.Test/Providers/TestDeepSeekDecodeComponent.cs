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
using SmartHopper.Providers.DeepSeek;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for DeepSeek response decoding.
    /// </summary>
    public class TestDeepSeekDecodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("F4B36D4B-7037-440F-A9E6-B7791C622AC5");

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestDeepSeekDecodeComponent()
            : base("Test DeepSeek Decode", "TEST-DEEPSEEK-DEC", "Tests DeepSeek response decoding to AIReturn", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("DeepSeek");
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
            private readonly TestDeepSeekDecodeComponent _parent;

            public Worker(TestDeepSeekDecodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    // Create mock DeepSeek response
                    var mockResponse = new JObject
                    {
                        ["choices"] = new JArray
                        {
                            new JObject
                            {
                                ["message"] = new JObject
                                {
                                    ["role"] = "assistant",
                                    ["content"] = "This is a DeepSeek test response"
                                },
                                ["finish_reason"] = "stop"
                            }
                        },
                        ["usage"] = new JObject
                        {
                            ["prompt_tokens"] = 15,
                            ["completion_tokens"] = 7,
                            ["total_tokens"] = 22
                        },
                        ["model"] = "deepseek-chat",
                        ["id"] = "deepseek-123"
                    };

                    // Decode using DeepSeek provider
                    var provider = DeepSeekProvider.Instance;
                    var interactions = provider.Decode(mockResponse);

                    // Verify decoding
                    if (interactions == null || interactions.Count == 0)
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Decoded interactions is null or empty"));
                        await Task.Yield();
                        return;
                    }

                    var textInteraction = interactions.OfType<AIInteractionText>().FirstOrDefault();
                    if (textInteraction == null || string.IsNullOrEmpty(textInteraction.Content))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Decoded text interaction is empty"));
                        await Task.Yield();
                        return;
                    }

                    if (!textInteraction.Content.Contains("DeepSeek test response"))
                    {
                        this._success = new GH_Boolean(false);
                        this._messages.Add(new GH_String("Decoded content doesn't match expected response"));
                        await Task.Yield();
                        return;
                    }

                    this._success = new GH_Boolean(true);
                    this._messages.Add(new GH_String("DeepSeek decoding successful"));
                    this._messages.Add(new GH_String($"Decoded content: {textInteraction.Content.Substring(0, Math.Min(50, textInteraction.Content.Length))}..."));
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
                message = this._success.Value ? "DeepSeek decoding test passed" : "DeepSeek decoding test failed";
            }
        }
    }
}
