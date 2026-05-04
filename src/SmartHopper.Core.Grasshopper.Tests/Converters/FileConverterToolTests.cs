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
    using System.IO;
    using System.Threading.Tasks;
    using SmartHopper.Core.Grasshopper.Converters;
    using SmartHopper.Core.Grasshopper.Converters.Formats;
    using Xunit;

    /// <summary>
    /// Tests for file2md AITool parameters and outputs.
    /// TC-F2MD-30, TC-F2MD-31, TC-F2MD-32
    /// </summary>
    public class FileConverterToolTests : IDisposable
    {
        private readonly string _tempDir;

        public FileConverterToolTests()
        {
            this._tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(this._tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(this._tempDir))
            {
                Directory.Delete(this._tempDir, true);
            }
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "FileConversionOptions_ExtractImages_DefaultFalse [Windows]")]
#else
        [Fact(DisplayName = "FileConversionOptions_ExtractImages_DefaultFalse [Core]")]
#endif
        public void FileConversionOptions_ExtractImages_DefaultFalse()
        {
            var options = new FileConversionOptions();
            Assert.False(options.ExtractImages);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "FileConversionOptions_ExtractImages_SetTrue [Windows]")]
#else
        [Fact(DisplayName = "FileConversionOptions_ExtractImages_SetTrue [Core]")]
#endif
        public void FileConversionOptions_ExtractImages_SetTrue()
        {
            var options = new FileConversionOptions { ExtractImages = true };
            Assert.True(options.ExtractImages);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Registry_IsSupported_Docx [Windows]")]
#else
        [Fact(DisplayName = "Registry_IsSupported_Docx [Core]")]
#endif
        public void Registry_IsSupported_Docx()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new DocxConverter());

            Assert.True(registry.IsSupported(".docx"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Registry_IsSupported_Xlsx [Windows]")]
#else
        [Fact(DisplayName = "Registry_IsSupported_Xlsx [Core]")]
#endif
        public void Registry_IsSupported_Xlsx()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new XlsxConverter());

            Assert.True(registry.IsSupported(".xlsx"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Registry_IsSupported_Pptx [Windows]")]
#else
        [Fact(DisplayName = "Registry_IsSupported_Pptx [Core]")]
#endif
        public void Registry_IsSupported_Pptx()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new PptxConverter());

            Assert.True(registry.IsSupported(".pptx"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Registry_IsSupported_Epub [Windows]")]
#else
        [Fact(DisplayName = "Registry_IsSupported_Epub [Core]")]
#endif
        public void Registry_IsSupported_Epub()
        {
            var registry = new FileConverterRegistry();
            registry.Register(new EpubConverter());

            Assert.True(registry.IsSupported(".epub"));
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "Registry_SupportedExtensions_ReturnsAll [Windows]")]
#else
        [Fact(DisplayName = "Registry_SupportedExtensions_ReturnsAll [Core]")]
#endif
        public void Registry_SupportedExtensions_ReturnsAll()
        {
            var registry = new FileConverterRegistry();
            registry.RegisterAll(new IFileConverter[]
            {
                new PdfConverter(),
                new DocxConverter(),
                new XlsxConverter(),
                new PptxConverter(),
                new HtmlConverter(),
                new CsvConverter(),
                new JsonConverter(),
                new XmlConverter(),
                new TxtConverter(),
                new EmlConverter(),
                new EpubConverter(),
                new RtfConverter(),
            });

            var extensions = registry.SupportedExtensions;

            Assert.Contains(".pdf", extensions);
            Assert.Contains(".docx", extensions);
            Assert.Contains(".xlsx", extensions);
            Assert.Contains(".pptx", extensions);
            Assert.Contains(".html", extensions);
            Assert.Contains(".csv", extensions);
            Assert.Contains(".json", extensions);
            Assert.Contains(".xml", extensions);
            Assert.Contains(".txt", extensions);
            Assert.Contains(".eml", extensions);
            Assert.Contains(".epub", extensions);
            Assert.Contains(".rtf", extensions);
        }
    }
}
