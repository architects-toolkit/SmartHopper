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

namespace SmartHopper.Infrastructure.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Sessions;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.Streaming;
    using Xunit;

    /// <summary>
    /// Verifies that <see cref="ConversationSession"/> never lets a tool call without a matching result reach a
    /// provider. OpenAI-compatible chat APIs (DeepSeek, OpenAI, Mistral, ...) reject an assistant
    /// <c>tool_calls</c> message that is not followed by one <c>tool</c> message per call id.
    /// Uses a testable request and mock executor so no real provider or Rhino runtime is required.
    /// </summary>
    public class ConversationSessionToolIntegrityTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession closes pending tool calls with synthetic results when tool execution is cancelled [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession closes pending tool calls with synthetic results when tool execution is cancelled [Core]")]
#endif
        public async Task RunToStableResult_CancelledDuringToolExecution_ReconcilesPendingToolCalls()
        {
            var request = CreateTestableRequest();
            request.ResponseInteractionsFactory = () => new List<IAIInteraction> { CreateToolCall("call_1") };
            var executor = new MockProviderExecutor { OnExecTool = _ => throw new OperationCanceledException() };
            var session = new ConversationSession(request, executor: executor);

            await session.RunToStableResult(new SessionOptions { ProcessTools = true }).ConfigureAwait(false);

            Assert.Equal(0, session.Request.Body.PendingToolCallsCount());
            var result = Assert.Single(session.Request.Body.Interactions.OfType<AIInteractionToolResult>());
            Assert.Equal("call_1", result.Id);
            Assert.False(result.Result["success"]?.Value<bool>());
            Assert.True(result.Result["cancelled"]?.Value<bool>());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession closes pending tool calls found in history before calling the provider [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession closes pending tool calls found in history before calling the provider [Core]")]
#endif
        public async Task RunToStableResult_HistoryWithPendingToolCall_IsReconciledBeforeProviderCall()
        {
            // Simulates a session whose previous run was aborted after the provider emitted a tool call.
            var request = CreateTestableRequest();
            request.Body = AIBodyBuilder.FromImmutable(request.Body).Add(CreateToolCall("stale_1"), markAsNew: false).Build();
            var pendingSeenByProvider = -1;
            request.ResponseInteractionsFactory = () =>
            {
                pendingSeenByProvider = request.Body.PendingToolCallsCount();
                return new List<IAIInteraction> { new AIInteractionText { Agent = AIAgent.Assistant, Content = "done" } };
            };

            var session = new ConversationSession(request, executor: new MockProviderExecutor());
            await session.RunToStableResult(new SessionOptions { ProcessTools = true }).ConfigureAwait(false);

            Assert.Equal(0, pendingSeenByProvider);
            var interactions = session.Request.Body.Interactions.ToList();
            var callIndex = interactions.FindIndex(i => i is AIInteractionToolCall tc && i is not AIInteractionToolResult && tc.Id == "stale_1");
            var resultIndex = interactions.FindIndex(i => i is AIInteractionToolResult tr && tr.Id == "stale_1");
            var answerIndex = interactions.FindIndex(i => i is AIInteractionText t && t.Agent == AIAgent.Assistant && t.Content == "done");
            Assert.True(callIndex >= 0 && resultIndex == callIndex + 1, "Synthetic result must directly follow the stale tool call");
            Assert.True(answerIndex > resultIndex, "Provider answer must come after the reconciled tool result");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession closes pending tool calls when the tool pass budget is exhausted [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession closes pending tool calls when the tool pass budget is exhausted [Core]")]
#endif
        public async Task RunToStableResult_MaxToolPassesExhausted_ReconcilesPendingToolCalls()
        {
            // Provider requests a new tool on every call, so tool passes never converge.
            var callCount = 0;
            var request = CreateTestableRequest();
            request.ResponseInteractionsFactory = () => new List<IAIInteraction> { CreateToolCall($"call_{++callCount}") };
            var session = new ConversationSession(request, executor: new MockProviderExecutor());

            await session.RunToStableResult(new SessionOptions { ProcessTools = true, MaxToolPasses = 2, MaxTurns = 1 }).ConfigureAwait(false);

            Assert.Equal(0, session.Request.Body.PendingToolCallsCount());

            // AIInteractionToolResult derives from AIInteractionToolCall, so results must be excluded explicitly.
            var calls = session.Request.Body.Interactions.Where(i => i is AIInteractionToolCall && i is not AIInteractionToolResult).Select(i => ((AIInteractionToolCall)i).Id).ToList();
            var results = session.Request.Body.Interactions.OfType<AIInteractionToolResult>().Select(tr => tr.Id).ToList();
            Assert.Equal(3, calls.Count); // initial call + one per tool pass
            Assert.Equal(calls.OrderBy(id => id, StringComparer.Ordinal), results.OrderBy(id => id, StringComparer.Ordinal));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession hides tools from the provider when ProcessTools is false and restores the filter afterwards [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession hides tools from the provider when ProcessTools is false and restores the filter afterwards [Core]")]
#endif
        public async Task RunToStableResult_ProcessToolsFalse_HidesToolsFromProviderAndRestoresFilter()
        {
            var request = CreateTestableRequest();
            request.Body = request.Body with { ToolFilter = "+gh_*" };
            string filterSeenByProvider = null;
            request.ResponseInteractionsFactory = () =>
            {
                filterSeenByProvider = request.Body.ToolFilter;
                return new List<IAIInteraction> { new AIInteractionText { Agent = AIAgent.Assistant, Content = "hi" } };
            };

            var session = new ConversationSession(request, executor: new MockProviderExecutor());
            await session.RunToStableResult(new SessionOptions { ProcessTools = false, MaxTurns = 1 }).ConfigureAwait(false);

            Assert.Equal("-*", filterSeenByProvider);
            Assert.Equal("+gh_*", session.Request.Body.ToolFilter);
        }

        private static AIInteractionToolCall CreateToolCall(string id)
        {
            return new AIInteractionToolCall
            {
                Id = id,
                Name = "test_tool",
                Arguments = new JObject(),
            };
        }

        private static TestableAIRequestCall CreateTestableRequest()
        {
            var body = AIBodyBuilder.Create()
                .WithTurnId(Guid.NewGuid().ToString("N"))
                .AddText(AIAgent.User, "test prompt")
                .Build();

            return new TestableAIRequestCall
            {
                Provider = "test-provider",
                Model = "test-model",
                Endpoint = "https://test.example.com",
                Body = body,
            };
        }

        /// <summary>
        /// Testable request that bypasses real provider validation and returns a controlled result.
        /// </summary>
        private sealed class TestableAIRequestCall : AIRequestCall
        {
            /// <summary>
            /// Invoked on every provider call to produce the result interactions.
            /// </summary>
            public Func<List<IAIInteraction>> ResponseInteractionsFactory { get; set; }

            public override (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
            {
                // Bypass provider/model/endpoint validation for unit tests
                return (true, new List<AIRuntimeMessage>());
            }

            public override Task<AIReturn> Exec(bool stream = false)
            {
                var ret = new AIReturn();
                ret.SetBody(this.ResponseInteractionsFactory?.Invoke() ?? new List<IAIInteraction>());
                return Task.FromResult(ret);
            }
        }

        /// <summary>
        /// Mock executor whose tool execution can be overridden (including throwing to simulate failures).
        /// </summary>
        private sealed class MockProviderExecutor : IProviderExecutor
        {
            public Func<AIToolCall, AIReturn?> OnExecTool { get; set; }

            public Task<AIReturn?> ExecProviderAsync(AIRequestCall request, CancellationToken ct)
            {
                var ret = new AIReturn();
                ret.SetBody(AIBodyBuilder.Create().AddText(AIAgent.Assistant, "mock").Build());
                return Task.FromResult<AIReturn?>(ret);
            }

            public Task<AIReturn?> ExecToolAsync(AIToolCall toolCall, CancellationToken ct)
            {
                if (this.OnExecTool != null)
                {
                    return Task.FromResult(this.OnExecTool(toolCall));
                }

                var ret = new AIReturn();
                ret.SetBody(AIBodyBuilder.Create().AddText(AIAgent.ToolResult, "tool result").Build());
                return Task.FromResult<AIReturn?>(ret);
            }

            public IStreamingAdapter? TryGetStreamingAdapter(AIRequestCall request)
            {
                return null;
            }
        }
    }
}
