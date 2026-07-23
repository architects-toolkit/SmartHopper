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
    /// AI tool that retrieves the list of available models for a given AI provider.
    /// </summary>
    public class get_available_models : IAIToolProvider
    {
        private readonly string toolName = "get_available_models";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Retrieve the list of available models for a given AI provider. Uses live provider APIs when possible and falls back to the static model list.",
                category: "Providers",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""provider"": { ""type"": ""string"", ""description"": ""Name of the AI provider. If omitted or set to 'Default', the current default provider is used."" }
                    },
                    ""required"": []
                }",
                execute: this.GetAvailableModelsAsync,
                requiredCapabilities: AICapability.None,
                mutatesCanvas: false,
                enabled: true,
                tags: new[] { "providers", "models", "api", "read-only" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""provider"": { ""type"": ""string"" }, ""models"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }, ""source"": { ""type"": ""string"" }, ""warning"": { ""type"": ""string"" } } }",
                annotations: new AIToolAnnotations(openWorldHint: true, readOnlyHint: true, destructiveHint: false));
        }

        private async Task<AIReturn> GetAvailableModelsAsync(AIToolCall toolCall)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            try
            {
                // Local/API tool: provider/model/finish_reason metrics are not meaningful here.
                toolCall.SkipMetricsValidation = true;

                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                var providerName = args["provider"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(providerName) || providerName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    providerName = ProviderManager.Instance.GetDefaultAIProvider();
                }

                if (string.IsNullOrWhiteSpace(providerName))
                {
                    output.CreateError("No AI provider selected or available.");
                    return output;
                }

                var provider = ProviderManager.Instance.GetProvider(providerName);
                if (provider == null)
                {
                    output.CreateError($"Provider '{providerName}' is not registered.");
                    return output;
                }

                await provider.InitializeProviderAsync().ConfigureAwait(false);

                List<string> models = null;
                string source = null;
                string warning = null;

                try
                {
                    var apiModels = await provider.Models.RetrieveApiModels().ConfigureAwait(false);
                    if (apiModels != null && apiModels.Count > 0)
                    {
                        models = apiModels
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        source = "api";
                    }
                }
                catch
                {
                    // Fall through to the static model list.
                }

                if (models == null)
                {
                    var caps = await provider.Models.RetrieveModels().ConfigureAwait(false) ?? new List<AIModelCapabilities>();
                    if (caps.Count == 0)
                    {
                        output.CreateError($"No models available from provider '{providerName}'.");
                        return output;
                    }

                    models = caps
                        .OrderByDescending(m => m.Verified)
                        .ThenByDescending(m => m.Rank)
                        .ThenBy(m => m.Deprecated)
                        .ThenBy(m => m.Model, StringComparer.OrdinalIgnoreCase)
                        .Select(m => m.Model)
                        .ToList();

                    source = "static";
                    warning = "Provider API models unavailable. Using fallback static model list.";
                }

                var result = new JObject()
                {
                    ["provider"] = provider.Name,
                    ["models"] = JArray.FromObject(models),
                    ["source"] = source,
                };

                if (!string.IsNullOrWhiteSpace(warning))
                {
                    result["warning"] = warning;
                }

                var body = AIBodyBuilder.Create()
                    .AddToolResult(result, toolInfo.Id, toolInfo.Name ?? this.toolName)
                    .Build();

                output.CreateSuccess(body, toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error executing {this.toolName}: {ex.Message}");
                return output;
            }
        }
    }
}