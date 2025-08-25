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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.JsonSchemas;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Baseline validator that checks provider response content against the request's JSON schema.
    /// Minimal: validates JSON parse of schema and instance. Full JSON Schema validation can be added later.
    /// </summary>
    public sealed class JsonSchemaResponseValidator : IValidator<AIReturn>
    {
        public AIRuntimeMessageSeverity FailOn { get; } = AIRuntimeMessageSeverity.Error;

        public Task<ValidationResult> ValidateAsync(AIReturn instance, ValidationContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var messages = new List<AIRuntimeMessage>();

            var rq = context?.Request;
            var rs = context?.Response ?? instance;

            if (rq?.Body == null || rs == null)
            {
                // Nothing to validate
                return Task.FromResult(new ValidationResult { IsValid = true, Messages = messages });
            }

            if (!rq.Body.RequiresJsonOutput)
            {
                // Not a JSON-output request
                return Task.FromResult(new ValidationResult { IsValid = true, Messages = messages });
            }

            var schemaText = rq.Body.JsonOutputSchema ?? string.Empty;
            if (string.IsNullOrWhiteSpace(schemaText))
            {
                // Core request validation already gates this; do not double-report
                return Task.FromResult(new ValidationResult { IsValid = true, Messages = messages });
            }

            // Find the latest assistant text interaction
            var content = rs?.Body?.Interactions?
                .OfType<AIInteractionText>()
                .Where(i => i.Agent == AIAgent.Assistant)
                .Select(i => i.Content)
                .LastOrDefault(c => !string.IsNullOrWhiteSpace(c));

            if (string.IsNullOrWhiteSpace(content))
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    "Expected JSON structured output from assistant, but content is missing"));
            }
            else
            {
                // Optionally unwrap using thread-local wrapper info
                var svc = JsonSchemaService.Instance;
                var info = svc.GetCurrentWrapperInfo();
                var instanceJson = svc.Unwrap(content, info);

                if (!svc.Validate(schemaText, instanceJson, out string error))
                {
                    messages.Add(new AIRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Validation,
                        $"Response JSON does not match schema: {error}"));
                }
            }

            var result = new ValidationResult { Messages = messages };
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
