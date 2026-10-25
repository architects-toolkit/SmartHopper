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

namespace SmartHopper.Infrastructure.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using SmartHopper.Infrastructure.Settings;
    using SmartHopper.ProviderSdk.Settings;
    using Xunit;

    /// <summary>
    /// Tests persistence and visibility behavior for hidden provider settings.
    /// </summary>
    public sealed class HiddenProviderSettingsTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "HiddenSettings_PageStyleSavePreservesHiddenValues [Windows]")]
#else
        [Fact(DisplayName = "HiddenSettings_PageStyleSavePreservesHiddenValues [Core]")]
#endif
        public void PageStyleSavePreservesHiddenValues()
        {
            var descriptors = new[]
            {
                new SettingDescriptor { Name = "Token", IsSecret = true, IsHidden = true },
                new SettingDescriptor { Name = "Model" },
            };
            var fixed32ByteKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            var settings = new SmartHopperSettings
            {
                DescriptorSource = _ => descriptors,
                EncryptionKeySource = () => fixed32ByteKey,
            };

            settings.SetSetting("Provider", "Token", "hidden-token");
            settings.SetSetting("Provider", "Model", "old-model");

            var updates = ProviderSettingDescriptors.Visible(descriptors)
                .Where(descriptor => descriptor.Name == "Model")
                .ToDictionary(descriptor => descriptor.Name, _ => (object)"new-model");
            settings.ApplyProviderSettingUpdates("Provider", updates);

            Assert.Equal("hidden-token", settings.GetSetting("Provider", "Token"));
            Assert.Equal("new-model", settings.GetSetting("Provider", "Model"));
            var providerSettings = settings.GetProviderSettings("Provider");
            Assert.Equal("hidden-token", providerSettings["Token"]);
            Assert.Equal("new-model", providerSettings["Model"]);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "HiddenSettings_SecretRoundTripsWithoutSerializingTestSeams [Windows]")]
#else
        [Fact(DisplayName = "HiddenSettings_SecretRoundTripsWithoutSerializingTestSeams [Core]")]
#endif
        public void SecretRoundTripsWithoutSerializingTestSeams()
        {
            var descriptors = new[]
            {
                new SettingDescriptor { Name = "Token", IsSecret = true, IsHidden = true },
            };
            var fixed32ByteKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            var settings = new SmartHopperSettings
            {
                DescriptorSource = _ => descriptors,
                EncryptionKeySource = () => fixed32ByteKey,
            };

            settings.SetSetting("Provider", "Token", "hidden-token");

            var storedValue = settings.ProviderSettings["Provider"]["Token"].ToString();
            var expectedPrefix = System.Security.Cryptography.AesGcm.IsSupported ? "SH04:" : "SH03:";
            Assert.StartsWith(expectedPrefix, storedValue, System.StringComparison.Ordinal);
            Assert.DoesNotContain("hidden-token", storedValue, System.StringComparison.Ordinal);
            Assert.Equal("hidden-token", settings.GetSetting("Provider", "Token"));

            settings.EncryptionKeySource = () => null;
            settings.SetSetting("Provider", "Token", "plaintext-must-not-be-stored");
            Assert.False(settings.ProviderSettings["Provider"].ContainsKey("Token"));

            var json = JsonConvert.SerializeObject(settings);
            Assert.DoesNotContain(nameof(SmartHopperSettings.DescriptorSource), json, System.StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(SmartHopperSettings.EncryptionKeySource), json, System.StringComparison.Ordinal);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "HiddenSettings_UsesHiddenDefaultWhenNothingIsStored [Windows]")]
#else
        [Fact(DisplayName = "HiddenSettings_UsesHiddenDefaultWhenNothingIsStored [Core]")]
#endif
        public void UsesHiddenDefaultWhenNothingIsStored()
        {
            var descriptors = new[]
            {
                new SettingDescriptor { Name = "InternalMode", DefaultValue = "managed", IsHidden = true },
            };
            var settings = new SmartHopperSettings
            {
                DescriptorSource = _ => descriptors,
                EncryptionKeySource = () => null,
            };

            Assert.Equal("managed", settings.GetSetting("Provider", "InternalMode"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "HiddenSettings_VisibleFiltersInOrderAndHandlesNull [Windows]")]
#else
        [Fact(DisplayName = "HiddenSettings_VisibleFiltersInOrderAndHandlesNull [Core]")]
#endif
        public void VisibleFiltersInOrderAndHandlesNull()
        {
            var descriptors = new SettingDescriptor[]
            {
                new SettingDescriptor { Name = "First" },
                new SettingDescriptor { Name = "Hidden", IsHidden = true },
                null!,
                new SettingDescriptor { Name = "Second" },
            };

            var visible = ProviderSettingDescriptors.Visible(descriptors);

            Assert.Equal(new[] { "First", "Second" }, visible.Select(descriptor => descriptor.Name));
            Assert.Empty(ProviderSettingDescriptors.Visible(null));
        }
    }
}
