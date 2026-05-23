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

namespace SmartHopper.Core.Grasshopper.Tests.Converters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using SmartHopper.Core.Grasshopper.Converters;
    using Xunit;

    public class MockFileConverter : IFileConverter
    {
        public IEnumerable<string> SupportedExtensions => new[] { ".mock" };

        public Task<FileConversionResult> ConvertAsync(string filePath, FileConversionOptions options)
        {
            return Task.FromResult(FileConversionResult.Success("Mock content", "mock"));
        }
    }

    public class FileConverterRegistryTests
    {
        [Fact(DisplayName = "Register_NullConverter_ThrowsArgumentNull")]
        public void Register_NullConverter_ThrowsArgumentNull()
        {
            var registry = new FileConverterRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
        }

        [Fact(DisplayName = "Register_ValidConverter_AddedToRegistry")]
        public void Register_ValidConverter_AddedToRegistry()
        {
            var registry = new FileConverterRegistry();
            var converter = new MockFileConverter();
            registry.Register(converter);
            Assert.True(registry.IsSupported(".mock"));
        }

        [Fact(DisplayName = "IsSupported_RegisteredExtension_ReturnsTrue")]
        public void IsSupported_RegisteredExtension_ReturnsTrue()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            Assert.True(registry.IsSupported(".mock"));
        }

        [Fact(DisplayName = "IsSupported_UnregisteredExtension_ReturnsFalse")]
        public void IsSupported_UnregisteredExtension_ReturnsFalse()
        {
            var registry = new FileConverterRegistry();
            Assert.False(registry.IsSupported(".unknown"));
        }

        [Fact(DisplayName = "IsSupported_NullOrWhitespace_ReturnsFalse")]
        public void IsSupported_NullOrWhitespace_ReturnsFalse()
        {
            var registry = new FileConverterRegistry();
            Assert.False(registry.IsSupported(null));
            Assert.False(registry.IsSupported(string.Empty));
            Assert.False(registry.IsSupported("   "));
        }

        [Fact(DisplayName = "IsSupported_ExtensionCaseInsensitive_ReturnsTrue")]
        public void IsSupported_ExtensionCaseInsensitive_ReturnsTrue()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            Assert.True(registry.IsSupported(".MOCK"));
            Assert.True(registry.IsSupported(".Mock"));
        }

        [Fact(DisplayName = "IsSupported_ExtensionWithOrWithoutDot_ReturnsTrue")]
        public void IsSupported_ExtensionWithOrWithoutDot_ReturnsTrue()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            Assert.True(registry.IsSupported(".mock"));
            Assert.True(registry.IsSupported("mock"));
        }

        [Fact(DisplayName = "ConvertAsync_NullPath_ThrowsArgumentException")]
        public async Task ConvertAsync_NullPath_ThrowsArgumentException()
        {
            var registry = new FileConverterRegistry();
            await Assert.ThrowsAsync<ArgumentException>(() => registry.ConvertAsync(null));
        }

        [Fact(DisplayName = "ConvertAsync_FileNotFound_ReturnsFailureResult")]
        public async Task ConvertAsync_FileNotFound_ReturnsFailureResult()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            var result = await registry.ConvertAsync("/nonexistent/file.mock");
            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact(DisplayName = "ConvertAsync_NoExtension_ReturnsFailureResult")]
        public async Task ConvertAsync_NoExtension_ReturnsFailureResult()
        {
            var registry = new FileConverterRegistry();
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test");
                File.Move(tempFile, Path.Combine(Path.GetDirectoryName(tempFile), "noextension"), true);
                var noExtPath = Path.Combine(Path.GetDirectoryName(tempFile), "noextension");
                var result = await registry.ConvertAsync(noExtPath);
                Assert.False(result.IsSuccess);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact(DisplayName = "ConvertAsync_UnsupportedFormat_ReturnsFailureResultWithList")]
        public async Task ConvertAsync_UnsupportedFormat_ReturnsFailureResultWithList()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test");
                var result = await registry.ConvertAsync(tempFile + ".unknown");
                Assert.False(result.IsSuccess);
                Assert.NotEmpty(result.Warnings);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact(DisplayName = "SupportedExtensions_ReturnsAllRegistered")]
        public void SupportedExtensions_ReturnsAllRegistered()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            var extensions = registry.SupportedExtensions;
            Assert.Contains(".mock", extensions);
        }

        [Fact(DisplayName = "RegisterAll_NullList_ThrowsArgumentNull")]
        public void RegisterAll_NullList_ThrowsArgumentNull()
        {
            var registry = new FileConverterRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.RegisterAll(null));
        }

        [Fact(DisplayName = "RegisterAll_MultipleConverters_AllRegistered")]
        public void RegisterAll_MultipleConverters_AllRegistered()
        {
            var registry = new FileConverterRegistry();
            var converters = new List<IFileConverter>
            {
                new MockFileConverter(),
                new MockFileConverter()
            };
            registry.RegisterAll(converters);
            Assert.True(registry.IsSupported(".mock"));
        }

        [Fact(DisplayName = "MaxContentLength_TruncatesContent_WhenExceeded")]
        public async Task MaxContentLength_TruncatesContent_WhenExceeded()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new MockFileConverter());
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test");
                File.Move(tempFile, tempFile + ".mock", true);
                var options = new FileConversionOptions { MaxContentLength = 2 };
                var result = await registry.ConvertAsync(tempFile + ".mock", options);
                Assert.True(result.IsSuccess);
                Assert.True(result.MarkdownContent.Length <= 2);
            }
            finally
            {
                if (File.Exists(tempFile + ".mock")) File.Delete(tempFile + ".mock");
            }
        }

        [Fact(DisplayName = "FileConversionResult_Success_IsSuccessTrue")]
        public void FileConversionResult_Success_IsSuccessTrue()
        {
            var result = FileConversionResult.Success("content", "txt");
            Assert.True(result.IsSuccess);
            Assert.Equal("content", result.MarkdownContent);
            Assert.Equal("txt", result.DetectedFormat);
        }

        [Fact(DisplayName = "FileConversionResult_Failure_IsSuccessFalse")]
        public void FileConversionResult_Failure_IsSuccessFalse()
        {
            var result = FileConversionResult.Failure("txt", "Error message");
            Assert.False(result.IsSuccess);
            Assert.NotEmpty(result.Warnings);
        }

        [Fact(DisplayName = "FileConversionOptions_Clone_ReturnsIdenticalCopy")]
        public void FileConversionOptions_Clone_ReturnsIdenticalCopy()
        {
            var original = new FileConversionOptions
            {
                PreserveTableStructure = false,
                RemoveHeadersFooters = true,
                DetectHeadings = false,
                ExtractImages = true,
                MaxContentLength = 1000
            };
            var cloned = original.Clone();
            Assert.Equal(original.PreserveTableStructure, cloned.PreserveTableStructure);
            Assert.Equal(original.RemoveHeadersFooters, cloned.RemoveHeadersFooters);
            Assert.Equal(original.DetectHeadings, cloned.DetectHeadings);
            Assert.Equal(original.ExtractImages, cloned.ExtractImages);
            Assert.Equal(original.MaxContentLength, cloned.MaxContentLength);
        }

        [Fact(DisplayName = "FileConversionOptions_Defaults_AreCorrect")]
        public void FileConversionOptions_Defaults_AreCorrect()
        {
            var options = new FileConversionOptions();
            Assert.True(options.PreserveTableStructure);
            Assert.True(options.RemoveHeadersFooters);
            Assert.True(options.DetectHeadings);
            Assert.False(options.ExtractImages);
            Assert.Equal(0, options.MaxContentLength);
        }
    }
}
