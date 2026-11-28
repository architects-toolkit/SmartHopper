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

using Newtonsoft.Json.Linq;
using SmartHopper.Providers.Anthropic;
using Xunit;

namespace SmartHopper.ProviderSdk.Tests.AICall.JsonSchemas
{
    /// <summary>
    /// Tests for <see cref="AnthropicJsonSchemaAdapter"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class AnthropicJsonSchemaAdapterTests
    {
        private readonly AnthropicJsonSchemaAdapter _adapter = new AnthropicJsonSchemaAdapter();

        /// <summary>
        /// Keywords rejected by Anthropic structured outputs (e.g. <c>maxItems</c>)
        /// must be stripped so requests stop returning HTTP 400.
        /// </summary>
        /// <param name="keyword">The unsupported schema keyword.</param>
        [Theory(DisplayName = "NormalizeSchema: strips Anthropic-unsupported keywords")]
        [InlineData("minimum")]
        [InlineData("maximum")]
        [InlineData("exclusiveMinimum")]
        [InlineData("exclusiveMaximum")]
        [InlineData("multipleOf")]
        [InlineData("minLength")]
        [InlineData("maxLength")]
        [InlineData("pattern")]
        [InlineData("maxItems")]
        [InlineData("uniqueItems")]
        [InlineData("minProperties")]
        [InlineData("maxProperties")]
        [InlineData("not")]
        public void NormalizeSchema_StripsUnsupportedKeywords(string keyword)
        {
            var schema = JObject.Parse($"{{\"type\":\"object\",\"properties\":{{\"items\":{{\"type\":\"array\",\"{keyword}\":3}}}}}}");

            AnthropicJsonSchemaAdapter.NormalizeSchema(schema);

            Assert.Null(schema["properties"]?["items"]?[keyword]);
        }

        /// <summary>
        /// Stripped constraints are preserved as a text hint in <c>description</c>
        /// so the model still sees the intended constraint.
        /// </summary>
        [Fact(DisplayName = "NormalizeSchema: stripped constraints move into description")]
        public void NormalizeSchema_StrippedConstraintsMoveToDescription()
        {
            var schema = JObject.Parse("{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"maxItems\":3}");

            AnthropicJsonSchemaAdapter.NormalizeSchema(schema);

            var description = schema["description"]?.ToString();
            Assert.NotNull(description);
            Assert.Contains("maxItems", description);
            Assert.Contains("3", description);
        }

        /// <summary>
        /// An existing description is preserved and the hint is appended.
        /// </summary>
        [Fact(DisplayName = "NormalizeSchema: appends hint to existing description")]
        public void NormalizeSchema_AppendsHintToExistingDescription()
        {
            var schema = JObject.Parse("{\"type\":\"integer\",\"description\":\"A count.\",\"minimum\":0}");

            AnthropicJsonSchemaAdapter.NormalizeSchema(schema);

            var description = schema["description"]?.ToString();
            Assert.Contains("A count.", description);
            Assert.Contains("minimum", description);
        }

        /// <summary>
        /// <c>minItems</c> is allowed by Anthropic only when it is 0 or 1.
        /// </summary>
        /// <param name="value">The minItems value.</param>
        /// <param name="expectedKept">Whether the keyword must be preserved.</param>
        [Theory(DisplayName = "NormalizeSchema: minItems kept only for 0 or 1")]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(4, false)]
        public void NormalizeSchema_MinItems_KeptOnlyForZeroOrOne(int value, bool expectedKept)
        {
            var schema = JObject.Parse($"{{\"type\":\"array\",\"items\":{{\"type\":\"string\"}},\"minItems\":{value}}}");

            AnthropicJsonSchemaAdapter.NormalizeSchema(schema);

            Assert.Equal(expectedKept, schema["minItems"] != null);
        }

        /// <summary>
        /// Unsupported <c>format</c> values are stripped; Anthropic-supported string
        /// formats are kept.
        /// </summary>
        /// <param name="format">The format value.</param>
        /// <param name="expectedKept">Whether the format must be preserved.</param>
        [Theory(DisplayName = "NormalizeSchema: format kept only for supported values")]
        [InlineData("date-time", true)]
        [InlineData("email", true)]
        [InlineData("uri", true)]
        [InlineData("uuid", true)]
        [InlineData("regex", false)]
        [InlineData("uri-reference", false)]
        public void NormalizeSchema_Format_KeptOnlyForSupportedValues(string format, bool expectedKept)
        {
            var schema = JObject.Parse($"{{\"type\":\"string\",\"format\":\"{format}\"}}");

            AnthropicJsonSchemaAdapter.NormalizeSchema(schema);

            Assert.Equal(expectedKept, schema["format"] != null);
        }

        /// <summary>
        /// Every object-type node gets <c>additionalProperties: false</c>, including
        /// nested schemas under <c>properties</c> and <c>items</c>.
        /// </summary>
        [Fact(DisplayName = "NormalizeSchema: injects additionalProperties false recursively")]
        public void NormalizeSchema_InjectsAdditionalPropertiesFalseRecursively()
        {
            var schema = JObject.Parse(
                "{\"type\":\"object\",\"properties\":{\"nested\":{\"type\":\"object\",\"properties\":{\"deep\":{\"type\":\"object\"}}},\"list\":{\"type\":\"array\",\"items\":{\"type\":\"object\"}}}}");

            AnthropicJsonSchemaAdapter.NormalizeSchema(schema);

            Assert.False(schema["additionalProperties"]!.Value<bool>());
            Assert.False(schema["properties"]!["nested"]!["additionalProperties"]!.Value<bool>());
            Assert.False(schema["properties"]!["nested"]!["properties"]!["deep"]!["additionalProperties"]!.Value<bool>());
            Assert.False(schema["properties"]!["list"]!["items"]!["additionalProperties"]!.Value<bool>());
        }

        /// <summary>
        /// The exact suggestions schema that produced the live HTTP 400 must be
        /// fully sanitized after <see cref="AnthropicJsonSchemaAdapter.Wrap"/>.
        /// </summary>
        [Fact(DisplayName = "Wrap: suggestions schema with maxItems no longer carries it")]
        public void Wrap_SuggestionsSchema_StripsMaxItems()
        {
            var schema = JObject.Parse(
                "{\"type\":\"object\",\"properties\":{\"suggestions\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"maxItems\":3}},\"required\":[\"suggestions\"],\"additionalProperties\":false}");

            var (wrapped, info) = this._adapter.Wrap(schema);

            Assert.False(info.IsWrapped);
            Assert.Null(wrapped["properties"]?["suggestions"]?["maxItems"]);
            Assert.Contains("maxItems", wrapped["properties"]?["suggestions"]?["description"]?.ToString());
        }

        /// <summary>
        /// The adapter reports the Anthropic provider name for registry lookup.
        /// </summary>
        [Fact(DisplayName = "ProviderName is Anthropic")]
        public void ProviderName_IsAnthropic()
        {
            Assert.Equal("Anthropic", this._adapter.ProviderName);
        }
    }
}
