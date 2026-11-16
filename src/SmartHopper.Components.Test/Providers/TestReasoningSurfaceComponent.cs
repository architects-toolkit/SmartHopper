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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component that surfaces the decoded <c>Reasoning</c> channel of the
    /// last assistant interaction. Proves live that provider thinking content is
    /// decoded into a user-visible field, not only counted as internal_reasoning
    /// metrics. Defaults to Anthropic but works with any provider selection.
    /// </summary>
    public class TestReasoningSurfaceComponent : ProviderTestComponentBase
    {
        public override Guid ComponentGuid => new Guid("C9D4E5F6-3A5B-4C7D-8E9F-1A2B3C4D5E6F");

        protected override string TestProviderName => "Anthropic";

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public TestReasoningSurfaceComponent()
            : base("Test Reasoning Surface", "TEST-REASONING", "Exposes the decoded Reasoning channel of the last assistant reply")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Reasoning Visible", "RV", "A non-empty Reasoning string was decoded", GH_ParamAccess.item);
            pManager.AddTextParameter("Reasoning", "R", "The decoded reasoning text", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _reasoningVisible = new GH_Boolean(false);
            private GH_String _reasoning = new GH_String(string.Empty);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestReasoningSurfaceComponent _parent;

            public Worker(TestReasoningSurfaceComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var provider = this._parent.GetActualAIProvider();
                    if (provider == null)
                    {
                        this._messages.Add(new GH_String("Provider not found"));
                        await Task.Yield();
                        return;
                    }

                    var builder = AIBodyBuilder.Create();
                    builder.Add(new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "A farmer has 17 sheep. All but 9 run away, then 2 come back and he buys 3 more. How many sheep does he have? Think briefly.",
                    });

                    var call = new AIRequestCall { Body = builder.Build() };

                    IAIReturn result = await provider.Call(call).ConfigureAwait(false);
                    var texts = result?.Body?.Interactions?.OfType<AIInteractionText>().ToList() ?? new List<AIInteractionText>();
                    var withReasoning = texts.Where(t => !string.IsNullOrWhiteSpace(t.Reasoning)).ToList();

                    this._messages.Add(new GH_String($"Provider: {provider.Name}, interactions: {texts.Count} text, {withReasoning.Count} with reasoning"));

                    foreach (var t in texts)
                    {
                        var pd = t.ProviderData == null ? "-" : string.Join(",", t.ProviderData.Properties().Select(p => p.Name));
                        this._messages.Add(new GH_String($"[{t.Agent}] content={t.Content?.Length ?? 0}ch reasoning={t.Reasoning?.Length ?? 0}ch providerData={pd}"));
                    }

                    if (result?.Metrics != null)
                    {
                        this._messages.Add(new GH_String($"internal_reasoning tokens: {result.Metrics.OutputTokensReasoning}"));
                    }

                    var last = withReasoning.LastOrDefault();
                    var reasoningText = last?.Reasoning ?? string.Empty;
                    this._reasoningVisible = new GH_Boolean(!string.IsNullOrWhiteSpace(reasoningText));
                    this._reasoning = new GH_String(reasoningText);

                    if (result != null)
                    {
                        this._parent.SetAIReturnSnapshot(result as AIReturn);
                    }
                }
                catch (Exception ex)
                {
                    this._reasoningVisible = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Reasoning Visible", this._reasoningVisible, DA);
                this._parent.SetPersistentOutput("Reasoning", this._reasoning, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                this._parent.SetMetricsOutput(DA);
                message = this._reasoningVisible.Value ? "Reasoning content decoded" : "No reasoning content decoded";
            }
        }
    }
}
