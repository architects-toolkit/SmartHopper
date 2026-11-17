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

namespace SmartHopper.Infrastructure.Tests.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.Infrastructure.Mcp;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using Xunit;

    /// <summary>
    /// Execution-contract tests for the MCP layer: sessions, in-flight request
    /// scoping, mutation reservations, lifecycle ownership and bounded observer
    /// waits. No Rhino/Grasshopper dependencies.
    /// </summary>
    public class McpExecutionContractTests
    {
        private const string ReadOnlySchema = "{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\"}}}";

        private static IReadOnlyDictionary<string, AITool> Catalog(params (string name, bool mutatesCanvas)[] entries)
        {
            var dict = new Dictionary<string, AITool>();
            foreach (var (name, mutatesCanvas) in entries)
            {
                dict[name] = new AITool(
                    name: name,
                    description: $"Test tool {name}",
                    category: "Test",
                    parametersSchema: ReadOnlySchema,
                    execute: _ => Task.FromResult(new AIReturn()),
                    mutatesCanvas: mutatesCanvas);
            }

            return dict;
        }

        private static AIReturn OkReturn(AIToolCall call, JObject payload)
        {
            var ret = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
            {
                Name = call.GetToolCall().Name,
                Result = payload,
            }));
            return ret;
        }

        // ------------------------------------------------------------------
        // Session requirement and reservation outcomes
        // ------------------------------------------------------------------

        [Fact]
        public async Task MutatingRequestId_WithoutSession_ReturnsSessionRequired()
        {
            var tools = Catalog(("gh_put", true));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                call => Task.FromResult(OkReturn(call, new JObject { ["ok"] = true })));

            var result = await adapter.ExecuteAsync(
                "gh_put",
                new JObject { ["requestId"] = "r1" });

            Assert.True(result.IsError);
            Assert.Equal("session_required", (string?)result.Payload?["code"]);
        }

        [Fact]
        public async Task MutatingRequestId_ConcurrentSameKey_ExecutesOnceAndJoins()
        {
            var tools = Catalog(("gh_put", true));
            var calls = 0;
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<AIReturn> Executor(AIToolCall call)
            {
                Interlocked.Increment(ref calls);
                return release.Task.ContinueWith(_ => OkReturn(call, new JObject { ["ok"] = true }));
            }

            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                (Func<AIToolCall, Task<AIReturn>>)Executor);
            var session = new McpSession("s1");
            var args = new JObject { ["requestId"] = "r-join" };

            var first = adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            var second = adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            await Task.Delay(50);
            release.SetResult(true);

            var r1 = await first;
            var r2 = await second;
            Assert.Equal(1, calls);
            Assert.False(r1.IsError);
            Assert.False(r2.IsError);
        }

        [Fact]
        public async Task MutatingRequestId_DifferentArguments_ReturnsIdempotencyConflict()
        {
            var tools = Catalog(("gh_put", true));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                call => Task.FromResult(OkReturn(call, new JObject { ["ok"] = true })));
            var session = new McpSession("s1");
            var requestId = $"req-{Guid.NewGuid():N}";

            var first = await adapter.ExecuteAsync(
                "gh_put",
                new JObject { ["requestId"] = requestId, ["q"] = "a" },
                session,
                CancellationToken.None);
            var second = await adapter.ExecuteAsync(
                "gh_put",
                new JObject { ["requestId"] = requestId, ["q"] = "b" },
                session,
                CancellationToken.None);

            Assert.False(first.IsError);
            Assert.True(second.IsError);
            Assert.Equal("idempotency_conflict", (string?)second.Payload?["code"]);
        }

        [Fact]
        public async Task MutatingRequestId_ReplayedResult_IsCloned()
        {
            var tools = Catalog(("gh_put", true));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                call => Task.FromResult(OkReturn(call, new JObject { ["nested"] = new JObject { ["v"] = 1 } })));
            var session = new McpSession("s1");
            var args = new JObject { ["requestId"] = $"req-{Guid.NewGuid():N}" };

            var first = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            var replayed = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);

            ((JObject)replayed.Payload["nested"])["v"] = 999;
            var replayedAgain = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            Assert.Equal(1, (int?)((JObject)replayedAgain.Payload["nested"])["v"]);
        }

        [Fact]
        public async Task MutatingRequestId_DocumentBindingChanged_ReturnsDocumentChanged()
        {
            var tools = Catalog(("gh_put", true));
            var binding = "doc-a";
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                (call, _) => Task.FromResult(OkReturn(call, new JObject { ["ok"] = true })),
                null,
                _ => binding);
            var session = new McpSession("s1");
            var requestId = $"req-{Guid.NewGuid():N}";
            var args = new JObject { ["requestId"] = requestId };

            var first = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            binding = "doc-b";
            var second = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);

            Assert.False(first.IsError);
            Assert.True(second.IsError);
            Assert.Equal("document_changed", (string?)second.Payload?["code"]);
        }

        [Fact]
        public async Task MutatingRequestId_SlowOperation_ReportsInProgressThenCompletes()
        {
            var tools = Catalog(("gh_put", true));
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions { ExposeMutatingTools = true },
                () => tools,
                (call, _) => release.Task.ContinueWith(_ => OkReturn(call, new JObject { ["applied"] = true })),
                null,
                null,
                TimeSpan.FromMilliseconds(150));
            var session = new McpSession("s1");
            var args = new JObject { ["requestId"] = $"req-{Guid.NewGuid():N}" };

            var first = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            Assert.False(first.IsError);
            Assert.Equal("operation_in_progress", (string?)first.Payload?["status"]);
            Assert.False(string.IsNullOrEmpty((string?)first.Payload?["operationId"]));

            release.SetResult(true);
            var retry = await adapter.ExecuteAsync("gh_put", args, session, CancellationToken.None);
            Assert.False(retry.IsError);
            Assert.True((bool?)retry.Payload?["applied"]);
        }

        // ------------------------------------------------------------------
        // Dispatcher: sessions, typed ids, cancellation
        // ------------------------------------------------------------------

        [Fact]
        public async Task Initialize_ReturnsSessionId_AndUnknownSessionIsRejected()
        {
            var tools = Catalog(("gh_get", false));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions(),
                () => tools,
                call => Task.FromResult(OkReturn(call, new JObject { ["ok"] = true })));
            var dispatcher = new JsonRpcDispatcher(new McpServerOptions(), adapter);

            var init = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}", null);
            Assert.False(string.IsNullOrEmpty(init.SessionId));
            Assert.Equal(200, init.StatusHint);

            var unknown = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}", "deadbeef-not-a-session");
            Assert.Equal(404, unknown.StatusHint);

            var ping = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}", init.SessionId);
            Assert.Equal(200, ping.StatusHint);
            Assert.Contains("\"result\"", ping.Body);
        }

        [Fact]
        public async Task DuplicateInFlightRequestId_IsRejected()
        {
            var tools = Catalog(("gh_get", false));
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<AIReturn>(TaskCreationOptions.RunContinuationsAsynchronously);
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions(),
                () => tools,
                call =>
                {
                    entered.TrySetResult(true);
                    return release.Task;
                });
            var dispatcher = new JsonRpcDispatcher(new McpServerOptions(), adapter);

            var init = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}", null);
            var sessionId = init.SessionId;

            var first = dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":\"x\",\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}",
                sessionId);
            await entered.Task;

            var second = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":\"x\",\"method\":\"ping\"}",
                sessionId);
            Assert.Contains("Duplicate request id", second.Body);

            var ret = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            ret.SetBody(AIBody.Empty.WithAppended(new AIInteractionToolResult
            {
                Name = "gh_get",
                Result = new JObject { ["ok"] = true },
            }));
            release.SetResult(ret);
            var firstResult = await first;
            Assert.Equal(200, firstResult.StatusHint);
        }

        [Fact]
        public async Task CancelledNotification_CancelsInFlightRequest()
        {
            var tools = Catalog(("gh_get", false));
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions(),
                () => tools,
                async (call, ct) =>
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                    return OkReturn(call, new JObject());
                });
            var dispatcher = new JsonRpcDispatcher(new McpServerOptions(), adapter);

            var init = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}", null);
            var sessionId = init.SessionId;

            var inflight = dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":\"c1\",\"method\":\"tools/call\",\"params\":{\"name\":\"gh_get\",\"arguments\":{}}}",
                sessionId);
            await entered.Task;

            var cancel = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/cancelled\",\"params\":{\"id\":\"c1\"}}",
                sessionId);
            Assert.Equal(204, cancel.StatusHint);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inflight);
        }

        [Fact]
        public async Task NotificationWithoutId_ProducesNoResponseBody()
        {
            var tools = Catalog(("gh_get", false));
            var adapter = new AIToolMcpAdapter(
                new McpServerOptions(),
                () => tools,
                call => Task.FromResult(OkReturn(call, new JObject { ["ok"] = true })));
            var dispatcher = new JsonRpcDispatcher(new McpServerOptions(), adapter);

            var init = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}", null);
            var notification = await dispatcher.DispatchRequestAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
                init.SessionId);

            Assert.Equal(204, notification.StatusHint);
            Assert.Null(notification.Body);
        }

        // ------------------------------------------------------------------
        // McpRequestRegistry: typed ids
        // ------------------------------------------------------------------

        [Fact]
        public void RequestRegistry_StringAndNumericIds_AreDistinct()
        {
            var registry = new McpRequestRegistry();
            Assert.True(registry.TryBegin(new JValue("1"), CancellationToken.None, out var stringScope));
            Assert.True(registry.TryBegin(new JValue(1), CancellationToken.None, out var numericScope));
            Assert.False(registry.TryBegin(new JValue("1"), CancellationToken.None, out _));

            stringScope?.Dispose();
            numericScope?.Dispose();
        }

        [Fact]
        public void RequestRegistry_CancelUnknownId_IsHarmless()
        {
            var registry = new McpRequestRegistry();
            Assert.False(registry.Cancel(new JValue("nope")));
            Assert.False(registry.Cancel(null));
        }

        // ------------------------------------------------------------------
        // McpSessionRegistry: bounded, idle-expiring, operation-protected
        // ------------------------------------------------------------------

        [Fact]
        public void SessionRegistry_IdleSessionsExpire_ButActiveOnesSurvive()
        {
            var now = DateTime.UtcNow;
            var registry = new McpSessionRegistry(() => now);
            var idle = registry.TryCreate();
            var active = registry.TryCreate();
            Assert.NotNull(idle);
            Assert.NotNull(active);
            active!.AddOwnedOperation();

            now += McpSessionRegistry.IdleLifetime + TimeSpan.FromMinutes(1);

            Assert.Null(registry.TryGet(idle!.Id));
            Assert.NotNull(registry.TryGet(active.Id));
        }

        [Fact]
        public void SessionRegistry_RefusesBeyondCapacity()
        {
            var registry = new McpSessionRegistry();
            var sessions = new List<McpSession>();
            for (var i = 0; i < McpSessionRegistry.MaxSessions; i++)
            {
                var session = registry.TryCreate();
                Assert.NotNull(session);
                session!.AddOwnedOperation(); // keep all alive: capacity is not eviction of owned work
                sessions.Add(session);
            }

            Assert.Null(registry.TryCreate());
        }

        // ------------------------------------------------------------------
        // McpMutationReservations: unit-level attempts
        // ------------------------------------------------------------------

        [Fact]
        public void Reservations_SameKeyDifferentFingerprint_Conflicts()
        {
            var reservations = new McpMutationReservations();
            var session = new McpSession("s");
            var attempt = reservations.TryReserve(session, "gh_put", "r1", "fp-a", null, out _);
            Assert.Equal(McpMutationReservations.Attempt.Owned, attempt);

            var retry = reservations.TryReserve(session, "gh_put", "r1", "fp-b", null, out _);
            Assert.Equal(McpMutationReservations.Attempt.Conflict, retry);
        }

        [Fact]
        public async Task Reservations_CompletePublishesTerminalResult()
        {
            var reservations = new McpMutationReservations();
            var session = new McpSession("s");
            Assert.Equal(
                McpMutationReservations.Attempt.Owned,
                reservations.TryReserve(session, "gh_put", "r1", "fp", null, out var owned));

            var terminal = McpToolCallResult.Ok(new JObject { ["done"] = true });
            reservations.Complete(session, owned!, terminal);

            var replay = reservations.TryReserve(session, "gh_put", "r1", "fp", null, out var joined);
            Assert.Equal(McpMutationReservations.Attempt.Joined, replay);
            var replayed = await joined!.Completion;
            Assert.True((bool?)replayed.Payload["done"]);
        }

        [Fact]
        public void Reservations_RecordedBindingJoinsWithoutResolution()
        {
            var reservations = new McpMutationReservations();
            var session = new McpSession("s");
            Assert.Equal(
                McpMutationReservations.Attempt.Owned,
                reservations.TryReserve(session, "gh_put", "r1", "fp", "doc-a", out _));

            // Retry cannot resolve the document (UI busy): recorded binding still joins.
            var retry = reservations.TryReserve(session, "gh_put", "r1", "fp", null, out _);
            Assert.Equal(McpMutationReservations.Attempt.Joined, retry);

            // Retry resolving to a different document is rejected, not re-executed.
            var drift = reservations.TryReserve(session, "gh_put", "r1", "fp", "doc-b", out _);
            Assert.Equal(McpMutationReservations.Attempt.DocumentChanged, drift);
        }

        // ------------------------------------------------------------------
        // ToolExecutionLifecycle transitions
        // ------------------------------------------------------------------

        [Fact]
        public void Lifecycle_QueuedCancelRacesCommitClaim()
        {
            var lifecycle = new ToolExecutionLifecycle();
            lifecycle.TryTransition(ToolExecutionState.Preparing, ToolExecutionState.Queued);

            if (lifecycle.TryClaimCommit())
            {
                Assert.False(lifecycle.TryCancelBeforeStart());
                Assert.True(lifecycle.MayBeApplying);
            }
            else
            {
                lifecycle.TryCancelBeforeStart();
                Assert.Equal(ToolExecutionState.CancelledNotStarted, lifecycle.State);
            }
        }

        [Fact]
        public void Lifecycle_TerminalWritesAreSingleShot()
        {
            var lifecycle = new ToolExecutionLifecycle();
            lifecycle.TryTransition(ToolExecutionState.Preparing, ToolExecutionState.Queued);
            Assert.True(lifecycle.TryClaimCommit());
            Assert.True(lifecycle.TryComplete(ToolExecutionState.Applied));
            Assert.False(lifecycle.TryComplete(ToolExecutionState.FailedPartial));
            Assert.False(lifecycle.TryCancelBeforeStart());
            Assert.True(lifecycle.IsTerminal);
        }

        [Fact]
        public void Lifecycle_CancelBeforeClaim_IsSideEffectFree()
        {
            var lifecycle = new ToolExecutionLifecycle();
            Assert.True(lifecycle.TryCancelBeforeStart());
            Assert.Equal(ToolExecutionState.CancelledNotStarted, lifecycle.State);
            Assert.False(lifecycle.CommitClaimed);
        }
    }
}
