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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.JsonSchemas;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Validates a tool call's arguments against the tool's JSON parameters schema.
    /// </summary>
    public sealed class ToolJsonSchemaValidator : IValidator<AIInteractionToolCall>
    {
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

            var tools = AIToolManager.GetTools();
            if (string.IsNullOrWhiteSpace(instance.Name) || !tools.ContainsKey(instance.Name))
            {
                // Defer unknown tool handling to ToolExistsValidator; avoid double-reporting here
                result.Messages = messages;
                result.IsValid = true;
                return result;
            }

            var tool = tools[instance.Name];
            var schemaText = tool.ParametersSchema ?? string.Empty;

            if (string.IsNullOrWhiteSpace(schemaText))
            {
                // No schema to validate; treat as pass
                result.Messages = messages;
                result.IsValid = true;
                return result;
            }

            // Require arguments when schema is present
            if (instance.Arguments == null)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    $"Tool '{instance.Name}' requires arguments matching its parameters schema, but arguments are missing"));
            }
            else
            {
                var svc = JsonSchemaService.Instance;
                var json = instance.Arguments.ToString(Formatting.None);
                if (!svc.Validate(schemaText, json, out string error))
                {
                    messages.Add(new AIRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Validation,
                        $"Arguments for tool '{instance.Name}' do not match schema: {error}"));
                }
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
