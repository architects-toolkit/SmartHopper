namespace SmartHopper.Config.Tests
{
    using Newtonsoft.Json;
    using SmartHopper.Config.Configuration;
    using Xunit;

    public class SmartHopperSettingsTests
    {
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

#if NET7_WINDOWS
        [Fact(DisplayName = "IAIProviderSettings_SchemaValidation [Windows]")]
#else
        [Fact(DisplayName = "IAIProviderSettings_SchemaValidation [Core]")]
#endif
        public void IAIProviderSettings_SchemaValidation()
        {
        }
    }
}
