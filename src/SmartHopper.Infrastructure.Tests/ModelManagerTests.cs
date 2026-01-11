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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

namespace SmartHopper.Infrastructure.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using SmartHopper.Infrastructure.AIModels;
    using Xunit;

    /// <summary>
    /// Tests for ModelManager functionality including singleton pattern, capability registration, and validation.
    /// </summary>
    public class ModelManagerTests
    {
        /// <summary>
        /// Reset the ModelManager singleton's internal registry for clean testing.
        /// </summary>
        private static void ResetManager()
        {
            var manager = ModelManager.Instance;
            var registryField = typeof(ModelManager).GetField("_registry", BindingFlags.NonPublic | BindingFlags.Instance);
            var newRegistry = System.Activator.CreateInstance(typeof(AIModelCapabilityRegistry));
            registryField?.SetValue(manager, newRegistry);
        }

        /// <summary>
        /// Tests that ModelManager returns the same singleton instance.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "Instance_ShouldReturnSameInstance [Windows]")]
#else
        [Fact(DisplayName = "Instance_ShouldReturnSameInstance [Core]")]
#endif
        public void Instance_ShouldReturnSameInstance()
        {
            // Arrange & Act
            var instance1 = ModelManager.Instance;
            var instance2 = ModelManager.Instance;

            // Assert
            Assert.Same(instance1, instance2);
            Assert.NotNull(instance1);
        }

        /// <summary>
        /// Tests that RegisterCapabilities successfully registers model capabilities.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterCapabilities_ShouldRegisterModel [Windows]")]
#else
        [Fact(DisplayName = "RegisterCapabilities_ShouldRegisterModel [Core]")]
#endif
        public void RegisterCapabilities_ShouldRegisterModel()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            const string model = "TestModel";
            const AICapability capabilities = AICapability.Text2Text | AICapability.Text2Json;
            const AICapability defaultFor = AICapability.Text2Text;

            // Act
            manager.RegisterCapabilities(provider, model, capabilities, defaultFor);

            // Assert
            var retrievedCapabilities = manager.GetCapabilities(provider, model);
            Assert.NotNull(retrievedCapabilities);
            Assert.Equal(provider.ToLower(CultureInfo.InvariantCulture), retrievedCapabilities.Provider);
            Assert.Equal(model, retrievedCapabilities.Model);
            Assert.Equal(capabilities, retrievedCapabilities.Capabilities);
            Assert.Equal(defaultFor, retrievedCapabilities.Default);
        }

        /// <summary>
        /// Tests that RegisterCapabilities ignores invalid input parameters.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterCapabilities_ShouldIgnoreInvalidInput [Windows]")]
#else
        [Fact(DisplayName = "RegisterCapabilities_ShouldIgnoreInvalidInput [Core]")]
#endif
        public void RegisterCapabilities_ShouldIgnoreInvalidInput()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;

            // Act & Assert - null/empty provider
            manager.RegisterCapabilities(null!, "TestModel", AICapability.Text2Text);
            manager.RegisterCapabilities(string.Empty, "TestModel", AICapability.Text2Text);
            manager.RegisterCapabilities("   ", "TestModel", AICapability.Text2Text);

            // Act & Assert - null/empty model
            manager.RegisterCapabilities("TestProvider", null!, AICapability.Text2Text);
            manager.RegisterCapabilities("TestProvider", string.Empty, AICapability.Text2Text);
            manager.RegisterCapabilities("TestProvider", "   ", AICapability.Text2Text);

            // Verify no models were registered
            Assert.Null(manager.GetCapabilities("TestProvider", "TestModel"));
            Assert.Null(manager.GetCapabilities(null!, "TestModel"));
            Assert.Null(manager.GetCapabilities(string.Empty, "TestModel"));
        }

        /// <summary>
        /// Tests that SetCapabilities handles null input gracefully.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SetCapabilities_ShouldHandleNullInput [Windows]")]
#else
        [Fact(DisplayName = "SetCapabilities_ShouldHandleNullInput [Core]")]
#endif
        public void SetCapabilities_ShouldHandleNullInput()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;

            // Act & Assert - should not throw
            manager.SetCapabilities(null!);
        }

        /// <summary>
        /// Tests that GetCapabilities returns null for unregistered models.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "GetCapabilities_ShouldReturnNullForUnregistered [Windows]")]
#else
        [Fact(DisplayName = "GetCapabilities_ShouldReturnNullForUnregistered [Core]")]
#endif
        public void GetCapabilities_ShouldReturnNullForUnregistered()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;

            // Act
            var capabilities = manager.GetCapabilities("UnknownProvider", "UnknownModel");

            // Assert
            Assert.Null(capabilities);
        }

        /// <summary>
        /// Tests that GetDefaultModel returns the correct default model for capabilities.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "GetDefaultModel_ShouldReturnCorrectDefault [Windows]")]
#else
        [Fact(DisplayName = "GetDefaultModel_ShouldReturnCorrectDefault [Core]")]
