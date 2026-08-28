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

namespace SmartHopper.Infrastructure.Tests.AICall
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Diagnostics;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="AIRequestCall"/> class.
    /// Focuses on validation, capability resolution, and request construction
    /// without requiring real providers to be loaded.
    /// </summary>
    public class AIRequestCallTests
    {
        #region Validation

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid null provider returns error [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid null provider returns error [Core]")]
#endif
        public void IsValid_NullProvider_ReturnsError()
        {
            var request = new AIRequestCall
            {
                Provider = null,
                Endpoint = "https://example.com",
                Body = CreateValidBody(),
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("Provider is required", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid empty provider returns error [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid empty provider returns error [Core]")]
#endif
        public void IsValid_EmptyProvider_ReturnsError()
        {
            var request = new AIRequestCall
            {
                Provider = string.Empty,
                Endpoint = "https://example.com",
                Body = CreateValidBody(),
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("Provider is required", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid missing endpoint returns error [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid missing endpoint returns error [Core]")]
#endif
        public void IsValid_MissingEndpoint_ReturnsError()
        {
            var request = new AIRequestCall
            {
                Provider = "test",
                Endpoint = null,
                Body = CreateValidBody(),
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("Endpoint is required", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid null body returns error for Generation [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid null body returns error for Generation [Core]")]
#endif
        public void IsValid_NullBody_ReturnsError()
        {
            var request = new AIRequestCall
            {
                Provider = "test",
                Endpoint = "https://example.com",
                Body = null,
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("Body is required", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid empty interactions returns error for Generation [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid empty interactions returns error for Generation [Core]")]
#endif
        public void IsValid_EmptyInteractions_ReturnsError()
        {
            var request = new AIRequestCall
            {
                Provider = "test",
                Endpoint = "https://example.com",
                Body = AIBody.Empty,
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("At least one interaction is required", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid ForceToolCall without FunctionCalling capability returns error [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid ForceToolCall without FunctionCalling capability returns error [Core]")]
#endif
        public void IsValid_ForceToolCallWithoutCapability_ReturnsError()
        {
            var request = new AIRequestCall
            {
                Provider = "test",
                Endpoint = "https://example.com",
                Body = CreateValidBody(),
                Capability = AICapability.TextOutput,
                ForceToolCall = true,
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("ForceToolCall requires FunctionCalling capability", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid missing TurnId returns error [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid missing TurnId returns error [Core]")]
#endif
        public void IsValid_MissingTurnId_ReturnsError()
        {
            // Construct body directly (not via builder) so TurnId is not auto-assigned
            var body = new AIBody(
                new List<IAIInteraction> { new AIInteractionText { Agent = AIAgent.User, Content = "hello" } },
                "-*",
                "-*",
                null,
                new List<int>());

            var request = new AIRequestCall
            {
                Provider = "test",
                Endpoint = "https://example.com",
                Body = body,
            };

            var (valid, errors) = request.IsValid();
            Assert.False(valid);
            Assert.Contains(errors, e => e.Message.Contains("TurnId", StringComparison.OrdinalIgnoreCase));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall IsValid Backoffice skips body validation [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall IsValid Backoffice skips body validation [Core]")]
#endif
        public void IsValid_Backoffice_SkipsBodyValidation()
        {
            var request = new AIRequestCall
            {
                Provider = "test",
                Endpoint = "https://example.com",
                Body = null,
                RequestKind = AIRequestKind.Backoffice,
            };

            var (valid, errors) = request.IsValid();
            // Body validation is skipped for Backoffice, but provider/endpoint still required
            // ProviderInstance will be null so it will still error on unknown provider
            Assert.False(valid);
            Assert.DoesNotContain(errors, e => e.Message.Contains("Body is required", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Capability Resolution

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall Capability auto-adds JsonOutput when body has schema [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall Capability auto-adds JsonOutput when body has schema [Core]")]
#endif
        public void Capability_BodyHasSchema_AddsJsonOutput()
        {
            var body = AIBodyBuilder.Create()
                .WithTurnId(Guid.NewGuid().ToString("N"))
                .WithJsonOutputSchema("{\"type\":\"object\"}")
                .AddText(AIAgent.User, "test")
                .Build();

            var request = new AIRequestCall
            {
                Body = body,
                Capability = AICapability.TextOutput,
            };

            var effective = request.Capability;
            Assert.True(effective.HasFlag(AICapability.JsonOutput));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall Capability auto-adds FunctionCalling when tool filter is active [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall Capability auto-adds FunctionCalling when tool filter is active [Core]")]
#endif
        public void Capability_ActiveToolFilter_AddsFunctionCalling()
        {
            var body = AIBodyBuilder.Create()
                .WithTurnId(Guid.NewGuid().ToString("N"))
                .WithToolFilter("gh_get")
                .AddText(AIAgent.User, "test")
                .Build();

            var request = new AIRequestCall
            {
                Body = body,
                Capability = AICapability.TextOutput,
            };

            var effective = request.Capability;
            Assert.True(effective.HasFlag(AICapability.FunctionCalling));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall Capability preserves explicitly set flags [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall Capability preserves explicitly set flags [Core]")]
#endif
        public void Capability_PreservesExplicitFlags()
        {
            var body = AIBodyBuilder.Create()
                .WithTurnId(Guid.NewGuid().ToString("N"))
                .AddText(AIAgent.User, "test")
                .Build();

            var request = new AIRequestCall
            {
                Body = body,
                Capability = AICapability.TextOutput | AICapability.ImageOutput,
            };

            var effective = request.Capability;
            Assert.True(effective.HasFlag(AICapability.TextOutput));
            Assert.True(effective.HasFlag(AICapability.ImageOutput));
        }

        #endregion

        #region EncodedRequestBody

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall EncodedRequestBody returns empty when invalid [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall EncodedRequestBody returns empty when invalid [Core]")]
#endif
        public void EncodedRequestBody_InvalidRequest_ReturnsEmpty()
        {
            var request = new AIRequestCall
            {
                Provider = null,
                Body = null,
            };

            var encoded = request.EncodedRequestBody;
            Assert.Equal(string.Empty, encoded);
        }

        #endregion

        #region Initialization

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall Initialize sets provider model endpoint and body [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall Initialize sets provider model endpoint and body [Core]")]
#endif
        public void Initialize_SetsProperties()
        {
            var request = new AIRequestCall();
            var body = CreateValidBody();

            request.Initialize("openai", "gpt-4", body, "https://api.openai.com/v1/chat/completions", AICapability.Text2Text);

            Assert.Equal("openai", request.Provider);
            // Model getter resolves via ProviderManager; without a loaded provider it returns empty.
            // The raw requested model is stored internally and used once a provider is available.
            Assert.Equal("https://api.openai.com/v1/chat/completions", request.Endpoint);
            Assert.Equal(AICapability.Text2Text, request.Capability);
            Assert.NotNull(request.Body);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall Initialize from interactions builds body [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall Initialize from interactions builds body [Core]")]
#endif
        public void Initialize_FromInteractions_BuildsBody()
        {
            var request = new AIRequestCall();
            var interactions = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.User, Content = "hello" },
            };

            request.Initialize("openai", "gpt-4", interactions, "https://api.openai.com/v1/chat/completions");

            Assert.NotNull(request.Body);
            Assert.Equal(1, request.Body.InteractionsCount);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "AIRequestCall Initialize applies tool filter when provided [Windows]")]
#else
        [Fact(DisplayName = "AIRequestCall Initialize applies tool filter when provided [Core]")]
#endif
        public void Initialize_WithToolFilter_AppliesFilter()
        {
            var request = new AIRequestCall();
            var body = CreateValidBody();

            request.Initialize("openai", "gpt-4", body, "https://api.openai.com/v1/chat/completions", toolFilter: "gh_get");

            Assert.Equal("gh_get", request.Body.ToolFilter);
        }

        #endregion

        #region Helpers

        private static AIBody CreateValidBody()
        {
            return AIBodyBuilder.Create()
                .WithTurnId(Guid.NewGuid().ToString("N"))
                .AddText(AIAgent.User, "test prompt")
                .Build();
        }

        #endregion
    }
}
