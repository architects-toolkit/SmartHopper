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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Validation;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Validates pending tool calls in the request body: existence, JSON schema, and capability compatibility.
    /// Emits structured diagnostics and makes request invalid on Error-level issues, to block early before provider call.
    /// </summary>
    public sealed class AIToolValidationRequestPolicy : IRequestPolicy
    {
        /// <summary>
        /// Validates pending tool calls in the request body using composed validators (existence, schema, capability).
        /// Adds structured diagnostics to the request and policy context without throwing on non-fatal issues.
        /// </summary>
        /// <param name="context">Policy context carrying the current request and ambient provider/model info.</param>
        /// <returns>A task that completes when validation is finished.</returns>
        public async Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            if (rq?.Body == null)
            {
                return;
            }

            var pendingToolCalls = rq.Body.PendingToolCallsList();
            if (pendingToolCalls == null || pendingToolCalls.Count == 0)
            {
                return;
            }

            var diagnostics = new List<AIRuntimeMessage>();

            foreach (var call in pendingToolCalls)
            {
                // Compose validators for this call
                var validators = new List<IValidator<AIInteractionToolCall>>
                {
                    new ToolExistsValidator(),
                    new ToolJsonSchemaValidator(),
                    new ToolCapabilityValidator(context.Provider, context.Model),
                };

                var vctx = ValidationContext.FromPolicyContext(context);
                foreach (var v in validators)
                {
                    var res = await v.ValidateAsync(call, vctx, CancellationToken.None).ConfigureAwait(false);
                    if (res?.Messages != null && res.Messages.Count > 0)
                    {
                        // Scope message to tool context for clarity
                        foreach (var m in res.Messages)
                        {
                            if (m == null) continue;
                            var prefix = string.IsNullOrWhiteSpace(call?.Name) ? "<unknown>" : call.Name;
                            var id = string.IsNullOrWhiteSpace(call?.Id) ? string.Empty : $" (id: {call.Id})";
                            diagnostics.Add(new AIRuntimeMessage(m.Severity, m.Origin, $"[tool:{prefix}{id}] {m.Message}"));
                        }
                    }
                }
            }

            if (diagnostics.Count == 0)
            {
                return;
            }

            // 1) Attach to shared policy diagnostics for tracing
            context.Diagnostics.AddRange(diagnostics);

            // 2) Attach to request-level messages so they are surfaced and can influence gating
            var merged = new List<AIRuntimeMessage>(rq.Messages ?? new List<AIRuntimeMessage>());
            merged.AddRange(diagnostics);
            rq.Messages = merged;

            return;
        }
    }
}