#endif
        public void GetDefaultModel_ShouldReturnCorrectDefault()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            const string chatModel = "ChatModel";
            const string toolsModel = "ToolsModel";

            manager.RegisterCapabilities(provider, chatModel, AICapability.Text2Text, AICapability.Text2Text);
            manager.RegisterCapabilities(provider, toolsModel, AICapability.Text2Json, AICapability.Text2Json);

            // Act & Assert
            var defaultChatModel = manager.GetDefaultModel(provider, AICapability.Text2Text);
            var defaultToolsModel = manager.GetDefaultModel(provider, AICapability.Text2Json);

            Assert.Equal(chatModel, defaultChatModel);
            Assert.Equal(toolsModel, defaultToolsModel);
        }

        /// <summary>
        /// Tests that HasProviderCapabilities returns correct status for registered and unregistered providers.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "HasProviderCapabilities_ShouldReturnCorrectStatus [Windows]")]
#else
        [Fact(DisplayName = "HasProviderCapabilities_ShouldReturnCorrectStatus [Core]")]
#endif
        public void HasProviderCapabilities_ShouldReturnCorrectStatus()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string registeredProvider = "RegisteredProvider";
            const string unregisteredProvider = "UnregisteredProvider";

            manager.RegisterCapabilities(registeredProvider, "TestModel", AICapability.Text2Text);

            // Act & Assert
            Assert.True(manager.HasProviderCapabilities(registeredProvider));
            Assert.False(manager.HasProviderCapabilities(unregisteredProvider));
            Assert.False(manager.HasProviderCapabilities(null!));
            Assert.False(manager.HasProviderCapabilities(string.Empty));
            Assert.False(manager.HasProviderCapabilities("   "));
        }

        /// <summary>
        /// Tests that ValidateCapabilities correctly validates model capabilities.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "ValidateCapabilities_ShouldValidateCorrectly [Windows]")]
#else
        [Fact(DisplayName = "ValidateCapabilities_ShouldValidateCorrectly [Core]")]
#endif
        public void ValidateCapabilities_ShouldValidateCorrectly()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            const string model = "TestModel";
            const AICapability capabilities = AICapability.Text2Text | AICapability.Text2Json;

            manager.RegisterCapabilities(provider, model, capabilities);

            // Act & Assert - Valid capabilities
            Assert.True(manager.ValidateCapabilities(provider, model, AICapability.Text2Text));
            Assert.True(manager.ValidateCapabilities(provider, model, AICapability.Text2Json));
            Assert.True(manager.ValidateCapabilities(provider, model, AICapability.Text2Text | AICapability.Text2Json));

            // Act & Assert - Invalid capabilities
            Assert.False(manager.ValidateCapabilities(provider, model, AICapability.Text2Image));
            Assert.False(manager.ValidateCapabilities(provider, model, AICapability.Text2Text | AICapability.Text2Image));

            // Act & Assert - Unregistered model bypasses validation
            Assert.True(manager.ValidateCapabilities("UnknownProvider", "UnknownModel", AICapability.Text2Text));
            Assert.True(manager.ValidateCapabilities(provider, "UnknownModel", AICapability.Text2Text));
            Assert.True(manager.ValidateCapabilities("UnknownProvider", model, AICapability.Text2Text));
        }

        /// <summary>
        /// Tests that SelectBestModel passes through an unknown user-specified model without overriding it.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_UserUnknown_PassesThrough [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_UserUnknown_PassesThrough [Core]")]
#endif
        public void SelectBestModel_UserUnknown_PassesThrough()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            manager.RegisterCapabilities(provider, "KnownModel", AICapability.Text2Text);

            // Act
            var selected = manager.SelectBestModel(provider, "UnknownModel", AICapability.Text2Text);

            // Assert
            Assert.Equal("UnknownModel", selected);
        }

        /// <summary>
        /// Tests that SelectBestModel uses the user model when it is known and capable.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_UserKnownCapable_UsesUser [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_UserKnownCapable_UsesUser [Core]")]
#endif
        public void SelectBestModel_UserKnownCapable_UsesUser()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            const string model = "CapableModel";
            manager.RegisterCapabilities(provider, model, AICapability.Text2Text);

            // Act
            var selected = manager.SelectBestModel(provider, model, AICapability.Text2Text);

            // Assert
            Assert.Equal(model, selected);
        }

        /// <summary>
        /// Tests that SelectBestModel falls back to preferredDefault when user model is known but not capable.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_UserKnownNotCapable_FallbacksToPreferred [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_UserKnownNotCapable_FallbacksToPreferred [Core]")]
#endif
        public void SelectBestModel_UserKnownNotCapable_FallbacksToPreferred()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            const string notCapable = "JsonOnly";
            const string preferred = "TextChat";
            manager.RegisterCapabilities(provider, notCapable, AICapability.Text2Json);
            manager.RegisterCapabilities(provider, preferred, AICapability.Text2Text);

            // Act
            var selected = manager.SelectBestModel(provider, notCapable, AICapability.Text2Text, preferred);

            // Assert
            Assert.Equal(preferred, selected);
        }

        /// <summary>
        /// Tests SelectBestModel priority: exact default > compatible default > best available by quality.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SelectBestModel_Priority_DefaultExactThenCompatibleThenBest [Windows]")]
