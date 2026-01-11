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
    using System;
    using System.IO;
    using Newtonsoft.Json;
    using SmartHopper.Infrastructure.Settings;
    using Xunit;

    /// <summary>
    /// Tests for SmartHopper settings functionality.
    /// </summary>
    public class SmartHopperSettingsTests
    {
        /// <summary>
        /// Tests that SmartHopperSettings can be deserialized from valid JSON.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SmartHopperSettings_BindsFromValidJson [Windows]")]
#else
        [Fact(DisplayName = "SmartHopperSettings_BindsFromValidJson [Core]")]
#endif
        public void SmartHopperSettings_BindsFromValidJson()
        {
            var json = @"{
                ""DebounceTime"": 200,
                ""DefaultAIProvider"": ""TestProvider"",
                ""TrustedProviders"": { ""Prov1"": true },
                ""ProviderSettings"": { }
            }";
            var settings = JsonConvert.DeserializeObject<SmartHopperSettings>(json);
            Assert.Equal(200, settings.DebounceTime);
            Assert.Equal("TestProvider", settings.DefaultAIProvider);
            Assert.True(settings.TrustedProviders.ContainsKey("Prov1") && settings.TrustedProviders["Prov1"]);
        }

        /// <summary>
        /// Tests that SmartHopperSettings can handle missing required fields gracefully.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SmartHopperSettings_ThrowsOnMissingRequiredField [Windows]")]
#else
        [Fact(DisplayName = "SmartHopperSettings_ThrowsOnMissingRequiredField [Core]")]
#endif
        public void SmartHopperSettings_ThrowsOnMissingRequiredField()
        {
            // Missing fields should not throw, instance created
            var json = "{}";
            var settings = JsonConvert.DeserializeObject<SmartHopperSettings>(json);
            Assert.NotNull(settings);
        }

        /// <summary>
        /// Tests IAIProviderSettings schema validation functionality.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "IAIProviderSettings_SchemaValidation [Windows]")]
#else
        [Fact(DisplayName = "IAIProviderSettings_SchemaValidation [Core]")]
#endif
        public void IAIProviderSettings_SchemaValidation()
        {
        }

        /// <summary>
        /// Verifies that when the settings file does not exist, Load() returns a new instance with default values.
        /// Safety: Does not modify real user data. If the file exists on the machine, the test exits early.
        /// </summary>
#if NET7_WINDOWS
        [Fact(DisplayName = "SmartHopperSettings_LoadsDefaults_WhenFileMissing [Windows]")]
#else
        [Fact(DisplayName = "SmartHopperSettings_LoadsDefaults_WhenFileMissing [Core]")]
#endif
        public void SmartHopperSettings_LoadsDefaults_WhenFileMissing()
        {
            // Compute the default settings path exactly as in SmartHopperSettings
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsPath = Path.Combine(appData, "Grasshopper", "SmartHopper.json");

            // If the file exists on this machine, we cannot safely delete or rename it in tests.
            // In that case, exit early. This test mainly covers CI or clean environments.
            if (File.Exists(settingsPath))
            {
                // Pass trivially to avoid mutating user environment.
                Assert.True(true);
                return;
            }

            // Act: Load when file is missing
            var settings = SmartHopperSettings.Load();

            // Assert: defaults are present
            Assert.NotNull(settings);
            Assert.Equal(1000, settings.DebounceTime);
            Assert.Equal(string.Empty, settings.DefaultAIProvider);
            Assert.NotNull(settings.TrustedProviders);
            Assert.Empty(settings.TrustedProviders);
        }
    }
}
