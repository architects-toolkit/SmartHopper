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
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for OpenAI batch API call.
    /// </summary>
    public class TestOpenAIBatchCallComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("2B36EFF6-46F3-4CAB-B032-369D0102D954");

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public TestOpenAIBatchCallComponent()
            : base("Test OpenAI Batch Call", "TEST-OPENAI-BATCH", "Tests OpenAI batch API call with service_tier=batch and metrics validation", "SmartHopper", "Test/Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("OpenAI");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Responses Success", "RS", "Responses batch API call succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("ChatComp Success", "CC", "Chat completions batch API call succeeded", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Metrics Valid", "MV", "Metrics structure is valid", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _responsesSuccess = new GH_Boolean(false);
            private GH_Boolean _chatCompSuccess = new GH_Boolean(false);
            private GH_Boolean _metricsValid = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestOpenAIBatchCallComponent _parent;

            public Worker(TestOpenAIBatchCallComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                bool responsesSuccess = false;
                bool chatCompSuccess = false;
                bool metricsValid = false;
                AIReturn result = null;

                try
                {
                    // Resolve provider and verify it supports batch
                    var provider = ProviderManager.Instance.GetProvider("OpenAI");
                    if (provider == null)
                    {
                        this._messages.Add(new GH_String("OpenAI provider not found"));
                        this._responsesSuccess = new GH_Boolean(false);
                        this._chatCompSuccess = new GH_Boolean(false);
                        this._metricsValid = new GH_Boolean(false);
                        return;
                    }

                    if (provider is not IAIBatchProvider batchProvider)
                    {
                        this._messages.Add(new GH_String("OpenAI provider does not implement IAIBatchProvider"));
                        this._responsesSuccess = new GH_Boolean(false);
                        this._chatCompSuccess = new GH_Boolean(false);
                        this._metricsValid = new GH_Boolean(false);
                        return;
                    }

                    // Build shared body
                    var builder = AIBodyBuilder.Create();
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.Context,
                        Content = "Say 'batch test' in two words."
                    });
                    var body = builder.Build();

                    var model = this._parent.GetModel();
                    var extras = new Dictionary<string, JToken> { { "service_tier", "batch" } };

                    // Batch Job 1: Responses API (default endpoint)
                    try
                    {
                        var responsesCall = new AIRequestCall
                        {
                            Body = body,
                            Endpoint = "/responses",
                            Parameters = new AIRequestParameters { Model = model, Extras = extras },
                        };

                        var customId1 = AIBatchSubmission.GenerateCustomId("test-batch-responses", 0);
                        var items1 = new List<(string CustomId, AIRequestCall Request)> { (customId1, responsesCall) };

                        this._messages.Add(new GH_String("Submitting Responses batch job..."));
                        var submission1 = await batchProvider.SubmitBatchAsync(items1, token).ConfigureAwait(false);
                        this._messages.Add(new GH_String($"Responses batch submitted: {submission1.BatchId}"));

                        var timeout1 = TimeSpan.FromSeconds(responsesCall.TimeoutSeconds ?? TimeoutDefaults.DefaultTimeoutSeconds);
                        var status1 = await PollBatchAsync(batchProvider, submission1, timeout1, token).ConfigureAwait(false);
                        if (status1 != null && status1.State == AIBatchState.Completed)
                        {
                            if (status1.Results != null && status1.Results.TryGetValue(customId1, out var resultBody1))
                            {
                                var decoded1 = provider.Decode(resultBody1);
                                if (decoded1 != null && decoded1.Count > 0)
                                {
                                    responsesSuccess = true;
                                    var lastText = decoded1.OfType<AIInteractionText>().LastOrDefault();
                                    this._messages.Add(new GH_String($"Responses batch result: {lastText?.Content ?? "No text"}"));
                                    result = new AIReturn();
                                    result.SetBody(decoded1);
                                }
                                else
                                {
                                    this._messages.Add(new GH_String("Responses batch decoded to empty interactions"));
                                }
                            }
                            else
                            {
                                this._messages.Add(new GH_String("Responses batch completed but custom_id not found"));
                            }
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"Responses batch did not complete: {status1?.State.ToString() ?? "unknown"}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"Responses batch failed: {ex.Message}"));
                    }

                    // Batch Job 2: Chat Completions API (explicit endpoint override)
                    try
                    {
                        var chatCompCall = new AIRequestCall
                        {
                            Body = body,
                            Endpoint = "/chat/completions",
                            Parameters = new AIRequestParameters { Model = model, Extras = extras },
                        };

                        var customId2 = AIBatchSubmission.GenerateCustomId("test-batch-chatcomp", 0);
                        var items2 = new List<(string CustomId, AIRequestCall Request)> { (customId2, chatCompCall) };

                        this._messages.Add(new GH_String("Submitting Chat Completions batch job..."));
                        var submission2 = await batchProvider.SubmitBatchAsync(items2, token).ConfigureAwait(false);
                        this._messages.Add(new GH_String($"Chat Completions batch submitted: {submission2.BatchId}"));

                        var timeout2 = TimeSpan.FromSeconds(chatCompCall.TimeoutSeconds ?? TimeoutDefaults.DefaultTimeoutSeconds);
                        var status2 = await PollBatchAsync(batchProvider, submission2, timeout2, token).ConfigureAwait(false);
                        if (status2 != null && status2.State == AIBatchState.Completed)
                        {
                            if (status2.Results != null && status2.Results.TryGetValue(customId2, out var resultBody2))
                            {
                                var decoded2 = provider.Decode(resultBody2);
                                if (decoded2 != null && decoded2.Count > 0)
                                {
                                    chatCompSuccess = true;
                                    var lastText = decoded2.OfType<AIInteractionText>().LastOrDefault();
                                    this._messages.Add(new GH_String($"Chat Completions batch result: {lastText?.Content ?? "No text"}"));
                                    result ??= new AIReturn();
                                    result.SetBody(decoded2);
                                }
                                else
                                {
                                    this._messages.Add(new GH_String("Chat Completions batch decoded to empty interactions"));
                                }
                            }
                            else
                            {
                                this._messages.Add(new GH_String("Chat Completions batch completed but custom_id not found"));
                            }
                        }
                        else
                        {
                            this._messages.Add(new GH_String($"Chat Completions batch did not complete: {status2?.State.ToString() ?? "unknown"}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"Chat Completions batch failed: {ex.Message}"));
                    }

                    // Validate metrics from the last successful result
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

                this._responsesSuccess = new GH_Boolean(responsesSuccess);
                this._chatCompSuccess = new GH_Boolean(chatCompSuccess);
                this._metricsValid = new GH_Boolean(metricsValid);
            }

            private static async Task<AIBatchStatus> PollBatchAsync(IAIBatchProvider batchProvider, AIBatchSubmission submission, TimeSpan timeout, CancellationToken token)
            {
                var start = DateTime.UtcNow;
                while (DateTime.UtcNow - start < timeout)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                    var status = await batchProvider.GetBatchStatusAsync(submission, token).ConfigureAwait(false);

                    if (status.State == AIBatchState.Completed ||
                        status.State == AIBatchState.Failed ||
                        status.State == AIBatchState.Cancelled ||
                        status.State == AIBatchState.Expired)
                    {
                        return status;
                    }
                }

                return null;
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Responses Success", this._responsesSuccess, DA);
                this._parent.SetPersistentOutput("ChatComp Success", this._chatCompSuccess, DA);
                this._parent.SetPersistentOutput("Metrics Valid", this._metricsValid, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                this._parent.SetMetricsOutput(DA);
                message = this._responsesSuccess.Value && this._chatCompSuccess.Value && this._metricsValid.Value
                    ? "OpenAI dual batch endpoint test passed"
                    : "OpenAI dual batch endpoint test failed";
            }
        }
    }
}
