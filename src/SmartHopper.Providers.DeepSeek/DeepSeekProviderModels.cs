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
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek provider-specific model management implementation.
    /// </summary>
    public class DeepSeekProviderModels : AIProviderModels
    {
        private readonly DeepSeekProvider deepSeekProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepSeekProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The DeepSeek provider instance.</param>
        public DeepSeekProviderModels(DeepSeekProvider provider)
            : base(provider)
        {
            this.deepSeekProvider = provider;
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            var provider = this.deepSeekProvider.Name.ToLowerInvariant();

            var models = new List<AIModelCapabilities>
            {
                // Released between May 2026 and August 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek-v4-flash-vision-exp",
                    Capabilities = AICapability.TextInput | AICapability.ImageInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 10000,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 8, 21),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000022m,
                        Completion = 0.00000066m,
                        InputCacheRead = 0.000000007m,
                    },
                },



                // Released between February 2026 and May 2026

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek-v4-flash",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.Text2Text | AICapability.ToolChat | AICapability.ReasoningChat | AICapability.ToolReasoningChat | AICapability.Text2Json,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9995,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.00000008008m,
                        Completion = 0.00000016016m,
                        InputCacheRead = 0.000000016016m,
                    },
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek-v4-pro",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    SupportsStreaming = true,
                    Verified = false,
                    Rank = 9990,
                    ContextLimit = 1048576,
                    Created = new DateTime(2026, 4, 24),
                    Pricing = new AIModelPricing
                    {
                        Prompt = 0.000000437436m,
                        Completion = 0.000000874872m,
                        InputCacheRead = 0.000000036453m,
                    },
                },



                // Deprecated models

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek-chat",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput,
                    SupportsStreaming = true,
                    Deprecated = true,
                    Rank = 0,
                    ContextLimit = 60000,
                    Created = new DateTime(2024, 12, 26),
                },

                new AIModelCapabilities
                {
                    Provider = provider,
                    Model = "deepseek-reasoner",
                    Capabilities = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput | AICapability.Reasoning,
                    Default = AICapability.ToolReasoningChat,
                    SupportsStreaming = true,
                    Deprecated = true,
                    Rank = -5,
                    ContextLimit = 64000,
                }
            };

            return Task.FromResult(models);
        }
    }
}
