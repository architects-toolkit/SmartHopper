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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Core.Returns
{
    /// <summary>
    /// Generic container for provider responses, carrying processed and raw results,
    /// request context, metrics, tool calls, status and error details.
    /// Implements <see cref="IAIReturn"/>.
    /// </summary>
    public class AIReturn : IAIReturn
    {
        /// <summary>
        /// Gets or sets the internal storage for the raw response.
        /// </summary>
        private JObject PrivateEncodedResult { get; set; }

        /// <summary>
        /// Gets or sets the internal storage for structured messages.
        /// </summary>
        private List<SHRuntimeMessage> PrivateStructuredMessages { get; set; } = new List<SHRuntimeMessage>();

        /// <inheritdoc/>
        public AIBody Body { get; private set; } = AIBody.Empty;

        /// <inheritdoc/>
        public IAIRequest Request { get; set; }

        /// <summary>
        /// When true, <see cref="IsValid"/> does not emit a "Request must not be null" error
        /// when <see cref="Request"/> is null. Intended for synthetic AIReturn instances used
        /// purely to relay already-captured messages (e.g. per-item batch diagnostics) where
        /// no originating request exists.
        /// </summary>
        public bool SkipRequestValidation { get; set; }

        /// <summary>
        /// When true, <see cref="IsValid"/> does not validate <see cref="Metrics"/> (neither
        /// the null check nor the deep metrics validation). Mirrors
        /// <see cref="AIRequestBase.SkipMetricsValidation"/> but is available directly on the
        /// return, so it can be used without attaching a synthetic request.
        /// </summary>
        public bool SkipMetricsValidation { get; set; }

        /// <inheritdoc/>
        public AIMetrics Metrics
        {
            get
            {
                return this.Body.Metrics;
            }
        }

        /// <inheritdoc/>
        public AICallStatus Status { get; set; } = AICallStatus.Idle;

        /// <inheritdoc/>
        public List<SHRuntimeMessage> Messages
        {
            get
            {
                // Build a combined list without mutating private storage to avoid duplicates across calls
                var combined = new List<SHRuntimeMessage>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                // 1) Structured messages already added by code paths
                if (this.PrivateStructuredMessages != null)
                {
                    foreach (var m in this.PrivateStructuredMessages)
                    {
                        if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                        {
                            combined.Add(m);
                        }
                    }
                }

                // 2) Include body messages (aggregated from interactions and body validation)
                if (this.Body != null && this.Body.Messages != null)
                {
                    foreach (var m in this.Body.Messages)
                    {
                        if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                        {
                            combined.Add(m);
                        }
                    }
                }

                // 3) Include request messages (request computes validation dynamically)
                if (this.Request != null && this.Request.Messages != null)
                {
                    foreach (var m in this.Request.Messages)
                    {
                        if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                        {
                            combined.Add(m);
                        }
                    }
                }

                // 4) Add this return's validation messages dynamically (do not store), but only if validation is not skipped
                if (!(this.SkipRequestValidation && this.SkipMetricsValidation))
                {
                    var (isValid, errors) = this.IsValid();
                    if (!isValid && errors != null)
                    {
                        foreach (var m in errors)
                        {
                            if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                            {
                                combined.Add(m);
                            }
                        }
                    }
                }

                // 5) Sort by severity: Error > Warning > Info
                int Rank(SHRuntimeMessageSeverity s) => s == SHRuntimeMessageSeverity.Error ? 3 : (s == SHRuntimeMessageSeverity.Warning ? 2 : 1);
                combined.Sort((a, b) => Rank(b.Severity).CompareTo(Rank(a.Severity)));

                return combined;
            }

            set
            {
                this.PrivateStructuredMessages = value ?? new List<SHRuntimeMessage>();
            }
        }

        /// <inheritdoc/>
        public bool Success
        {
            get
            {
                // Computed from structured messages - no errors = success
                return !this.Messages.Any(m => m != null && m.Severity == SHRuntimeMessageSeverity.Error);
            }
        }

        /// <summary>
        /// Gets the raw JSON returned by the provider, if available.
        /// </summary>
        /// <returns>The raw provider response as a JObject, or null when unavailable.</returns>
        public JObject Raw => this.PrivateEncodedResult;

        /// <inheritdoc/>
        public (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
        {
            var errors = new List<SHRuntimeMessage>();

            if (this.Request == null && !this.SkipRequestValidation)
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Return,
                    SHMessageCode.ReturnInvalid,
                    "Request must not be null"));
            }

            var metrics = this.Metrics;
            var skipMetricsValidation = this.SkipMetricsValidation
                || (this.Request as AIRequestBase)?.SkipMetricsValidation == true;

            if (metrics == null && !skipMetricsValidation)
            {
                errors.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Return,
                    SHMessageCode.ReturnInvalid,
                    "Metrics must not be null"));
            }
            else if (metrics != null && !skipMetricsValidation)
            {
                var (mOk, mErr) = metrics.IsValid();
                if (mErr != null)
                {
                    errors.AddRange(mErr);
                }
            }

            if (this.Body == AIBody.Empty && !this.PrivateStructuredMessages.Any())
            {
                errors.Add(new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, SHMessageCode.ReturnInvalid, "Either body or messages must be set"));
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Creates a new successful result.
        /// </summary>
        /// <param name="body">The result value.</param>
        /// <param name="request">The request that generated the result.</param>
        public void CreateSuccess(AIBody body, IAIRequest? request = null)
        {
            var req = request ?? this.Request ?? new AIRequestCall();

            // Get provider and model from request
            var provider = req.Provider ?? "Unknown";
            var model = req.Model ?? "Unknown";

            // Extract and update provider and model from interactions
            foreach (var interaction in body.Interactions)
            {
                interaction.Metrics.Provider = provider;
                interaction.Metrics.Model = model;
            }

            // Create a new body from new interactions
            body = AIBodyBuilder.FromImmutable(body)
                .ReplaceLastRange(body.Interactions)
                .Build();

            this.Body = body;
            this.Request = req;
            this.Status = AICallStatus.Finished;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AIReturn.CreateSuccess(body)] finalized body: interactions={this.Body?.InteractionsCount ?? 0}, new={string.Join(",", this.Body?.InteractionsNew ?? new List<int>())}");
            }
            catch
            {
                /* logging only */
            }
        }

        /// <summary>
        /// Creates a new successful result.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="request">The request that generated the result.</param>
        /// <param name="metrics">The metrics from the response.</param>
        public void CreateSuccess(List<IAIInteraction> result, IAIRequest? request = null, AIMetrics? metrics = null)
        {
            var req = request ?? this.Request ?? new AIRequestCall();

            // Get provider and model from request
            var provider = req.Provider ?? "Unknown";
            var model = req.Model ?? "Unknown";

            // Update provider and model to interactions
            foreach (var interaction in result)
            {
                interaction.Metrics.Provider = provider;
                interaction.Metrics.Model = model;
            }

            // Build immutable body from interactions; metrics are aggregated within the immutable body.
            var body = AIBodyBuilder.Create()
                .AddRange(result)
                .Build();

            this.CreateSuccess(body, req);
        }

        /// <summary>
        /// Creates a new successful result from the raw response.
        /// </summary>
        /// <param name="raw">The raw response from the provider.</param>
        /// <param name="request">The request that generated the result.</param>
        public void CreateSuccess(JObject raw, IAIRequest request)
        {
            this.Request = request;
            this.Status = AICallStatus.Finished;
            this.SetBody(raw);
        }

        /// <summary>
        /// Creates a new error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="request">The request that generated the error.</param>
        /// <param name="metrics">Optional metrics associated with the error; if null, a new metrics instance may be created downstream.</param>
        public void CreateError(string message, IAIRequest? request = null, AIMetrics? metrics = null)
        {
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            this.Request = request;
            this.Status = AICallStatus.Finished;

            // Add structured error message instead of setting ErrorMessage directly
            this.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Return, message);

            this.Body = AIBodyBuilder.Create()
                .AddError(message, metrics)
                .Build();
        }

        /// <summary>
        /// Creates a standardized provider error.
        /// Adds a structured message with Provider origin for consistent UI surfacing.
        /// </summary>
        /// <param name="rawMessage">Raw provider error message.</param>
        /// <param name="request">The request context.</param>
        public void CreateProviderError(string rawMessage, IAIRequest? request = null)
        {
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            this.Request = request;
            this.Status = AICallStatus.Finished;

            // Add structured message with Provider origin (not calling CreateError to avoid Return origin)
            this.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Provider, $"Provider error: {rawMessage}");

            this.Body = AIBodyBuilder.Create()
                .AddError(rawMessage, null)
                .Build();
        }

        /// <summary>
        /// Creates a standardized network error (e.g., DNS, connectivity).
        /// Adds a structured message with Network origin.
        /// </summary>
        /// <param name="rawMessage">The raw network error message.</param>
        /// <param name="request">The request context.</param>
        public void CreateNetworkError(string rawMessage, IAIRequest? request = null)
        {
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            this.Request = request;
            this.Status = AICallStatus.Finished;

            // Add structured message with Network origin
            this.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Network, $"Network error: {rawMessage}");

            this.Body = AIBodyBuilder.Create()
                .AddError(rawMessage, null)
                .Build();
        }

        /// <summary>
        /// Creates a standardized tool error.
        /// Adds a structured message with Tool origin.
        /// </summary>
        /// <param name="rawMessage">The raw tool error message.</param>
        /// <param name="request">The request context.</param>
        public void CreateToolError(string rawMessage, IAIRequest? request = null)
        {
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            this.Request = request;
            this.Status = AICallStatus.Finished;

            // Add structured message with Tool origin
            this.AddRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Tool, $"Tool error: {rawMessage}");

            this.Body = AIBodyBuilder.Create()
                .AddError(rawMessage, null)
                .Build();
        }

        /// <summary>
        /// Adds a structured message to this return without modifying <see cref="ErrorMessage"/>.
        /// </summary>
        /// <param name="severity">The severity of the message (error, warning, info).</param>
        /// <param name="origin">The origin of the message (provider, tool, return, network, etc.).</param>
        /// <param name="text">The message content to add.</param>
        public void AddRuntimeMessage(SHRuntimeMessageSeverity severity, SHRuntimeMessageOrigin origin, string text)
        {
            this.PrivateStructuredMessages.Add(new SHRuntimeMessage(severity, origin, SHMessageCode.Unknown, text ?? string.Empty));
        }

        /// <inheritdoc/>
        public void SetBody(AIBody body)
        {
            this.Body = body;
        }

        /// <summary>
        /// Sets the result from a list of interactions.
        /// </summary>
        /// <param name="interactions">The list of interactions to set as result.</param>
        public void SetBody(List<IAIInteraction> interactions)
        {
            this.Body = AIBodyBuilder.Create()
                .AddRange(interactions)
                .Build();
        }

        /// <summary>
        /// Sets the result from a raw response.
        /// </summary>
        /// <param name="raw">The raw response from the provider.</param>
        public void SetBody(JObject raw)
        {
            // Request must be set before calling this method
            if (this.Request == null)
            {
                throw new InvalidOperationException("Request is null and required for decoding raw response");
            }

            this.PrivateEncodedResult = raw;

            // Build the new immutable body
            var b = new AIBodyBuilder();

            // Call provider-specific decoder
            var interactions = this.Request.ProviderInstance.Decode(raw);

            // Get provider and model from request
            var provider = this.Request.Provider ?? "Unknown";
            var model = this.Request.Model ?? "Unknown";

            // Update provider and model to interactions
            foreach (var interaction in interactions)
            {
                interaction.Metrics.Provider = provider;
                interaction.Metrics.Model = model;
            }

            b.AddRange(interactions);

            this.Body = b.Build();
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AIReturn.SetBody(raw)] built body: interactions={this.Body?.InteractionsCount ?? 0}, new={string.Join(",", this.Body?.InteractionsNew ?? new List<int>())}");
            }
            catch
            {
                /* logging only */
            }
        }

        /// <summary>
        /// Sets the completion time to the last interaction.
        /// </summary>
        /// <param name="completionTime">The completion time to set.</param>
        public void SetCompletionTime(double completionTime)
        {
            this.Body = AIBodyBuilder.FromImmutable(this.Body)
                .SetCompletionTime(completionTime)
                .Build();
        }
    }
}
