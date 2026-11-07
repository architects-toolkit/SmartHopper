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

namespace SmartHopper.Infrastructure.Tests.AICall.Subagents
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Sessions;
    using SmartHopper.Infrastructure.AICall.Subagents;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.Diagnostics;
    using SmartHopper.ProviderSdk.Streaming;
    using Xunit;
    /// <summary>
    /// Contract tests for <see cref="SubagentHost"/>: spawn/send/wait/list semantics,
    /// terminal-run reporting, and the delivered flag that drives wake turns.
    /// Child sessions are substituted through the session factory so runs complete
    /// without a real provider.
    /// </summary>
    public class SubagentHostTests
    {
        [Fact]
        public async Task SpawnAsync_RunCompletes_FiresRunFinishedWithResult()
        {
            var host = NewHost();
            var reportSource = new TaskCompletionSource<SubagentRunReport>();
            host.RunFinished += (_, report) => reportSource.TrySetResult(report);

            var item = await host.SpawnAsync("scraper", "scan the document", CancellationToken.None);

            Assert.Equal("running", item.Status);
            Assert.False(string.IsNullOrWhiteSpace(item.SubagentId));
            Assert.False(string.IsNullOrWhiteSpace(item.RunId));

            var report = await WithTimeout(reportSource.Task);
            Assert.Equal(item.SubagentId, report.SubagentId);
            Assert.Equal("scraper", report.Label);
            Assert.Equal(SubagentRunStatus.Succeeded, report.Status);
            Assert.Equal("child done", report.Result);

            // The report fired but nothing collected the outcome yet: it is still
            // pending delivery, which is what makes the conversation wake up.
            Assert.False(report.Run.Delivered);
        }

        [Fact]
        public async Task WaitAsync_TerminalRun_CollectsResultAndMarksDelivered()
        {
            var host = NewHost();
            var reportSource = new TaskCompletionSource<SubagentRunReport>();
            host.RunFinished += (_, report) => reportSource.TrySetResult(report);

            var item = await host.SpawnAsync("scraper", "scan the document", CancellationToken.None);
            var report = await WithTimeout(reportSource.Task);

            var items = await host.WaitAsync(new[] { item.SubagentId }, timeoutSeconds: 5, CancellationToken.None);

            var collected = Assert.Single(items);
            Assert.Equal("succeeded", collected.Status);
            Assert.Equal("child done", collected.Result);
            Assert.True(report.Run.Delivered);
        }

        [Fact]
        public async Task WaitAsync_StillRunning_ReturnsRunningStatusWithoutDelivering()
        {
            var gate = new TaskCompletionSource<bool>();
            var host = NewHost(onExec: async ct =>
            {
                await gate.Task.ConfigureAwait(false);
                return TextReturn("child done");
            });
            var reportSource = new TaskCompletionSource<SubagentRunReport>();
            host.RunFinished += (_, report) => reportSource.TrySetResult(report);

            var item = await host.SpawnAsync("scraper", "scan the document", CancellationToken.None);

            var items = await host.WaitAsync(new[] { item.SubagentId }, timeoutSeconds: 1, CancellationToken.None);
            var collected = Assert.Single(items);
            Assert.Equal("running", collected.Status);
            Assert.Equal(1, host.RunningCount);

            gate.TrySetResult(true);
            var report = await WithTimeout(reportSource.Task);
            Assert.False(report.Run.Delivered);
        }

        [Fact]
        public async Task SendAsync_IdleSubagent_StartsNewRun_ResolvesByIdPrefix()
        {
            var host = NewHost();
            var first = new TaskCompletionSource<SubagentRunReport>();
            var second = new TaskCompletionSource<SubagentRunReport>();
            var seen = 0;
            host.RunFinished += (_, report) =>
            {
                if (Interlocked.Increment(ref seen) == 1)
                {
                    first.TrySetResult(report);
                }
                else
                {
                    second.TrySetResult(report);
                }
            };

            var item = await host.SpawnAsync("scraper", "first brief", CancellationToken.None);
            await WithTimeout(first.Task);

            // Models sometimes truncate ids — an unambiguous prefix resolves.
            var followUp = await host.SendAsync(item.SubagentId.Substring(0, 8), "second instruction", CancellationToken.None);
            Assert.Equal("running", followUp.Status);
            Assert.NotEqual(item.RunId, followUp.RunId);

            var report = await WithTimeout(second.Task);
            Assert.Equal("second instruction", report.Run.Instruction);
        }

        [Fact]
        public async Task SendAsync_RunningSubagent_Fails()
        {
            var gate = new TaskCompletionSource<bool>();
            var host = NewHost(onExec: async ct =>
            {
                await gate.Task.ConfigureAwait(false);
                return TextReturn("child done");
            });

            var item = await host.SpawnAsync("scraper", "first brief", CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.SendAsync(item.SubagentId, "second instruction", CancellationToken.None));

            gate.TrySetResult(true);
        }

        [Fact]
        public async Task SpawnAsync_BeyondLimit_Fails()
        {
            var host = NewHost();
            for (var i = 0; i < SubagentHost.MaxSubagents; i++)
            {
                await host.SpawnAsync($"worker-{i}", "brief", CancellationToken.None);
            }

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.SpawnAsync("overflow", "brief", CancellationToken.None));
        }

        [Fact]
        public async Task SpawnAsync_ChildRequest_ExcludesConductorTools()
        {
            var captured = new List<AIRequestCall>();
            var host = NewHost(parentFilter: "gh_* -gh_put", captured: captured);

            await host.SpawnAsync("scraper", "scan the document", CancellationToken.None);

            var childFilter = Assert.Single(captured).Body.ToolFilter;
            Assert.Contains("gh_*", childFilter);
            Assert.Contains("-gh_put", childFilter);
            Assert.Contains("-spawn_subagent", childFilter);
            Assert.Contains("-send_to_subagent", childFilter);
            Assert.Contains("-wait_subagents", childFilter);
            Assert.Contains("-list_subagents", childFilter);
        }

        [Fact]
        public async Task RunWithoutAssistantReply_ReportsFailed()
        {
            var host = NewHost(onExec: _ => Task.FromResult(new AIReturn()));
            var reportSource = new TaskCompletionSource<SubagentRunReport>();
            host.RunFinished += (_, report) => reportSource.TrySetResult(report);

            await host.SpawnAsync("scraper", "scan the document", CancellationToken.None);

            var report = await WithTimeout(reportSource.Task);
            Assert.Equal(SubagentRunStatus.Failed, report.Status);
            Assert.NotNull(report.Error);
        }

        [Fact]
        public async Task CancelAll_InFlightRun_ReportsFailed()
        {
            var host = NewHost(onExec: async ct =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);
                return TextReturn("unreachable");
            });
            var reportSource = new TaskCompletionSource<SubagentRunReport>();
            host.RunFinished += (_, report) => reportSource.TrySetResult(report);

            await host.SpawnAsync("scraper", "scan the document", CancellationToken.None);
            await Task.Delay(100);

            host.CancelAll();

            var report = await WithTimeout(reportSource.Task);
            Assert.Equal(SubagentRunStatus.Failed, report.Status);
        }

        /// <summary>
        /// Builds a host whose child sessions execute against a testable request.
        /// <paramref name="captured"/> receives the child request the host built so
        /// tests can assert the cloned provider/model/filter contract.
        /// </summary>
        private static SubagentHost NewHost(
            Func<CancellationToken, Task<AIReturn>>? onExec = null,
            string responseText = "child done",
            string parentFilter = "-gh_put",
            List<AIRequestCall>? captured = null)
        {
            var exec = new MockProviderExecutor();
            var parent = new AIRequestCall
            {
                Provider = "test-provider",
                Model = "test-model",
                Endpoint = "https://test.example.com",
                Body = AIBodyBuilder.Create().WithToolFilter(parentFilter).Build(),
            };

            return new SubagentHost(
                parent,
                executor: exec,
                childSessionFactory: req =>
                {
                    captured?.Add(req);
                    var testable = new TestableAIRequestCall
                    {
                        Provider = "test-provider",
                        Model = "test-model",
                        Endpoint = "https://test.example.com",
                        Body = req.Body,
                        ResponseText = responseText,
                        OnExec = onExec,
                    };
                    return new ConversationSession(testable, observer: null, executor: exec, generateGreeting: false);
                });
        }

        private static AIReturn TextReturn(string text)
        {
            var ret = new AIReturn();
            ret.SetBody(AIBodyBuilder.Create().AddText(AIAgent.Assistant, text).Build());
            return ret;
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int seconds = 15)
        {
            var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds))).ConfigureAwait(false);
            if (winner != task)
            {
                throw new TimeoutException("Timed out waiting for the subagent report.");
            }

            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Testable request that bypasses real provider validation and returns a controlled result.
        /// </summary>
        private sealed class TestableAIRequestCall : AIRequestCall
        {
            public string ResponseText { get; set; } = "mock";

            /// <summary>
            /// Optional custom provider turn. The session invokes <see cref="Exec"/> for
            /// provider calls (the executor is only used for tool calls), so this is the
            /// seam that controls blocking, failure, and empty-return behavior.
            /// </summary>
            public Func<CancellationToken, Task<AIReturn>>? OnExec { get; set; }

            public override (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
            {
                return (true, new List<SHRuntimeMessage>());
            }

            public override Task<AIReturn> Exec(bool stream = false, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return this.OnExec?.Invoke(cancellationToken) ?? Task.FromResult(TextReturn(this.ResponseText));
            }
        }

        /// <summary>
        /// Executor used for tool calls in the child session; provider turns bypass it
        /// (<see cref="ConversationSession"/> invokes the request's own Exec).
        /// </summary>
        private sealed class MockProviderExecutor : IProviderExecutor
        {
            public Task<AIReturn?> ExecProviderAsync(AIRequestCall request, CancellationToken ct)
            {
                return Task.FromResult<AIReturn?>(TextReturn("child done"));
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
    }
}
