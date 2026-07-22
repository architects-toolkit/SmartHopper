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
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Batch;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for Anthropic cancellation across all async operations.
    /// </summary>
    public class TestAnthropicCancellationComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("D5B281DA-988B-4E6D-9F76-A905DC12EAFA");

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public TestAnthropicCancellationComponent()
            : base("Test Anthropic Cancellation", "TEST-ANTHROPIC-CANCEL", "Tests Anthropic cancellation across all async operations", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("Anthropic");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Standard Call Cancelled", "SCC", "Standard API call was cancelled", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Batch Submit Cancelled", "BSC", "Batch submit was cancelled", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Batch Remote Cancelled", "BRC", "Batch was actually cancelled on the remote provider", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Vision Call Cancelled", "VCC", "Vision API call was cancelled (local only)", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _standardCallCancelled = new GH_Boolean(false);
            private GH_Boolean _batchSubmitCancelled = new GH_Boolean(false);
            private GH_Boolean _batchRemoteCancelled = new GH_Boolean(false);
            private GH_Boolean _visionCallCancelled = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestAnthropicCancellationComponent _parent;

            public Worker(TestAnthropicCancellationComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var provider = ProviderManager.Instance.GetProvider("Anthropic");
                    if (provider == null)
                    {
                        this._messages.Add(new GH_String("Anthropic provider not found"));
                        this._standardCallCancelled = new GH_Boolean(false);
                        this._batchSubmitCancelled = new GH_Boolean(false);
                        this._batchRemoteCancelled = new GH_Boolean(false);
                        this._visionCallCancelled = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    this._messages.Add(new GH_String("=== Test 1: Standard Call Cancellation ==="));
                    bool standardCancelled = await this.TestStandardCallCancellation(provider, token).ConfigureAwait(false);
                    this._standardCallCancelled = new GH_Boolean(standardCancelled);
                    this._messages.Add(new GH_String(standardCancelled ? "Standard call cancelled (local only — remote verification not possible for regular calls)" : "Standard call not cancelled (completed before token fired)"));

                    this._messages.Add(new GH_String("=== Test 2: Batch Submit Cancellation ==="));
                    bool batchSubmitCancelled = await this.TestBatchSubmitCancellation(provider, token).ConfigureAwait(false);
                    this._batchSubmitCancelled = new GH_Boolean(batchSubmitCancelled);
                    this._messages.Add(new GH_String(batchSubmitCancelled ? "Batch submit cancelled (local HTTP abort only)" : "Batch submit not cancelled (completed before token fired)"));

                    this._messages.Add(new GH_String("=== Test 3: Batch Remote Cancellation ==="));
                    bool batchRemoteCancelled = await this.TestBatchRemoteCancellation(provider, token).ConfigureAwait(false);
                    this._batchRemoteCancelled = new GH_Boolean(batchRemoteCancelled);
                    this._messages.Add(new GH_String(batchRemoteCancelled ? "Batch remote cancellation verified" : "Batch remote cancellation failed"));

                    this._messages.Add(new GH_String("=== Test 4: Vision Call Cancellation ==="));
                    bool visionCancelled = await this.TestVisionCallCancellation(provider, token).ConfigureAwait(false);
                    this._visionCallCancelled = new GH_Boolean(visionCancelled);
                    this._messages.Add(new GH_String(visionCancelled ? "Vision call cancelled (local only — remote verification not possible for regular calls)" : "Vision call not cancelled (completed before token fired)"));
                }
                catch (OperationCanceledException)
                {
                    this._messages.Add(new GH_String("Worker was cancelled"));
                }
                catch (Exception ex)
                {
                    this._standardCallCancelled = new GH_Boolean(false);
                    this._batchSubmitCancelled = new GH_Boolean(false);
                    this._batchRemoteCancelled = new GH_Boolean(false);
                    this._visionCallCancelled = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            private async Task<bool> TestStandardCallCancellation(IAIProvider provider, CancellationToken token)
            {
                try
                {
                    var call = new AIRequestCall();
                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "Say 'test' in one word.",
                    });
                    call.Body = builder.Build();

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                    await provider.Call(call, cts.Token).ConfigureAwait(false);

                    return false;
                }
                catch (OperationCanceledException)
                {
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"Standard call cancellation error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestBatchSubmitCancellation(IAIProvider provider, CancellationToken token)
            {
                if (provider is not IAIBatchProvider batchProvider)
                {
                    this._messages.Add(new GH_String("Provider does not implement IAIBatchProvider"));
                    return false;
                }

                try
                {
                    var call = new AIRequestCall();
                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "Say 'batch test' in two words.",
                    });
                    call.Body = builder.Build();

                    var customId = AIBatchSubmission.GenerateCustomId("test-cancel", 0);
                    var items = new List<(string CustomId, AIRequestCall Request)>
                    {
                        (customId, call),
                    };

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                    await batchProvider.SubmitBatchAsync(items, cts.Token).ConfigureAwait(false);

                    return false;
                }
                catch (OperationCanceledException)
                {
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"Batch submit cancellation error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestBatchRemoteCancellation(IAIProvider provider, CancellationToken token)
            {
                if (provider is not IAIBatchProvider batchProvider)
                {
                    this._messages.Add(new GH_String("Provider does not implement IAIBatchProvider"));
                    return false;
                }

                AIBatchSubmission submission = null;
                try
                {
                    var call = new AIRequestCall();
                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "Say 'batch test' in two words.",
                    });
                    call.Body = builder.Build();

                    var customId = AIBatchSubmission.GenerateCustomId("test-cancel", 0);
                    var items = new List<(string CustomId, AIRequestCall Request)>
                    {
                        (customId, call),
                    };

                    submission = await batchProvider.SubmitBatchAsync(items, token).ConfigureAwait(false);
                    this._messages.Add(new GH_String($"Batch submitted: {submission.BatchId}"));
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"Batch submission failed: {ex.Message}"));
                    return false;
                }

                try
                {
                    this._messages.Add(new GH_String("Calling CancelBatchAsync..."));
                    await batchProvider.CancelBatchAsync(submission, token).ConfigureAwait(false);
                    this._messages.Add(new GH_String("CancelBatchAsync returned"));
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"CancelBatchAsync failed: {ex.Message}"));
                    return false;
                }

                try
                {
                    this._messages.Add(new GH_String("Polling for Cancelled status..."));
                    var timeoutSeconds = SmartHopperSettings.Instance.GetSetting("Global", "TimeoutSeconds") as int? ?? 600;
                    var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    var start = DateTime.UtcNow;
                    AIBatchStatus status = null;

                    while (DateTime.UtcNow - start < timeout)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                        status = await batchProvider.GetBatchStatusAsync(submission, token).ConfigureAwait(false);
                        this._messages.Add(new GH_String($"Poll: {status.State}"));

                        if (status.State == AIBatchState.Cancelled)
                        {
                            this._messages.Add(new GH_String("Batch confirmed Cancelled on remote"));
                            return true;
                        }

                        if (status.State == AIBatchState.Completed ||
                            status.State == AIBatchState.Failed ||
                            status.State == AIBatchState.Expired)
                        {
                            this._messages.Add(new GH_String($"Batch ended in terminal state: {status.State} (not Cancelled)"));
                            return false;
                        }
                    }

                    this._messages.Add(new GH_String($"Timeout waiting for Cancelled status. Last state: {status?.State.ToString() ?? "unknown"}"));
                    return false;
                }
                catch (OperationCanceledException)
                {
                    this._messages.Add(new GH_String("Polling was cancelled by user token"));
                    return false;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"Batch remote cancellation error: {ex.Message}"));
                    return false;
                }
            }

            private async Task<bool> TestVisionCallCancellation(IAIProvider provider, CancellationToken token)
            {
                try
                {
                    const string base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwAhgGAWjR9awAAAABJRU5ErkJggg==";

                    var call = new AIRequestCall();
                    var builder = AIBodyBuilder.FromImmutable(call.Body);
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "Analyze this image",
                    });
                    builder.Add(new AIInteractionImage
                    {
                        ImageData = base64Image,
                    });
                    call.Body = builder.Build();

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                    await provider.Call(call, cts.Token).ConfigureAwait(false);

                    return false;
                }
                catch (OperationCanceledException)
                {
                    return true;
                }
                catch (Exception ex)
                {
                    this._messages.Add(new GH_String($"Vision call cancellation error: {ex.Message}"));
                    return false;
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Standard Call Cancelled", this._standardCallCancelled, DA);
                this._parent.SetPersistentOutput("Batch Submit Cancelled", this._batchSubmitCancelled, DA);
                this._parent.SetPersistentOutput("Batch Remote Cancelled", this._batchRemoteCancelled, DA);
                this._parent.SetPersistentOutput("Vision Call Cancelled", this._visionCallCancelled, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);

                bool anyPassed = this._standardCallCancelled.Value || this._batchSubmitCancelled.Value ||
                               this._batchRemoteCancelled.Value || this._visionCallCancelled.Value;
                message = anyPassed ? "Anthropic cancellation tests completed" : "Anthropic cancellation tests completed (no cancellations triggered)";
            }
        }
    }
}
