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
using System.Net.Http;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents a fully-specified AI request that providers can execute, including
    /// provider information, model resolution, capability validation, and request body.
    /// </summary>
    public class AIRequestCall : AIRequestBase
    {
        private AICapability capability = AICapability.BasicChat;

        /// <inheritdoc/>
        public override AICapability Capability
        {
            get => this.GetEffectiveCapabilities(out _);
            set => this.capability = value;
        }

        /// <summary>
        /// Gets or sets the HTTP method to use for the request.
        /// </summary>
        public string HttpMethod { get; set; } = "POST";

        /// <summary>
        /// Gets or sets the authentication method to use for the request.
        /// </summary>
        public string Authentication { get; set; } = "bearer";

        /// <summary>
        /// Gets or sets the content type to use for the request.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        public AIBody Body { get; set; }

        /// <summary>
        /// Gets the encoded request for the specified provider.
        /// </summary>
        public string EncodedRequestBody {
            get {
                var (valid, errors) = this.IsValid();
                if (valid)
                {
                    return this.ProviderInstance.Encode(this);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> Errors) IsValid()
        {
            var messages = new List<string>();
            bool hasErrors = false;

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
                hasErrors = true;
            }

            var effectiveCapability = this.GetEffectiveCapabilities(out var capabilityNotes);

            // Append any capability notes (informational)
            if (capabilityNotes.Count > 0)
            {
                messages.AddRange(capabilityNotes);
            }

            if (string.IsNullOrEmpty(this.Endpoint))
            {
                messages.Add("Endpoint is required");
                hasErrors = true;
            }

            if (string.IsNullOrEmpty(this.HttpMethod))
            {
                messages.Add("HttpMethod is required");
                hasErrors = true;
            }

            if (string.IsNullOrEmpty(this.Authentication))
            {
                messages.Add("Authentication method is required");
                hasErrors = true;
            }

            if (this.Body == null)
            {
                messages.Add("Body is required");
                hasErrors = true;
            }
            else
            {
                if (effectiveCapability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
                {
                    messages.Add("JsonOutput capability requires a non-empty JsonOutputSchema");
                    hasErrors = true;
                }

                var (bodyOk, bodyErr) = this.Body.IsValid();
                if (!bodyOk)
                {
                    messages.AddRange(bodyErr);
                    hasErrors = true;
                }
            }

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public override async Task<AIReturn> Exec()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Debug.WriteLine($"[AIRequest.Do] Loading Call method from {this.Provider} with model '{this.Model}' and tools filtered by {this.Body?.ToolFilter ?? "null"}");

            try
            {
                // Guard against missing provider
                if (this.ProviderInstance == null)
                {
                    stopwatch.Stop();
                    var errorMetrics = new AIMetrics
                    {
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    };

                    var errorResult = AIReturn.CreateError("Provider is missing", this, metrics: errorMetrics);

                    return errorResult;
                }

                // Execute the request from the provider
                var result = await this.ProviderInstance.Call(this).ConfigureAwait(false);

                return (AIReturn)result;
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                var error = $"Error: API request failed - {ex.Message}";

                var errorMetrics = new AIMetrics
                {
                    FinishReason = "error",
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };

                var errorResult = AIReturn.CreateError(error, this, metrics: errorMetrics);

                return errorResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var error = $"Error: {ex.Message}";

                var errorMetrics = new AIMetrics
                {
                    FinishReason = "error",
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };

                var errorResult = AIReturn.CreateError(error, this, metrics: errorMetrics);

                return errorResult;
            }
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public void Initialize(string provider, string model,  AIBody body, string endpoint = string.Empty, AICapability capability = AICapability.TextOutput)
        {
            this.Provider = provider;
            this.Model = model;
            this.Endpoint = endpoint;
            this.Body = body;
            this.Capability = capability;
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public void Initialize(string provider, string model,  List<IAIInteraction> interactions, string endpoint = string.Empty, AICapability capability = AICapability.TextOutput, string toolFilter = null)
        {
            var body = new AIBody(interactions, toolFilter);
            this.Initialize(provider, model, body, endpoint, capability);
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public void Initialize(string provider, string model,  string systemPrompt, string endpoint = string.Empty, AICapability capability = AICapability.TextOutput, string toolFilter = null)
        {
            var interactionList = new List<IAIInteraction>();
            interactionList.Add(new AIInteractionText { Role = "system", Content = systemPrompt });

            this.Initialize(provider, model, interactionList, endpoint, capability, toolFilter);
        }

        /// <summary>
        /// Replace the interactions list from the body.
        /// </summary>
        public void OverrideInteractions(List<IAIInteraction> interactions)
        {
            this.Body.OverrideInteractions(interactions);
        }

        /// <summary>
        /// Computes the effective capabilities for this request, augmenting with additional flags
        /// implied by the body (e.g., JsonOutput when a schema is provided, FunctionCalling when tools are requested).
        /// Returns the effective capabilities and a list of informational notes describing adjustments.
        /// </summary>
        private AICapability GetEffectiveCapabilities(out List<string> notes)
        {
            notes = new List<string>();
            var effective = this.capability;

            // If body requires JSON output but capability lacks it, add it (informational)
            if (this.Body?.RequiresJsonOutput() == true && !effective.HasFlag(AICapability.JsonOutput))
            {
                effective |= AICapability.JsonOutput;
                notes.Add("(Info) Body requires JSON output but Capability lacks JsonOutput - treating request as JsonOutput");
            }

            // If tools are requested but capability lacks FunctionCalling, add it (informational)
            if (!string.IsNullOrEmpty(this.Body?.ToolFilter) && !effective.HasFlag(AICapability.FunctionCalling))
            {
                effective |= AICapability.FunctionCalling;
                notes.Add("(Info) Tool filter provided but Capability lacks FunctionCalling - treating request as requiring FunctionCalling");
            }

            return effective;
        }
    }
}
