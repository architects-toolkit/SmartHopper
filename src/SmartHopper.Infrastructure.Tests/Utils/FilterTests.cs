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

namespace SmartHopper.Infrastructure.Tests.Utils
{
    using SmartHopper.ProviderSdk.Utils;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="Filter"/> and <see cref="Filtering"/> utilities.
    /// </summary>
    public class FilterTests
    {
        #region Parse

#if NET7_WINDOWS
        [Fact(DisplayName = "Filter.Parse null returns include-all [Windows]")]
#else
        [Fact(DisplayName = "Filter.Parse null returns include-all [Core]")]
#endif
        public void Parse_Null_ReturnsIncludeAll()
        {
            var f = Filter.Parse(null);
            Assert.True(f.IncludeAll);
            Assert.False(f.ExcludeAll);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Filter.Parse empty returns include-all [Windows]")]
#else
        [Fact(DisplayName = "Filter.Parse empty returns include-all [Core]")]
#endif
        public void Parse_Empty_ReturnsIncludeAll()
        {
            var f = Filter.Parse("   ");
            Assert.True(f.IncludeAll);
            Assert.False(f.ExcludeAll);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Filter.Parse dash-star returns exclude-all [Windows]")]
#else
        [Fact(DisplayName = "Filter.Parse dash-star returns exclude-all [Core]")]
#endif
        public void Parse_DashStar_ReturnsExcludeAll()
        {
            var f = Filter.Parse("-*");
            Assert.True(f.ExcludeAll);
            Assert.False(f.IncludeAll);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Filter.Parse explicit includes [Windows]")]
#else
        [Fact(DisplayName = "Filter.Parse explicit includes [Core]")]
#endif
        public void Parse_ExplicitIncludes()
        {
            var f = Filter.Parse("gh_get,gh_put");
            Assert.False(f.IncludeAll);
            Assert.Contains("gh_get", f.IncludeSet);
            Assert.Contains("gh_put", f.IncludeSet);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Filter.Parse include-all with excludes [Windows]")]
#else
        [Fact(DisplayName = "Filter.Parse include-all with excludes [Core]")]
#endif
        public void Parse_IncludeAllWithExcludes()
        {
            var f = Filter.Parse("* -web2md -file2md");
            Assert.True(f.IncludeAll);
            Assert.Contains("web2md", f.ExcludeSet);
            Assert.Contains("file2md", f.ExcludeSet);
        }

        #endregion

        #region ShouldInclude

#if NET7_WINDOWS
        [Theory(DisplayName = "Filter.ShouldInclude with exclude-all blocks everything [Windows]")]
#else
        [Theory(DisplayName = "Filter.ShouldInclude with exclude-all blocks everything [Core]")]
#endif
        [InlineData("-*", "anything", false)]
        [InlineData("-*", "gh_get", false)]
        public void ShouldInclude_ExcludeAll_BlocksEverything(string filter, string key, bool expected)
        {
            var f = Filter.Parse(filter);
            Assert.Equal(expected, f.ShouldInclude(key));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Filter.ShouldInclude with include-all allows everything [Windows]")]
#else
        [Theory(DisplayName = "Filter.ShouldInclude with include-all allows everything [Core]")]
#endif
        [InlineData("", "anything", true)]
        [InlineData("*", "anything", true)]
        public void ShouldInclude_IncludeAll_AllowsEverything(string filter, string key, bool expected)
        {
            var f = Filter.Parse(filter);
            Assert.Equal(expected, f.ShouldInclude(key));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Filter.ShouldInclude explicit includes only listed [Windows]")]
#else
        [Theory(DisplayName = "Filter.ShouldInclude explicit includes only listed [Core]")]
#endif
        [InlineData("gh_get", "gh_get", true)]
        [InlineData("gh_get", "gh_put", false)]
        public void ShouldInclude_ExplicitIncludes_OnlyListed(string filter, string key, bool expected)
        {
            var f = Filter.Parse(filter);
            Assert.Equal(expected, f.ShouldInclude(key));
        }

#if NET7_WINDOWS
        [Theory(DisplayName = "Filter.ShouldInclude case insensitive [Windows]")]
#else
        [Theory(DisplayName = "Filter.ShouldInclude case insensitive [Core]")]
#endif
        [InlineData("GH_GET", "gh_get", true)]
        [InlineData("gh_get", "GH_GET", true)]
        public void ShouldInclude_CaseInsensitive(string filter, string key, bool expected)
        {
            var f = Filter.Parse(filter);
            Assert.Equal(expected, f.ShouldInclude(key));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Filter.ShouldInclude exclude overrides include-all [Windows]")]
#else
        [Fact(DisplayName = "Filter.ShouldInclude exclude overrides include-all [Core]")]
#endif
        public void ShouldInclude_ExcludeOverridesIncludeAll()
        {
            var f = Filter.Parse("* -gh_get");
            Assert.True(f.ShouldInclude("gh_put"));
            Assert.False(f.ShouldInclude("gh_get"));
        }

        #endregion
    }
}
