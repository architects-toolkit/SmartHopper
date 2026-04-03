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
using SmartHopper.Infrastructure.AICall.Core.Returns;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for Anthropic standard API call.
    /// </summary>
    public class TestAnthropicStandardCallComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("C3D4E5F6-A7B8-9012-CDEF-345678901234");

        public TestAnthropicStandardCallComponent()
            : base("Test Anthropic Standard Call", "TEST-ANTHROPIC-CALL", "Tests Anthropic standard API call and metrics validation", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Anthropic");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Call Success", "CS", "API call succeeded", GH_ParamAccess.item);
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
            private readonly TestAnthropicStandardCallComponent _parent;

            public Worker(TestAnthropicStandardCallComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create test AIRequestCall
                    var call = new AIRequestCall();
                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.Context,
                        Content = "Say 'test' in one word."
                    });
                    call.Body = builder.Build();

                    // Get provider from manager
                    var providerManager = SmartHopper.Infrastructure.AIProviders.ProviderManager.Instance;
                    var provider = providerManager.GetProvider("Anthropic");

                    if (provider == null)
                    {
                        _messages.Add(new GH_String("Anthropic provider not found"));
                        _callSuccess = new GH_Boolean(false);
                        _metricsValid = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    // Make API call
                    IAIReturn result = null;
                    try
                    {
                        result = await provider.Call(call);

                        if (result != null && result.Body != null && result.Body.InteractionsCount > 0)
                        {
                            callSuccess = true;
                            var lastInteraction = result.Body.Interactions.LastOrDefault() as AIInteractionText;
                            var responseText = lastInteraction?.Content ?? "No text response";
                            _messages.Add(new GH_String($"API call successful: {responseText.Substring(0, Math.Min(50, responseText.Length))}..."));
                        }
                        else
                        {
                            _messages.Add(new GH_String("API call returned empty result"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _messages.Add(new GH_String($"API call failed: {ex.Message}"));
                    }

                    // Validate metrics
                    if (result?.Metrics != null)
                    {
                        metricsValid = true;

                        if (result.Metrics.InputTokens <= 0)
                        {
                            metricsValid = false;
                            _messages.Add(new GH_String("Input tokens not set or invalid"));
                        }

                        if (result.Metrics.OutputTokens <= 0)
                        {
                            metricsValid = false;
                            _messages.Add(new GH_String("Output tokens not set or invalid"));
                        }

                        if (metricsValid)
                        {
                            _messages.Add(new GH_String($"Metrics valid - Input: {result.Metrics.InputTokens}, Output: {result.Metrics.OutputTokens}"));
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
                message = _callSuccess.Value && _metricsValid.Value ? "Anthropic standard call test passed" : "Anthropic standard call test failed";
            }
        }
    }
}
