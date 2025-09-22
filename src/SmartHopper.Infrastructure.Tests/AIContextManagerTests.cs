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
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using SmartHopper.Infrastructure.AIContext;
    using Xunit;

    /// <summary>
    /// Tests for AIContextManager functionality including provider registration, context retrieval, and filtering.
    /// </summary>
    public class AIContextManagerTests
    {
        /// <summary>
        /// Reset the AIContextManager static state for clean testing.
        /// </summary>
        private void ResetManager()
        {
            var managerType = typeof(AIContextManager);
            var providersField = managerType.GetField("_contextProviders", BindingFlags.NonPublic | BindingFlags.Static);
            if (providersField == null)
            {
                throw new MissingFieldException("The field '_contextProviders' was not found in AIContextManager.");
            }
            var providersList = providersField.GetValue(null) as List<IAIContextProvider>;
            if (providersList == null)
            {
                throw new InvalidCastException("The field '_contextProviders' is not of the expected type 'List<IAIContextProvider>'.");
            }
            providersList.Clear();
        }

        /// <summary>
        /// Mock implementation of IAIContextProvider for testing.
        /// </summary>
        private sealed class MockContextProvider : IAIContextProvider
        {
            public MockContextProvider(string providerId, Dictionary<string, string>? context = null)
            {
                this.ProviderId = providerId;
                this.Context = context ?? new Dictionary<string, string>();
            }

            public string ProviderId { get; }

            public Dictionary<string, string> Context { get; }

            public Dictionary<string, string> GetContext()
            {
                return Context;
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterProvider_ShouldAddProvider [Windows]")]
#else
        [Fact(DisplayName = "RegisterProvider_ShouldAddProvider [Core]")]
#endif
        public void RegisterProvider_ShouldAddProvider()
        {
            // Arrange
            ResetManager();
            var provider = new MockContextProvider("test-provider");

            // Act
            AIContextManager.RegisterProvider(provider);

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Single(providers);
            Assert.Equal("test-provider", providers[0].ProviderId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterProvider_ShouldReplaceExistingProvider [Windows]")]
#else
        [Fact(DisplayName = "RegisterProvider_ShouldReplaceExistingProvider [Core]")]
#endif
        public void RegisterProvider_ShouldReplaceExistingProvider()
        {
            // Arrange
            ResetManager();
            var provider1 = new MockContextProvider("test-provider", new Dictionary<string, string> { ["key1"] = "value1" });
            var provider2 = new MockContextProvider("test-provider", new Dictionary<string, string> { ["key2"] = "value2" });

            // Act
            AIContextManager.RegisterProvider(provider1);
            AIContextManager.RegisterProvider(provider2);

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Single(providers);
            Assert.Equal("test-provider", providers[0].ProviderId);
            Assert.Contains("key2", providers[0].GetContext().Keys);
            Assert.DoesNotContain("key1", providers[0].GetContext().Keys);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "RegisterProvider_ShouldIgnoreNullProvider [Windows]")]
#else
        [Fact(DisplayName = "RegisterProvider_ShouldIgnoreNullProvider [Core]")]
#endif
        public void RegisterProvider_ShouldIgnoreNullProvider()
        {
            // Arrange
            ResetManager();

            // Act
            AIContextManager.RegisterProvider(null);

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Empty(providers);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UnregisterProvider_ById_ShouldRemoveProvider [Windows]")]
#else
        [Fact(DisplayName = "UnregisterProvider_ById_ShouldRemoveProvider [Core]")]
#endif
        public void UnregisterProvider_ById_ShouldRemoveProvider()
        {
            // Arrange
            ResetManager();
            var provider1 = new MockContextProvider("provider1");
            var provider2 = new MockContextProvider("provider2");
            AIContextManager.RegisterProvider(provider1);
            AIContextManager.RegisterProvider(provider2);

            // Act
            AIContextManager.UnregisterProvider("provider1");

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Single(providers);
            Assert.Equal("provider2", providers[0].ProviderId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UnregisterProvider_ById_ShouldIgnoreInvalidId [Windows]")]
#else
        [Fact(DisplayName = "UnregisterProvider_ById_ShouldIgnoreInvalidId [Core]")]
#endif
        public void UnregisterProvider_ById_ShouldIgnoreInvalidId()
        {
            // Arrange
            ResetManager();
            var provider = new MockContextProvider("test-provider");
            AIContextManager.RegisterProvider(provider);

            // Act
            AIContextManager.UnregisterProvider((string)null);
            AIContextManager.UnregisterProvider("");
            AIContextManager.UnregisterProvider("nonexistent");

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Single(providers);
            Assert.Equal("test-provider", providers[0].ProviderId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UnregisterProvider_ByInstance_ShouldRemoveProvider [Windows]")]
#else
        [Fact(DisplayName = "UnregisterProvider_ByInstance_ShouldRemoveProvider [Core]")]
#endif
        public void UnregisterProvider_ByInstance_ShouldRemoveProvider()
        {
            // Arrange
            ResetManager();
            var provider1 = new MockContextProvider("provider1");
            var provider2 = new MockContextProvider("provider2");
            AIContextManager.RegisterProvider(provider1);
            AIContextManager.RegisterProvider(provider2);

            // Act
            AIContextManager.UnregisterProvider(provider1);

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Single(providers);
            Assert.Equal("provider2", providers[0].ProviderId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "UnregisterProvider_ByInstance_ShouldIgnoreNullProvider [Windows]")]
#else
        [Fact(DisplayName = "UnregisterProvider_ByInstance_ShouldIgnoreNullProvider [Core]")]
#endif
        public void UnregisterProvider_ByInstance_ShouldIgnoreNullProvider()
        {
            // Arrange
            ResetManager();
            var provider = new MockContextProvider("test-provider");
            AIContextManager.RegisterProvider(provider);

            // Act
            AIContextManager.UnregisterProvider((IAIContextProvider)null);

            // Assert
            var providers = AIContextManager.GetProviders();
            Assert.Single(providers);
            Assert.Equal("test-provider", providers[0].ProviderId);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetProvider_ShouldReturnCorrectProvider [Windows]")]
#else
        [Fact(DisplayName = "GetProvider_ShouldReturnCorrectProvider [Core]")]
#endif
        public void GetProvider_ShouldReturnCorrectProvider()
        {
            // Arrange
            ResetManager();
            var provider1 = new MockContextProvider("provider1");
            var provider2 = new MockContextProvider("provider2");
            AIContextManager.RegisterProvider(provider1);
            AIContextManager.RegisterProvider(provider2);

            // Act
            var retrievedProvider1 = AIContextManager.GetProvider("provider1");
            var retrievedProvider2 = AIContextManager.GetProvider("provider2");
            var nonexistentProvider = AIContextManager.GetProvider("nonexistent");

            // Assert
            Assert.Same(provider1, retrievedProvider1);
            Assert.Same(provider2, retrievedProvider2);
            Assert.Null(nonexistentProvider);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetCurrentContext_ShouldCombineAllProviders [Windows]")]
#else
        [Fact(DisplayName = "GetCurrentContext_ShouldCombineAllProviders [Core]")]
#endif
        public void GetCurrentContext_ShouldCombineAllProviders()
        {
            // Arrange
            ResetManager();
            var provider1 = new MockContextProvider("time", new Dictionary<string, string>
            {
                ["current-datetime"] = "2025-01-01",
                ["timezone"] = "UTC",
            });
            var provider2 = new MockContextProvider("environment", new Dictionary<string, string>
            {
                ["os"] = "Windows",
                ["version"] = "11",
            });
            AIContextManager.RegisterProvider(provider1);
            AIContextManager.RegisterProvider(provider2);

            // Act
            var context = AIContextManager.GetCurrentContext();

            // Assert
            Assert.Equal(4, context.Count);
            Assert.Equal("2025-01-01", context["time_current-datetime"]);
            Assert.Equal("UTC", context["time_timezone"]);
            Assert.Equal("Windows", context["environment_os"]);
            Assert.Equal("11", context["environment_version"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetCurrentContext_ShouldFilterByProvider [Windows]")]
#else
        [Fact(DisplayName = "GetCurrentContext_ShouldFilterByProvider [Core]")]
#endif
        public void GetCurrentContext_ShouldFilterByProvider()
        {
            // Arrange
            ResetManager();
            var timeProvider = new MockContextProvider("time", new Dictionary<string, string>
            {
                ["current-datetime"] = "2025-01-01",
            });
            var envProvider = new MockContextProvider("environment", new Dictionary<string, string>
            {
                ["os"] = "Windows",
            });
            AIContextManager.RegisterProvider(timeProvider);
            AIContextManager.RegisterProvider(envProvider);

            // Act - Include only time provider
            var timeContext = AIContextManager.GetCurrentContext("time");
            
            // Act - Exclude time provider
            var nonTimeContext = AIContextManager.GetCurrentContext("-time");

            // Assert
            Assert.Single(timeContext);
            Assert.Contains("time_current-datetime", timeContext.Keys);
            Assert.DoesNotContain("environment_os", timeContext.Keys);

            Assert.Single(nonTimeContext);
            Assert.Contains("environment_os", nonTimeContext.Keys);
            Assert.DoesNotContain("time_current-datetime", nonTimeContext.Keys);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetCurrentContext_ShouldHandleComplexFiltering [Windows]")]
#else
        [Fact(DisplayName = "GetCurrentContext_ShouldHandleComplexFiltering [Core]")]
#endif
        public void GetCurrentContext_ShouldHandleComplexFiltering()
        {
            // Arrange
            ResetManager();
            var timeProvider = new MockContextProvider("time", new Dictionary<string, string>
            {
                ["current-datetime"] = "2025-01-01",
                ["timezone"] = "UTC",
            });
            var envProvider = new MockContextProvider("environment", new Dictionary<string, string>
            {
                ["os"] = "Windows",
                ["version"] = "11",
            });
            var fileProvider = new MockContextProvider("file", new Dictionary<string, string>
            {
                ["path"] = "/test/path",
                ["size"] = "1024",
            });
            AIContextManager.RegisterProvider(timeProvider);
            AIContextManager.RegisterProvider(envProvider);
            AIContextManager.RegisterProvider(fileProvider);

            // Act - Include time and environment, exclude timezone
            var context = AIContextManager.GetCurrentContext("time,environment");

            // Assert
            Assert.Equal(4, context.Count);
            Assert.Contains("time_current-datetime", context.Keys);
            Assert.Contains("environment_os", context.Keys);
            Assert.Contains("environment_version", context.Keys);
            Assert.Contains("time_timezone", context.Keys);
            Assert.DoesNotContain("file_path", context.Keys);
            Assert.DoesNotContain("file_size", context.Keys);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetCurrentContext_ShouldHandleKeyWithoutUnderscorePrefix [Windows]")]
#else
        [Fact(DisplayName = "GetCurrentContext_ShouldHandleKeyWithoutUnderscorePrefix [Core]")]
#endif
        public void GetCurrentContext_ShouldHandleKeyWithoutUnderscorePrefix()
        {
            // Arrange
            ResetManager();
            var provider = new MockContextProvider("test", new Dictionary<string, string>
            {
                ["simple-key"] = "value1",
                ["test_prefixed-key"] = "value2", // Already has provider prefix
            });
            AIContextManager.RegisterProvider(provider);

            // Act
            var context = AIContextManager.GetCurrentContext();

            // Assert
            Assert.Equal(2, context.Count);
            Assert.Contains("test_simple-key", context.Keys); // Should get prefixed
            Assert.Contains("test_prefixed-key", context.Keys); // Should remain as-is
        }
    }
}
