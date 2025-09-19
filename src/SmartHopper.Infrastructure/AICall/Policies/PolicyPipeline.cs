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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Policies.Request;
using SmartHopper.Infrastructure.AICall.Tools;

namespace SmartHopper.Infrastructure.AICall.Policies
{
    /// <summary>
    /// Orchestrates execution of request/response policies.
    /// </summary>
    public sealed class PolicyPipeline
    {
        public List<IRequestPolicy> RequestPolicies { get; } = new List<IRequestPolicy>();
        public List<IResponsePolicy> ResponsePolicies { get; } = new List<IResponsePolicy>();

        public static PolicyPipeline Default { get; } = CreateDefault();

        private static PolicyPipeline CreateDefault()
        {
            var pipeline = new PolicyPipeline();

            // Request policies can be added here as they are implemented (context injection, capability enforcement, schema attach...)
            pipeline.RequestPolicies.Add(new Request.RequestTimeoutPolicy());
            pipeline.RequestPolicies.Add(new Request.ToolFilterNormalizationRequestPolicy());
            pipeline.RequestPolicies.Add(new Request.AIToolValidationRequestPolicy());
            pipeline.RequestPolicies.Add(new Request.ContextInjectionRequestPolicy());
            pipeline.RequestPolicies.Add(new SchemaAttachRequestPolicy());
            pipeline.RequestPolicies.Add(new SchemaValidateRequestPolicy());

            // Response policies: start with compatibility decode to preserve behavior until new mappers are introduced
            pipeline.ResponsePolicies.Add(new Response.SchemaValidateResponsePolicy());
            pipeline.ResponsePolicies.Add(new Response.FinishReasonNormalizeResponsePolicy());
            return pipeline;
        }

        public async Task ApplyRequestPoliciesAsync(AIRequestCall request)
        {
            if (request == null) return;
            var context = new PolicyContext { Request = request };
            foreach (var policy in this.RequestPolicies)
            {
                try
                {
                    await policy.ApplyAsync(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Non-fatal: convert to diagnostic (immutable)
                    if (request?.Body != null)
                    {
                        request.Body = AIBodyBuilder.FromImmutable(request.Body)
                            .AddSystem($"Request policy {policy.GetType().Name} failed: {ex.Message}")
                            .Build();
                    }
                    Debug.WriteLine($"[PolicyPipeline] Request policy {policy.GetType().Name} exception: {ex}");
                }
            }
        }

        /// <summary>
        /// Applies a minimal set of request policies for tool-only requests by shimming the tool call
        /// into a transient <see cref="AIRequestCall"/>. Non-breaking: existing policies continue to target
        /// <see cref="AIRequestCall"/>; this overload simply reuses them for tool validation and timeout normalization.
        /// </summary>
        public async Task ApplyRequestPoliciesAsync(AIToolCall toolCall)
        {
            if (toolCall == null) return;

            // Build a transient AIRequestCall carrying over relevant context
            var shim = new AIRequestCall
            {
                Provider = toolCall.Provider,
                Model = toolCall.Model,
                Endpoint = toolCall.Endpoint,
                Capability = toolCall.Capability,
                Body = toolCall.Body,
                TimeoutSeconds = toolCall.TimeoutSeconds,
            };

            // Run a minimal, safe subset of request policies relevant to tool calls
            var policies = new List<IRequestPolicy>
            {
                new Request.RequestTimeoutPolicy(),
                new Request.AIToolValidationRequestPolicy(),
            };

            var context = new PolicyContext { Request = shim };
            foreach (var policy in policies)
            {
                try
                {
                    await policy.ApplyAsync(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Non-fatal: convert to diagnostic on shim
                    if (shim?.Body != null)
                    {
                        shim.Body = AIBodyBuilder.FromImmutable(shim.Body)
                            .AddSystem($"Request policy {policy.GetType().Name} failed: {ex.Message}")
                            .Build();
                    }
                    Debug.WriteLine($"[PolicyPipeline] Tool request policy {policy.GetType().Name} exception: {ex}");
                }
            }

            // Merge back normalized fields and diagnostics
            toolCall.TimeoutSeconds = shim.TimeoutSeconds;
            if (shim.Body != null)
            {
                toolCall.Body = shim.Body;
            }

            // Merge request-level diagnostics into the tool call messages
            var merged = new List<AIRuntimeMessage>(toolCall.Messages ?? new List<AIRuntimeMessage>());
            if (shim.Messages != null && shim.Messages.Count > 0)
            {
                merged.AddRange(shim.Messages);
            }
            toolCall.Messages = merged;
        }

        public async Task ApplyResponsePoliciesAsync(AIReturn response)
        {
            if (response == null) return;
            var context = new PolicyContext { Request = response.Request as AIRequestCall, Response = response };
            foreach (var policy in this.ResponsePolicies)
            {
                try
                {
                    try
                    {
                        Debug.WriteLine($"[PolicyPipeline] before {policy.GetType().Name}: interactions={response?.Body?.InteractionsCount ?? 0}, new={string.Join(",", response?.Body?.InteractionsNew ?? new System.Collections.Generic.List<int>())}");
                    }
                    catch { /* logging only */ }
                    await policy.ApplyAsync(context).ConfigureAwait(false);
                    try
                    {
                        Debug.WriteLine($"[PolicyPipeline] after  {policy.GetType().Name}: interactions={response?.Body?.InteractionsCount ?? 0}, new={string.Join(",", response?.Body?.InteractionsNew ?? new System.Collections.Generic.List<int>())}");
                    }
                    catch { /* logging only */ }
                }
                catch (Exception ex)
                {
                    // Non-fatal: attach diagnostic to AIReturn
                    response.AddRuntimeMessage(
                        AIRuntimeMessageSeverity.Warning,
                        AIRuntimeMessageOrigin.Return,
                        $"Response policy {policy.GetType().Name} failed: {ex.Message}");
                    Debug.WriteLine($"[PolicyPipeline] Response policy {policy.GetType().Name} exception: {ex}");
                }
            }
        }
    }
}
