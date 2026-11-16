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

namespace SmartHopper.ProviderSdk.Tests.Providers.OpenRouter
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenRouter;
    using SmartHopper.ProviderSdk.AICall.Core;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Encoding tests for OpenRouter provider-specific extras: the reasoning object and
    /// the provider-routing preference lists (order/only/ignore).
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class OpenRouterEncodingTests : IDisposable
    {
        private readonly IProviderRegistryHost previousRegistry;

        public OpenRouterEncodingTests()
        {
            this.previousRegistry = ProviderSdkHost.ProviderRegistry;
            ProviderSdkHost.ProviderRegistry = new FakeProviderRegistryHost(OpenRouterProvider.Instance);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ProviderSdkHost.ProviderRegistry = this.previousRegistry;
        }

        private static AIRequestCall BuildRequest(IReadOnlyDictionary<string, JToken> extras = null)
        {
            var body = AIBodyBuilder.Create()
                .Add(new AIInteractionText { Agent = AIAgent.User, Content = "Hello." })
                .Build();

            return new AIRequestCall
            {
                Provider = OpenRouterProvider.Instance.Name,
                Model = "google/gemini-2.5-flash",
                Body = body,
                Capability = AICapability.Text2Text,
                Parameters = new AIRequestParameters { Extras = extras },
            };
        }

        private static void RefreshSettings(Dictionary<string, object> extra = null)
        {
            var settings = new Dictionary<string, object>
            {
                { "ApiKey", "test-key" },
                { "MaxTokens", 1024 },
                { "Temperature", 0.5 },
                { "AllowFallbacks", true },
                { "Sort", "price" },
                { "DataCollection", "deny" },
            };

            if (extra != null)
            {
                foreach (var kvp in extra)
                {
                    settings[kvp.Key] = kvp.Value;
                }
            }

            OpenRouterProvider.Instance.RefreshCachedSettings(settings);
        }

        /// <summary>
        /// reasoning_* extras assemble the top-level reasoning object; effort
        /// takes precedence over max_tokens because the API rejects both together.
        /// </summary>
        [Fact]
        public void Encode_ReasoningExtras_EmitReasoningObject()
        {
            RefreshSettings();

            var request = BuildRequest(new Dictionary<string, JToken>
            {
                ["reasoning_effort"] = new JValue("low"),
                ["reasoning_max_tokens"] = new JValue(2000),
                ["reasoning_exclude"] = new JValue(true),
            });

            var requestObj = JObject.Parse(OpenRouterProvider.Instance.Encode(request));

            Assert.Equal("low", requestObj["reasoning"]?["effort"]?.ToString());
            Assert.Null(requestObj["reasoning"]?["max_tokens"]);
            Assert.Equal(true, requestObj["reasoning"]?["exclude"]?.Value<bool?>());
        }

        /// <summary>
        /// reasoning_max_tokens emits when reasoning_effort is absent.
        /// </summary>
        [Fact]
        public void Encode_ReasoningMaxTokens_EmitsWhenEffortAbsent()
        {
            RefreshSettings();

            var request = BuildRequest(new Dictionary<string, JToken>
            {
                ["reasoning_max_tokens"] = new JValue(2000),
            });

            var requestObj = JObject.Parse(OpenRouterProvider.Instance.Encode(request));

            Assert.Equal(2000, requestObj["reasoning"]?["max_tokens"]?.Value<int?>());
            Assert.Null(requestObj["reasoning"]?["effort"]);
        }

        /// <summary>
        /// provider.order/only/ignore come from settings by default and are overridden
        /// by the provider_* extras. Comma-separated strings become arrays.
        /// </summary>
        [Fact]
        public void Encode_ProviderPrefs_SettingsAndExtraOverride()
        {
            RefreshSettings(new Dictionary<string, object>
            {
                { "ProviderOnly", "google,anthropic" },
                { "ProviderIgnore", "deepinfra" },
            });

            var request = BuildRequest(new Dictionary<string, JToken>
            {
                ["provider_order"] = new JValue("groq,cerebras"),
            });

            var requestObj = JObject.Parse(OpenRouterProvider.Instance.Encode(request));

            var provider = requestObj["provider"] as JObject;
            Assert.NotNull(provider);
            Assert.Equal(new[] { "groq", "cerebras" }, provider["order"]?.ToObject<string[]>());
            Assert.Equal(new[] { "google", "anthropic" }, provider["only"]?.ToObject<string[]>());
            Assert.Equal(new[] { "deepinfra" }, provider["ignore"]?.ToObject<string[]>());
        }

        /// <summary>
        /// A JSON array form for provider_only passes through unchanged.
        /// </summary>
        [Fact]
        public void Encode_ProviderOnly_ArrayForm_PassesThrough()
        {
            RefreshSettings();

            var request = BuildRequest(new Dictionary<string, JToken>
            {
                ["provider_only"] = new JArray("google", "azure"),
            });

            var requestObj = JObject.Parse(OpenRouterProvider.Instance.Encode(request));

            Assert.Equal(
                new[] { "google", "azure" },
                requestObj["provider"]?["only"]?.ToObject<string[]>());
        }
    }
}

#endif
