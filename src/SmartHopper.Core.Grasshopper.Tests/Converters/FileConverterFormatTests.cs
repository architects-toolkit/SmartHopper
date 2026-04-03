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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SmartHopper.Core.Grasshopper.Converters.Formats;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.Converters
{
    public class TxtConverterTests : IDisposable
    {
        private readonly string _tempDir;

        public TxtConverterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact(DisplayName = "ConvertAsync_SimpleTextFile_ReturnsContent")]
        public async Task ConvertAsync_SimpleTextFile_ReturnsContent()
        {
            var converter = new TxtConverter();
            var filePath = Path.Combine(_tempDir, "test.txt");
            File.WriteAllText(filePath, "Hello, World!");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.Contains("Hello, World!", result.MarkdownContent);
            Assert.Equal("txt", result.DetectedFormat);
        }

        [Fact(DisplayName = "ConvertAsync_EmptyFile_ReturnsSuccessEmptyish")]
        public async Task ConvertAsync_EmptyFile_ReturnsSuccessEmptyish()
        {
            var converter = new TxtConverter();
            var filePath = Path.Combine(_tempDir, "empty.txt");
            File.WriteAllText(filePath, "");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.Empty(result.MarkdownContent);
        }

        [Fact(DisplayName = "ConvertAsync_NormalizesLineEndings_CRLFtoLF")]
        public async Task ConvertAsync_NormalizesLineEndings_CRLFtoLF()
        {
            var converter = new TxtConverter();
            var filePath = Path.Combine(_tempDir, "crlf.txt");
            File.WriteAllText(filePath, "Line1\r\nLine2\r\nLine3", Encoding.UTF8);
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.DoesNotContain("\r\n", result.MarkdownContent);
            Assert.Contains("\n", result.MarkdownContent);
        }

        [Fact(DisplayName = "ConvertAsync_MissingFile_ReturnsFailure")]
        public async Task ConvertAsync_MissingFile_ReturnsFailure()
        {
            var converter = new TxtConverter();
            var result = await converter.ConvertAsync("/nonexistent/file.txt", null);
            Assert.False(result.IsSuccess);
        }
    }

    public class JsonConverterTests : IDisposable
    {
        private readonly string _tempDir;

        public JsonConverterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact(DisplayName = "ConvertAsync_ValidJson_ReturnsFencedBlock")]
        public async Task ConvertAsync_ValidJson_ReturnsFencedBlock()
        {
            var converter = new JsonConverter();
            var filePath = Path.Combine(_tempDir, "test.json");
            File.WriteAllText(filePath, "{\"key\": \"value\"}");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.Contains("```json", result.MarkdownContent);
            Assert.Contains("```", result.MarkdownContent);
            Assert.Equal("json", result.DetectedFormat);
        }

        [Fact(DisplayName = "ConvertAsync_InvalidJson_ReturnsFencedBlockRaw")]
        public async Task ConvertAsync_InvalidJson_ReturnsFencedBlockRaw()
        {
            var converter = new JsonConverter();
            var filePath = Path.Combine(_tempDir, "invalid.json");
            File.WriteAllText(filePath, "{invalid json}");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.Contains("```json", result.MarkdownContent);
        }

        [Fact(DisplayName = "ConvertAsync_MissingFile_ReturnsFailure")]
        public async Task ConvertAsync_MissingFile_ReturnsFailure()
        {
            var converter = new JsonConverter();
            var result = await converter.ConvertAsync("/nonexistent/file.json", null);
            Assert.False(result.IsSuccess);
        }
    }

    public class XmlConverterTests : IDisposable
    {
        private readonly string _tempDir;

        public XmlConverterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact(DisplayName = "ConvertAsync_SimpleXml_ReturnsMarkdown")]
        public async Task ConvertAsync_SimpleXml_ReturnsMarkdown()
        {
            var converter = new XmlConverter();
            var filePath = Path.Combine(_tempDir, "test.xml");
            File.WriteAllText(filePath, "<?xml version=\"1.0\"?><root><item>test</item></root>");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.Equal("xml", result.DetectedFormat);
        }

        [Fact(DisplayName = "ConvertAsync_MalformedXml_ReturnsFailureOrFallback")]
        public async Task ConvertAsync_MalformedXml_ReturnsFailureOrFallback()
        {
            var converter = new XmlConverter();
            var filePath = Path.Combine(_tempDir, "malformed.xml");
            File.WriteAllText(filePath, "<root><unclosed>");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.NotNull(result);
        }

        [Fact(DisplayName = "ConvertAsync_MissingFile_ReturnsFailure")]
        public async Task ConvertAsync_MissingFile_ReturnsFailure()
        {
            var converter = new XmlConverter();
            var result = await converter.ConvertAsync("/nonexistent/file.xml", null);
            Assert.False(result.IsSuccess);
        }
    }

    public class CsvConverterTests : IDisposable
    {
        private readonly string _tempDir;

        public CsvConverterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact(DisplayName = "ConvertAsync_SimpleCsv_ReturnsMarkdownTable")]
        public async Task ConvertAsync_SimpleCsv_ReturnsMarkdownTable()
        {
            var converter = new CsvConverter();
            var filePath = Path.Combine(_tempDir, "test.csv");
            File.WriteAllText(filePath, "Name,Age\nAlice,30\nBob,25");
            var result = await converter.ConvertAsync(filePath, null);
            Assert.True(result.IsSuccess);
            Assert.Contains("|", result.MarkdownContent);
            Assert.Equal("csv", result.DetectedFormat);
        }

        [Fact(DisplayName = "ConvertAsync_MissingFile_ReturnsFailure")]
        public async Task ConvertAsync_MissingFile_ReturnsFailure()
        {
            var converter = new CsvConverter();
            var result = await converter.ConvertAsync("/nonexistent/file.csv", null);
            Assert.False(result.IsSuccess);
        }
    }
}
