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
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Policies.Request;

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
            pipeline.RequestPolicies.Add(new SchemaAttachRequestPolicy());
            // Response policies: start with compatibility decode to preserve behavior until new mappers are introduced
            pipeline.ResponsePolicies.Add(new Response.CompatibilityDecodeResponsePolicy());
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
                    // Non-fatal: convert to diagnostic
                    request?.Body?.AddInteraction("System", $"Request policy {policy.GetType().Name} failed: {ex.Message}");
                    Debug.WriteLine($"[PolicyPipeline] Request policy {policy.GetType().Name} exception: {ex}");
                }
            }
        }

        public async Task ApplyResponsePoliciesAsync(AIReturn response)
        {
            if (response == null) return;
            var context = new PolicyContext { Request = response.Request as AIRequestCall, Response = response };
            foreach (var policy in this.ResponsePolicies)
            {
                try
                {
                    await policy.ApplyAsync(context).ConfigureAwait(false);
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
