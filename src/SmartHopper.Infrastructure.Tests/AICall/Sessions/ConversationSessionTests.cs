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

namespace SmartHopper.Infrastructure.Tests.AICall.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Sessions;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.Diagnostics;
    using SmartHopper.ProviderSdk.Streaming;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="ConversationSession"/> class.
    /// Uses a testable request and mock executor to validate session lifecycle
    /// without requiring real providers or Rhino runtime.
    /// </summary>
    public class ConversationSessionTests
    {
        #region Construction

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession constructor throws ArgumentNullException for null request [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession constructor throws ArgumentNullException for null request [Core]")]
#endif
        public void Constructor_NullRequest_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ConversationSession(null));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession constructor accepts valid request and mock executor [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession constructor accepts valid request and mock executor [Core]")]
#endif
        public void Constructor_ValidRequest_CreatesInstance()
        {
            var request = CreateTestableRequest();
            var executor = new MockProviderExecutor();
            var session = new ConversationSession(request, executor: executor);
            Assert.NotNull(session);
        }

        #endregion

        #region RunToStableResult

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession RunToStableResult returns error for invalid request [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession RunToStableResult returns error for invalid request [Core]")]
#endif
        public async Task RunToStableResult_InvalidRequest_ReturnsError()
        {
            var request = new AIRequestCall();
            var session = new ConversationSession(request);

            var result = await session.RunToStableResult(new SessionOptions()).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.True(result.Messages.Any(m => m.Severity == SHRuntimeMessageSeverity.Error));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession RunToStableResult returns mock provider result [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession RunToStableResult returns mock provider result [Core]")]
#endif
        public async Task RunToStableResult_MockProvider_ReturnsResult()
        {
            var expectedText = "Hello from mock provider";
            var request = CreateTestableRequest(expectedText);
            var executor = new MockProviderExecutor();
            var session = new ConversationSession(request, executor: executor);

            var result = await session.RunToStableResult(new SessionOptions { ProcessTools = false }).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.NotNull(result.Body);
            Assert.Equal(expectedText, result.Body.GetLastText());
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession RunToStableResult converges in a single provider call [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession RunToStableResult converges in a single provider call [Core]")]
#endif
        public async Task RunToStableResult_Converges_SingleProviderCall()
        {
            var callCount = 0;
            var request = CreateTestableRequest(onExec: () =>
            {
                callCount++;
                return Task.FromResult($"turn {callCount}");
            });

            var session = new ConversationSession(request);
            var options = new SessionOptions { ProcessTools = false };

            var result = await session.RunToStableResult(options).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.Equal(1, callCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession RunToStableResult stamps provider interactions with a single session TurnId [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession RunToStableResult stamps provider interactions with a single session TurnId [Core]")]
#endif
        public async Task RunToStableResult_ProviderInteractions_ShareSessionTurnId()
        {
            // Provider results are built through AIBodyBuilder.Build(), which assigns a random TurnId to every
            // interaction. The session must replace those with its own per-turn identifier; otherwise each
            // interaction (and each streaming delta) is rendered as a separate turn by observers.
            var request = CreateTestableRequest();
            var userTurnId = request.Body.Interactions.Single().TurnId;
            request.ResponseInteractions = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "part 1", TurnId = "provider-a" },
                new AIInteractionText { Agent = AIAgent.Assistant, Content = "part 2", TurnId = "provider-b" },
            };

            var session = new ConversationSession(request);
            await session.RunToStableResult(new SessionOptions { ProcessTools = false }).ConfigureAwait(false);

            var assistant = session.Request.Body.Interactions.Where(i => i.Agent == AIAgent.Assistant).ToList();
            Assert.Equal(2, assistant.Count);
            Assert.Single(assistant.Select(i => i.TurnId).Distinct(StringComparer.Ordinal));
            Assert.DoesNotContain(assistant, i => i.TurnId == "provider-a" || i.TurnId == "provider-b");

            // Pre-existing history keeps its own turn identifier.
            var user = session.Request.Body.Interactions.Single(i => i.Agent == AIAgent.User);
            Assert.Equal(userTurnId, user.TurnId);
            Assert.NotEqual(userTurnId, assistant[0].TurnId);
        }

        #endregion

        #region Streaming

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession Stream yields mock provider result [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession Stream yields mock provider result [Core]")]
#endif
        public async Task Stream_MockProvider_YieldsResult()
        {
            var expectedText = "Streaming result";
            var request = CreateTestableRequest(expectedText);
            var executor = new MockProviderExecutor();
            var session = new ConversationSession(request, executor: executor);

            var results = new List<AIReturn>();
            await foreach (var delta in session.Stream(
                new SessionOptions { ProcessTools = false },
                new StreamingOptions(),
                CancellationToken.None).ConfigureAwait(false))
            {
                results.Add(delta);
            }

            Assert.NotEmpty(results);
            var final = results.Last();
            Assert.Equal(expectedText, final.Body?.GetLastText());
        }

        #endregion

        #region Cancellation

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession Cancel interrupts RunToStableResult [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession Cancel interrupts RunToStableResult [Core]")]
#endif
        public async Task Cancel_InterruptsExecution()
        {
            var tcs = new TaskCompletionSource<bool>();
            var request = CreateTestableRequest(onExec: async () =>
            {
                // Wait until cancellation is requested
                await tcs.Task.ConfigureAwait(false);
                return "should not reach";
            });

            var session = new ConversationSession(request);
            var options = new SessionOptions { ProcessTools = false };

            var runTask = session.RunToStableResult(options);

            // Cancel the session
            session.Cancel();
            tcs.TrySetResult(true);

            var result = await runTask.ConfigureAwait(false);

            // Result may be null or error because cancellation happened
            Assert.NotNull(result);
        }

        #endregion

        #region Tool-call history integrity

        // OpenAI-compatible chat APIs (DeepSeek, OpenAI, Mistral, ...) reject an assistant tool_calls message that is
        // not followed by one tool message per call id. The session guarantees that history never carries a
        // pending tool call into a provider request.

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession closes pending tool calls with synthetic results when tool execution is cancelled [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession closes pending tool calls with synthetic results when tool execution is cancelled [Core]")]
#endif
        public async Task RunToStableResult_CancelledDuringToolExecution_ReconcilesPendingToolCalls()
        {
            var request = CreateTestableRequest();
            request.ResponseInteractions = new List<IAIInteraction> { CreateToolCall("call_1") };
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
            var request = CreateTestableRequest("done");
            request.Body = AIBodyBuilder.FromImmutable(request.Body).Add(CreateToolCall("stale_1"), markAsNew: false).Build();
            var pendingSeenByProvider = -1;
            request.OnExec = () =>
            {
                pendingSeenByProvider = request.Body.PendingToolCallsCount();
                return Task.FromResult("done");
            };

            var session = new ConversationSession(request, executor: new MockProviderExecutor());
            await session.RunToStableResult(new SessionOptions { ProcessTools = true }).ConfigureAwait(false);

            Assert.Equal(0, pendingSeenByProvider);
            var interactions = session.Request.Body.Interactions.ToList();
            var callIndex = interactions.FindIndex(i => i is AIInteractionToolCall tc && tc.Id == "stale_1");
            var resultIndex = interactions.FindIndex(i => i is AIInteractionToolResult tr && tr.Id == "stale_1");
            var answerIndex = interactions.FindIndex(i => i is AIInteractionText t && t.Agent == AIAgent.Assistant && t.Content == "done");
            Assert.True(callIndex >= 0 && resultIndex == callIndex + 1, "Synthetic result must directly follow the stale tool call");
            Assert.True(answerIndex > resultIndex, "Provider answer must come after the reconciled tool result");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession stops before the first provider call when the time budget is exhausted [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession stops before the first provider call when the time budget is exhausted [Core]")]
#endif
        public async Task RunToStableResult_TimeBudgetExhausted_StopsBeforeProviderCall()
        {
            var callCount = 0;
            var request = CreateTestableRequest(onExec: () =>
            {
                callCount++;
                return Task.FromResult("unreachable");
            });

            var session = new ConversationSession(request);

            // One TimeSpan tick (~100ns) is consumed before the first budget check runs.
            var result = await session.RunToStableResult(new SessionOptions { MaxAutonomousTime = TimeSpan.FromTicks(1) }).ConfigureAwait(false);

            Assert.Equal(0, callCount);
            Assert.NotNull(result);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession closes pending tool calls when the token budget is exhausted [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession closes pending tool calls when the token budget is exhausted [Core]")]
#endif
        public async Task RunToStableResult_TokenBudgetExhausted_ReconcilesPendingToolCalls()
        {
            // Provider requests a new tool on every call (each reporting tokens), so the run never converges.
            var callCount = 0;
            var request = CreateTestableRequest();
            request.ResponseInteractionsFactory = () => new List<IAIInteraction>
            {
                new AIInteractionToolCall
                {
                    Id = $"call_{++callCount}",
                    Name = "test_tool",
                    Arguments = new JObject(),
                    Metrics = new AIMetrics { InputTokensPrompt = 10, OutputTokensGeneration = 5 },
                },
            };
            var session = new ConversationSession(request, executor: new MockProviderExecutor());

            await session.RunToStableResult(new SessionOptions { ProcessTools = true, MaxAutonomousTokens = 1 }).ConfigureAwait(false);

            Assert.Equal(0, session.Request.Body.PendingToolCallsCount());

            // AIInteractionToolResult derives from AIInteractionToolCall, so results must be excluded explicitly.
            var calls = session.Request.Body.Interactions.Where(i => i is AIInteractionToolCall && i is not AIInteractionToolResult).Select(i => ((AIInteractionToolCall)i).Id).ToList();
            var results = session.Request.Body.Interactions.OfType<AIInteractionToolResult>().Select(tr => tr.Id).ToList();
            Assert.Single(calls); // first call exhausts the budget; later calls never happen
            Assert.Equal(calls.OrderBy(id => id, StringComparer.Ordinal), results.OrderBy(id => id, StringComparer.Ordinal));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession GetAutonomyUsage reports consumption and configured limits [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession GetAutonomyUsage reports consumption and configured limits [Core]")]
#endif
        public async Task RunToStableResult_GetAutonomyUsage_ReportsConsumptionAndLimits()
        {
            var request = CreateTestableRequest();
            request.ResponseInteractions = new List<IAIInteraction>
            {
                new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = "done",
                    Metrics = new AIMetrics { InputTokensPrompt = 42 },
                },
            };

            var session = new ConversationSession(request);
            await session.RunToStableResult(new SessionOptions
            {
                ProcessTools = false,
                MaxAutonomousTime = TimeSpan.FromMinutes(5),
                MaxAutonomousTokens = 1000,
            }).ConfigureAwait(false);

            var usage = session.GetAutonomyUsage();
            Assert.Equal(300, usage.MaxSeconds);
            Assert.Equal(1000, usage.MaxTokens);
            Assert.Equal(42, usage.Tokens);
            Assert.False(usage.IsRunning);
            Assert.False(usage.IsExhausted);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession extracts tool-result images into interaction Images and compacts the result JSON [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession extracts tool-result images into interaction Images and compacts the result JSON [Core]")]
#endif
        public async Task RunToStableResult_ToolResultImage_IsExtractedFromResultJson()
        {
            // The provider requests a tool once, then answers with text.
            var callCount = 0;
            var request = CreateTestableRequest();
            request.ResponseInteractionsFactory = () =>
                ++callCount == 1
                    ? new List<IAIInteraction> { CreateToolCall("call_img") }
                    : new List<IAIInteraction> { new AIInteractionText { Agent = AIAgent.Assistant, Content = "done" } };

            var executor = new MockProviderExecutor
            {
                OnExecTool = tc =>
                {
                    var ret = new AIReturn();
                    ret.SetBody(AIBodyBuilder.Create()
                        .Add(new AIInteractionToolResult
                        {
                            Id = tc.GetToolCall().Id,
                            Name = "canvas_screenshot",
                            Result = new JObject
                            {
                                ["imageBase64"] = "QUJD",
                                ["mimeType"] = "image/png",
                                ["imageAudience"] = "model",
                                ["width"] = 10,
                            },
                        })
                        .Build());
                    return ret;
                },
            };
            var session = new ConversationSession(request, executor: executor);

            await session.RunToStableResult(new SessionOptions { ProcessTools = true }).ConfigureAwait(false);

            var result = session.Request.Body.Interactions.OfType<AIInteractionToolResult>().Single();
            Assert.Null(result.Result["imageBase64"]);
            Assert.Equal("model", result.Result["imageAudience"]?.ToString());
            Assert.True(result.Result["imageAttached"]?.Value<bool>());
            Assert.Equal("image/png", result.Result["mimeType"]?.ToString());

            var image = Assert.Single(result.Images);
            Assert.Equal("QUJD", image.ImageData);
            Assert.Equal("image/png", image.MimeType);
            Assert.True(image.SendToModel);
            Assert.Equal(10, image.Width);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConversationSession hides tools from the provider when ProcessTools is false and restores the filter afterwards [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession hides tools from the provider when ProcessTools is false and restores the filter afterwards [Core]")]
#endif
        public async Task RunToStableResult_ProcessToolsFalse_HidesToolsFromProviderAndRestoresFilter()
        {
            var request = CreateTestableRequest("hi");
            request.Body = request.Body with { ToolFilter = "+gh_*" };
            string filterSeenByProvider = null;
            request.OnExec = () =>
            {
                filterSeenByProvider = request.Body.ToolFilter;
                return Task.FromResult("hi");
            };

            var session = new ConversationSession(request, executor: new MockProviderExecutor());
            await session.RunToStableResult(new SessionOptions { ProcessTools = false }).ConfigureAwait(false);

            Assert.Equal("-*", filterSeenByProvider);
            Assert.Equal("+gh_*", session.Request.Body.ToolFilter);
        }

        #endregion

        #region Helpers

        private static AIInteractionToolCall CreateToolCall(string id)
        {
            return new AIInteractionToolCall
            {
                Id = id,
                Name = "test_tool",
                Arguments = new JObject(),
            };
        }

        private static TestableAIRequestCall CreateTestableRequest(string responseText = null, Func<Task<string>> onExec = null)
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
                ResponseText = responseText ?? "mock response",
                OnExec = onExec,
            };
        }

        /// <summary>
        /// Testable request that bypasses real provider validation and returns a controlled result.
        /// </summary>
        private sealed class TestableAIRequestCall : AIRequestCall
        {
            public string ResponseText { get; set; } = "mock";

            public Func<Task<string>> OnExec { get; set; }

            /// <summary>
            /// When set, returned verbatim as the provider result instead of a single text built from <see cref="ResponseText"/>.
            /// </summary>
            public List<IAIInteraction> ResponseInteractions { get; set; }

            /// <summary>
            /// When set, invoked on every provider call to produce the result interactions (takes precedence over <see cref="ResponseInteractions"/>).
            /// </summary>
            public Func<List<IAIInteraction>> ResponseInteractionsFactory { get; set; }

            public override (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
            {
                // Bypass provider/model/endpoint validation for unit tests
                return (true, new List<SHRuntimeMessage>());
            }

            public override async Task<AIReturn> Exec(bool stream = false, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ret = new AIReturn();
                var interactions = this.ResponseInteractionsFactory?.Invoke() ?? this.ResponseInteractions;
                if (interactions != null)
                {
                    ret.SetBody(interactions);
                    return ret;
                }

                var text = this.OnExec != null ? await this.OnExec() : this.ResponseText;
                var body = AIBodyBuilder.Create()
                    .WithTurnId(Guid.NewGuid().ToString("N"))
                    .AddText(AIAgent.Assistant, text)
                    .Build();

                ret.SetBody(body);
                return ret;
            }
        }

        /// <summary>
        /// Mock executor that returns empty results for tool calls.
        /// </summary>
        private sealed class MockProviderExecutor : IProviderExecutor
        {
            /// <summary>
            /// When set, invoked instead of the default tool execution (may throw to simulate failures).
            /// </summary>
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

        #endregion
    }
}
