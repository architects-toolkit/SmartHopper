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
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Batch;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for DeepSeek batch API call.
    /// </summary>
    public class TestDeepSeekBatchCallComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("9D264C28-E309-47BA-894C-7571AA3CBE3F");

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestDeepSeekBatchCallComponent()
            : base("Test DeepSeek Batch Call", "TEST-DEEPSEEK-BATCH", "Tests DeepSeek batch API call with service_tier=batch and metrics validation", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("DeepSeek");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Call Success", "CS", "Batch API call succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Metrics Valid", "MV", "Metrics structure is valid", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _callSuccess = new GH_Boolean(false);
            private GH_Boolean _metricsValid = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestDeepSeekBatchCallComponent _parent;

            public Worker(TestDeepSeekBatchCallComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    bool callSuccess = false;
                    bool metricsValid = false;

                    // Create test AIRequestCall with batch parameters
                    var call = new AIRequestCall();
                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.Context,
                        Content = "Say 'batch test' in two words."
                    });
                    call.Body = builder.Build();

                    // Build the test request with service_tier=batch
                    call.Parameters = new AIRequestParameters
                    {
                        Model = this._parent.GetModel(),
                        Extras = new Dictionary<string, JToken> { { "service_tier", "batch" } },
                    };

                    // Resolve provider and verify it supports batch
                    var provider = ProviderManager.Instance.GetProvider("DeepSeek");
                    if (provider == null)
                    {
                        this._messages.Add(new GH_String("DeepSeek provider not found"));
                        this._callSuccess = new GH_Boolean(false);
                        this._metricsValid = new GH_Boolean(false);
                        return;
                    }

                    if (provider is not IAIBatchProvider batchProvider)
                    {
                        this._messages.Add(new GH_String("DeepSeek provider does not implement IAIBatchProvider"));
                        this._callSuccess = new GH_Boolean(false);
                        this._metricsValid = new GH_Boolean(false);
                        return;
                    }

                    // Submit batch job via the true Batch API
                    var customId = AIBatchSubmission.GenerateCustomId("test-batch", 0);
                    var items = new List<(string CustomId, AIRequestCall Request)>
                    {
                        (customId, call),
                    };

                    this._messages.Add(new GH_String("Submitting batch job..."));
                    var submission = await batchProvider.SubmitBatchAsync(items, token).ConfigureAwait(false);
                    this._messages.Add(new GH_String($"Batch submitted: {submission.BatchId}"));

                    // Poll until completion (timeout: 5 minutes)
                    AIBatchStatus status = null;
                    var timeout = TimeSpan.FromMinutes(5);
                    var start = DateTime.UtcNow;
                    while (DateTime.UtcNow - start < timeout)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                        status = await batchProvider.GetBatchStatusAsync(submission, token).ConfigureAwait(false);
                        this._messages.Add(new GH_String($"Poll: {status.State}"));

                        if (status.State == AIBatchState.Completed)
                        {
                            break;
                        }

                        if (status.State == AIBatchState.Failed ||
                            status.State == AIBatchState.Cancelled ||
                            status.State == AIBatchState.Expired)
                        {
                            break;
                        }
                    }

                    if (status == null || status.State != AIBatchState.Completed)
                    {
                        this._messages.Add(new GH_String($"Batch did not complete successfully: {status?.State.ToString() ?? "unknown"}"));
                        this._callSuccess = new GH_Boolean(false);
                        this._metricsValid = new GH_Boolean(false);
                        return;
                    }

                    // Decode the batch result body
                    if (status.Results != null && status.Results.TryGetValue(customId, out var resultBody))
                    {
                        var decoded = provider.Decode(resultBody);
                        if (decoded != null && decoded.Count > 0)
                        {
                            callSuccess = true;
                            var lastText = decoded.OfType<AIInteractionText>().LastOrDefault();
                            var responseText = lastText?.Content ?? "No text response";
                            this._messages.Add(new GH_String($"Batch result: {responseText}"));

                            // Build an AIReturn from decoded interactions for metrics/output
                            result = new AIReturn();
                            result.SetBody(decoded);
                            if (lastText?.Metrics != null)
                            {
                                result.Metrics = lastText.Metrics;
                            }
                        }
                        else
                        {
                            this._messages.Add(new GH_String("Batch result decoded to empty interactions"));
                        }
                    }
                    else
                    {
                        this._messages.Add(new GH_String("Batch completed but custom_id not found in results"));
                    }

                    // Validate metrics
                    if (result?.Metrics != null)
                    {
                        metricsValid = true;
                        if (result.Metrics.InputTokens <= 0)
                        {
                            metricsValid = false;
                            this._messages.Add(new GH_String("Input tokens not set or invalid"));
                        }

                        if (result.Metrics.OutputTokens <= 0)
                        {
                            metricsValid = false;
                            this._messages.Add(new GH_String("Output tokens not set or invalid"));
                        }

                        if (metricsValid)
                        {
                            this._messages.Add(new GH_String($"Metrics valid - Input: {result.Metrics.InputTokens}, Output: {result.Metrics.OutputTokens}"));
                        }
                    }
                    else
                    {
                        this._messages.Add(new GH_String("Metrics not populated"));
                    }
                }
                catch (OperationCanceledException)
                {
                    this._messages.Add(new GH_String("Batch call was cancelled"));
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                if (result != null)
                {
                    this._parent.SetAIReturnSnapshot(result);
                }

                this._callSuccess = new GH_Boolean(callSuccess);
                this._metricsValid = new GH_Boolean(metricsValid);
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Call Success", this._callSuccess, DA);
                this._parent.SetPersistentOutput("Metrics Valid", this._metricsValid, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                this._parent.SetMetricsOutput(DA);
                message = this._callSuccess.Value && this._metricsValid.Value ? "DeepSeek batch call test passed" : "DeepSeek batch call test failed";
            }
        }
    }
}
