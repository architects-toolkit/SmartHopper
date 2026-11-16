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

namespace SmartHopper.Core.Tests.Types
{
    using System;
    using SmartHopper.Core.Types;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="VersatileImage"/> parsing factories that do not
    /// require bitmap decoding or Grasshopper assemblies.
    /// </summary>
    public class VersatileImageTests
    {
        [Fact(DisplayName = "FromInteraction prefers the resolved image URL")]
        public void FromInteraction_UrlPreferred()
        {
            var interaction = new AIInteractionImage
            {
                Agent = AIAgent.Assistant,
                ImageUrl = new Uri("https://example.com/generated.png"),
                ImageData = "QUJD",
            };

            var image = VersatileImage.FromInteraction(interaction);

            Assert.Equal(VersatileImageKind.Url, image.Kind);
            Assert.Equal("https://example.com/generated.png", image.RawValue);
        }

        [Fact(DisplayName = "FromInteraction wraps inline data in a data URI with declared MIME")]
        public void FromInteraction_Base64DataUri()
        {
            var interaction = new AIInteractionImage
            {
                Agent = AIAgent.Assistant,
                ImageData = "QUJD",
                MimeType = "image/jpeg",
            };

            var image = VersatileImage.FromInteraction(interaction);

            Assert.Equal(VersatileImageKind.DataUri, image.Kind);
            Assert.Equal("data:image/jpeg;base64,QUJD", image.RawValue);
        }

        [Fact(DisplayName = "FromInteraction defaults missing or non-image MIME to image/png")]
        public void FromInteraction_MimeFallback()
        {
            var missing = VersatileImage.FromInteraction(new AIInteractionImage { ImageData = "QUJD" });
            Assert.Equal("data:image/png;base64,QUJD", missing.RawValue);

            var nonImage = VersatileImage.FromInteraction(new AIInteractionImage
            {
                ImageData = "QUJD",
                MimeType = "application/octet-stream",
            });
            Assert.Equal("data:image/png;base64,QUJD", nonImage.RawValue);
        }

        [Fact(DisplayName = "FromInteraction throws when the interaction carries no content")]
        public void FromInteraction_ThrowsWithoutContent()
        {
            Assert.Throws<ArgumentException>(() =>
                VersatileImage.FromInteraction(new AIInteractionImage { Agent = AIAgent.Assistant }));
            Assert.Throws<ArgumentException>(() =>
                VersatileImage.FromInteraction(new AIInteractionImage { ImageData = "   " }));
        }

        [Fact(DisplayName = "FromInteraction throws on null interaction")]
        public void FromInteraction_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => VersatileImage.FromInteraction(null));
        }
    }
}
