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
    /// Test component for Google Gemini response decoding.
    /// </summary>
    public class TestGeminiDecodeComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("8A175DAD-66A4-4153-9D28-87B46DAA6DDC");
        protected override string ComponentName => "Test Gemini Decode";
        protected override string ComponentDescription => "Tests Google Gemini response decoding to AIReturn";
        protected override string ComponentCategory => "SmartHopper/Test/Providers";
        protected override string ComponentSubCategory => "Gemini";

        public TestGeminiDecodeComponent()
            : base("Test Gemini Decode", "TEST-GEMINI-DEC", "Tests Google Gemini response decoding to AIReturn", "SmartHopper", "Test/Providers")
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
            private readonly TestGeminiDecodeComponent _parent;

            public Worker(TestGeminiDecodeComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    // Create mock Gemini response
                    var mockResponse = new JObject
                    {
                        ["candidates"] = new JArray
                        {
                            new JObject
                            {
                                ["content"] = new JObject
                                {
                                    ["role"] = "model",
                                    ["parts"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["text"] = "This is a Gemini test response"
                                        }
                                    }
                                },
                                ["finishReason"] = "STOP"
                            }
                        },
                        ["usageMetadata"] = new JObject
                        {
                            ["promptTokenCount"] = 20,
                            ["candidatesTokenCount"] = 8,
                            ["totalTokenCount"] = 28
                        },
                        ["modelVersion"] = "gemini-pro"
                    };

                    // Decode using Gemini provider
                    var provider = new GoogleGeminiProvider();
                    var result = provider.Decode<string>(mockResponse.ToString());

                    // Verify decoding
                    if (result == null)
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Decoded result is null"));
                        await Task.Yield();
                        return;
                    }

                    if (string.IsNullOrEmpty(result.Body))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Decoded body is empty"));
                        await Task.Yield();
                        return;
                    }

                    if (!result.Body.Contains("Gemini test response"))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Decoded content doesn't match expected response"));
                        await Task.Yield();
                        return;
                    }

                    _success = new GH_Boolean(true);
                    _messages.Add(new GH_String("Gemini decoding successful"));
                    _messages.Add(new GH_String($"Decoded content: {result.Body.Substring(0, Math.Min(50, result.Body.Length))}..."));
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
                message = _success.Value ? "Gemini decoding test passed" : "Gemini decoding test failed";
            }
        }
    }
}
