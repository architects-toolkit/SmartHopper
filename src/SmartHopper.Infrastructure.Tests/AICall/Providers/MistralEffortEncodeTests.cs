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

#if NET7_WINDOWS

namespace SmartHopper.Infrastructure.Tests.AICall.Providers
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.MistralAI;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Settings;
    using Xunit;

    /// <summary>
    /// Unit tests verifying that <see cref="MistralAIProvider"/> only emits a
    /// <c>reasoning_effort</c> value the target model actually accepts. Per
    /// Mistral docs, <c>mistral-large-4*</c> only accepts low|medium|high
    /// (<c>none</c> returns HTTP 400 — the documented migration maps it to
    /// <c>low</c>), <c>zai-glm-5-3*</c> accepts low|high|max (no <c>none</c>),
    /// and the other reasoning-capable Mistral models accept none|high. The
    /// provider must normalize per model and omit the parameter on models
    /// known not to support reasoning.
    /// </summary>
    [Collection("ProviderSdk")]
    public class MistralEffortEncodeTests : IDisposable
    {
        private readonly IProviderRegistryHost previousRegistry = ProviderSdkHost.ProviderRegistry;

        public MistralEffortEncodeTests()
        {
            // request.Model resolves through the provider registry (alias -> dated id);
            // without a host the model is empty and the normalizer cannot gate.
            ProviderSdkHost.ProviderRegistry = new SingleProviderRegistry(MistralAIProvider.Instance);

            // Model capabilities gate the parameter and are only populated on init.
            MistralAIProvider.Instance.InitializeProviderAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
            SetGlobalEffort("none"); // descriptor default
        }

        // ── Mistral Large 4: only low|medium|high are valid ───────────────
        [Theory(DisplayName = "Encode_ReasoningEffort_KeptOnLarge4")]
        [InlineData("mistral-large-4-0", "low")]
        [InlineData("mistral-large-4-0", "medium")]
        [InlineData("mistral-large-4-0", "high")]
        [InlineData("mistral-large-4", "medium")]
        [InlineData("mistral-large-4.0", "high")]
        public void Encode_ReasoningEffort_KeptOnLarge4(string model, string effort)
        {
            var body = EncodeBody(model, WithEffort(effort));

            Assert.Equal(effort, ReasoningEffort(body)?.ToString());
        }

        // ── 'none' is rejected by Large 4 (HTTP 400); documented migration: low ──
        [Theory(DisplayName = "Encode_ReasoningEffort_NoneNormalizesToLowOnLarge4")]
        [InlineData("mistral-large-4-0")]
        [InlineData("mistral-large-4")]
        [InlineData("mistral-large-4.0")]
        public void Encode_ReasoningEffort_NoneNormalizesToLowOnLarge4(string model)
        {
            var body = EncodeBody(model, WithEffort("none"));

            Assert.Equal("low", ReasoningEffort(body)?.ToString());
        }

        // ── Invalid values never reach the wire ───────────────────────────
        [Theory(DisplayName = "Encode_ReasoningEffort_InvalidValuesNormalized")]
        [InlineData("mistral-large-4-0", "max", "high")]
        [InlineData("mistral-large-4-0", "xhigh", "high")]
        [InlineData("mistral-large-4-0", "banana", "high")]
        [InlineData("mistral-small-2603", "max", "high")]
        [InlineData("zai-glm-5-3", "banana", "high")]
        public void Encode_ReasoningEffort_InvalidValuesNormalized(string model, string requested, string expected)
        {
            var body = EncodeBody(model, WithEffort(requested));

            Assert.Equal(expected, ReasoningEffort(body)?.ToString());
        }

        // ── Legacy none|high models (small/medium/magistral/deepseek) ─────
        [Theory(DisplayName = "Encode_ReasoningEffort_LegacyModels")]
        [InlineData("mistral-small-2603", "none", "none")]
        [InlineData("mistral-small-2603", "high", "high")]
        [InlineData("mistral-small-2603", "low", "none")]
        [InlineData("mistral-small-2603", "medium", "high")]
        [InlineData("mistral-medium-2604", "none", "none")]
        [InlineData("mistral-medium-3-5", "high", "high")]
        [InlineData("magistral-medium-2509", "low", "none")]
        [InlineData("deepseek-v4-1-flash-1017", "none", "none")]
        public void Encode_ReasoningEffort_LegacyModels(string model, string requested, string expected)
        {
            var body = EncodeBody(model, WithEffort(requested));

            Assert.Equal(expected, ReasoningEffort(body)?.ToString());
        }

        // ── Z.ai GLM 5.3 family: low|high|max, no none ────────────────────
        [Theory(DisplayName = "Encode_ReasoningEffort_Glm53")]
        [InlineData("zai-glm-5-3", "low", "low")]
        [InlineData("zai-glm-5-3", "high", "high")]
        [InlineData("zai-glm-5-3", "max", "max")]
        [InlineData("zai-glm-5-3-flash", "max", "max")]
        [InlineData("zai-glm-5-3", "none", "low")]
        [InlineData("zai-glm-5-3", "medium", "high")]
        public void Encode_ReasoningEffort_Glm53(string model, string requested, string expected)
        {
            var body = EncodeBody(model, WithEffort(requested));

            Assert.Equal(expected, ReasoningEffort(body)?.ToString());
        }

        // ── Known non-reasoning models: parameter never sent ──────────────
        [Theory(DisplayName = "Encode_ReasoningEffort_OmittedOnNonReasoningModels")]
        [InlineData("mistral-large-2512")]
        [InlineData("mistral-large-2411")]
        [InlineData("ministral-8b-2512")]
        [InlineData("codestral-2508")]
        public void Encode_ReasoningEffort_OmittedOnNonReasoningModels(string model)
        {
            var body = EncodeBody(model, WithEffort("high"));

            Assert.Null(ReasoningEffort(body));
        }

        // ── Global provider setting path ──────────────────────────────────
        [Fact(DisplayName = "Encode_ReasoningEffort_GlobalNoneNormalizesToLowOnLarge4")]
        public void Encode_ReasoningEffort_GlobalNoneNormalizesToLowOnLarge4()
        {
            SetGlobalEffort("none");
            var body = EncodeBody("mistral-large-4-0");

            Assert.Equal("low", ReasoningEffort(body)?.ToString());
        }

        [Fact(DisplayName = "Encode_ReasoningEffort_GlobalEffortKeptOnLegacyModel")]
        public void Encode_ReasoningEffort_GlobalEffortKeptOnLegacyModel()
        {
            SetGlobalEffort("high");
            var body = EncodeBody("mistral-small-2603");

            Assert.Equal("high", ReasoningEffort(body)?.ToString());
        }

        [Fact(DisplayName = "Encode_ReasoningEffort_ExtraOverridesGlobal")]
        public void Encode_ReasoningEffort_ExtraOverridesGlobal()
        {
            SetGlobalEffort("none");
            var body = EncodeBody("mistral-large-4-0", WithEffort("medium"));

            Assert.Equal("medium", ReasoningEffort(body)?.ToString());
        }

        private static JObject EncodeBody(string model, AIRequestParameters? parameters = null)
        {
            var request = new AIRequestCall();
            request.Initialize(
                "MistralAI",
                model,
                new List<IAIInteraction> { new AIInteractionText { Agent = AIAgent.User, Content = "hello" } },
                "chat/completions");

            if (parameters != null)
            {
                request.Parameters = parameters;
            }

            return JObject.Parse(MistralAIProvider.Instance.Encode(request));
        }

        private static AIRequestParameters WithEffort(string effort) =>
            AIRequestParameters.Create()
                .WithExtra("reasoning_effort", JToken.FromObject(effort))
                .Build();

        private static JToken? ReasoningEffort(JObject body) => body["reasoning_effort"];

        private static void SetGlobalEffort(string effort) =>
            MistralAIProvider.Instance.RefreshCachedSettings(
                new Dictionary<string, object> { ["ReasoningEffort"] = effort });

        /// <summary>
        /// Minimal registry that resolves every provider lookup to the given provider.
        /// </summary>
        private sealed class SingleProviderRegistry : IProviderRegistryHost
        {
            private readonly MistralAIProvider provider;

            public SingleProviderRegistry(MistralAIProvider provider)
            {
                this.provider = provider;
            }

            public IAIProvider GetProvider(string providerName) => this.provider;

            public IAIProviderSettings GetProviderSettings(string providerName) =>
                new MistralAIProviderSettings(this.provider);
        }
    }
}

#endif
