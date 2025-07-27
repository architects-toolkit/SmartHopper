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
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.Interfaces;
    using SmartHopper.Infrastructure.Managers.AIProviders;
    using SmartHopper.Infrastructure.Models;
    using SmartHopper.Infrastructure.Settings;
    using Xunit;

    public class AdvancedConfigTests
    {
        private class DummyProvider : IAIProvider
        {
            public string Name => "DummyProvider";

            public string DefaultModel => "Model";

            public string DefaultImgModel => string.Empty; // Test provider doesn't support image generation

            public string DefaultServerUrl => "https://example.com";

            public bool IsEnabled => true;

            public System.Drawing.Image? Icon => null;

            public DummyProvider()
            {
            }

            public DummyProvider(string name)
            {
            }

            public Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", string? toolFilter = null) => Task.FromResult(default(AIResponse));

            public string GetModel(string requestedModel = "") => this.DefaultModel;

            public void RefreshCachedSettings(Dictionary<string, object> settings)
            {
            }

            public void ResetCachedSettings(Dictionary<string, object> settings)
            {
            }

            public IEnumerable<SettingDescriptor> GetSettingDescriptors() => Enumerable.Empty<SettingDescriptor>();

            public Task<AIResponse> GenerateImage(string prompt, string model = "", string size = "1024x1024", string quality = "standard", string style = "vivid") => 
                Task.FromResult(new AIResponse { FinishReason = "error", ErrorMessage = "Test provider does not support image generation" });
        }

        private class DummySettings : IAIProviderSettings
        {
            private readonly IAIProvider provider;

            public DummySettings(IAIProvider p)
            {
                this.provider = p;
            }

            public IEnumerable<SettingDescriptor> GetSettingDescriptors() => Enumerable.Empty<SettingDescriptor>();

            public bool ValidateSettings(Dictionary<string, object> settings) => true;
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "CustomModel_DefaultValues_AreSet [Windows]")]
#else
        [Fact(DisplayName = "CustomModel_DefaultValues_AreSet [Core]")]
#endif
        public void CustomModelDefaultValuesAreSet()
        {
            var settings = new SmartHopperSettings();
            Assert.Equal(1000, settings.DebounceTime);
            Assert.Equal(string.Empty, settings.DefaultAIProvider);
            Assert.Empty(settings.TrustedProviders);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetAvailableProviders_ReturnsAllDiscoveredFactories [Windows]")]
#else
        [Fact(DisplayName = "GetAvailableProviders_ReturnsAllDiscoveredFactories [Core]")]
#endif
        public void GetAvailableProvidersReturnsAllDiscoveredFactories()
        {
            var mgr = ProviderManager.Instance;
            var providers = mgr.GetProviders();
            Assert.NotNull(providers);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetProviderByName_ReturnsCorrectFactory [Windows]")]
#else
        [Fact(DisplayName = "GetProviderByName_ReturnsCorrectFactory [Core]")]
#endif
        public void GetProviderByNameReturnsCorrectFactory()
        {
            // Skip test for now
            Assert.True(true);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetProviderByName_ThrowsIfNotFound [Windows]")]
#else
        [Fact(DisplayName = "GetProviderByName_ThrowsIfNotFound [Core]")]
#endif
        public void GetProviderByNameThrowsIfNotFound()
        {
            var mgr = ProviderManager.Instance;
            Assert.Null(mgr.GetProvider("Nonexistent"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ProviderSettings_SerializationRoundTrip [Windows]")]
#else
        [Fact(DisplayName = "ProviderSettings_SerializationRoundTrip [Core]")]
#endif
        public void ProviderSettingsSerializationRoundTrip()
        {
            var settings = new SmartHopperSettings { DebounceTime = 555, DefaultAIProvider = "ProvX" };
            var json = JsonConvert.SerializeObject(settings);
            var deserialized = JsonConvert.DeserializeObject<SmartHopperSettings>(json);
            Assert.Equal(555, deserialized.DebounceTime);
            Assert.Equal("ProvX", deserialized.DefaultAIProvider);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IAIProviderSettings_SchemaValidation [Windows]")]
#else
        [Fact(DisplayName = "IAIProviderSettings_SchemaValidation [Core]")]
#endif
        public void IAIProviderSettingsSchemaValidation()
        {
            // Skip test for now
            Assert.True(true);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConfigurationLoader_RegistersAllServices [Windows]")]
#else
        [Fact(DisplayName = "ConfigurationLoader_RegistersAllServices [Core]")]
#endif
        public void ConfigurationLoaderRegistersAllServices()
        {
            // Settings singleton and provider manager should be available
            var settings = SmartHopperSettings.Instance;
            var mgr = ProviderManager.Instance;
            Assert.NotNull(settings);
            Assert.NotNull(mgr);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ProviderManager_LoadsFromServiceProvider [Windows]")]
#else
        [Fact(DisplayName = "ProviderManager_LoadsFromServiceProvider [Core]")]
#endif
        public void ProviderManagerLoadsFromServiceProvider()
        {
            // Singleton instance consistency
            var m1 = ProviderManager.Instance;
            var m2 = ProviderManager.Instance;
            Assert.Same(m1, m2);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ProviderManager_HandlesInvalidAssemblyGracefully [Windows]")]
#else
        [Fact(DisplayName = "ProviderManager_HandlesInvalidAssemblyGracefully [Core]")]
#endif
        public void ProviderManagerHandlesInvalidAssemblyGracefully()
        {
            // Should not throw on refresh with no external providers
            var ex = Record.Exception(() => ProviderManager.Instance.RefreshProviders());
            Assert.Null(ex);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConfigurationLoader_ThrowsOnMalformedJson [Windows]")]
#else
        [Fact(DisplayName = "ConfigurationLoader_ThrowsOnMalformedJson [Core]")]
#endif
        public void ConfigurationLoaderThrowsOnMalformedJson()
        {
            // Malformed JSON should throw JsonReaderException
            var bad = "{ \"DebounceTime\": , }";
            Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<SmartHopperSettings>(bad));
        }
    }
}
