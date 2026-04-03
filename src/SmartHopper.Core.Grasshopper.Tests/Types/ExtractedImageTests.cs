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

using System;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Types;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Types
{
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

    public class GH_ExtractedImageTests
    {
        private static string CreateValidBase64Png()
        {
            using (var bitmap = new Bitmap(1, 1))
            {
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        [Fact(DisplayName = "DefaultConstructor_IsNotValid")]
        public void DefaultConstructor_IsNotValid()
        {
            var ghImage = new GH_ExtractedImage();
            Assert.False(ghImage.IsValid);
        }

        [Fact(DisplayName = "Constructor_WithValidData_IsValid")]
        public void Constructor_WithValidData_IsValid()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            Assert.True(ghImage.IsValid);
        }

        [Fact(DisplayName = "IsValid_NullBase64_ReturnsFalse")]
        public void IsValid_NullBase64_ReturnsFalse()
        {
            var image = new ExtractedImage("img-1", null, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            Assert.False(ghImage.IsValid);
        }

        [Fact(DisplayName = "ToString_NullValue_ReturnsNullMessage")]
        public void ToString_NullValue_ReturnsNullMessage()
        {
            var ghImage = new GH_ExtractedImage();
            Assert.Contains("Null", ghImage.ToString());
        }

        [Fact(DisplayName = "ToString_WithValue_ContainsId")]
        public void ToString_WithValue_ContainsId()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            Assert.Contains("img-1", ghImage.ToString());
        }

        [Fact(DisplayName = "CastFrom_AnotherGHExtractedImage_Succeeds")]
        public void CastFrom_AnotherGHExtractedImage_Succeeds()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage1 = new GH_ExtractedImage(image);
            var ghImage2 = new GH_ExtractedImage();
            Assert.True(ghImage2.CastFrom(ghImage1));
            Assert.Equal(ghImage1.Value.Id, ghImage2.Value.Id);
        }

        [Fact(DisplayName = "CastFrom_ExtractedImageDirect_Succeeds")]
        public void CastFrom_ExtractedImageDirect_Succeeds()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage();
            Assert.True(ghImage.CastFrom(image));
            Assert.Equal(image.Id, ghImage.Value.Id);
        }

        [Fact(DisplayName = "CastFrom_GH_String_CreatesFromBase64")]
        public void CastFrom_GH_String_CreatesFromBase64()
        {
            var base64 = CreateValidBase64Png();
            var ghString = new GH_String(base64);
            var ghImage = new GH_ExtractedImage();
            Assert.True(ghImage.CastFrom(ghString));
            Assert.NotNull(ghImage.Value);
        }

        [Fact(DisplayName = "CastFrom_PlainString_CreatesFromBase64")]
        public void CastFrom_PlainString_CreatesFromBase64()
        {
            var base64 = CreateValidBase64Png();
            var ghImage = new GH_ExtractedImage();
            Assert.True(ghImage.CastFrom(base64));
            Assert.NotNull(ghImage.Value);
        }

        [Fact(DisplayName = "CastFrom_Null_ReturnsFalse")]
        public void CastFrom_Null_ReturnsFalse()
        {
            var ghImage = new GH_ExtractedImage();
            Assert.False(ghImage.CastFrom(null));
        }

        [Fact(DisplayName = "CastTo_GH_String_ReturnsBase64")]
        public void CastTo_GH_String_ReturnsBase64()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            GH_String result = null;
            Assert.True(ghImage.CastTo(ref result));
            Assert.NotNull(result);
            Assert.Equal(base64, result.Value);
        }

        [Fact(DisplayName = "CastTo_String_ReturnsBase64")]
        public void CastTo_String_ReturnsBase64()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            string result = null;
            Assert.True(ghImage.CastTo(ref result));
            Assert.NotNull(result);
            Assert.Equal(base64, result);
        }

        [Fact(DisplayName = "ScriptVariable_ValidBase64_ReturnsBitmap")]
        public void ScriptVariable_ValidBase64_ReturnsBitmap()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            var scriptVar = ghImage.ScriptVariable();
            Assert.NotNull(scriptVar);
            Assert.IsType<Bitmap>(scriptVar);
        }

        [Fact(DisplayName = "ScriptVariable_NullData_ReturnsNull")]
        public void ScriptVariable_NullData_ReturnsNull()
        {
            var image = new ExtractedImage("img-1", null, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            var scriptVar = ghImage.ScriptVariable();
            Assert.Null(scriptVar);
        }

        [Fact(DisplayName = "Duplicate_ReturnsNewInstance")]
        public void Duplicate_ReturnsNewInstance()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            var duplicate = ghImage.Duplicate();
            Assert.NotSame(ghImage, duplicate);
            Assert.IsType<GH_ExtractedImage>(duplicate);
        }

        [Fact(DisplayName = "TypeName_ReturnsExtractedImage")]
        public void TypeName_ReturnsExtractedImage()
        {
            var ghImage = new GH_ExtractedImage();
            Assert.Equal("Extracted Image", ghImage.TypeName);
        }

        [Fact(DisplayName = "IsValidWhyNot_WithValidData_ReturnsEmpty")]
        public void IsValidWhyNot_WithValidData_ReturnsEmpty()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage = new GH_ExtractedImage(image);
            Assert.Empty(ghImage.IsValidWhyNot);
        }

        [Fact(DisplayName = "IsValidWhyNot_WithNullData_ReturnsMessage")]
        public void IsValidWhyNot_WithNullData_ReturnsMessage()
        {
            var ghImage = new GH_ExtractedImage();
            Assert.NotEmpty(ghImage.IsValidWhyNot);
        }

        [Fact(DisplayName = "Write_Read_RoundTrip")]
        public void Write_Read_RoundTrip()
        {
            var base64 = CreateValidBase64Png();
            var image = new ExtractedImage("img-1", base64, "image/png", "Page 1", 1);
            var ghImage1 = new GH_ExtractedImage(image);
            
            var writer = new GH_IO.Serialization.GH_LooseChunk("ExtractedImage");
            ghImage1.Write(writer);
            var bytes = writer.Serialize_Binary();
            
            var reader = new GH_IO.Serialization.GH_LooseChunk("ExtractedImage");
            reader.Deserialize_Binary(bytes);
            var ghImage2 = new GH_ExtractedImage();
            ghImage2.Read(reader);
            
            Assert.Equal(ghImage1.Value.Id, ghImage2.Value.Id);
            Assert.Equal(ghImage1.Value.Base64Data, ghImage2.Value.Base64Data);
        }
    }
}
