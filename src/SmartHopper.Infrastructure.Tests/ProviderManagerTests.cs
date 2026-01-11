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
    using SmartHopper.Infrastructure.AIProviders;
    using Xunit;

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
