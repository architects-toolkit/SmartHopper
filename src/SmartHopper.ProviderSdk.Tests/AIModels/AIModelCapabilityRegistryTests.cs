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

namespace SmartHopper.ProviderSdk.Tests.AIModels
{
    using System.Collections.Generic;
    using SmartHopper.ProviderSdk.AIModels;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AIModelCapabilityRegistry"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AIModelCapabilityRegistryTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "SetCapabilities_ThenGetCapabilities_ExactMatch [Windows]")]
#else
        [Fact(DisplayName = "SetCapabilities_ThenGetCapabilities_ExactMatch [Core]")]
#endif
        public void SetCapabilities_ThenGetCapabilities_ExactMatch()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
            });

            var caps = registry.GetCapabilities("OpenAI", "gpt-4");

            Assert.NotNull(caps);
            Assert.Equal("openai", caps.Provider);
            Assert.Equal("gpt-4", caps.Model);
            Assert.True(caps.HasCapability(AICapability.Text2Text));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetCapabilities_AliasMatches [Windows]")]
#else
        [Fact(DisplayName = "GetCapabilities_AliasMatches [Core]")]
#endif
        public void GetCapabilities_AliasMatches()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4-turbo",
                Aliases = new List<string> { "gpt-4t" },
                Capabilities = AICapability.Text2Text,
            });

            var caps = registry.GetCapabilities("openai", "gpt-4t");

            Assert.NotNull(caps);
            Assert.Equal("gpt-4-turbo", caps.Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetCapabilities_UnknownModel_ReturnsNull [Windows]")]
#else
        [Fact(DisplayName = "GetCapabilities_UnknownModel_ReturnsNull [Core]")]
#endif
        public void GetCapabilities_UnknownModel_ReturnsNull()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            var caps = registry.GetCapabilities("unknown", "unknown");

            Assert.Null(caps);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "HasProviderCapabilities_DetectsProvider [Windows]")]
#else
        [Fact(DisplayName = "HasProviderCapabilities_DetectsProvider [Core]")]
#endif
        public void HasProviderCapabilities_DetectsProvider()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
            });

            Assert.True(registry.HasProviderCapabilities("openai"));
            Assert.False(registry.HasProviderCapabilities("anthropic"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetProviderModels_ReturnsOnlySameProvider [Windows]")]
#else
        [Fact(DisplayName = "GetProviderModels_ReturnsOnlySameProvider [Core]")]
#endif
        public void GetProviderModels_ReturnsOnlySameProvider()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
            });
            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "anthropic",
                Model = "claude-3",
                Capabilities = AICapability.Text2Text,
            });

            var openAiModels = registry.GetProviderModels("openai");

            Assert.Single(openAiModels);
            Assert.Equal("gpt-4", openAiModels[0].Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "FindModelsWithCapabilities_FiltersByCapability [Windows]")]
#else
        [Fact(DisplayName = "FindModelsWithCapabilities_FiltersByCapability [Core]")]
#endif
        public void FindModelsWithCapabilities_FiltersByCapability()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "dall-e",
                Capabilities = AICapability.Text2Image,
            });
            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
            });

            var textModels = registry.FindModelsWithCapabilities(AICapability.Text2Text);

            Assert.Single(textModels);
            Assert.Equal("gpt-4", textModels[0].Model);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SetDefault_Exclusive_RemovesDefaultFromOthers [Windows]")]
#else
        [Fact(DisplayName = "SetDefault_Exclusive_RemovesDefaultFromOthers [Core]")]
#endif
        public void SetDefault_Exclusive_RemovesDefaultFromOthers()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.Text2Text,
            });
            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-3.5",
                Capabilities = AICapability.Text2Text,
            });

            registry.SetDefault("openai", "gpt-3.5", AICapability.Text2Text, exclusive: true);

            var gpt4 = registry.GetCapabilities("openai", "gpt-4");
            var gpt35 = registry.GetCapabilities("openai", "gpt-3.5");

            Assert.False((gpt4.Default & AICapability.Text2Text) == AICapability.Text2Text);
            Assert.True((gpt35.Default & AICapability.Text2Text) == AICapability.Text2Text);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetDefaultModel_ReturnsDefaultForCapability [Windows]")]
#else
        [Fact(DisplayName = "GetDefaultModel_ReturnsDefaultForCapability [Core]")]
#endif
        public void GetDefaultModel_ReturnsDefaultForCapability()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.Text2Text,
                Rank = 10,
            });

            var defaultModel = registry.GetDefaultModel("openai", AICapability.Text2Text);

            Assert.Equal("gpt-4", defaultModel);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_UnknownUserModel_Allowed [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_UnknownUserModel_Allowed [Core]")]
#endif
        public void SelectBestModel_UnknownUserModel_Allowed()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
            });

            var selected = registry.SelectBestModel("openai", "custom-model", AICapability.Text2Text);

            Assert.Equal("custom-model", selected);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_ReturnsDefaultWhenNoUserModel [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_ReturnsDefaultWhenNoUserModel [Core]")]
#endif
        public void SelectBestModel_ReturnsDefaultWhenNoUserModel()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.Text2Text,
            });

            var selected = registry.SelectBestModel("openai", string.Empty, AICapability.Text2Text);

            Assert.Equal("gpt-4", selected);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ModelSupportsStreaming_TrueWhenSupported [Windows]")]
#else
        [Fact(DisplayName = "ModelSupportsStreaming_TrueWhenSupported [Core]")]
#endif
        public void ModelSupportsStreaming_TrueWhenSupported()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = "openai",
                Model = "gpt-4",
                SupportsStreaming = true,
            });

            Assert.True(registry.ModelSupportsStreaming("openai", "gpt-4"));
            Assert.False(registry.ModelSupportsStreaming("openai", "unknown"));
        }
    }
}
