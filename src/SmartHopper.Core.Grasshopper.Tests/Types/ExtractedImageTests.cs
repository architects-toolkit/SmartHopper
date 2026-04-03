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

namespace SmartHopper.Core.Grasshopper.Tests.Types
{
    using SmartHopper.Core.Grasshopper.Converters;
    using Xunit;

    public class ExtractedImagePocoTests
    {
        [Fact(DisplayName = "Constructor_SetsAllProperties")]
        public void Constructor_SetsAllProperties()
        {
            var image = new ExtractedImage("img-1", "base64data", "image/png", "Page 1", 1);
            Assert.Equal("img-1", image.Id);
            Assert.Equal("base64data", image.Base64Data);
            Assert.Equal("image/png", image.MimeType);
            Assert.Equal("Page 1", image.Context);
            Assert.Equal(1, image.PageOrSlide);
        }

        [Fact(DisplayName = "Id_IsReadOnly")]
        public void Id_IsReadOnly()
        {
            var image = new ExtractedImage("img-1", "base64data", "image/png", "Page 1", 1);
            Assert.Equal("img-1", image.Id);
        }

        [Fact(DisplayName = "Base64Data_IsReadOnly")]
        public void Base64Data_IsReadOnly()
        {
            var image = new ExtractedImage("img-1", "base64data", "image/png", "Page 1", 1);
            Assert.Equal("base64data", image.Base64Data);
        }

        [Fact(DisplayName = "MimeType_IsReadOnly")]
        public void MimeType_IsReadOnly()
        {
            var image = new ExtractedImage("img-1", "base64data", "image/png", "Page 1", 1);
            Assert.Equal("image/png", image.MimeType);
        }

        [Fact(DisplayName = "Context_IsReadOnly")]
        public void Context_IsReadOnly()
        {
            var image = new ExtractedImage("img-1", "base64data", "image/png", "Page 1", 1);
            Assert.Equal("Page 1", image.Context);
        }

        [Fact(DisplayName = "PageOrSlide_IsReadOnly")]
        public void PageOrSlide_IsReadOnly()
        {
            var image = new ExtractedImage("img-1", "base64data", "image/png", "Page 1", 1);
            Assert.Equal(1, image.PageOrSlide);
        }
    }
}
