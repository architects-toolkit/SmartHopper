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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// AI tool that retrieves the list of enabled AI providers and their configuration status.
    /// </summary>
    public class get_available_providers : IAIToolProvider
    {
        private readonly string toolName = "get_available_providers";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Retrieve the list of enabled AI providers registered in SmartHopper, including whether each provider is properly configured in the current environment.",
                category: "Providers",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {},
                    ""required"": []
                }",
                execute: this.GetAvailableProvidersAsync,
                requiredCapabilities: AICapability.None,
                mutatesCanvas: false,
                enabled: true,
                tags: new[] { "providers", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""providers"": { ""type"": ""array"", ""items"": { ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" }, ""configured"": { ""type"": ""boolean"", ""description"": ""True when the provider has all required settings (API key, endpoint URL, etc.) configured in the current environment."" } }, ""required"": [""name"", ""configured""] } } } }",
                annotations: new AIToolAnnotations(openWorldHint: false, readOnlyHint: true, destructiveHint: false));
        }

        private Task<AIReturn> GetAvailableProvidersAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local tool: provider/model/finish_reason metrics are not meaningful here.
                toolCall.SkipMetricsValidation = true;

                var toolInfo = toolCall.GetToolCall();

                var providers = ProviderManager.Instance.GetProviders()
                    .Where(p => p.IsEnabled)
                    .Select(p => new JObject
                    {
                        ["name"] = p.Name,
                        ["configured"] = p.IsConfigured,
                    })
                    .OrderBy(p => p["name"]?.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var result = new JObject()
                {
                    ["providers"] = new JArray(providers),
                };

                var body = AIBodyBuilder.Create()
                    .AddToolResult(result, toolInfo.Id, toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return Task.FromResult(output);
            }
            catch (Exception ex)
            {
                output.CreateError($"Error executing {this.toolName}: {ex.Message}");
                return Task.FromResult(output);
            }
        }
    }
}
