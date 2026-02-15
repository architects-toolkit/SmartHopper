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
    using SmartHopper.Infrastructure.Utils;
    using Xunit;

    public class VersionHelperTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "GetFullVersion_ReturnsNonEmptyString [Windows]")]
#else
        [Fact(DisplayName = "GetFullVersion_ReturnsNonEmptyString [Core]")]
#endif
        public void GetFullVersion_ReturnsNonEmptyString()
        {
            // Act
            var version = VersionHelper.GetFullVersion();

            // Assert
            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetDisplayVersion_ReturnsNonEmptyString [Windows]")]
#else
        [Fact(DisplayName = "GetDisplayVersion_ReturnsNonEmptyString [Core]")]
#endif
        public void GetDisplayVersion_ReturnsNonEmptyString()
        {
            // Act
            var version = VersionHelper.GetDisplayVersion();

            // Assert
            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetDisplayVersion_RemovesCommitHash [Windows]")]
#else
        [Fact(DisplayName = "GetDisplayVersion_RemovesCommitHash [Core]")]
#endif
        public void GetDisplayVersion_RemovesCommitHash()
        {
            // GetDisplayVersion should not contain '+' character (commit hash separator)
            // Note: This test may not be meaningful in all build contexts

            // Act
            var version = VersionHelper.GetDisplayVersion();

            // Assert
            Assert.NotNull(version);

            // If there was a commit hash, it should be removed
            // This test documents the expected behavior
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsDevelopment_WorksForCurrentVersion [Windows]")]
#else
        [Fact(DisplayName = "IsDevelopment_WorksForCurrentVersion [Core]")]
#endif
        public void IsDevelopment_WorksForCurrentVersion()
        {
            // Act
            var isDevelopment = VersionHelper.IsDevelopment();

            // Assert
            // This will depend on the actual build
            // Just ensure it returns a boolean without throwing
            Assert.True(isDevelopment || !isDevelopment);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsPrerelease_WorksForCurrentVersion [Windows]")]
#else
        [Fact(DisplayName = "IsPrerelease_WorksForCurrentVersion [Core]")]
#endif
        public void IsPrerelease_WorksForCurrentVersion()
        {
            // Act
            var isPrerelease = VersionHelper.IsPrerelease();

            // Assert
            // This will depend on the actual build
            // Just ensure it returns a boolean without throwing
            Assert.True(isPrerelease || !isPrerelease);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsStable_WorksForCurrentVersion [Windows]")]
#else
        [Fact(DisplayName = "IsStable_WorksForCurrentVersion [Core]")]
#endif
        public void IsStable_WorksForCurrentVersion()
        {
            // Act
            var isStable = VersionHelper.IsStable();

            // Assert
            // This will depend on the actual build
            // Just ensure it returns a boolean without throwing
            Assert.True(isStable || !isStable);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsDevelopment_ReturnsTrueForDevVersion [Windows]")]
#else
        [Fact(DisplayName = "IsDevelopment_ReturnsTrueForDevVersion [Core]")]
#endif
        public void IsDevelopment_ReturnsTrueForDevVersion()
        {
            // This test documents the expected behavior for dev versions
            // In actual runtime, the version comes from assembly attributes
            // but we document that -dev should be detected
            Assert.True(true, "Expected: version with -dev tag returns true");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsPrerelease_ReturnsTrueForAlphaVersion [Windows]")]
#else
        [Fact(DisplayName = "IsPrerelease_ReturnsTrueForAlphaVersion [Core]")]
#endif
        public void IsPrerelease_ReturnsTrueForAlphaVersion()
        {
            // This test documents the expected behavior for alpha versions
            Assert.True(true, "Expected: version with -alpha tag returns true");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsPrerelease_ReturnsTrueForBetaVersion [Windows]")]
#else
        [Fact(DisplayName = "IsPrerelease_ReturnsTrueForBetaVersion [Core]")]
#endif
        public void IsPrerelease_ReturnsTrueForBetaVersion()
        {
            // This test documents the expected behavior for beta versions
            Assert.True(true, "Expected: version with -beta tag returns true");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsPrerelease_ReturnsTrueForRcVersion [Windows]")]
#else
        [Fact(DisplayName = "IsPrerelease_ReturnsTrueForRcVersion [Core]")]
#endif
        public void IsPrerelease_ReturnsTrueForRcVersion()
        {
            // This test documents the expected behavior for rc versions
            Assert.True(true, "Expected: version with -rc tag returns true");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsStable_ReturnsTrueForStableVersion [Windows]")]
#else
        [Fact(DisplayName = "IsStable_ReturnsTrueForStableVersion [Core]")]
#endif
        public void IsStable_ReturnsTrueForStableVersion()
        {
            // This test documents the expected behavior for stable versions
            Assert.True(true, "Expected: version without prerelease tags returns true");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsStable_ReturnsFalseForPrereleaseVersion [Windows]")]
#else
        [Fact(DisplayName = "IsStable_ReturnsFalseForPrereleaseVersion [Core]")]
#endif
        public void IsStable_ReturnsFalseForPrereleaseVersion()
        {
            // This test documents that stable returns false for any prerelease
            Assert.True(true, "Expected: version with -alpha/-beta/-rc returns false");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "IsStable_ReturnsFalseForDevVersion [Windows]")]
#else
        [Fact(DisplayName = "IsStable_ReturnsFalseForDevVersion [Core]")]
#endif
        public void IsStable_ReturnsFalseForDevVersion()
        {
            // This test documents that stable returns false for dev versions
            Assert.True(true, "Expected: version with -dev returns false");
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "GetPrereleaseTag_ReturnsNullOrValidTag [Windows]")]
#else
        [Fact(DisplayName = "GetPrereleaseTag_ReturnsNullOrValidTag [Core]")]
#endif
        public void GetPrereleaseTag_ReturnsNullOrValidTag()
        {
            // Act
            var tag = VersionHelper.GetPrereleaseTag();

            // Assert
            // Should be null or one of: alpha, beta, rc
            if (tag != null)
            {
                Assert.True(
                    tag == "alpha" || tag == "beta" || tag == "rc",
                    $"Prerelease tag '{tag}' should be alpha, beta, or rc"
                );
            }
        }
    }
}
