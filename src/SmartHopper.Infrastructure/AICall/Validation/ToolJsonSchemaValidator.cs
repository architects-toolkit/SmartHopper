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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public Task<ValidationResult> ValidateAsync(AIInteractionToolCall instance, ValidationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messages = new List<AIRuntimeMessage>();

            if (instance == null)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    "Tool call instance is null"));
                var early = new ValidationResult
                {
                    Messages = messages,
                };
                early.IsValid = false;
                return Task.FromResult(early);
            }

            var tools = AIToolManager.GetTools();
            if (string.IsNullOrWhiteSpace(instance.Name) || !tools.ContainsKey(instance.Name))
            {
                // Defer unknown tool handling to ToolExistsValidator; avoid double-reporting here
                var pass = new ValidationResult { Messages = messages };
                pass.IsValid = true;
                return Task.FromResult(pass);
            }

            var tool = tools[instance.Name];
            var schemaText = tool.ParametersSchema ?? string.Empty;

            if (string.IsNullOrWhiteSpace(schemaText))
            {
                // No schema to validate; treat as pass
                var ok = new ValidationResult { Messages = messages };
                ok.IsValid = true;
                return Task.FromResult(ok);
            }

            // Check arguments against schema
            if (instance.Arguments != null)
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
            else
            {
                // If a schema exists but no arguments were provided, initialize to an empty object
                // so downstream execution receives a valid JSON object. This mirrors permissive
                // handling for tools that support optional arguments.
                instance.Arguments = new JObject();
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Info,
                    AIRuntimeMessageOrigin.Validation,
                    $"No arguments provided for tool '{instance.Name}'. Created default empty arguments {{}} to satisfy the schema."));
            }

            var result = new ValidationResult
            {
                Messages = messages,
            };
            result.IsValid = !HasAtOrAbove(messages, this.FailOn);
            return Task.FromResult(result);
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
