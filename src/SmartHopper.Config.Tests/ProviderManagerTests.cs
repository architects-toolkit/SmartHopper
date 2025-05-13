using Xunit;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Interfaces;

namespace SmartHopper.Config.Tests
{
    public class ProviderManagerTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "GetProviders_ReturnsNonNullCollection [Windows]")]
#else
        [Fact(DisplayName = "GetProviders_ReturnsNonNullCollection [Core]")]
#endif
        public void GetProviders_ReturnsNonNullCollection()
        {
            var mgr = ProviderManager.Instance;
            var providers = mgr.GetProviders();
            Assert.NotNull(providers);
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "GetProvider_NullOrEmptyName_ReturnsNull [Windows]")]
#else
        [Theory(DisplayName = "GetProvider_NullOrEmptyName_ReturnsNull [Core]")]
#endif
        [InlineData(null)]
        [InlineData("")]
        public void GetProvider_NullOrEmptyName_ReturnsNull(string name)
        {
            var mgr = ProviderManager.Instance;
            Assert.Null(mgr.GetProvider(name));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetProvider_NotFound_ReturnsNull [Windows]")]
#else
        [Fact(DisplayName = "GetProvider_NotFound_ReturnsNull [Core]")]
#endif
        public void GetProvider_NotFound_ReturnsNull()
        {
            var mgr = ProviderManager.Instance;
            Assert.Null(mgr.GetProvider("nonexistent"));
        }
    }
}
