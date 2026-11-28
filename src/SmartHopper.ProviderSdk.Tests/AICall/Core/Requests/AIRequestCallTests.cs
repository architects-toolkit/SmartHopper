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

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Requests
{
    using System.Collections.Generic;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIRequestCall"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIRequestCallTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "Constructor_Defaults [Windows]")]
#else
        [Fact(DisplayName = "Constructor_Defaults [Core]")]
#endif
        public void Constructor_Defaults()
        {
            var request = new AIRequestCall();

            Assert.Equal("POST", request.HttpMethod);
            Assert.Equal("bearer", request.Authentication);
            Assert.Equal("application/json", request.ContentType);
            Assert.Empty(request.Headers);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Headers_AreCaseInsensitive [Windows]")]
#else
        [Fact(DisplayName = "Headers_AreCaseInsensitive [Core]")]
#endif
        public void Headers_AreCaseInsensitive()
        {
            var request = new AIRequestCall();

            request.Headers["x-custom"] = "value";

            Assert.Single(request.Headers);
            Assert.Equal("value", request.Headers["X-CUSTOM"]);
            Assert.Equal("value", request.Headers["X-Custom"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Capability_InfersJsonOutput [Windows]")]
#else
        [Fact(DisplayName = "Capability_InfersJsonOutput [Core]")]
#endif
        public void Capability_InfersJsonOutput()
        {
            var request = new AIRequestCall();
            request.Body = AIBodyBuilder.Create()
                .WithJsonOutputSchema("{ \"type\": \"object\" }")
                .Build();

            Assert.True(request.Capability.HasFlag(AICapability.Text2Text));
            Assert.True(request.Capability.HasFlag(AICapability.JsonOutput));
            Assert.False(request.Capability.HasFlag(AICapability.FunctionCalling));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Capability_InfersFunctionCalling [Windows]")]
#else
        [Fact(DisplayName = "Capability_InfersFunctionCalling [Core]")]
#endif
        public void Capability_InfersFunctionCalling()
        {
            var request = new AIRequestCall();
            request.Body = AIBodyBuilder.Create()
                .WithToolFilter("+gh_*")
                .Build();

            Assert.True(request.Capability.HasFlag(AICapability.Text2Text));
            Assert.True(request.Capability.HasFlag(AICapability.FunctionCalling));
            Assert.False(request.Capability.HasFlag(AICapability.JsonOutput));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Capability_InfersBoth [Windows]")]
#else
        [Fact(DisplayName = "Capability_InfersBoth [Core]")]
#endif
        public void Capability_InfersBoth()
        {
            var request = new AIRequestCall();
            request.Body = AIBodyBuilder.Create()
                .WithJsonOutputSchema("{ \"type\": \"object\" }")
                .WithToolFilter("+gh_*")
                .Build();

            Assert.True(request.Capability.HasFlag(AICapability.Text2Text));
            Assert.True(request.Capability.HasFlag(AICapability.JsonOutput));
            Assert.True(request.Capability.HasFlag(AICapability.FunctionCalling));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Initialize_SetsBodyOriginAndParameters [Windows]")]
#else
        [Fact(DisplayName = "Initialize_SetsBodyOriginAndParameters [Core]")]
#endif
        public void Initialize_SetsBodyOriginAndParameters()
        {
            var body = AIBodyBuilder.Create()
                .AddUser("hello")
                .Build();

            var request = new AIRequestCall();
            request.Initialize(
                provider: null!,
                model: "test-model",
                body: body,
                origin: "https://test/endpoint",
                capability: AICapability.None);

            Assert.Same(body, request.Body);
            Assert.Equal("https://test/endpoint", request.Origin);
            Assert.NotNull(request.Parameters);
            Assert.Equal("test-model", request.Parameters.Model);
            Assert.Equal("test-model", request.Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RequestKind_DefaultsToGenerationAndCanBeSet [Windows]")]
#else
        [Fact(DisplayName = "RequestKind_DefaultsToGenerationAndCanBeSet [Core]")]
#endif
        public void RequestKind_DefaultsToGenerationAndCanBeSet()
        {
            var request = new AIRequestCall();

            Assert.Equal(AIRequestKind.Generation, request.RequestKind);

            request.RequestKind = AIRequestKind.Backoffice;

            Assert.Equal(AIRequestKind.Backoffice, request.RequestKind);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_ContextOnlyBody_FailsConversationContent [Windows]")]
#else
        [Fact(DisplayName = "IsValid_ContextOnlyBody_FailsConversationContent [Core]")]
#endif
        public void IsValid_ContextOnlyBody_FailsConversationContent()
        {
            // Generation requests made only of system/context interactions cannot be
            // answered by any provider — they encode as instructions with no
            // conversation input (Gemini/Anthropic return HTTP 400).
            var request = new AIRequestCall();
            request.Initialize(
                provider: "prov",
                model: "m",
                body: AIBodyBuilder.Create()
                    .AddSystem("be helpful")
                    .AddText(AIAgent.Context, "project context")
                    .Build(),
                origin: "test",
                capability: AICapability.None);

            var (isValid, errors) = request.IsValid();

            Assert.False(isValid);
            Assert.Contains(errors, e => e.Message.Contains("no conversation content"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_UserTurn_PassesConversationContent [Windows]")]
#else
        [Fact(DisplayName = "IsValid_UserTurn_PassesConversationContent [Core]")]
#endif
        public void IsValid_UserTurn_PassesConversationContent()
        {
            var request = new AIRequestCall();
            request.Initialize(
                provider: "prov",
                model: "m",
                body: AIBodyBuilder.Create()
                    .AddSystem("be helpful")
                    .AddUser("hello")
                    .Build(),
                origin: "test",
                capability: AICapability.None);

            var (_, errors) = request.IsValid();

            Assert.DoesNotContain(errors, e => e.Message.Contains("no conversation content"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsValid_BackofficeSkipsConversationContent [Windows]")]
#else
        [Fact(DisplayName = "IsValid_BackofficeSkipsConversationContent [Core]")]
#endif
        public void IsValid_BackofficeSkipsConversationContent()
        {
            // Backoffice (non-generation) requests are exempt from the conversation rule.
            var request = new AIRequestCall();
            request.Initialize(
                provider: "prov",
                model: "m",
                body: AIBodyBuilder.Create()
                    .AddSystem("be helpful")
                    .Build(),
                origin: "test",
                capability: AICapability.None);
            request.RequestKind = AIRequestKind.Backoffice;

            var (_, errors) = request.IsValid();

            Assert.DoesNotContain(errors, e => e.Message.Contains("no conversation content"));
        }
    }
}
