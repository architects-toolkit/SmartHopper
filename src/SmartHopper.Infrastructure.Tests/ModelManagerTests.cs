/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.Tests
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Reflection;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall;
    using SmartHopper.Infrastructure.AIModels;
    using SmartHopper.Infrastructure.AIProviders;
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
            Assert.NotNull(instance1.Registry);
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
            const AICapability capabilities = AICapability.BasicChat | AICapability.JsonGenerator;
            const AICapability defaultFor = AICapability.BasicChat;

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
            manager.RegisterCapabilities(null!, "TestModel", AICapability.BasicChat);
            manager.RegisterCapabilities(string.Empty, "TestModel", AICapability.BasicChat);
            manager.RegisterCapabilities("   ", "TestModel", AICapability.BasicChat);

            // Act & Assert - null/empty model
            manager.RegisterCapabilities("TestProvider", null!, AICapability.BasicChat);
            manager.RegisterCapabilities("TestProvider", string.Empty, AICapability.BasicChat);
            manager.RegisterCapabilities("TestProvider", "   ", AICapability.BasicChat);

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

            manager.RegisterCapabilities(provider, chatModel, AICapability.BasicChat, AICapability.BasicChat);
            manager.RegisterCapabilities(provider, toolsModel, AICapability.JsonGenerator, AICapability.JsonGenerator);

            // Act & Assert
            var defaultChatModel = manager.GetDefaultModel(provider, AICapability.BasicChat);
            var defaultToolsModel = manager.GetDefaultModel(provider, AICapability.JsonGenerator);

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

            manager.RegisterCapabilities(registeredProvider, "TestModel", AICapability.BasicChat);

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
            const AICapability capabilities = AICapability.BasicChat | AICapability.JsonGenerator;

            manager.RegisterCapabilities(provider, model, capabilities);

            // Act & Assert - Valid capabilities
            Assert.True(manager.ValidateCapabilities(provider, model, AICapability.BasicChat));
            Assert.True(manager.ValidateCapabilities(provider, model, AICapability.JsonGenerator));
            Assert.True(manager.ValidateCapabilities(provider, model, AICapability.BasicChat | AICapability.JsonGenerator));

            // Act & Assert - Invalid capabilities
            Assert.False(manager.ValidateCapabilities(provider, model, AICapability.ImageGenerator));
            Assert.False(manager.ValidateCapabilities(provider, model, AICapability.BasicChat | AICapability.ImageGenerator));

            // Act & Assert - Unregistered model
            Assert.False(manager.ValidateCapabilities("UnknownProvider", "UnknownModel", AICapability.BasicChat));
        }
    }
}
