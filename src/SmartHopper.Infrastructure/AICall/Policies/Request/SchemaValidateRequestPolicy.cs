/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.JsonSchemas;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Validates the request's JSON output schema and prepares provider wrapper info
    /// for consistent response unwrapping/validation.
    /// Non-intrusive: does not mutate capabilities; only records diagnostics on failure.
    /// </summary>
    public sealed class SchemaValidateRequestPolicy : IRequestPolicy
    {
        public Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            if (rq?.Body == null)
            {
                return Task.CompletedTask;
            }

            // Only when JSON output is requested
            if (!rq.Body.RequiresJsonOutput)
            {
                return Task.CompletedTask;
            }

            var schemaText = rq.Body.JsonOutputSchema;
            if (string.IsNullOrWhiteSpace(schemaText))
            {
                // Gating error is handled by core request validation; no duplication here
                return Task.CompletedTask;
            }

            var svc = JsonSchemaService.Instance;
            try
            {
                if (!svc.TryParseSchema(schemaText, out JObject schemaObj, out string parseError))
                {
                    context?.Diagnostics?.Add(new AIRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Validation,
                        $"Invalid JSON output schema: {parseError}"));
                    return Task.CompletedTask;
                }

                // Compute provider-aware wrapping metadata. Providers may need it for decoding.
                var providerName = rq.Provider ?? string.Empty;
                var (_, info) = svc.WrapForProvider(schemaObj, providerName);
                svc.SetCurrentWrapperInfo(info);
            }
            catch (Exception ex)
            {
                context?.Diagnostics?.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Warning,
                    AIRuntimeMessageOrigin.Validation,
                    $"Schema validation setup failed: {ex.Message}"));
            }

            return Task.CompletedTask;
        }
    }
}
