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

namespace SmartHopper.Infrastructure.Tests.Utilities
{
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.Utilities;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="JsonFormatHelper"/> utility.
    /// </summary>
    public class JsonFormatHelperTests
    {
        #region ExtractFromMarkdownCodeBlock

#if NET7_WINDOWS
        [Theory(DisplayName = "JsonFormatHelper ExtractFromMarkdownCodeBlock extracts JSON [Windows]")]
#else
        [Theory(DisplayName = "JsonFormatHelper ExtractFromMarkdownCodeBlock extracts JSON [Core]")]
#endif
        [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
        [InlineData("```txt\nhello\n```", "hello")]
        [InlineData("```\nraw\n```", "raw")]
        [InlineData("{\"a\":1}", "{\"a\":1}")]
        public void ExtractFromMarkdownCodeBlock_VariousInputs_ReturnsExpected(string input, string expected)
        {
            var result = JsonFormatHelper.ExtractFromMarkdownCodeBlock(input);
            Assert.Equal(expected, result);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper ExtractFromMarkdownCodeBlock null returns null [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper ExtractFromMarkdownCodeBlock null returns null [Core]")]
#endif
        public void ExtractFromMarkdownCodeBlock_Null_ReturnsNull()
        {
            Assert.Null(JsonFormatHelper.ExtractFromMarkdownCodeBlock(null));
        }

        #endregion

        #region SanitizeJsonString

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString escapes newlines in strings [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString escapes newlines in strings [Core]")]
#endif
        public void SanitizeJsonString_NewlinesInStrings_AreEscaped()
        {
            var input = "{\"text\":\"line1\nline2\"}";
            var result = JsonFormatHelper.SanitizeJsonString(input);
            Assert.Contains("\\n", result);
            Assert.DoesNotContain("\n", result);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString replaces smart quotes [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString replaces smart quotes [Core]")]
#endif
        public void SanitizeJsonString_SmartQuotes_AreReplaced()
        {
            var input = "\u201Ckey\u201D: \u201Cvalue\u201D";
            var result = JsonFormatHelper.SanitizeJsonString(input);
            Assert.DoesNotContain("\u201C", result);
            Assert.DoesNotContain("\u201D", result);
            Assert.Contains("\"key\"", result);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString auto-closes unclosed containers [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString auto-closes unclosed containers [Core]")]
#endif
        public void SanitizeJsonString_UnclosedObject_IsClosed()
        {
            var input = "{\"a\":1";
            var result = JsonFormatHelper.SanitizeJsonString(input);
            Assert.EndsWith("}", result);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString removes trailing commas [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper SanitizeJsonString removes trailing commas [Core]")]
#endif
        public void SanitizeJsonString_TrailingComma_IsRemoved()
        {
            var input = "[1,2,3,]";
            var result = JsonFormatHelper.SanitizeJsonString(input);
            // After sanitization, the trailing comma before ] should be removed
            Assert.Contains("3]", result);
        }

        #endregion

        #region IsValidJson

#if NET7_WINDOWS
        [Theory(DisplayName = "JsonFormatHelper IsValidJson recognizes valid/invalid JSON [Windows]")]
#else
        [Theory(DisplayName = "JsonFormatHelper IsValidJson recognizes valid/invalid JSON [Core]")]
#endif
        [InlineData("{\"a\":1}", true)]
        [InlineData("[1,2,3]", true)]
        [InlineData("not json", false)]
        [InlineData("", false)]
        public void IsValidJson_VariousInputs_ReturnsExpected(string input, bool expected)
        {
            Assert.Equal(expected, JsonFormatHelper.IsValidJson(input));
        }

        #endregion

        #region StringToJson / JsonToString

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper StringToJson parses object and JsonToString round-trips [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper StringToJson parses object and JsonToString round-trips [Core]")]
#endif
        public void StringToJson_JsonToString_RoundTrip()
        {
            var original = "{\"name\":\"test\",\"value\":42}";
            var token = JsonFormatHelper.StringToJson(original);
            Assert.NotNull(token);
            Assert.True(token is JObject);

            var back = JsonFormatHelper.JsonToString(token);
            Assert.Contains("\"name\":\"test\"", back);
            Assert.Contains("\"value\":42", back);
        }

        #endregion

        #region ParseJsonLines

#if NET7_WINDOWS
        [Fact(DisplayName = "JsonFormatHelper ParseJsonLines extracts multiple objects [Windows]")]
#else
        [Fact(DisplayName = "JsonFormatHelper ParseJsonLines extracts multiple objects [Core]")]
#endif
        public void ParseJsonLines_MultiLineInput_ExtractsObjects()
        {
            var input = "{\"id\":1}\n{\"id\":2}\n{\"id\":3}";
            var objects = JsonFormatHelper.ParseJsonLines(input).ToList();
            Assert.Equal(3, objects.Count);
            Assert.Equal(1, objects[0]["id"].Value<int>());
            Assert.Equal(2, objects[1]["id"].Value<int>());
            Assert.Equal(3, objects[2]["id"].Value<int>());
        }

        #endregion
    }
}
