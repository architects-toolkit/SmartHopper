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

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace SmartHopper.DocumentProcessing.Tests
{
    /// <summary>Strict parser tests for the shared PDF page/region selection contract.</summary>
    public sealed class DocumentSelectionJsonParserTests
    {
        [Fact]
        public void OmittedPages_ReturnsNullWithoutError()
        {
            Assert.True(DocumentSelectionJsonParser.TryParsePages(null, out var pages, out var error));
            Assert.Null(pages);
            Assert.Null(error);
        }

        [Fact]
        public void ValidPages_AreSortedUnique()
        {
            Assert.True(DocumentSelectionJsonParser.TryParsePages(JArray.Parse("[3, 1]"), out var pages, out var error));
            Assert.Equal(new List<int> { 1, 3 }, pages);
            Assert.Null(error);
        }

        [Theory]
        [InlineData("[]", "empty")]
        [InlineData("[1, 1]", "duplicates")]
        [InlineData("[1.5]", "fractional")]
        [InlineData("[0]", "non-positive")]
        [InlineData("[-2]", "negative")]
        [InlineData("[\"a\"]", "string entry")]
        [InlineData("[99999999999]", "int32 overflow")]
        [InlineData("3", "not an array")]
        public void InvalidPages_AreErrors(string json, string _case)
        {
            Assert.False(DocumentSelectionJsonParser.TryParsePages(JToken.Parse(json), out var pages, out var error));
            Assert.Null(pages);
            Assert.NotNull(error);
            Assert.Contains("pages", error);
        }

        [Fact]
        public void OmittedRegion_ReturnsNullWithoutError()
        {
            Assert.True(DocumentSelectionJsonParser.TryParseRegion(null, out var region, out var error));
            Assert.Null(region);
            Assert.Null(error);
        }

        [Fact]
        public void ValidRegion_Parses()
        {
            Assert.True(DocumentSelectionJsonParser.TryParseRegion(
                JObject.Parse(@"{""page"":3,""x"":0.25,""y"":0.25,""width"":0.5,""height"":0.5}"),
                out var region, out var error));
            Assert.Equal(3, region.Page);
            Assert.Equal(0.25, region.X);
            Assert.Equal(0.5, region.Width);
        }

        [Theory]
        [InlineData(@"{""page"":3,""x"":0.25,""y"":0.25,""width"":0.5}", "missing height")]
        [InlineData(@"{""page"":0,""x"":0,""y"":0,""width"":0.5,""height"":0.5}", "page < 1")]
        [InlineData(@"{""page"":1,""x"":0,""y"":0,""width"":0,""height"":0.5}", "zero width")]
        [InlineData(@"{""page"":1,""x"":-0.1,""y"":0,""width"":0.5,""height"":0.5}", "negative x")]
        [InlineData(@"{""page"":1,""x"":0.8,""y"":0,""width"":0.5,""height"":0.5}", "overflow")]
        [InlineData(@"{""page"":1,""x"":""a"",""y"":0,""width"":0.5,""height"":0.5}", "string x")]
        [InlineData(@"{""page"":99999999999,""x"":0,""y"":0,""width"":0.5,""height"":0.5}", "page int32 overflow")]
        public void InvalidRegions_AreErrors(string json, string _case)
        {
            Assert.False(DocumentSelectionJsonParser.TryParseRegion(JToken.Parse(json), out var region, out var error));
            Assert.Null(region);
            Assert.NotNull(error);
            Assert.Contains("region", error);
        }
    }
}
