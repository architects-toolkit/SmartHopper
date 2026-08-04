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

namespace SmartHopper.Infrastructure.Tests.AICall
{
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.AICall.Core;
    using Xunit;

    public class AIRequestParametersTests
    {
        [Fact(DisplayName = "Empty_IsImmutableRecord_WithNullDefaults")]
        public void Empty_IsImmutableRecord_WithNullDefaults()
        {
            var empty = AIRequestParameters.Empty;
            Assert.NotNull(empty);
            Assert.Null(empty.Model);
            Assert.Null(empty.Temperature);
            Assert.Null(empty.MaxTokens);
            Assert.Null(empty.TopP);
            Assert.Null(empty.Seed);
            Assert.False(empty.BatchTier);
        }

        [Fact(DisplayName = "FromModel_WithModel_SetsModelOnly")]
        public void FromModel_WithModel_SetsModelOnly()
        {
            var param = AIRequestParameters.FromModel("gpt-4");
            Assert.NotNull(param);
            Assert.Equal("gpt-4", param.Model);
            Assert.Null(param.Temperature);
            Assert.Null(param.MaxTokens);
        }

        [Fact(DisplayName = "FromModel_WithNullOrWhitespace_ReturnsEmpty")]
        public void FromModel_WithNullOrWhitespace_ReturnsEmpty()
        {
            var param1 = AIRequestParameters.FromModel(null);
            var param2 = AIRequestParameters.FromModel("   ");
            Assert.Equal(AIRequestParameters.Empty, param1);
            Assert.Equal(AIRequestParameters.Empty, param2);
        }

        [Fact(DisplayName = "Builder_WithModel_SetsModel")]
        public void Builder_WithModel_SetsModel()
        {
            var param = AIRequestParameters.Create()
                .WithModel("gpt-4")
                .Build();
            Assert.Equal("gpt-4", param.Model);
        }

        [Fact(DisplayName = "Builder_WithTemperature_SetsTemperature")]
        public void Builder_WithTemperature_SetsTemperature()
        {
            var param = AIRequestParameters.Create()
                .WithTemperature(0.7)
                .Build();
            Assert.Equal(0.7, param.Temperature);
        }

        [Fact(DisplayName = "Builder_WithMaxTokens_SetsMaxTokens")]
        public void Builder_WithMaxTokens_SetsMaxTokens()
        {
            var param = AIRequestParameters.Create()
                .WithMaxTokens(2048)
                .Build();
            Assert.Equal(2048, param.MaxTokens);
        }

        [Fact(DisplayName = "Builder_WithTopP_SetsTopP")]
        public void Builder_WithTopP_SetsTopP()
        {
            var param = AIRequestParameters.Create()
                .WithTopP(0.9)
                .Build();
            Assert.Equal(0.9, param.TopP);
        }

        [Fact(DisplayName = "Builder_WithSeed_SetsSeed")]
        public void Builder_WithSeed_SetsSeed()
        {
            var param = AIRequestParameters.Create()
                .WithSeed(42)
                .Build();
            Assert.Equal(42, param.Seed);
        }

        [Fact(DisplayName = "Builder_WithBatchTier_SetsBatchTier")]
        public void Builder_WithBatchTier_SetsBatchTier()
        {
            var param = AIRequestParameters.Create()
                .WithBatchTier(true)
                .Build();
            Assert.True(param.BatchTier);
        }

        [Fact(DisplayName = "Builder_ClearBatchTier_ResetsFlagToFalse")]
        public void Builder_ClearBatchTier_ResetsFlagToFalse()
        {
            var param = AIRequestParameters.Create()
                .WithBatchTier(true)
                .ClearBatchTier()
                .Build();
            Assert.False(param.BatchTier);
        }

        [Fact(DisplayName = "Builder_WithExtra_AddsKeyValue")]
        public void Builder_WithExtra_AddsKeyValue()
        {
            var param = AIRequestParameters.Create()
                .WithExtra("reasoning_effort", JToken.FromObject("high"))
                .Build();
            Assert.NotNull(param.Extras);
            Assert.Contains("reasoning_effort", param.Extras.Keys);
        }

        [Fact(DisplayName = "Builder_WithExtra_NullKeyIgnored")]
        public void Builder_WithExtra_NullKeyIgnored()
        {
            var param = AIRequestParameters.Create()
                .WithExtra(null, JToken.FromObject("value"))
                .Build();
            Assert.Empty(param.Extras);
        }

        [Fact(DisplayName = "Builder_WithExtras_MergesAll")]
        public void Builder_WithExtras_MergesAll()
        {
            var extras = new Dictionary<string, JToken>
            {
                { "key1", JToken.FromObject("value1") },
                { "key2", JToken.FromObject("value2") }
            };
            var param = AIRequestParameters.Create()
                .WithExtras(extras)
                .Build();
            Assert.Equal(2, param.Extras.Count);
        }

        [Fact(DisplayName = "Builder_RemoveExtra_RemovesKey")]
        public void Builder_RemoveExtra_RemovesKey()
        {
            var param = AIRequestParameters.Create()
                .WithExtra("key1", JToken.FromObject("value1"))
                .WithExtra("key2", JToken.FromObject("value2"))
                .RemoveExtra("key1")
                .Build();
            Assert.Single(param.Extras);
            Assert.Contains("key2", param.Extras.Keys);
        }

        [Fact(DisplayName = "Builder_ClearExtras_EmptiesExtras")]
        public void Builder_ClearExtras_EmptiesExtras()
        {
            var param = AIRequestParameters.Create()
                .WithExtra("key1", JToken.FromObject("value1"))
                .ClearExtras()
                .Build();
            Assert.Empty(param.Extras);
        }

        [Fact(DisplayName = "Builder_Build_ReturnsImmutableRecord")]
        public void Builder_Build_ReturnsImmutableRecord()
        {
            var builder = AIRequestParameters.Create()
                .WithModel("gpt-4")
                .WithTemperature(0.7);
            var param = builder.Build();
            Assert.NotNull(param);
            Assert.Equal("gpt-4", param.Model);
            Assert.Equal(0.7, param.Temperature);
        }

        [Fact(DisplayName = "Builder_FluentChain_AllProperties")]
        public void Builder_FluentChain_AllProperties()
        {
            var param = AIRequestParameters.Create()
                .WithModel("gpt-4")
                .WithTemperature(0.8)
                .WithMaxTokens(4096)
                .WithTopP(0.95)
                .WithSeed(123)
                .WithBatchTier(true)
                .WithExtra("reasoning_effort", JToken.FromObject("high"))
                .Build();
            Assert.Equal("gpt-4", param.Model);
            Assert.Equal(0.8, param.Temperature);
            Assert.Equal(4096, param.MaxTokens);
            Assert.Equal(0.95, param.TopP);
            Assert.Equal(123, param.Seed);
            Assert.True(param.BatchTier);
            Assert.Single(param.Extras);
        }

        [Fact(DisplayName = "Build_TwoCalls_ReturnIndependentInstances")]
        public void Build_TwoCalls_ReturnIndependentInstances()
        {
            var builder = AIRequestParameters.Create()
                .WithModel("gpt-4");
            var param1 = builder.Build();
            var param2 = builder.Build();
            Assert.NotSame(param1, param2);
            Assert.Equal(param1.Model, param2.Model);
        }
    }
}
