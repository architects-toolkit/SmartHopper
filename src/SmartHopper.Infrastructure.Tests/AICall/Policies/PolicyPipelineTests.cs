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

namespace SmartHopper.Infrastructure.Tests.AICall.Policies
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Policies;
    using SmartHopper.Infrastructure.AICall.Policies.Request;
    using SmartHopper.Infrastructure.AICall.Policies.Response;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.Diagnostics;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="PolicyPipeline"/> class.
    /// Validates request/response policy ordering, exception handling, and the tool-call shim path.
    /// </summary>
    public class PolicyPipelineTests
    {
        #region Default Pipeline Composition

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline Default contains expected request policies in order [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline Default contains expected request policies in order [Core]")]
#endif
        public void Default_RequestPolicies_OrderedCorrectly()
        {
            var pipeline = PolicyPipeline.Default;
            Assert.NotNull(pipeline);
            Assert.Equal(6, pipeline.RequestPolicies.Count);

            var expectedOrder = new[]
            {
                typeof(RequestTimeoutPolicy),
                typeof(ToolFilterNormalizationRequestPolicy),
                typeof(AIToolValidationRequestPolicy),
                typeof(ContextInjectionRequestPolicy),
                typeof(SchemaAttachRequestPolicy),
                typeof(SchemaValidateRequestPolicy),
            };

            var actualOrder = pipeline.RequestPolicies.Select(p => p.GetType()).ToList();
            Assert.Equal(expectedOrder, actualOrder);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline Default contains expected response policies in order [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline Default contains expected response policies in order [Core]")]
#endif
        public void Default_ResponsePolicies_OrderedCorrectly()
        {
            var pipeline = PolicyPipeline.Default;
            Assert.NotNull(pipeline);
            Assert.Equal(2, pipeline.ResponsePolicies.Count);

            var expectedOrder = new[]
            {
                typeof(SchemaValidateResponsePolicy),
                typeof(FinishReasonNormalizeResponsePolicy),
            };

            var actualOrder = pipeline.ResponsePolicies.Select(p => p.GetType()).ToList();
            Assert.Equal(expectedOrder, actualOrder);
        }

        #endregion

        #region Request Policy Application

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline ApplyRequestPoliciesAsync applies policies in order [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline ApplyRequestPoliciesAsync applies policies in order [Core]")]
#endif
        public async Task ApplyRequestPoliciesAsync_AppliesInOrder()
        {
            var pipeline = new PolicyPipeline();
            var callOrder = new List<string>();

            pipeline.RequestPolicies.Add(new TrackingRequestPolicy("first", callOrder));
            pipeline.RequestPolicies.Add(new TrackingRequestPolicy("second", callOrder));
            pipeline.RequestPolicies.Add(new TrackingRequestPolicy("third", callOrder));

            var request = CreateMinimalRequest();
            await pipeline.ApplyRequestPoliciesAsync(request).ConfigureAwait(false);

            Assert.Equal(new[] { "first", "second", "third" }, callOrder);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline ApplyRequestPoliciesAsync null request is no-op [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline ApplyRequestPoliciesAsync null request is no-op [Core]")]
#endif
        public async Task ApplyRequestPoliciesAsync_NullRequest_IsNoOp()
        {
            var pipeline = new PolicyPipeline();
            var tracker = new TrackingRequestPolicy("should-not-run", new List<string>());
            pipeline.RequestPolicies.Add(tracker);

            await pipeline.ApplyRequestPoliciesAsync((AIRequestCall)null).ConfigureAwait(false);

            Assert.Empty(tracker.Calls);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline exception in request policy is caught and converted to diagnostic [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline exception in request policy is caught and converted to diagnostic [Core]")]
#endif
        public async Task ApplyRequestPoliciesAsync_Exception_CapturedAsDiagnostic()
        {
            var pipeline = new PolicyPipeline();
            pipeline.RequestPolicies.Add(new ThrowingRequestPolicy("explode"));
            pipeline.RequestPolicies.Add(new TrackingRequestPolicy("after", new List<string>()));

            var request = CreateMinimalRequest();
            await pipeline.ApplyRequestPoliciesAsync(request).ConfigureAwait(false);

            // Exception should be caught; subsequent policies should still run
            var bodyMessages = request.Body?.Messages;
            Assert.NotNull(bodyMessages);
            Assert.Contains(bodyMessages, m => m.Message.Contains("explode"));
        }

        #endregion

        #region Response Policy Application

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline ApplyResponsePoliciesAsync applies policies in order [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline ApplyResponsePoliciesAsync applies policies in order [Core]")]
#endif
        public async Task ApplyResponsePoliciesAsync_AppliesInOrder()
        {
            var pipeline = new PolicyPipeline();
            var callOrder = new List<string>();

            pipeline.ResponsePolicies.Add(new TrackingResponsePolicy("first", callOrder));
            pipeline.ResponsePolicies.Add(new TrackingResponsePolicy("second", callOrder));

            var response = new AIReturn();
            response.SetBody(AIBody.Empty);
            await pipeline.ApplyResponsePoliciesAsync(response).ConfigureAwait(false);

            Assert.Equal(new[] { "first", "second" }, callOrder);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline ApplyResponsePoliciesAsync null response is no-op [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline ApplyResponsePoliciesAsync null response is no-op [Core]")]
#endif
        public async Task ApplyResponsePoliciesAsync_NullResponse_IsNoOp()
        {
            var pipeline = new PolicyPipeline();
            var tracker = new TrackingResponsePolicy("should-not-run", new List<string>());
            pipeline.ResponsePolicies.Add(tracker);

            await pipeline.ApplyResponsePoliciesAsync(null).ConfigureAwait(false);

            Assert.Empty(tracker.Calls);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline exception in response policy is caught and attached to response [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline exception in response policy is caught and attached to response [Core]")]
#endif
        public async Task ApplyResponsePoliciesAsync_Exception_CapturedAsDiagnostic()
        {
            var pipeline = new PolicyPipeline();
            pipeline.ResponsePolicies.Add(new ThrowingResponsePolicy("boom"));

            var response = new AIReturn();
            response.SetBody(AIBody.Empty);
            await pipeline.ApplyResponsePoliciesAsync(response).ConfigureAwait(false);

            var messages = response.Messages;
            Assert.NotNull(messages);
            Assert.Contains(messages, m => m.Message.Contains("boom"));
        }

        #endregion

        #region Tool Call Shim

#if NET7_WINDOWS
        [Fact(DisplayName = "PolicyPipeline ApplyRequestPoliciesAsync tool-call shim merges timeout and diagnostics [Windows]")]
#else
        [Fact(DisplayName = "PolicyPipeline ApplyRequestPoliciesAsync tool-call shim merges timeout and diagnostics [Core]")]
#endif
        public async Task ApplyRequestPoliciesAsync_ToolCallShim_MergesBack()
        {
            var pipeline = new PolicyPipeline();
            pipeline.RequestPolicies.Add(new TimeoutModifyingRequestPolicy());

            var toolCall = new AIToolCall
            {
                Provider = "test",
                Model = "test-model",
                TimeoutSeconds = 30,
                Body = AIBodyBuilder.Create().AddText(AIAgent.User, "hello").Build(),
            };

            await pipeline.ApplyRequestPoliciesAsync(toolCall).ConfigureAwait(false);

            // Timeout should have been modified by the shim and merged back
            Assert.NotNull(toolCall.TimeoutSeconds);
            Assert.Equal(120, toolCall.TimeoutSeconds);
        }

        #endregion

        #region Helpers

        private static AIRequestCall CreateMinimalRequest()
        {
            var body = AIBodyBuilder.Create()
                .WithTurnId(Guid.NewGuid().ToString("N"))
                .AddText(AIAgent.User, "test")
                .Build();

            return new AIRequestCall
            {
                Provider = "test",
                Model = "test-model",
                Endpoint = "https://example.com",
                Body = body,
            };
        }

        private sealed class TrackingRequestPolicy : IRequestPolicy
        {
            private readonly string name;
            private readonly List<string> callOrder;

            public TrackingRequestPolicy(string name, List<string> callOrder)
            {
                this.name = name;
                this.callOrder = callOrder;
            }

            public List<string> Calls => this.callOrder;

            public Task ApplyAsync(PolicyContext context)
            {
                this.callOrder.Add(this.name);
                return Task.CompletedTask;
            }
        }

        private sealed class TrackingResponsePolicy : IResponsePolicy
        {
            private readonly string name;
            private readonly List<string> callOrder;

            public TrackingResponsePolicy(string name, List<string> callOrder)
            {
                this.name = name;
                this.callOrder = callOrder;
            }

            public List<string> Calls => this.callOrder;

            public Task ApplyAsync(PolicyContext context)
            {
                this.callOrder.Add(this.name);
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingRequestPolicy : IRequestPolicy
        {
            private readonly string message;

            public ThrowingRequestPolicy(string message)
            {
                this.message = message;
            }

            public Task ApplyAsync(PolicyContext context)
            {
                throw new InvalidOperationException(this.message);
            }
        }

        private sealed class ThrowingResponsePolicy : IResponsePolicy
        {
            private readonly string message;

            public ThrowingResponsePolicy(string message)
            {
                this.message = message;
            }

            public Task ApplyAsync(PolicyContext context)
            {
                throw new InvalidOperationException(this.message);
            }
        }

        private sealed class TimeoutModifyingRequestPolicy : IRequestPolicy
        {
            public Task ApplyAsync(PolicyContext context)
            {
                if (context?.Request != null)
                {
                    context.Request.TimeoutSeconds = 120;
                }

                return Task.CompletedTask;
            }
        }

        #endregion
    }
}
