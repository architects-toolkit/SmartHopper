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
    public class AIRequest : IAIRequest
    {
        /// <summary>
        /// Store the desired model.
        /// </summary>
        private string model;
        
        /// <inheritdoc/>
        public string Provider { get; set; }

        /// <inheritdoc/>
        public IAIProvider ProviderInstance { get => ProviderManager.Instance.GetProvider(this.Provider); }

        /// <inheritdoc/>
        public string Model { get => this.GetModelToUse(); set => this.model = value; }

        /// <inheritdoc/>
        public AICapability Capability { get; set; } = AICapability.BasicChat;

        /// <inheritdoc/>
        public string Endpoint { get; set; }

        /// <inheritdoc/>
        public string HttpMethod { get; set; } = "POST";

        /// <inheritdoc/>
        public string Authentication { get; set; } = "bearer";

        /// <inheritdoc/>
        public string ContentType { get; set; } = "application/json";

        /// <inheritdoc/>
        public IAIRequestBody Body { get; set; }

        /// <inheritdoc/>
        public string EncodedRequest { get => 
            {
                var (valid, errors) = this.IsValid();
                if (valid)
                {
                    return this.ProviderInstance.Encode(this);
                }
                else
                {
                    return string.Empty;
                }
            }; }

        /// <inheritdoc/>
        public (bool IsValid, List<string> Errors) IsValid()
        {
            var messages = new List<string>();
            bool hasErrors = false;
            var effectiveCapability = this.GetEffectiveCapabilities(out var capabilityNotes);

            // Append any capability notes (informational)
            if (capabilityNotes.Count > 0)
            {
                messages.AddRange(capabilityNotes);
            }

            if (string.IsNullOrEmpty(this.Provider))
            {
                messages.Add("Provider is required");
                hasErrors = true;
            }

            if (string.IsNullOrEmpty(this.model))
            {
                messages.Add($"(Info) Model is not specified - the default model '{this.GetModelToUse()}' will be used");
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

            if (!effectiveCapability.HasInput() || !effectiveCapability.HasOutput())
            {
                messages.Add("Capability field is required with both input and output capabilities");
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

            if (this.ProviderInstance == null)
            {
                messages.Add($"Unknown provider '{this.Provider}'");
                hasErrors = true;
            }

            if (!string.IsNullOrEmpty(this.model) && this.model != this.GetModelToUse())
            {
                messages.Add($"(Info) Model '{this.model}' is not capable for this request - the default model '{this.GetModelToUse()}' will be used");
            }

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public async Task<AIReturn<T>> Do<T>()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Debug.WriteLine($"[AIRequest.Do] Loading getResponse from {this.Provider} with model '{this.Model}' and tools filtered by {this.Body?.ToolFilter ?? "null"}");

            try
            {
                // Guard against missing provider
                if (this.ProviderInstance == null)
                {
                    stopwatch.Stop();
                    return new AIReturn<T>
                    {
                        Metrics = new AIMetrics()
                        {
                            FinishReason = "error",
                            CompletionTime = stopwatch.Elapsed.TotalSeconds,
                        },
                        Status = AICallStatus.Finished,
                        ErrorMessage = $"Error: Unknown provider '{this.Provider}'",
                    };
                }

                this = this.ProviderInstance.Encode(this);

                // Execute the request from the provider
                var result = await this.ProviderInstance.Call<T>(this).ConfigureAwait(false);

                this = this.Provide

                return (AIReturn<T>)result;
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                var error = $"Error: API request failed - {ex.Message}";

                return new AIReturn<T>
                {
                    Metrics = new AIMetrics()
                    {
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    },
                    Status = AICallStatus.Finished,
                    ErrorMessage = error,
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var error = $"Error: {ex.Message}";

                return new AIReturn<T>
                {
                    Metrics = new AIMetrics()
                    {
                        FinishReason = "error",
                        CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    },
                    Status = AICallStatus.Finished,
                    ErrorMessage = error,
                };
            }
        }


        /// <summary>
        /// Gets the model to use for the request.
        /// </summary>
        private string GetModelToUse()
        {
            if (string.IsNullOrEmpty(this.Provider))
            {
                return null;
            }

            var provider = this.ProviderInstance;
            if (provider == null)
            {
                return null;
            }

            var defaultModel = provider.GetDefaultModel(this.GetEffectiveCapabilities(out _));

            if (string.IsNullOrEmpty(this.model))
            {
                return defaultModel;
            }

            // Validate capabilities and return default if not capable
            if (!this.ValidModelCapabilities())
            {
                return defaultModel;
            }

            return this.model;
        }

        /// <summary>
        /// Validates the model capabilities and mentions the default model that will be used if the specified model is not capable to perform this request.
        /// </summary>
        private bool ValidModelCapabilities()
        {
            if (string.IsNullOrEmpty(this.Provider))
            {
                return false;
            }

            if (string.IsNullOrEmpty(this.model))
            {
                return false;
            }

            // Validate capabilities
            var capabilities = this.GetEffectiveCapabilities(out _);
            bool valid = ModelManager.Instance.ValidateCapabilities(this.Provider, this.model, capabilities);
            return valid;
        }

        /// <summary>
        /// Computes the effective capabilities for this request, augmenting with additional flags
        /// implied by the body (e.g., JsonOutput when a schema is provided, FunctionCalling when tools are requested).
        /// Returns the effective capabilities and a list of informational notes describing adjustments.
        /// </summary>
        private AICapability GetEffectiveCapabilities(out List<string> notes)
        {
            notes = new List<string>();
            var effective = this.Capability;

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
