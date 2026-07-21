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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json.Linq;
using Rhino;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// AI tool that configures an <see cref="IProviderComponent"/> by setting its
    /// selected AI provider and wiring a new Panel with the model name into its
    /// Settings input.
    /// </summary>
    public class set_ai_provider_and_model : IAIToolProvider
    {
        private readonly string toolName = "set_ai_provider_and_model";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: this.toolName,
                description: "Set the AI provider and/or model for a component that implements IProviderComponent. If a provider is supplied, the component's AI provider selection is updated. If a model is supplied, a new Panel containing the model name is created and wired to the component's Settings input.",
                category: "Components",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""componentGuid"": { ""type"": ""string"", ""description"": ""Instance GUID of the IProviderComponent to configure."" },
                        ""provider"": { ""type"": ""string"", ""description"": ""AI provider name to select (e.g. OpenAI, Anthropic, MistralAI, Gemini, Default). If omitted, the currently selected provider is kept."" },
                        ""model"": { ""type"": ""string"", ""description"": ""Model name to place in a new Panel wired to the component's Settings input. If omitted, no Panel is created and the component uses its default model."" }
                    },
                    ""required"": [""componentGuid""]
                }",
                execute: this.SetAIProviderAndModelAsync,
                requiredCapabilities: AICapability.None,
                mutatesCanvas: true,
                enabled: true,
                tags: new[] { "canvas", "components", "mutating", "settings", "provider", "model" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""success"": { ""type"": ""boolean"" }, ""componentGuid"": { ""type"": ""string"" }, ""providerSet"": { ""type"": ""boolean"" }, ""selectedProvider"": { ""type"": ""string"" }, ""provider"": { ""type"": ""string"" }, ""panelConnected"": { ""type"": ""boolean"" }, ""panelGuid"": { ""type"": ""string"" }, ""model"": { ""type"": ""string"" } } }",
                annotations: new AIToolAnnotations(destructiveHint: false));
        }

        private Task<AIReturn> SetAIProviderAndModelAsync(AIToolCall toolCall)
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
                var args = toolInfo.GetArgumentsOrEmpty();

                var componentGuidStr = args["componentGuid"]?.ToString();
                if (!Guid.TryParse(componentGuidStr, out var componentGuid))
                {
                    output.CreateError("componentGuid is required and must be a valid GUID.");
                    return Task.FromResult(output);
                }

                var provider = args["provider"]?.ToString()?.Trim();
                var model = args["model"]?.ToString()?.Trim();

                if (string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(model))
                {
                    output.CreateError("Either provider or model must be provided.");
                    return Task.FromResult(output);
                }

                var obj = CanvasAccess.FindInstance(componentGuid);
                if (obj == null)
                {
                    output.CreateError($"Component {componentGuid} not found on the canvas.");
                    return Task.FromResult(output);
                }

                if (CanvasProtection.IsProtected(componentGuid))
                {
                    output.CreateError($"Component {componentGuid} is protected and cannot be modified.");
                    return Task.FromResult(output);
                }

                if (!(obj is IProviderComponent providerComp))
                {
                    output.CreateError($"Component {componentGuid} does not support AI provider selection.");
                    return Task.FromResult(output);
                }

                if (!(obj is GH_Component ghComp))
                {
                    output.CreateError($"Component {componentGuid} is not a Grasshopper component.");
                    return Task.FromResult(output);
                }

                // Normalize and validate the provider name against registered providers.
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    var registeredProviders = ProviderManager.Instance.GetProviders().ToList();
                    var matchingProvider = registeredProviders
                        .FirstOrDefault(p => string.Equals(p.Name, provider, StringComparison.OrdinalIgnoreCase));

                    bool isDefault = string.Equals(
                        provider,
                        ProviderSelectionCore.DEFAULT_PROVIDER,
                        StringComparison.OrdinalIgnoreCase);

                    if (matchingProvider == null && !isDefault)
                    {
                        var available = string.Join(
                            ", ",
                            registeredProviders.Select(p => p.Name).Append(ProviderSelectionCore.DEFAULT_PROVIDER));
                        output.CreateError($"Unknown provider '{provider}'. Available providers: {available}");
                        return Task.FromResult(output);
                    }

                    provider = matchingProvider?.Name ?? ProviderSelectionCore.DEFAULT_PROVIDER;
                }

                // Locate the Settings input when a model panel is requested.
                IGH_Param? settingsParam = null;
                if (!string.IsNullOrWhiteSpace(model))
                {
                    settingsParam = ghComp.Params.Input
                        .FirstOrDefault(p =>
                            string.Equals(p.Name, WellKnownInputs.Settings, StringComparison.OrdinalIgnoreCase));

                    if (settingsParam == null)
                    {
                        output.CreateError("Target component does not have a Settings input.");
                        return Task.FromResult(output);
                    }
                }

                var toolResult = new JObject()
                {
                    ["componentGuid"] = componentGuidStr,
                };

                GH_Panel? panel = null;

                RhinoApp.InvokeOnUiThread(() =>
                {
                    var doc = CanvasAccess.GetCurrentCanvas();
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document.");
                    }

                    var undo = doc.UndoUtil.CreateGenericObjectEvent(
                        "[SH] Set AI provider and model",
                        ghComp);

                    if (!string.IsNullOrWhiteSpace(provider))
                    {
                        providerComp.SetSelectedProviderName(provider);
                        toolResult["providerSet"] = true;
                        toolResult["provider"] = provider;
                    }

                    if (!string.IsNullOrWhiteSpace(model) && settingsParam != null)
                    {
                        var inputBounds = settingsParam.Attributes?.Bounds ?? ghComp.Attributes.Bounds;

                        panel = new GH_Panel()
                        {
                            NickName = "Model",
                            UserText = model,
                        };

                        panel.CreateAttributes();

                        // Start with a rough left-aligned position and measure the panel bounds,
                        // then align the panel's right edge to the left of the Settings input
                        // and vertically center it with the input.
                        panel.Attributes.Pivot = new PointF(inputBounds.X - 220, inputBounds.Y);
                        panel.Attributes.ExpireLayout();

                        var panelBounds = panel.Attributes.Bounds;
                        if (panelBounds.Width > 0 && panelBounds.Height > 0)
                        {
                            float x = inputBounds.X - panelBounds.Width - 20;
                            float y = inputBounds.Y + ((inputBounds.Height - panelBounds.Height) / 2);
                            panel.Attributes.Pivot = new PointF(x, y);
                            panel.Attributes.ExpireLayout();
                        }

                        doc.AddObject(panel, false);

                        settingsParam.RecordUndoEvent(undo);
                        settingsParam.RemoveAllSources();
                        settingsParam.AddSource(panel);

                        toolResult["panelConnected"] = true;
                        toolResult["panelGuid"] = panel.InstanceGuid.ToString();
                        toolResult["model"] = model;
                    }

                    doc.UndoUtil.RecordEvent(undo);

                    if (panel != null)
                    {
                        doc.UndoUtil.RecordAddObjectEvent("[SH] Add model panel", panel);
                    }

                    ghComp.ExpireSolution(true);
                    doc.NewSolution(false);
                    Instances.RedrawCanvas();
                });

                toolResult["success"] = true;
                toolResult["selectedProvider"] = providerComp.SelectedProviderName;

                var body = AIBodyBuilder.Create()
                    .AddToolResult(toolResult, toolInfo.Id, toolInfo.Name ?? this.toolName)
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
