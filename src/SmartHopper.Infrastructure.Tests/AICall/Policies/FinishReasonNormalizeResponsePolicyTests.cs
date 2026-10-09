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
    using System.Linq;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Policies;
    using SmartHopper.Infrastructure.AICall.Policies.Response;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using SmartHopper.ProviderSdk.AICall.Metrics;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="FinishReasonNormalizeResponsePolicy"/>. Failed calls must
    /// keep an error-appropriate (or absent) finish reason instead of being defaulted to
    /// "stop", while successful calls keep their provider finish reason or receive the
    /// documented "stop" default with a warning.
    /// </summary>
    public class FinishReasonNormalizeResponsePolicyTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Failed return with error diagnostic keeps no finish_reason stop [Windows]")]
#else
        [Fact(DisplayName = "Failed return with error diagnostic keeps no finish_reason stop [Core]")]
#endif
        public async Task ApplyAsync_ErrorDiagnosticBody_DoesNotDefaultToStop()
        {
            var response = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            response.CreateProviderError("HTTP 400: invalid request", CreateFakeRequest());

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Null(response.Metrics.FinishReason);
            Assert.DoesNotContain(response.Messages, m => m.Message.Contains("Finish reason missing"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Failed return with only structured error does not default to stop [Windows]")]
#else
        [Fact(DisplayName = "Failed return with only structured error does not default to stop [Core]")]
#endif
        public async Task ApplyAsync_StructuredErrorOnly_DoesNotDefaultToStop()
        {
            var response = new AIReturn
            {
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            response.SetBody(AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "partial content", new AIMetrics())
                .Build());
            response.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, "call failed");

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Null(response.Body.Interactions.Last().Metrics.FinishReason);
            Assert.DoesNotContain(response.Messages, m => m.Message.Contains("Finish reason missing"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Successful return preserves provider finish reason [Windows]")]
#else
        [Fact(DisplayName = "Successful return preserves provider finish reason [Core]")]
#endif
        public async Task ApplyAsync_Success_PreservesFinishReason()
        {
            var response = CreateSuccessResponse(new AIMetrics { FinishReason = "tool_calls" });

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Equal("tool_calls", response.Metrics.FinishReason);
            Assert.DoesNotContain(response.Messages, m => m.Message.Contains("Finish reason missing"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Successful return without finish reason defaults to stop [Windows]")]
#else
        [Fact(DisplayName = "Successful return without finish reason defaults to stop [Core]")]
#endif
        public async Task ApplyAsync_SuccessMissingFinishReason_DefaultsToStop()
        {
            var response = CreateSuccessResponse(new AIMetrics());

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Equal("stop", response.Metrics.FinishReason);
            Assert.Contains(response.Messages, m =>
                m.Severity == SHRuntimeMessageSeverity.Warning &&
                m.Message.Contains("defaulted to 'stop'"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Successful return normalizes provider-specific finish reason [Windows]")]
#else
        [Fact(DisplayName = "Successful return normalizes provider-specific finish reason [Core]")]
#endif
        public async Task ApplyAsync_SuccessNormalizesFinishReason()
        {
            var response = CreateSuccessResponse(new AIMetrics { FinishReason = "end_turn" });

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Equal("stop", response.Metrics.FinishReason);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Failed return with length finish reason still surfaces truncation error [Windows]")]
#else
        [Fact(DisplayName = "Failed return with length finish reason still surfaces truncation error [Core]")]
#endif
        public async Task ApplyAsync_FailedWithLengthReason_SurfacesTruncationError()
        {
            var response = CreateSuccessResponse(new AIMetrics { FinishReason = "length" });
            response.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Provider, "partial failure");

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Equal("length", response.Metrics.FinishReason);
            Assert.Contains(response.Messages, m =>
                m.Severity == SHRuntimeMessageSeverity.Error &&
                m.Message.Contains("maximum token limit"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Failed return with provider finish reason keeps it without warning [Windows]")]
#else
        [Fact(DisplayName = "Failed return with provider finish reason keeps it without warning [Core]")]
#endif
        public async Task ApplyAsync_FailedWithProviderReason_KeepsReasonWithoutWarning()
        {
            var response = CreateSuccessResponse(new AIMetrics { FinishReason = "stop" });
            response.SetBody(AIBodyBuilder.FromImmutable(response.Body)
                .AddError("provider reported failure")
                .Build());

            await new FinishReasonNormalizeResponsePolicy().ApplyAsync(new PolicyContext { Response = response }).ConfigureAwait(false);

            Assert.Equal("stop", response.Metrics.FinishReason);
            Assert.DoesNotContain(response.Messages, m => m.Message.Contains("Finish reason missing"));
        }

        private static AIReturn CreateSuccessResponse(AIMetrics metrics)
        {
            var response = new AIReturn
            {
                Request = CreateFakeRequest(),
                SkipRequestValidation = true,
                SkipMetricsValidation = true,
            };
            response.SetBody(AIBodyBuilder.Create()
                .AddText(AIAgent.Assistant, "hello", metrics)
                .Build());
            return response;
        }

        private static AIRequestBase CreateFakeRequest()
        {
            return new AIRequestBase
            {
                Capability = AICapability.None,
                Provider = null,
                WantsStreaming = false,
                Body = AIBody.Empty,
                Model = "test-model",
            };
        }
    }
}
