/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.JsonSchemas;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Request policy that validates and canonicalizes JSON output schema prior to provider encoding.
    /// Non-intrusive: it does not modify capabilities nor inject messages. It only normalizes the schema
    /// when possible to avoid downstream provider parse errors.
    /// </summary>
    public sealed class SchemaAttachRequestPolicy : IRequestPolicy
    {
        public Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            if (rq?.Body == null) return Task.CompletedTask;

            var schemaText = rq.Body.JsonOutputSchema;
            if (string.IsNullOrWhiteSpace(schemaText))
            {
                return Task.CompletedTask;
            }

            var svc = JsonSchemaService.Instance;
            if (svc.TryParseSchema(schemaText, out JObject schemaObj, out _))
            {
                // Canonicalize formatting (minified) so providers get a stable schema string
                rq.Body = AIBodyBuilder
                    .FromImmutable(rq.Body)
                    .WithJsonOutputSchema(schemaObj.ToString(Formatting.None))
                    .Build();
            }

            return Task.CompletedTask;
        }
    }
}
