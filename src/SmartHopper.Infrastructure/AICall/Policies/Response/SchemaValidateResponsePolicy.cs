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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
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
