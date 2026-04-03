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
    /// Test component for Google Gemini vision input handling.
    /// </summary>
    public class TestGeminiVisionComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("6F640798-7D61-48D2-9133-8DFB537651ED");
        protected override string ComponentName => "Test Gemini Vision";
        protected override string ComponentDescription => "Tests Google Gemini vision input handling with base64 images";
        protected override string ComponentCategory => "SmartHopper/Test/Providers";
        protected override string ComponentSubCategory => "Gemini";

        public TestGeminiVisionComponent()
            : base("Test Gemini Vision", "TEST-GEMINI-VISION", "Tests Google Gemini vision input handling with base64 images", "SmartHopper", "Test/Providers")
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
            private readonly TestGeminiVisionComponent _parent;

            public Worker(TestGeminiVisionComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    // Create test AIRequestCall with image content
                    var call = new AIRequestCall();
                    
                    // Hardcoded minimal base64 PNG (1x1 transparent pixel)
                    const string base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwAhgGAWjR9awAAAABJRU5ErkJggg==";

                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.Context,
                        Content = "Analyze this image",
                        ImageContent = new List<AIImageContent>
                        {
                            new AIImageContent
                            {
                                Data = base64Image,
                                MediaType = "image/png"
                            }
                        }
                    });

                    // Encode using Gemini provider
                    var provider = new GoogleGeminiProvider();
                    var encoded = provider.Encode(call);

                    // Verify encoding
                    if (string.IsNullOrEmpty(encoded))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Encoded message is empty"));
                        await Task.Yield();
                        return;
                    }

                    // Check for image content in encoding
                    if (!encoded.Contains("\"inlineData\"") && !encoded.Contains("\"inline_data\""))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing inline image data in encoding"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains("image/png") && !encoded.Contains("image/jpeg"))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Missing image MIME type in encoding"));
                        await Task.Yield();
                        return;
                    }

                    if (!encoded.Contains(base64Image.Substring(0, 20)))
                    {
                        _success = new GH_Boolean(false);
                        _messages.Add(new GH_String("Base64 image data not found in encoding"));
                        await Task.Yield();
                        return;
                    }

                    _success = new GH_Boolean(true);
                    _messages.Add(new GH_String("Gemini vision encoding successful"));
                    _messages.Add(new GH_String("- Inline image data present"));
                    _messages.Add(new GH_String("- MIME type correctly set"));
                    _messages.Add(new GH_String("- Base64 data correctly encoded"));
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
                message = _success.Value ? "Gemini vision test passed" : "Gemini vision test failed";
            }
        }
    }
}
