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
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;
using Xunit;

namespace SmartHopper.ProviderSdk.Tests.AICall.JsonSchemas
{
    /// <summary>
    /// Tests for <see cref="OpenAICompatibleJsonSchemaAdapter"/>.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenAICompatibleJsonSchemaAdapterTests
    {
        private const string ProviderName = "TestProvider";

        private readonly OpenAICompatibleJsonSchemaAdapter _adapter;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAICompatibleJsonSchemaAdapterTests"/> class.
        /// </summary>
        public OpenAICompatibleJsonSchemaAdapterTests()
        {
            this._adapter = new OpenAICompatibleJsonSchemaAdapter(ProviderName);
        }

        /// <summary>
        /// Object-root schemas must be returned without wrapping.
        /// </summary>
        [Fact(DisplayName = "Wrap: object root is returned unwrapped")]
        public void Wrap_ObjectRoot_ReturnsUnwrapped()
        {
            var schema = JObject.Parse("{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}");

            var (wrapped, info) = this._adapter.Wrap(schema);

            Assert.Same(schema, wrapped);
            Assert.False(info.IsWrapped);
            Assert.Equal(string.Empty, info.WrapperType);
            Assert.Equal(string.Empty, info.PropertyName);
            Assert.Equal(ProviderName, info.ProviderName);
        }

        /// <summary>
        /// Array-root schemas are wrapped under an <c>items</c> property.
        /// </summary>
        [Fact(DisplayName = "Wrap: array is wrapped under items")]
        public void Wrap_Array_WrapsUnderItems()
        {
            var schema = JObject.Parse("{\"type\":\"array\",\"items\":{\"type\":\"string\"}}");

            var (wrapped, info) = this._adapter.Wrap(schema);

            Assert.Equal("object", wrapped["type"]?.ToString());
            Assert.Equal("array", wrapped["properties"]?["items"]?["type"]?.ToString());
            Assert.Equal("items", wrapped["required"]?[0]?.ToString());
            Assert.True(info.IsWrapped);
            Assert.Equal("array", info.WrapperType);
            Assert.Equal("items", info.PropertyName);
            Assert.Equal(ProviderName, info.ProviderName);
        }

        /// <summary>
        /// String-root schemas are wrapped under a <c>value</c> property.
        /// </summary>
        [Fact(DisplayName = "Wrap: string primitive is wrapped under value")]
        public void Wrap_String_WrapsUnderValue()
        {
            var schema = JObject.Parse("{\"type\":\"string\"}");

            var (wrapped, info) = this._adapter.Wrap(schema);

            Assert.Equal("object", wrapped["type"]?.ToString());
            Assert.Equal("string", wrapped["properties"]?["value"]?["type"]?.ToString());
            Assert.Equal("value", wrapped["required"]?[0]?.ToString());
            Assert.True(info.IsWrapped);
            Assert.Equal("string", info.WrapperType);
            Assert.Equal("value", info.PropertyName);
            Assert.Equal(ProviderName, info.ProviderName);
        }

        /// <summary>
        /// Primitive-root schemas (number, integer, boolean) are wrapped under <c>value</c>
        /// with the original type preserved in <see cref="SchemaWrapperInfo.WrapperType"/>.
        /// </summary>
        /// <param name="type">The JSON schema primitive type.</param>
        [Theory(DisplayName = "Wrap: primitive types are wrapped under value")]
        [InlineData("number")]
        [InlineData("integer")]
        [InlineData("boolean")]
        public void Wrap_Primitive_WrapsUnderValue(string type)
        {
            var schema = new JObject { ["type"] = type };

            var (wrapped, info) = this._adapter.Wrap(schema);

            Assert.Equal("object", wrapped["type"]?.ToString());
            Assert.Equal(type, wrapped["properties"]?["value"]?["type"]?.ToString());
            Assert.Equal("value", wrapped["required"]?[0]?.ToString());
            Assert.True(info.IsWrapped);
            Assert.Equal(type, info.WrapperType);
            Assert.Equal("value", info.PropertyName);
        }

        /// <summary>
        /// Unknown-root schemas (no explicit <c>type</c>) are wrapped under <c>data</c>.
        /// </summary>
        [Fact(DisplayName = "Wrap: unknown root type is wrapped under data")]
        public void Wrap_Unknown_WrapsUnderData()
        {
            var schema = new JObject { ["description"] = "no explicit type" };

            var (wrapped, info) = this._adapter.Wrap(schema);

            Assert.Equal("object", wrapped["type"]?.ToString());
            Assert.NotNull(wrapped["properties"]?["data"]);
            Assert.Equal("data", wrapped["required"]?[0]?.ToString());
            Assert.True(info.IsWrapped);
            Assert.Equal("unknown", info.WrapperType);
            Assert.Equal("data", info.PropertyName);
        }

        /// <summary>
        /// Passing <c>null</c> to <see cref="OpenAICompatibleJsonSchemaAdapter.Wrap"/> throws.
        /// </summary>
        [Fact(DisplayName = "Wrap: null schema throws ArgumentNullException")]
        public void Wrap_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => this._adapter.Wrap(null));
        }

        /// <summary>
        /// <see cref="OpenAICompatibleJsonSchemaAdapter.Unwrap"/> returns the content unchanged by default.
        /// </summary>
        [Fact(DisplayName = "Unwrap: returns content unchanged by default")]
        public void Unwrap_Default_ReturnsContent()
        {
            const string content = "{\"value\":\"hello\"}";

            var result = this._adapter.Unwrap(
                content,
                new SchemaWrapperInfo { IsWrapped = true, PropertyName = "value" });

            Assert.Equal(content, result);
        }

        /// <summary>
        /// Subclasses can override <see cref="OpenAICompatibleJsonSchemaAdapter.Unwrap"/>.
        /// </summary>
        [Fact(DisplayName = "Unwrap: can be overridden by subclasses")]
        public void Unwrap_Subclass_Override()
        {
            var overrideAdapter = new OverrideAdapter();
            const string content = "original";

            var result = overrideAdapter.Unwrap(content, new SchemaWrapperInfo());

            Assert.Equal("overridden", result);
        }

        /// <summary>
        /// The adapter exposes the provider name passed to its constructor.
        /// </summary>
        [Fact(DisplayName = "ProviderName is set from constructor")]
        public void ProviderName_IsSetFromConstructor()
        {
            Assert.Equal(ProviderName, this._adapter.ProviderName);
        }

        /// <summary>
        /// The registry default is an <see cref="OpenAICompatibleJsonSchemaAdapter"/>
        /// with the reserved <c>__default__</c> provider name.
        /// </summary>
        [Fact(DisplayName = "Default adapter is an OpenAICompatibleJsonSchemaAdapter")]
        public void DefaultAdapter_UsesSharedBase()
        {
            Assert.IsType<OpenAICompatibleJsonSchemaAdapter>(JsonSchemaAdapterRegistry.Default);
            Assert.Equal("__default__", JsonSchemaAdapterRegistry.Default.ProviderName);
        }

        /// <summary>
        /// Test subclass that overrides <see cref="OpenAICompatibleJsonSchemaAdapter.Unwrap"/>.
        /// </summary>
        private sealed class OverrideAdapter : OpenAICompatibleJsonSchemaAdapter
        {
            public OverrideAdapter()
                : base("Override")
            {
            }

            /// <inheritdoc/>
            public override string Unwrap(string content, SchemaWrapperInfo info) => "overridden";
        }
    }
}