#else
        [Fact(DisplayName = "SelectBestModel_Priority_DefaultExactThenCompatibleThenBest [Core]")]
#endif
        public void SelectBestModel_Priority_DefaultExactThenCompatibleThenBest()
        {
            // Arrange 1: exact default exists
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";

            // exact default for Text2Text
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "ExactDefault",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.Text2Text,
                Verified = true,
                Rank = 1,
                Deprecated = false,
            });

            // compatible default (default set for another cap but still capable of Text2Text)
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "CompatibleDefault",
                Capabilities = AICapability.Text2Text | AICapability.Text2Json,
                Default = AICapability.Text2Json,
                Verified = true,
                Rank = 10,
                Deprecated = false,
            });

            // a high-rank, non-default candidate
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "BestNonDefault",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.None,
                Verified = true,
                Rank = 100,
                Deprecated = false,
            });

            // Act & Assert 1: exact default wins
            var selected1 = manager.SelectBestModel(provider, null, AICapability.Text2Text);
            Assert.Equal("ExactDefault", selected1);

            // Arrange 2: remove exact default flag to test compatible-default path
            ResetManager();
            manager = ModelManager.Instance;
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "CompatibleDefault",
                Capabilities = AICapability.Text2Text | AICapability.Text2Json,
                Default = AICapability.Text2Json,
                Verified = true,
                Rank = 10,
                Deprecated = false,
            });
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "BestNonDefault",
                Capabilities = AICapability.Text2Text,
                Default = AICapability.None,
                Verified = true,
                Rank = 100,
                Deprecated = false,
            });

            var selected2 = manager.SelectBestModel(provider, null, AICapability.Text2Text);
            Assert.Equal("CompatibleDefault", selected2);

            // Arrange 3: no defaults -> choose best by quality (Verified, Rank, !Deprecated)
            ResetManager();
            manager = ModelManager.Instance;
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "LowRank",
                Capabilities = AICapability.Text2Text,
                Rank = 1,
                Verified = true,
                Deprecated = false,
            });
            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "HighRank",
                Capabilities = AICapability.Text2Text,
                Rank = 50,
                Verified = true,
                Deprecated = false,
            });

            var selected3 = manager.SelectBestModel(provider, null, AICapability.Text2Text);
            Assert.Equal("HighRank", selected3);
        }

        /// <summary>
        /// Tests SetDefault exclusivity clears default flags on other models of the same provider.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SetDefault_ShouldBeExclusivePerProvider [Windows]")]
#else
        [Fact(DisplayName = "SetDefault_ShouldBeExclusivePerProvider [Core]")]
#endif
        public void SetDefault_ShouldBeExclusivePerProvider()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";
            manager.RegisterCapabilities(provider, "A", AICapability.Text2Text, AICapability.Text2Text);
            manager.RegisterCapabilities(provider, "B", AICapability.Text2Text);

            // Act: set B as default for Text2Text exclusively
            manager.SetDefault(provider, "B", AICapability.Text2Text, exclusive: true);

            // Assert
            var a = manager.GetCapabilities(provider, "A");
            var b = manager.GetCapabilities(provider, "B");
            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.False((a!.Default & AICapability.Text2Text) == AICapability.Text2Text);
            Assert.True((b!.Default & AICapability.Text2Text) == AICapability.Text2Text);
        }

        /// <summary>
        /// Tests SetDefault creates a new entry when the model does not exist yet.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SetDefault_ShouldCreateEntryWhenMissing [Windows]")]
#else
        [Fact(DisplayName = "SetDefault_ShouldCreateEntryWhenMissing [Core]")]
#endif
        public void SetDefault_ShouldCreateEntryWhenMissing()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "TestProvider";

            // Act
            manager.SetDefault(provider, "NewModel", AICapability.Text2Text);

            // Assert
            var created = manager.GetCapabilities(provider, "NewModel");
            Assert.NotNull(created);
            Assert.True((created!.Default & AICapability.Text2Text) == AICapability.Text2Text);
        }

        /// <summary>
        /// Tests alias-based lookup resolves to the correct model capabilities.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "GetCapabilities_ByAlias_ShouldResolve [Windows]")]
#else
        [Fact(DisplayName = "GetCapabilities_ByAlias_ShouldResolve [Core]")]
#endif
        public void GetCapabilities_ByAlias_ShouldResolve()
        {
            // Arrange
            ResetManager();
            var manager = ModelManager.Instance;
            const string provider = "AliasProvider";

            manager.SetCapabilities(new AIModelCapabilities
            {
                Provider = provider.ToLower(System.Globalization.CultureInfo.InvariantCulture),
                Model = "PrimaryModel",
                Capabilities = AICapability.Text2Text,
                Aliases = new List<string> { "pmodel", "primary" },
            });

            // Act
            var resolved = manager.GetCapabilities(provider, "pmodel");

            // Assert
            Assert.NotNull(resolved);
            Assert.Equal("PrimaryModel", resolved!.Model);
        }

    }
}
