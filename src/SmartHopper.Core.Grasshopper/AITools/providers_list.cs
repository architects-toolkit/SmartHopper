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
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// AI tool that retrieves the list of registered AI providers and their status
    /// (enabled, configured, default).
    /// </summary>
    public class providers_list : IAIToolProvider
    {
        private readonly string toolName = "providers_list";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Retrieve the list of AI providers registered in SmartHopper with per-provider status flags: enabled (registered for use), configured (all required settings such as API key or endpoint present in the current environment), and isDefault (currently selected default). Includes the top-level defaultProvider name.",
                category: "Providers",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {},
                    ""required"": []
                }",
                execute: this.ProvidersListAsync,
                requiredCapabilities: AICapability.None,
                mutatesCanvas: false,
                enabled: true,
                tags: new[] { "providers", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""defaultProvider"": { ""type"": ""string"" }, ""providers"": { ""type"": ""array"", ""items"": { ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" }, ""enabled"": { ""type"": ""boolean"", ""description"": ""True when the provider is enabled for use."" }, ""configured"": { ""type"": ""boolean"", ""description"": ""True when the provider has all required settings (API key, endpoint URL, etc.) configured in the current environment."" }, ""isDefault"": { ""type"": ""boolean"", ""description"": ""True when the provider is the current default."" } }, ""required"": [""name"", ""enabled"", ""configured"", ""isDefault""] } } } }",
                annotations: new AIToolAnnotations(openWorldHint: false, readOnlyHint: true, destructiveHint: false));
        }

        private Task<AIReturn> ProvidersListAsync(AIToolCall toolCall)
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

                var defaultProvider = ProviderManager.Instance.GetDefaultAIProvider();

                var providers = ProviderManager.Instance.GetProviders()
                    .Select(p => new JObject
                    {
                        ["name"] = p.Name,
                        ["enabled"] = p.IsEnabled,
                        ["configured"] = p.IsConfigured,
                        ["isDefault"] = string.Equals(p.Name, defaultProvider, StringComparison.OrdinalIgnoreCase),
                    })
                    .OrderByDescending(p => p["isDefault"]?.ToObject<bool>() ?? false)
                    .ThenBy(p => p["name"]?.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var result = new JObject()
                {
                    ["defaultProvider"] = defaultProvider,
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
