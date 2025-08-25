/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Validates that the selected provider/model supports the capabilities required by the tool.
    /// </summary>
    public sealed class ToolCapabilityValidator : IValidator<AIInteractionToolCall>
    {
        private readonly string provider;
        private readonly string model;

        public ToolCapabilityValidator(string provider, string model)
        {
            this.provider = provider ?? string.Empty;
            this.model = model ?? string.Empty;
        }

        public AIRuntimeMessageSeverity FailOn { get; } = AIRuntimeMessageSeverity.Error;

        public ValidationResult Validate(AIInteractionToolCall instance)
        {
            var result = new ValidationResult { IsValid = true };
            var messages = new List<AIRuntimeMessage>();

            if (instance == null)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    "Tool call instance is null"));
                result.Messages = messages;
                result.IsValid = false;
                return result;
            }

            // Without provider/model context, we cannot validate; do not fail here
            if (string.IsNullOrWhiteSpace(this.provider) || string.IsNullOrWhiteSpace(this.model))
            {
                result.Messages = messages;
                result.IsValid = true;
                return result;
            }

            var tools = AIToolManager.GetTools();
            if (string.IsNullOrWhiteSpace(instance.Name) || !tools.ContainsKey(instance.Name))
            {
                // Defer to ToolExistsValidator
                result.Messages = messages;
                result.IsValid = true;
                return result;
            }

            var tool = tools[instance.Name];
            var required = tool.RequiredCapabilities;
            if (required == AICapability.None)
            {
                result.Messages = messages;
                result.IsValid = true;
                return result;
            }

            var ok = ModelManager.Instance.ValidateCapabilities(this.provider, this.model, required);
            if (!ok)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    $"Selected model '{this.model}' on provider '{this.provider}' does not support required capabilities ({required}) for tool '{instance.Name}'"));
            }

            result.Messages = messages;
            result.IsValid = !HasAtOrAbove(messages, this.FailOn);
            return result;
        }

        private static bool HasAtOrAbove(List<AIRuntimeMessage> messages, AIRuntimeMessageSeverity threshold)
        {
            foreach (var m in messages)
            {
                if (m != null && m.Severity >= threshold)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
