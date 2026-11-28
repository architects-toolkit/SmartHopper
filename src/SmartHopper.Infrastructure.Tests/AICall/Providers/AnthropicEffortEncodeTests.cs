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
    using SmartHopper.Providers.Anthropic;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Settings;
    using Xunit;

    /// <summary>
    /// Unit tests verifying that <see cref="AnthropicProvider"/> only emits
    /// <c>output_config.effort</c> for models that accept the parameter. Models
    /// older than the 4.6 generation (e.g. claude-haiku-4-5) reject it with
    /// HTTP 400 "This model does not support the effort parameter.", so the
    /// provider must drop it instead of forwarding the configured value.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AnthropicEffortEncodeTests : IDisposable
    {
        private readonly IProviderRegistryHost previousRegistry = ProviderSdkHost.ProviderRegistry;

        public AnthropicEffortEncodeTests()
        {
            // request.Model resolves through the provider registry (alias -> dated id);
            // without a host the model is empty and the normalizer cannot gate.
            ProviderSdkHost.ProviderRegistry = new SingleProviderRegistry(AnthropicProvider.Instance);
        }

        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
        }

        // ── Models that reject the parameter outright ────────────────────
        [Theory(DisplayName = "Encode_Effort_DroppedOnPre46Models")]
        [InlineData("claude-haiku-4-5")]
        [InlineData("claude-haiku-4-5-20251001")]
        [InlineData("claude-sonnet-4-5-20250929")]
        [InlineData("claude-opus-4-5-20251101")]
        [InlineData("claude-opus-4-1-20250805")]
        [InlineData("claude-sonnet-4-20250514")]
        [InlineData("claude-3-7-sonnet-20250219")]
        public void Encode_Effort_DroppedOnPre46Models(string model)
        {
            var body = EncodeBody(model, WithEffort("medium"));

            Assert.True(Effort(body) == null, $"model={body["model"]} output_config={body["output_config"]?.ToString() ?? "<absent>"}");
        }

        // ── Models that accept the parameter ─────────────────────────────
        [Theory(DisplayName = "Encode_Effort_KeptOnSupportedModels")]
        [InlineData("claude-opus-4-6", "medium")]
        [InlineData("claude-sonnet-4-6", "low")]
        [InlineData("claude-opus-4-7", "medium")]
        [InlineData("claude-opus-5", "high")]
        [InlineData("claude-sonnet-5", "medium")]
        [InlineData("claude-haiku-5-5", "medium")]
        [InlineData("claude-fable-5", "medium")]
        public void Encode_Effort_KeptOnSupportedModels(string model, string effort)
        {
            var body = EncodeBody(model, WithEffort(effort));

            Assert.Equal(effort, Effort(body)?.ToString());
        }

        // ── Level normalization still applies on supported models ────────
        [Theory(DisplayName = "Encode_Effort_LevelDowngradeOnSupportedModels")]
        [InlineData("claude-sonnet-4-6", "xhigh", "high")]
        [InlineData("claude-opus-4-6", "xhigh", "high")]
        [InlineData("claude-opus-4-7", "max", "max")]
        [InlineData("claude-opus-4-7", "xhigh", "xhigh")]
        [InlineData("claude-fable-5", "xhigh", "xhigh")]
        public void Encode_Effort_LevelNormalization(string model, string requested, string expected)
        {
            var body = EncodeBody(model, WithEffort(requested));

            Assert.Equal(expected, Effort(body)?.ToString());
        }

        // ── Global provider setting path (the live 400 failure) ──────────
        [Fact(DisplayName = "Encode_Effort_GlobalSetting_DroppedOnHaiku45")]
        public void Encode_Effort_GlobalSetting_DroppedOnHaiku45()
        {
            try
            {
                SetGlobalEffort("medium");
                var body = EncodeBody("claude-haiku-4-5");

                Assert.Null(Effort(body));
            }
            finally
            {
                // "medium" is the descriptor default; re-setting it is a no-op restore.
                SetGlobalEffort("medium");
            }
        }

        [Fact(DisplayName = "Encode_Effort_GlobalSetting_KeptOnOpus46")]
        public void Encode_Effort_GlobalSetting_KeptOnOpus46()
        {
            try
            {
                SetGlobalEffort("medium");
                var body = EncodeBody("claude-opus-4-6");

                Assert.Equal("medium", Effort(body)?.ToString());
            }
            finally
            {
                SetGlobalEffort("medium");
            }
        }

        private static JObject EncodeBody(string model, AIRequestParameters? parameters = null)
        {
            var request = new AIRequestCall();
            request.Initialize(
                "Anthropic",
                model,
                new List<IAIInteraction> { new AIInteractionText { Agent = AIAgent.User, Content = "hello" } },
                "messages");

            if (parameters != null)
            {
                request.Parameters = parameters;
            }

            return JObject.Parse(AnthropicProvider.Instance.Encode(request));
        }

        private static AIRequestParameters WithEffort(string effort) =>
            AIRequestParameters.Create()
                .WithExtra("effort", JToken.FromObject(effort))
                .Build();

        private static JToken? Effort(JObject body) => body["output_config"]?["effort"];

        private static void SetGlobalEffort(string effort) =>
            AnthropicProvider.Instance.RefreshCachedSettings(
                new Dictionary<string, object> { ["ReasoningEffort"] = effort });

        /// <summary>
        /// Minimal registry that resolves every provider lookup to the given provider.
        /// </summary>
        private sealed class SingleProviderRegistry : IProviderRegistryHost
        {
            private readonly AIProvider provider;

            public SingleProviderRegistry(AIProvider provider)
            {
                this.provider = provider;
            }

            public IAIProvider GetProvider(string providerName) => this.provider;

            public IAIProviderSettings GetProviderSettings(string providerName) =>
                new AnthropicProviderSettings(AnthropicProvider.Instance);
        }
    }
}

#endif
