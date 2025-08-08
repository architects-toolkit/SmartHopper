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
    public class AIRequest : IAIRequest
    {
        /// <inheritdoc/>
        public string Provider { get; set; }

        /// <inheritdoc/>
        public IAIProvider ProviderInstance { get => ProviderManager.Instance.GetProvider(this.Provider); }

        /// <inheritdoc/>
        public string Model { get; set; }

        /// <inheritdoc/>
        public AICapability Capability { get; set; } = AICapability.None;

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
        public (bool IsValid, List<string> Errors) IsValid()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(this.Provider))
            {
                errors.Add("Provider is required");
            }

            if (string.IsNullOrEmpty(this.Model))
            {
                errors.Add("Model is required");
            }

            if (string.IsNullOrEmpty(this.Endpoint))
            {
                errors.Add("Endpoint is required");
            }

            if (string.IsNullOrEmpty(this.HttpMethod))
            {
                errors.Add("HttpMethod is required");
            }

            if (string.IsNullOrEmpty(this.Authentication))
            {
                errors.Add("Authentication method is required");
            }

            if (!this.Capability.HasInput() || !this.Capability.HasOutput())
            {
                errors.Add("Capability field is required with both input and output capabilities");
            }

            if (this.Capability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
            {
                errors.Add("JsonOutput capability requires a non-empty JsonOutputSchema");
            }

            var (bodyOk, bodyErr) = this.Body.IsValid();
            if (!bodyOk)
            {
                errors.AddRange(bodyErr);
            }

            if (this.ProviderInstance == null)
            {
                errors.Add($"Unknown provider '{this.Provider}'");
            }

            // TODO: Check valid model capabilities

            return (errors.Count == 0, errors);
        }

        /// <inheritdoc/>
        public async Task<AIReturn<T>> Do<T>()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Use default model if none specified
            if (string.IsNullOrWhiteSpace(this.Model))
            {
                // If jsonSchema is required -> use JsonOutput capability
                // If toolFilter is not null -> use FunctionCalling capability
                if (this.Body.RequiresJsonOutput())
                {
                    this.Model = this.ProviderInstance.GetDefaultModel(AICapability.JsonGenerator, false);
                }
                else if (!string.IsNullOrEmpty(this.Body.ToolFilter))
                {
                    this.Model = this.ProviderInstance.GetDefaultModel(AICapability.FunctionCalling, false);
                }
                else
                {
                    this.Model = this.ProviderInstance.GetDefaultModel(AICapability.BasicChat, false);
                }

                Debug.WriteLine($"[AIRequest.Do] No model specified, using provider's default model: {this.Model}");
            }

            // TODO: Replace model with capable model is not capable to perform this request

            Debug.WriteLine($"[AIRequest.Do] Calling {this.Provider} provider with model '{this.Model}' and tools filtered by {this.Body.ToolFilter ?? "null"}");

            try
            {
                // Execute the request from the provider
                return await this.ProviderInstance.Call<T>(this).ConfigureAwait(false);
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
    }
}
