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

namespace SmartHopper.ProviderSdk.Tests.AIProviders
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.AIProviders;
    using SmartHopper.ProviderSdk.Settings;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIProvider.GetDefaultModel"/> model resolution,
    /// including providers whose Model descriptor default re-enters the method lazily.
    /// </summary>
    [Collection("ProviderSdk")]
    public sealed class AIProviderGetDefaultModelTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        [Fact(DisplayName = nameof(GetDefaultModel_DoesNotEvaluateDescriptorLazyDefault) + PlatformSuffix)]
        public void GetDefaultModel_DoesNotEvaluateDescriptorLazyDefault()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterModel("registry-default", isDefault: true);

            var provider = new ReentrantModelProvider();
            provider.RefreshCachedSettings(new Dictionary<string, object>());

            var model = provider.GetDefaultModel();

            Assert.Equal("registry-default", model);
            Assert.Equal(0, provider.LazyDefaultEvaluations);
        }

        [Fact(DisplayName = nameof(GetDefaultModel_PrefersConfiguredModel) + PlatformSuffix)]
        public void GetDefaultModel_PrefersConfiguredModel()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterModel("configured-model");
            RegisterModel("registry-default", isDefault: true);

            var provider = new ReentrantModelProvider();
            provider.RefreshCachedSettings(new Dictionary<string, object> { ["Model"] = "configured-model" });

            var model = provider.GetDefaultModel();

            Assert.Equal("configured-model", model);
            Assert.Equal(0, provider.LazyDefaultEvaluations);
        }

        [Fact(DisplayName = nameof(GetModelSetting_LazyDefaultResolvesRegistryDefault) + PlatformSuffix)]
        public void GetModelSetting_LazyDefaultResolvesRegistryDefault()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            RegisterModel("registry-default", isDefault: true);

            var provider = new ReentrantModelProvider();
            provider.RefreshCachedSettings(new Dictionary<string, object>());

            var model = provider.GetModelSetting();

            Assert.Equal("registry-default", model);
            Assert.Equal(1, provider.LazyDefaultEvaluations);
        }

        private static void RegisterModel(string model, bool isDefault = false)
        {
            AIModelCapabilityRegistry.Instance.SetCapabilities(new AIModelCapabilities
            {
                Provider = ReentrantModelProvider.ProviderName.ToLowerInvariant(),
                Model = model,
                Capabilities = AICapability.Text2Text,
                Default = isDefault ? AICapability.Text2Text : AICapability.None,
            });
        }

        /// <summary>
        /// Provider whose Model descriptor lazily re-enters <see cref="AIProvider.GetDefaultModel"/>,
        /// matching the unconfigured-provider descriptor pattern (e.g. Ollama, LocalAI).
        /// </summary>
        private sealed class ReentrantModelProvider : AIProvider
        {
            public const string ProviderName = "ReentrantProvider";

            public int LazyDefaultEvaluations;

            public override string Name => ProviderName;

            public override Image Icon => new Bitmap(1, 1);

            public override bool IsEnabled => true;

            public override bool IsConfigured => true;

            public override Uri DefaultServerUrl => new Uri("https://fake.example/");

            public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
            {
                var descriptor = new SettingDescriptor { Name = "Model", Type = typeof(string) };
                descriptor.SetLazyDefault(() =>
                {
                    this.LazyDefaultEvaluations++;
                    return this.GetDefaultModel() ?? string.Empty;
                });
                return new[] { descriptor };
            }

            public string? GetModelSetting()
            {
                return this.GetSetting<string>("Model");
            }

            public override string Encode(AIRequestCall request)
            {
                return "{}";
            }

            public override string Encode(IAIInteraction interaction)
            {
                return "{}";
            }

            public override List<IAIInteraction> Decode(JObject response)
            {
                return new List<IAIInteraction>();
            }
        }
    }
}
