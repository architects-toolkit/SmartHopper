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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for DeepSeek cancellation across all async operations.
    /// </summary>
    public class TestDeepSeekCancellationComponent : AIStatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("E6B02D63-FAF8-43F5-AAD6-71A3BA70A79A");

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public TestDeepSeekCancellationComponent()
            : base("Test DeepSeek Cancellation", "TEST-DEEPSEEK-CANCEL", "Tests DeepSeek cancellation across all async operations", "SmartHopper Tests", "Testing Providers")
        {
            this.RunOnlyOnInputChanges = false;
            this.SetSelectedProviderName("DeepSeek");
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Standard Call Cancelled", "SCC", "Standard API call was cancelled", GH_ParamAccess.item);
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
            private GH_Boolean _visionCallCancelled = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestDeepSeekCancellationComponent _parent;

            public Worker(TestDeepSeekCancellationComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var provider = ProviderManager.Instance.GetProvider("DeepSeek");
                    if (provider == null)
                    {
                        this._messages.Add(new GH_String("DeepSeek provider not found"));
                        this._standardCallCancelled = new GH_Boolean(false);
                        this._visionCallCancelled = new GH_Boolean(false);
                        await Task.Yield();
                        return;
                    }

                    this._messages.Add(new GH_String("=== Test 1: Standard Call Cancellation ==="));
                    bool standardCancelled = await this.TestStandardCallCancellation(provider, token).ConfigureAwait(false);
                    this._standardCallCancelled = new GH_Boolean(standardCancelled);
                    this._messages.Add(new GH_String(standardCancelled ? "Standard call cancelled (local only — remote verification not possible for regular calls)" : "Standard call not cancelled (completed before token fired)"));

                    this._messages.Add(new GH_String("=== Test 2: Vision Call Cancellation ==="));
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
                this._parent.SetPersistentOutput("Vision Call Cancelled", this._visionCallCancelled, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);

                bool anyPassed = this._standardCallCancelled.Value || this._visionCallCancelled.Value;
                message = anyPassed ? "DeepSeek cancellation tests completed" : "DeepSeek cancellation tests completed (no cancellations triggered)";
            }
        }
    }
}
