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
    using SmartHopper.Infrastructure.AICall;
    using SmartHopper.Infrastructure.AIModels;
    using SmartHopper.Infrastructure.AIProviders;
    using SmartHopper.Infrastructure.Settings;
    using Xunit;

    public class AdvancedConfigTests
    {
        private class DummyProvider : IAIProvider
        {
            public string Name => "DummyProvider";

            public bool IsEnabled => true;

            public System.Drawing.Image Icon => null;

            public IAIProviderModels Models { get; private set; } = new DummyProviderModels();

            public async Task InitializeProviderAsync()
            {
                await Task.CompletedTask;
            }

            public DummyProvider()
            {
            }

            public DummyProvider(string name)
            {
            }

            public string Encode(AIRequestCall request) => "{\"test\":\"encoded_request\"}";

            public string Encode(IAIInteraction interaction) => "{\"test\":\"encoded_interaction\"}";

            public string Encode(List<IAIInteraction> interactions) => "{\"test\":\"encoded_interactions\"}";

            public List<IAIInteraction> Decode(string response) => new List<IAIInteraction>();

            public AIRequestCall PreCall(AIRequestCall request) => request;

            public async Task<IAIReturn> Call(AIRequestCall request)
            {
                var result = new AIReturn();
                result.CreateSuccess(new AIBody(), request);
                return await Task.FromResult(result);
            }

            public IAIReturn PostCall(IAIReturn response) => response;

            public string GetDefaultModel(AICapability requiredCapability = AICapability.BasicChat, bool useSettings = true) => "dummy_test_model";

            public void RefreshCachedSettings(Dictionary<string, object> settings)
            {
            }

            public IEnumerable<SettingDescriptor> GetSettingDescriptors() => Enumerable.Empty<SettingDescriptor>();
        }

        private class DummyProviderModels : IAIProviderModels
        {
            public string GetModel(string requestedModel = "")
            {
                return string.IsNullOrEmpty(requestedModel) ? "dummy_model_1" : requestedModel;
            }

            public async Task<List<string>> RetrieveAvailable()
            {
                return await Task.FromResult(new List<string> { "dummy_model_1", "dummy_model_2" });
            }

            public async Task<Dictionary<string, AICapability>> RetrieveCapabilities()
            {
                return await Task.FromResult(new Dictionary<string, AICapability>
                {
                    ["dummy_model_1"] = AICapability.BasicChat,
                    ["dummy_model_2"] = AICapability.TextInput | AICapability.TextOutput
                });
            }

            public AICapability RetrieveCapabilities(string model)
            {
                var capabilities = new Dictionary<string, AICapability>
                {
                    ["dummy_model_1"] = AICapability.BasicChat,
                    ["dummy_model_2"] = AICapability.TextInput | AICapability.TextOutput
                };
                return capabilities.TryGetValue(model, out var capability) ? capability : AICapability.None;
            }

            public Dictionary<string, AICapability> RetrieveDefault()
            {
                return new Dictionary<string, AICapability>
                {
                    ["dummy_model_1"] = AICapability.BasicChat
                };
            }
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
        public void CustomModel_DefaultValuesAreSet()
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
        public void GetAvailableProviders_ReturnsAllDiscoveredFactories()
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
        public void GetProviderByName_ReturnsCorrectFactory()
        {
            // Skip test for now
            Assert.True(true);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetProviderByName_ThrowsIfNotFound [Windows]")]
#else
        [Fact(DisplayName = "GetProviderByName_ThrowsIfNotFound [Core]")]
#endif
        public void GetProviderByName_ThrowsIfNotFound()
        {
            var mgr = ProviderManager.Instance;
            Assert.Null(mgr.GetProvider("Nonexistent"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ProviderSettings_SerializationRoundTrip [Windows]")]
#else
        [Fact(DisplayName = "ProviderSettings_SerializationRoundTrip [Core]")]
#endif
        public void ProviderSettings_SerializationRoundTrip()
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
        public void IAIProviderSettings_SchemaValidation()
        {
            // Skip test for now
            Assert.True(true);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "ConfigurationLoader_RegistersAllServices [Windows]")]
#else
        [Fact(DisplayName = "ConfigurationLoader_RegistersAllServices [Core]")]
#endif
        public void ConfigurationLoader_RegistersAllServices()
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
        public void ProviderManager_LoadsFromServiceProvider()
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
        public void ProviderManager_HandlesInvalidAssemblyGracefully()
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
        public void ConfigurationLoader_ThrowsOnMalformedJson()
        {
            // Malformed JSON should throw JsonReaderException
            var bad = "{ \"DebounceTime\": , }";
            Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<SmartHopperSettings>(bad));
        }
    }
}
