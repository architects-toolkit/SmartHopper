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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Validation;

namespace SmartHopper.Infrastructure.AICall.Policies.Response
{
    /// <summary>
    /// Validates decoded provider response content against the request's JSON schema.
    /// </summary>
    public sealed class SchemaValidateResponsePolicy : IResponsePolicy
    {
        public async Task ApplyAsync(PolicyContext context)
        {
            var response = context?.Response;
            if (response == null)
            {
                return;
            }

            try
            {
                var validator = new JsonSchemaResponseValidator();
                var vctx = Validation.ValidationContext.FromPolicyContext(context);
                var result = await validator.ValidateAsync(response, vctx, CancellationToken.None).ConfigureAwait(false);
                if (result?.Messages != null)
                {
                    foreach (var m in result.Messages)
                    {
                        if (m != null)
                        {
                            response.AddRuntimeMessage(m.Severity, m.Origin, m.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response.AddRuntimeMessage(
                    AIRuntimeMessageSeverity.Warning,
                    AIRuntimeMessageOrigin.Validation,
                    $"Schema response validation failed: {ex.Message}");
            }
        }
    }
}
