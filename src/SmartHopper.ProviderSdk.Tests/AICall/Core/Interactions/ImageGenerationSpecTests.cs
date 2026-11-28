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

using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using Xunit;

namespace SmartHopper.ProviderSdk.Tests.AICall.Core.Interactions
{
    /// <summary>
    /// Tests for provider-agnostic image-generation input parsing (<see cref="ImageGenerationSpec"/>).
    /// </summary>
    [Collection("ProviderSdk")]
    public class ImageGenerationSpecTests
    {
#if NET7_WINDOWS
        private const string PlatformSuffix = " [Windows]";
#else
        private const string PlatformSuffix = " [Core]";
#endif

        [Fact(DisplayName = nameof(TryParseDimensions_ParsesWxH) + PlatformSuffix)]
        public void TryParseDimensions_ParsesWxH()
        {
            Assert.True(ImageGenerationSpec.TryParseDimensions("1024x1536", out var w, out var h));
            Assert.Equal(1024, w);
            Assert.Equal(1536, h);
        }

        [Fact(DisplayName = nameof(TryParseDimensions_RejectsNonDimensions) + PlatformSuffix)]
        public void TryParseDimensions_RejectsNonDimensions()
        {
            Assert.False(ImageGenerationSpec.TryParseDimensions("1K", out _, out _));
            Assert.False(ImageGenerationSpec.TryParseDimensions("16:9", out _, out _));
            Assert.False(ImageGenerationSpec.TryParseDimensions(null, out _, out _));
        }

        [Fact(DisplayName = nameof(ResolveAspectRatio_PrefersExplicit) + PlatformSuffix)]
        public void ResolveAspectRatio_PrefersExplicit()
        {
            Assert.Equal("16:9", ImageGenerationSpec.ResolveAspectRatio("16:9", "1024x1024"));
            Assert.Equal("3:4", ImageGenerationSpec.ResolveAspectRatio("3:4", null));
        }

        [Fact(DisplayName = nameof(ResolveAspectRatio_DerivesFromSize) + PlatformSuffix)]
        public void ResolveAspectRatio_DerivesFromSize()
        {
            Assert.Equal("16:9", ImageGenerationSpec.ResolveAspectRatio(null, "1920x1080"));
            Assert.Equal("1:1", ImageGenerationSpec.ResolveAspectRatio(null, "1024x1024"));
            Assert.Null(ImageGenerationSpec.ResolveAspectRatio(null, "1K"));
        }

        [Fact(DisplayName = nameof(ResolveMaxDimension_TiersAndSizes) + PlatformSuffix)]
        public void ResolveMaxDimension_TiersAndSizes()
        {
            Assert.Equal(1024, ImageGenerationSpec.ResolveMaxDimension("1k"));
            Assert.Equal(2048, ImageGenerationSpec.ResolveMaxDimension("2K"));
            Assert.Equal(4096, ImageGenerationSpec.ResolveMaxDimension("4K"));
            Assert.Equal(1536, ImageGenerationSpec.ResolveMaxDimension("1536x1024"));
            Assert.Null(ImageGenerationSpec.ResolveMaxDimension("auto"));
        }

        [Fact(DisplayName = nameof(SnapToClosestRatio_PicksNearest) + PlatformSuffix)]
        public void SnapToClosestRatio_PicksNearest()
        {
            Assert.Equal("16:9", ImageGenerationSpec.SnapToClosestRatio("7:4", "1:1", "16:9", "9:16"));
            Assert.Equal("9:16", ImageGenerationSpec.SnapToClosestRatio("9:18", "1:1", "16:9", "9:16"));
            Assert.Null(ImageGenerationSpec.SnapToClosestRatio("wide", "1:1", "16:9"));
        }
    }
}
