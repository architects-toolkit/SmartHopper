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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Components.Test.Providers
{
    /// <summary>
    /// Test component for a live Gemini function-calling round-trip.
    /// Turn 1 forces the model to emit a <c>functionCall</c> for the
    /// <c>text2boolean</c> tool; turn 2 replays the assistant tool call (with its
    /// <c>thoughtSignature</c> when present) plus a <c>functionResponse</c> and
    /// expects a plain text answer. Exercises encode → decode → continuation
    /// encode end to end, not just request shapes.
    /// </summary>
    public class TestGeminiToolLoopComponent : ProviderTestComponentBase
    {
        public override Guid ComponentGuid => new Guid("B8C3D4E5-2F4A-4B6C-9D7E-0F1A2B3C4D5E");

        protected override string TestProviderName => "Gemini";

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public TestGeminiToolLoopComponent()
            : base("Test Gemini Tool Loop", "TEST-GEMINI-TOOLLOOP", "Tests a live Gemini function-call / function-response round-trip")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Tool Call Success", "TS", "Model emitted the forced functionCall", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Loop Success", "LS", "functionResponse turn produced a text answer", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test messages", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private const string ToolName = "text2boolean";
            private const string ToolFilter = "Data.Text";
            private const string UserPrompt = "Use the text2boolean tool to answer this: text 'The building is 30m tall', question 'Is the building taller than 25m?'";

            private GH_Boolean _toolCallSuccess = new GH_Boolean(false);
            private GH_Boolean _loopSuccess = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly TestGeminiToolLoopComponent _parent;

            public Worker(TestGeminiToolLoopComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                        this._messages.Add(new GH_String("Gemini provider not found"));
                        await Task.Yield();
                        return;
                    }

                    // Turn 1: force a functionCall for text2boolean.
                    var builder1 = AIBodyBuilder.Create().WithToolFilter(ToolFilter);
                    builder1.Add(new AIInteractionText { Agent = AIAgent.User, Content = UserPrompt });

                    var call1 = new AIRequestCall
                    {
                        Body = builder1.Build(),
                        ForceToolName = ToolName,
                    };

                    IAIReturn result1 = await provider.Call(call1).ConfigureAwait(false);
                    var toolCall = result1?.Body?.Interactions?.OfType<AIInteractionToolCall>().FirstOrDefault();
                    if (toolCall == null)
                    {
                        this._messages.Add(new GH_String("Turn 1 failed: response contains no AIInteractionToolCall"));
                        await Task.Yield();
                        return;
                    }

                    var args = toolCall.Arguments?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
                    this._messages.Add(new GH_String($"Turn 1: functionCall '{toolCall.Name}' args={args}"));

                    bool toolCallOk = string.Equals(toolCall.Name, ToolName, StringComparison.Ordinal);
                    if (!toolCallOk)
                    {
                        this._messages.Add(new GH_String($"Unexpected tool name '{toolCall.Name}' (forced '{ToolName}')"));
                    }

                    var signature = toolCall.ProviderData?["thoughtSignature"]?.ToString();
                    this._messages.Add(new GH_String(string.IsNullOrEmpty(signature)
                        ? "No thoughtSignature on the functionCall (model may not attach one)"
                        : "thoughtSignature captured in ProviderData"));

                    // Turn 2: replay the assistant turn verbatim (ProviderData carries the
                    // signature) plus a functionResponse, and expect a text answer.
                    var builder2 = AIBodyBuilder.Create().WithToolFilter(ToolFilter);
                    builder2.Add(new AIInteractionText { Agent = AIAgent.User, Content = UserPrompt });
                    builder2.AddRange(result1.Body.Interactions.ToList());
                    builder2.Add(new AIInteractionToolResult
                    {
                        Id = toolCall.Id,
                        Name = toolCall.Name,
                        Result = new JObject { ["result"] = true },
                    });

                    var call2 = new AIRequestCall { Body = builder2.Build() };

                    IAIReturn result2 = await provider.Call(call2).ConfigureAwait(false);
                    var answerText = result2?.Body?.Interactions?.OfType<AIInteractionText>()
                        .LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Content))?.Content;

                    bool loopOk = !string.IsNullOrWhiteSpace(answerText);
                    this._messages.Add(new GH_String(loopOk
                        ? $"Turn 2: text answer after functionResponse: '{answerText.Substring(0, Math.Min(60, answerText.Length))}'"
                        : "Turn 2 failed: no text answer after functionResponse"));

                    if (result2 != null)
                    {
                        this._parent.SetAIReturnSnapshot(result2 as AIReturn);
                    }

                    this._toolCallSuccess = new GH_Boolean(toolCallOk);
                    this._loopSuccess = new GH_Boolean(loopOk);
                }
                catch (Exception ex)
                {
                    this._toolCallSuccess = new GH_Boolean(false);
                    this._loopSuccess = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Error: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                await Task.Yield();
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Tool Call Success", this._toolCallSuccess, DA);
                this._parent.SetPersistentOutput("Loop Success", this._loopSuccess, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                this._parent.SetMetricsOutput(DA);
                message = this._toolCallSuccess.Value && this._loopSuccess.Value ? "Gemini tool loop test passed" : "Gemini tool loop test failed";
            }
        }
    }
}
