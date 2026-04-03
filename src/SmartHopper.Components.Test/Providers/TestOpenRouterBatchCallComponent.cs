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
using SmartHopper.Providers.OpenRouter;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for OpenRouter batch API call.
    /// </summary>
    public class TestOpenRouterBatchCallComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("EFAF7B39-D37C-4842-8FD5-369445201284");
        protected override string ComponentName => "Test OpenRouter Batch Call";
        protected override string ComponentDescription => "Tests OpenRouter batch API call with service_tier=batch and metrics validation";
        protected override string ComponentCategory => "SmartHopper/Test/Providers";
        protected override string ComponentSubCategory => "OpenRouter";

        public TestOpenRouterBatchCallComponent()
            : base("Test OpenRouter Batch Call", "TEST-OPENROUTER-BATCH", "Tests OpenRouter batch API call with service_tier=batch and metrics validation", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Forces the OpenRouter provider for this test component.
        /// </summary>
        protected override SmartHopper.Infrastructure.AIProviders.IAIProvider GetActualAIProvider()
        {
            return new OpenRouterProvider();
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Call Success", "CS", "Batch API call succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Metrics Valid", "MV", "Metrics structure is valid", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _callSuccess = new GH_Boolean(false);
            private GH_Boolean _metricsValid = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestOpenRouterBatchCallComponent _parent;

            public Worker(TestOpenRouterBatchCallComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    bool callSuccess = false;
                    bool metricsValid = false;

                    // Create test AIRequestCall with batch parameters
                    var call = new AIRequestCall();
                    call.Body.Add(new AIInteraction
                    {
                        Role = AIAgent.Context,
                        Content = "Say 'batch test' in two words."
                    });

                    // Set batch parameters
                    call.Parameters = new AIRequestParameters
                    {
                        Model = "openrouter/auto",
                        Batch = true
                    };

                    // Get provider from manager
                    var providerManager = SmartHopper.Infrastructure.Managers.AIProviders.ProviderManager.Instance;
                    var provider = providerManager.GetProvider("OpenRouter");

                    if (provider == null)
                    {
                        _messages.Add(new GH_String("OpenRouter provider not found"));
                        _callSuccess = new GH_Boolean(false);
                        _metricsValid = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    // Make batch API call
                    try
                    {
                        var result = provider.Call<string>(call);
                        
                        if (result != null && !string.IsNullOrEmpty(result.Body))
                        {
                            callSuccess = true;
                            _messages.Add(new GH_String($"Batch API call successful: {result.Body.Substring(0, Math.Min(50, result.Body.Length))}..."));
                        }
                        else
                        {
                            _messages.Add(new GH_String("Batch API call returned empty result"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"Batch API call failed: {ex.Message}"));
                    }

                    // Validate metrics
                    if (call.Metrics != null)
                    {
                        metricsValid = true;

                        if (call.Metrics.InputTokens <= 0)
                        {
                            metricsValid = false;
                            _messages.Add(new GH_String("Input tokens not set or invalid"));
                        }

                        if (call.Metrics.OutputTokens <= 0)
                        {
                            metricsValid = false;
                            _messages.Add(new GH_String("Output tokens not set or invalid"));
                        }

                        if (metricsValid)
                        {
                            _messages.Add(new GH_String($"Metrics valid - Input: {call.Metrics.InputTokens}, Output: {call.Metrics.OutputTokens}"));
                        }
                    }
                    else
                    {
                        _messages.Add(new GH_String("Metrics not populated"));
                    }

                    _callSuccess = new GH_Boolean(callSuccess);
                    _metricsValid = new GH_Boolean(metricsValid);
                }
                catch (Exception ex)
                {
                    _callSuccess = new GH_Boolean(false);
                    _metricsValid = new GH_Boolean(false);
                    _messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Call Success", _callSuccess, DA);
                _parent.SetPersistentOutput("Metrics Valid", _metricsValid, DA);
                _parent.SetPersistentOutput("Messages", _messages, DA);
                message = _callSuccess.Value && _metricsValid.Value ? "OpenRouter batch call test passed" : "OpenRouter batch call test failed";
            }
        }
    }
}
