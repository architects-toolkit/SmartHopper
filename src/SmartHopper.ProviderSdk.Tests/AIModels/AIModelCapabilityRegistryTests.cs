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

#if NET7_WINDOWS
        [Fact(DisplayName = "Instance_ShouldReturnSameInstance [Windows]")]
#else
        [Fact(DisplayName = "Instance_ShouldReturnSameInstance [Core]")]
#endif
        public void Instance_ShouldReturnSameInstance()
        {
            var instance1 = AIModelCapabilityRegistry.Instance;
            var instance2 = AIModelCapabilityRegistry.Instance;

            Assert.Same(instance1, instance2);
            Assert.NotNull(instance1);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterCapabilities_ShouldRegisterModel [Windows]")]
#else
        [Fact(DisplayName = "RegisterCapabilities_ShouldRegisterModel [Core]")]
#endif
        public void RegisterCapabilities_ShouldRegisterModel()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";
            const string model = "TestModel";
            const AICapability capabilities = AICapability.Text2Text | AICapability.Text2Json;
            const AICapability defaultFor = AICapability.Text2Text;

            registry.RegisterCapabilities(provider, model, capabilities, defaultFor);

            var retrieved = registry.GetCapabilities(provider, model);
            Assert.NotNull(retrieved);
            Assert.Equal(provider.ToLowerInvariant(), retrieved.Provider);
            Assert.Equal(model, retrieved.Model);
            Assert.Equal(capabilities, retrieved.Capabilities);
            Assert.Equal(defaultFor, retrieved.Default);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterCapabilities_ShouldIgnoreInvalidInput [Windows]")]
#else
        [Fact(DisplayName = "RegisterCapabilities_ShouldIgnoreInvalidInput [Core]")]
#endif
        public void RegisterCapabilities_ShouldIgnoreInvalidInput()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.RegisterCapabilities(null!, "TestModel", AICapability.Text2Text);
            registry.RegisterCapabilities(string.Empty, "TestModel", AICapability.Text2Text);
            registry.RegisterCapabilities("   ", "TestModel", AICapability.Text2Text);

            registry.RegisterCapabilities("TestProvider", null!, AICapability.Text2Text);
            registry.RegisterCapabilities("TestProvider", string.Empty, AICapability.Text2Text);
            registry.RegisterCapabilities("TestProvider", "   ", AICapability.Text2Text);

            Assert.Null(registry.GetCapabilities("TestProvider", "TestModel"));
            Assert.Null(registry.GetCapabilities(null!, "TestModel"));
            Assert.Null(registry.GetCapabilities(string.Empty, "TestModel"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SetCapabilities_ShouldHandleNullInput [Windows]")]
#else
        [Fact(DisplayName = "SetCapabilities_ShouldHandleNullInput [Core]")]
#endif
        public void SetCapabilities_ShouldHandleNullInput()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(null!);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetDefaultModel_ShouldReturnCorrectDefault [Windows]")]
#else
        [Fact(DisplayName = "GetDefaultModel_ShouldReturnCorrectDefault [Core]")]
#endif
        public void GetDefaultModel_ShouldReturnCorrectDefault()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";
            const string chatModel = "ChatModel";
            const string toolsModel = "ToolsModel";

            registry.RegisterCapabilities(provider, chatModel, AICapability.Text2Text, AICapability.Text2Text);
            registry.RegisterCapabilities(provider, toolsModel, AICapability.Text2Json, AICapability.Text2Json);

            Assert.Equal(chatModel, registry.GetDefaultModel(provider, AICapability.Text2Text));
            Assert.Equal(toolsModel, registry.GetDefaultModel(provider, AICapability.Text2Json));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ValidateCapabilities_ShouldValidateCorrectly [Windows]")]
#else
        [Fact(DisplayName = "ValidateCapabilities_ShouldValidateCorrectly [Core]")]
#endif
        public void ValidateCapabilities_ShouldValidateCorrectly()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";
            const string model = "TestModel";
            const AICapability capabilities = AICapability.Text2Text | AICapability.Text2Json;

            registry.RegisterCapabilities(provider, model, capabilities);

            Assert.True(registry.ValidateCapabilities(provider, model, AICapability.Text2Text));
            Assert.True(registry.ValidateCapabilities(provider, model, AICapability.Text2Json));
            Assert.True(registry.ValidateCapabilities(provider, model, AICapability.Text2Text | AICapability.Text2Json));

            Assert.False(registry.ValidateCapabilities(provider, model, AICapability.Text2Image));
            Assert.False(registry.ValidateCapabilities(provider, model, AICapability.Text2Text | AICapability.Text2Image));

            Assert.True(registry.ValidateCapabilities("UnknownProvider", "UnknownModel", AICapability.Text2Text));
            Assert.True(registry.ValidateCapabilities(provider, "UnknownModel", AICapability.Text2Text));
            Assert.True(registry.ValidateCapabilities("UnknownProvider", model, AICapability.Text2Text));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_UserKnownCapable_UsesUser [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_UserKnownCapable_UsesUser [Core]")]
#endif
        public void SelectBestModel_UserKnownCapable_UsesUser()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";
            const string model = "CapableModel";
            registry.RegisterCapabilities(provider, model, AICapability.Text2Text);

            var selected = registry.SelectBestModel(provider, model, AICapability.Text2Text);

            Assert.Equal(model, selected);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_UserKnownNotCapable_FallbacksToPreferred [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_UserKnownNotCapable_FallbacksToPreferred [Core]")]
#endif
        public void SelectBestModel_UserKnownNotCapable_FallbacksToPreferred()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";
            const string notCapable = "JsonOnly";
            const string preferred = "TextChat";
            registry.RegisterCapabilities(provider, notCapable, AICapability.Text2Json);
            registry.RegisterCapabilities(provider, preferred, AICapability.Text2Text);

            var selected = registry.SelectBestModel(provider, notCapable, AICapability.Text2Text, preferred);

            Assert.Equal(preferred, selected);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_Priority_DefaultExactThenCompatibleThenBest [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_Priority_DefaultExactThenCompatibleThenBest [Core]")]
#endif
        public void SelectBestModel_Priority_DefaultExactThenCompatibleThenBest()
        {
            // Arrange 1: exact default exists
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;
            const string provider = "TestProvider";

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "ExactDefault",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.Text2Text,
                Verified = true,
                Rank = 1,
                Deprecated = false,
            });

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "CompatibleDefault",
                Capabilities = AICapability.Text2Text | AICapability.Text2Json,
                Default = AICapability.Text2Json,
                Verified = true,
                Rank = 10,
                Deprecated = false,
            });

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "BestNonDefault",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.None,
                Verified = true,
                Rank = 100,
                Deprecated = false,
            });

            var selected1 = registry.SelectBestModel(provider, null, AICapability.Text2Text);
            Assert.Equal("ExactDefault", selected1);

            // Arrange 2: remove exact default flag to test compatible-default path
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "CompatibleDefault",
                Capabilities = AICapability.Text2Text | AICapability.Text2Json,
                Default = AICapability.Text2Json,
                Verified = true,
                Rank = 10,
                Deprecated = false,
            });

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "BestNonDefault",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.None,
                Verified = true,
                Rank = 100,
                Deprecated = false,
            });

            var selected2 = registry.SelectBestModel(provider, null, AICapability.Text2Text);
            Assert.Equal("CompatibleDefault", selected2);

            // Arrange 3: no defaults -> choose best by quality
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            registry = AIModelCapabilityRegistry.Instance;

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "LowRank",
                Capabilities = AICapability.Text2Text,
                Rank = 1,
                Verified = true,
                Deprecated = false,
            });

            registry.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLowerInvariant(),
                Model = "HighRank",
                Capabilities = AICapability.Text2Text,
                Rank = 50,
                Verified = true,
                Deprecated = false,
            });

            var selected3 = registry.SelectBestModel(provider, null, AICapability.Text2Text);
            Assert.Equal("HighRank", selected3);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "SetDefault_ShouldCreateEntryWhenMissing [Windows]")]
