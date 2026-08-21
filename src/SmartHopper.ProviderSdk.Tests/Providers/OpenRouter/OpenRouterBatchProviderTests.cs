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

#if NET7_WINDOWS

namespace SmartHopper.ProviderSdk.Tests.Providers.OpenRouter
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Providers.OpenRouter;
    using SmartHopper.ProviderSdk.AICall.Batch;
    using SmartHopper.ProviderSdk.AICall.Core.Requests;
    using SmartHopper.ProviderSdk.AIModels;
    using Xunit;

    /// <summary>
    /// Unit tests for the OpenRouter batch provider and the generated model metadata.
    /// </summary>
    [Collection("ProviderSdk")]
    public class OpenRouterBatchProviderTests
    {
        /// <summary>
        /// The OpenRouter provider instance must implement <see cref="IAIBatchProvider"/>.
        /// </summary>
        [Fact]
        public void Provider_ImplementsBatchInterface()
        {
            var provider = OpenRouterProvider.Instance;

            Assert.NotNull(provider);
            Assert.IsAssignableFrom<IAIBatchProvider>(provider);
        }

        /// <summary>
        /// The generated OpenRouter model list must not contain any canonical model
        /// ids ending with <c>:batch</c>. Those should have been merged into the base
        /// model with the <c>SupportsBatch</c> flag.
        /// </summary>
        [Fact]
        public async Task Models_NoCanonicalBatchModelIds()
        {
            var provider = OpenRouterProvider.Instance;
            var models = await provider.Models.RetrieveModels().ConfigureAwait(false);

            var batchModelIds = models
                .Where(m => m.Model != null && m.Model.EndsWith(":batch", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Model)
                .ToList();

            Assert.Empty(batchModelIds);
        }

        /// <summary>
        /// A known batch-eligible model should be marked <see cref="AIModelCapabilities.SupportsBatch"/>
        /// and should expose discounted <see cref="AIModelCapabilities.BatchPricing"/>.
        /// </summary>
        [Fact]
        public async Task Models_BatchEligibleModelHasMetadata()
        {
            var provider = OpenRouterProvider.Instance;
            var models = await provider.Models.RetrieveModels().ConfigureAwait(false);

            var model = models.FirstOrDefault(m =>
                string.Equals(m.Model, "openai/gpt-5.6-luna", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(model);
            Assert.True(model.SupportsBatch);
            Assert.NotNull(model.BatchPricing);
            Assert.True(model.BatchPricing.Prompt < model.Pricing.Prompt);
            Assert.Contains("openai/gpt-5.6-luna:batch", model.Aliases);
        }

        /// <summary>
        /// Resolving a model through the <c>:batch</c> alias should return the canonical
        /// model with batch support.
        /// </summary>
        [Fact]
        public async Task Registry_ResolvesBatchAliasToCanonicalModel()
        {
            var provider = OpenRouterProvider.Instance;
            var registry = AIModelCapabilityRegistry.Instance;
            var models = await provider.Models.RetrieveModels().ConfigureAwait(false);

            // Ensure provider models are loaded in the registry.
            foreach (var model in models)
            {
                registry.SetCapabilities(model);
            }

            var viaAlias = registry.GetCapabilities("OpenRouter", "openai/gpt-5.6-luna:batch");
            var viaBase = registry.GetCapabilities("OpenRouter", "openai/gpt-5.6-luna");

            Assert.NotNull(viaAlias);
            Assert.NotNull(viaBase);
            Assert.Same(viaAlias, viaBase);
            Assert.True(viaAlias.SupportsBatch);
            Assert.NotNull(viaAlias.BatchPricing);
        }

        /// <summary>
        /// <see cref="OpenRouterProvider.PreCall(AIRequestCall)"/> must strip the
        /// <c>:batch</c> suffix from the model slug so the same model works for both
        /// chat and batch endpoints.
        /// </summary>
        [Fact]
        public void PreCall_StripsBatchSuffix()
        {
            var provider = OpenRouterProvider.Instance;
            var request = new AIRequestCall
            {
                Provider = "OpenRouter",
                Model = "openai/gpt-5.6-luna:batch",
                Capability = SmartHopper.ProviderSdk.AIModels.AICapability.None,
            };

            var prepared = provider.PreCall(request);

            Assert.Equal("openai/gpt-5.6-luna", prepared.Model);
        }

        /// <summary>
        /// Batch result parsing must extract successful response bodies keyed by
        /// <c>custom_id</c>.
        /// </summary>
        [Fact]
        public void ParseBatchResultsFiles_SuccessResponse_ReturnsBody()
        {
            var provider = OpenRouterProvider.Instance;
            var json = @"[
                {
                    ""custom_id"": ""sh-20260821000000-req-00-00000000"",
                    ""response"": {
                        ""status_code"": 200,
                        ""body"": {
                            ""id"": ""chatcmpl-test"",
                            ""choices"": [
                                { ""message"": { ""role"": ""assistant"", ""content"": ""hello"" }, ""finish_reason"": ""stop"" }
                            ]
                        }
                    }
                }
            ]";

            var status = provider.ParseBatchResultsFiles(new[] { json }, "batch_123");

            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.Single(status.Results);
            Assert.True(status.Results.ContainsKey("sh-20260821000000-req-00-00000000"));
            Assert.Equal("chatcmpl-test", status.Results["sh-20260821000000-req-00-00000000"]["id"]?.ToString());
        }

        /// <summary>
        /// Item-level errors in the results array must surface as provider messages
        /// instead of being added to the successful result dictionary.
        /// </summary>
        [Fact]
        public void ParseBatchResultsFiles_ItemError_EmitsMessage()
        {
            var provider = OpenRouterProvider.Instance;
            var json = @"[
                {
                    ""custom_id"": ""sh-20260821000000-req-00-00000000"",
                    ""error"": { ""message"": ""Batch item rejected"" }
                }
            ]";

            var status = provider.ParseBatchResultsFiles(new[] { json }, "batch_123");

            Assert.Empty(status.Results);
            Assert.Single(status.Messages);
            Assert.Contains("Batch item rejected", status.Messages[0].Message);
        }

        /// <summary>
        /// Non-2xx HTTP responses at the item level must be reported as errors.
        /// </summary>
        [Fact]
        public void ParseBatchResultsFiles_Non2xxResponse_EmitsErrorMessage()
        {
            var provider = OpenRouterProvider.Instance;
            var json = @"[
                {
                    ""custom_id"": ""sh-20260821000000-req-00-00000000"",
                    ""response"": {
                        ""status_code"": 422,
                        ""body"": { ""error"": { ""message"": ""Unprocessable entity"" } }
                    }
                }
            ]";

            var status = provider.ParseBatchResultsFiles(new[] { json }, "batch_123");

            Assert.Empty(status.Results);
            Assert.Single(status.Messages);
            Assert.Contains("Unprocessable entity", status.Messages[0].Message);
        }

        /// <summary>
        /// A full batch object containing a <c>results</c> array must be parsed as well
        /// as a raw results array.
        /// </summary>
        [Fact]
        public void ParseBatchResultsFiles_BatchObjectWithResults_ReturnsBody()
        {
            var provider = OpenRouterProvider.Instance;
            var json = @"{
                ""id"": ""batch_123"",
                ""status"": ""completed"",
                ""results"": [
                    {
                        ""custom_id"": ""sh-20260821000000-req-00-00000000"",
                        ""response"": {
                            ""status_code"": 200,
                            ""body"": { ""id"": ""chatcmpl-batch"" }
                        }
                    }
                ]
            }";

            var status = provider.ParseBatchResultsFiles(new[] { json }, "batch_123");

            Assert.Equal(AIBatchState.Completed, status.State);
            Assert.Single(status.Results);
        }
    }
}

#endif
