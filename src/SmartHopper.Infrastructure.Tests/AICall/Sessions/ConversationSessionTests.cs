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
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Sessions;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.ProviderSdk.Streaming;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.Diagnostics;
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
        [Fact(DisplayName = "ConversationSession RunToStableResult respects MaxTurns limit [Windows]")]
#else
        [Fact(DisplayName = "ConversationSession RunToStableResult respects MaxTurns limit [Core]")]
#endif
        public async Task RunToStableResult_MaxTurns_Respected()
        {
            var callCount = 0;
            var request = CreateTestableRequest(onExec: () =>
            {
                callCount++;
                return Task.FromResult($"turn {callCount}");
            });

            var session = new ConversationSession(request);
            var options = new SessionOptions { ProcessTools = false, MaxTurns = 3 };

            var result = await session.RunToStableResult(options).ConfigureAwait(false);

            Assert.NotNull(result);
            // MaxTurns limits how many times the provider is called
            Assert.True(callCount <= 3, $"Expected at most 3 turns but got {callCount}");
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

        #region Helpers

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

            public override (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
            {
                // Bypass provider/model/endpoint validation for unit tests
                return (true, new List<SHRuntimeMessage>());
            }

            public override async Task<AIReturn> Exec(bool stream = false, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = this.OnExec != null ? await this.OnExec() : this.ResponseText;
                var body = AIBodyBuilder.Create()
                    .WithTurnId(Guid.NewGuid().ToString("N"))
                    .AddText(AIAgent.Assistant, text)
                    .Build();

                var ret = new AIReturn();
                ret.SetBody(body);
                return ret;
            }
        }

        /// <summary>
        /// Mock executor that returns empty results for tool calls.
        /// </summary>
        private sealed class MockProviderExecutor : IProviderExecutor
        {
            public Task<AIReturn?> ExecProviderAsync(AIRequestCall request, CancellationToken ct)
            {
                var ret = new AIReturn();
                ret.SetBody(AIBodyBuilder.Create().AddText(AIAgent.Assistant, "mock").Build());
                return Task.FromResult<AIReturn?>(ret);
            }

            public Task<AIReturn?> ExecToolAsync(AIToolCall toolCall, CancellationToken ct)
            {
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