#else
        [Fact(DisplayName = "SetDefault_ShouldCreateEntryWhenMissing [Core]")]
#endif
        public void SetDefault_ShouldCreateEntryWhenMissing()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";

            registry.SetDefault(provider, "NewModel", AICapability.Text2Text);

            var created = registry.GetCapabilities(provider, "NewModel");
            Assert.NotNull(created);
            Assert.True((created!.Default & AICapability.Text2Text) == AICapability.Text2Text);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ValidateCapabilities_Image2Text_ReturnsCorrectStatus [Windows]")]
#else
        [Fact(DisplayName = "ValidateCapabilities_Image2Text_ReturnsCorrectStatus [Core]")]
#endif
        public void ValidateCapabilities_Image2Text_ReturnsCorrectStatus()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";

            registry.RegisterCapabilities(provider, "vision-model", AICapability.Text2Text | AICapability.Image2Text);
            registry.RegisterCapabilities(provider, "text-only-model", AICapability.Text2Text);

            Assert.True(registry.ValidateCapabilities(provider, "vision-model", AICapability.Image2Text));
            Assert.False(registry.ValidateCapabilities(provider, "text-only-model", AICapability.Image2Text));
            Assert.True(registry.ValidateCapabilities(provider, "vision-model", AICapability.Text2Text | AICapability.Image2Text));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ValidateCapabilities_Text2Image_ReturnsCorrectStatus [Windows]")]
#else
        [Fact(DisplayName = "ValidateCapabilities_Text2Image_ReturnsCorrectStatus [Core]")]
#endif
        public void ValidateCapabilities_Text2Image_ReturnsCorrectStatus()
        {
            ProviderSdkTestHelper.ResetCapabilityRegistry();
            var registry = AIModelCapabilityRegistry.Instance;

            const string provider = "TestProvider";

            registry.RegisterCapabilities(provider, "image-gen-model", AICapability.Text2Image);
            registry.RegisterCapabilities(provider, "text-only-model", AICapability.Text2Text);

            Assert.True(registry.ValidateCapabilities(provider, "image-gen-model", AICapability.Text2Image));
            Assert.False(registry.ValidateCapabilities(provider, "text-only-model", AICapability.Text2Image));
        }
    }
}
