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

namespace SmartHopper.Infrastructure.Tests.AICall.Execution
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.Streaming;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="DefaultProviderExecutor"/> class.
    /// </summary>
    public class DefaultProviderExecutorTests
    {
        #region ExecProviderAsync

#if NET7_WINDOWS
        [Fact(DisplayName = "DefaultProviderExecutor ExecProviderAsync null request returns null [Windows]")]
#else
        [Fact(DisplayName = "DefaultProviderExecutor ExecProviderAsync null request returns null [Core]")]
#endif
        public async Task ExecProviderAsync_NullRequest_ReturnsNull()
        {
            var executor = new DefaultProviderExecutor();
            var result = await executor.ExecProviderAsync(null, CancellationToken.None).ConfigureAwait(false);
            Assert.Null(result);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "DefaultProviderExecutor ExecProviderAsync delegates to request.Exec [Windows]")]
#else
        [Fact(DisplayName = "DefaultProviderExecutor ExecProviderAsync delegates to request.Exec [Core]")]
#endif
        public async Task ExecProviderAsync_ValidRequest_DelegatesToExec()
        {
            var request = new TestableRequest
            {
                Provider = "test",
                Model = "test-model",
                Endpoint = "https://test.example.com",
                Body = AIBodyBuilder.Create()
                    .WithTurnId(Guid.NewGuid().ToString("N"))
                    .AddText(AIAgent.User, "hello")
                    .Build(),
            };

            var executor = new DefaultProviderExecutor();
            var result = await executor.ExecProviderAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.Equal("mock-result", result.Body?.GetLastText());
        }

        #endregion

        #region ExecToolAsync

#if NET7_WINDOWS
        [Fact(DisplayName = "DefaultProviderExecutor ExecToolAsync null toolCall returns null [Windows]")]
#else
        [Fact(DisplayName = "DefaultProviderExecutor ExecToolAsync null toolCall returns null [Core]")]
#endif
        public async Task ExecToolAsync_NullToolCall_ReturnsNull()
        {
            var executor = new DefaultProviderExecutor();
            var result = await executor.ExecToolAsync(null, CancellationToken.None).ConfigureAwait(false);
            Assert.Null(result);
        }

        #endregion

        #region TryGetStreamingAdapter

#if NET7_WINDOWS
        [Fact(DisplayName = "DefaultProviderExecutor TryGetStreamingAdapter null request returns null [Windows]")]
#else
        [Fact(DisplayName = "DefaultProviderExecutor TryGetStreamingAdapter null request returns null [Core]")]
#endif
        public void TryGetStreamingAdapter_NullRequest_ReturnsNull()
        {
            var executor = new DefaultProviderExecutor();
            var adapter = executor.TryGetStreamingAdapter(null);
            Assert.Null(adapter);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "DefaultProviderExecutor TryGetStreamingAdapter request without provider returns null [Windows]")]
#else
        [Fact(DisplayName = "DefaultProviderExecutor TryGetStreamingAdapter request without provider returns null [Core]")]
#endif
        public void TryGetStreamingAdapter_NoProvider_ReturnsNull()
        {
            var request = new AIRequestCall();
            var executor = new DefaultProviderExecutor();
            var adapter = executor.TryGetStreamingAdapter(request);
            Assert.Null(adapter);
        }

        #endregion

        #region Cancellation

#if NET7_WINDOWS
        [Fact(DisplayName = "DefaultProviderExecutor ExecProviderAsync respects cancellation token [Windows]")]
#else
        [Fact(DisplayName = "DefaultProviderExecutor ExecProviderAsync respects cancellation token [Core]")]
#endif
        public async Task ExecProviderAsync_Cancellation_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var request = new TestableRequest { DelayMs = 100 };
            var executor = new DefaultProviderExecutor();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await executor.ExecProviderAsync(request, cts.Token).ConfigureAwait(false));
        }

        #endregion

        #region Helpers

        private sealed class TestableRequest : AIRequestCall
        {
            public int DelayMs { get; set; }

            public override (bool IsValid, System.Collections.Generic.List<Diagnostics.SHRuntimeMessage> Errors) IsValid()
            {
                return (true, new System.Collections.Generic.List<Diagnostics.SHRuntimeMessage>());
            }

            public override async Task<AIReturn> Exec(bool stream = false, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (this.DelayMs > 0)
                {
                    await Task.Delay(this.DelayMs, cancellationToken).ConfigureAwait(false);
                }

                var body = AIBodyBuilder.Create()
                    .WithTurnId(Guid.NewGuid().ToString("N"))
                    .AddText(AIAgent.Assistant, "mock-result")
                    .Build();

                var ret = new AIReturn();
                ret.SetBody(body);
                return ret;
            }
        }

        #endregion
    }
}
