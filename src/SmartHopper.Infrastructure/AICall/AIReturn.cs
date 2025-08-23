/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Generic container for provider responses, carrying processed and raw results,
    /// request context, metrics, tool calls, status and error details.
    /// Implements <see cref="IAIReturn"/>.
    /// </summary>
    public class AIReturn : IAIReturn
    {
        /// <summary>
        /// Internal storage for the raw response.
        /// </summary>
        private JObject PrivateEncodedResult { get; set; }

        /// <summary>
        /// Internal storage for global metrics.
        /// </summary>
        private AIMetrics PrivateGlobalMetrics { get; set; } = new AIMetrics();

        /// <summary>
        /// Internal storage for structured messages.
        /// </summary>
        private List<AIRuntimeMessage> PrivateStructuredMessages { get; set; } = new List<AIRuntimeMessage>();

        /// <inheritdoc/>
        public AIBody Body { get; private set; } = new AIBody();

        /// <inheritdoc/>
        public IAIRequest Request { get; set; }

        /// <inheritdoc/>
        public AIMetrics Metrics
        {
            get
            {
                var metrics = this.Body.Metrics ?? new AIMetrics();
                metrics.Combine(this.PrivateGlobalMetrics);
                return metrics;
            }

            set
            {
                Debug.WriteLine($"[AIReturn] Setting global metrics: {value}");
                var metrics = this.PrivateGlobalMetrics ?? new AIMetrics();
                metrics.Combine(value);
                this.PrivateGlobalMetrics = metrics;
            }
        }

        /// <inheritdoc/>
        public AICallStatus Status { get; set; } = AICallStatus.Idle;

        /// <inheritdoc/>
        public string ErrorMessage { get; set; }

        /// <inheritdoc/>
        public List<AIRuntimeMessage> Messages
        {
            get
            {
                // Build a combined list without mutating private storage to avoid duplicates across calls
                var combined = new List<AIRuntimeMessage>();
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

                // 2) Reflect ErrorMessage as a structured error message (mirroring, without mutating storage)
                if (!string.IsNullOrEmpty(this.ErrorMessage))
                {
                    if (seen.Add(this.ErrorMessage))
                    {
                        combined.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Return, this.ErrorMessage));
                    }
                }

                // 3) Include body messages (aggregated from interactions and body validation)
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

                // 4) Include request messages (request computes validation dynamically)
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

                // 5) Add this return's validation messages dynamically (do not store)
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

                // 6) Sort by severity: Error > Warning > Info
                int Rank(AIRuntimeMessageSeverity s) => s == AIRuntimeMessageSeverity.Error ? 3 : (s == AIRuntimeMessageSeverity.Warning ? 2 : 1);
                combined.Sort((a, b) => Rank(b.Severity).CompareTo(Rank(a.Severity)));

                return combined;
            }
            set
            {
                this.PrivateStructuredMessages = value ?? new List<AIRuntimeMessage>();
            }
        }

        /// <inheritdoc/>
        public bool Success => string.IsNullOrEmpty(this.ErrorMessage);

        /// <inheritdoc/>
        public (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
        {
            var errors = new List<AIRuntimeMessage>();

            if (this.Request == null)
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Return, "Request must not be null"));
            }
            else
            {
                var (rqOk, rqErr) = this.Request.IsValid();
                if (rqErr != null)
                {
                    errors.AddRange(rqErr);
                }
            }

            if (this.Metrics == null)
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Return, "Metrics must not be null"));
            }
            else
            {
                var (mOk, mErr) = this.Metrics.IsValid();
                if (mErr != null)
                {
                    errors.AddRange(mErr);
                }
            }

            if (this.Body == null && this.ErrorMessage == null)
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Return, "Either body or error message must be set"));
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
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            this.Body = body;
            this.Request = request;
            this.Status = AICallStatus.Finished;
        }

        /// <summary>
        /// Creates a new successful result.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="request">The request that generated the result.</param>
        /// <param name="metrics">The metrics from the response.</param>
        public void CreateSuccess(List<IAIInteraction> result, IAIRequest? request = null, AIMetrics? metrics = null)
        {
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            if (metrics == null && this.Body.Metrics != null)
            {
                metrics = this.Body.Metrics;
            }
            else if (metrics == null)
            {
                metrics = new AIMetrics();
            }

            var body = new AIBody
            {
                Interactions = result,
            };

            this.CreateSuccess(body, request);
        }

        /// <summary>
        /// Creates a new successful result from the raw response.
        /// </summary>
        /// <param name="raw">The raw response from the provider.</param>
        /// <param name="request">The request that generated the result.</param>
        public void CreateSuccess(JObject raw, IAIRequest? request = null)
        {
            if (request == null && this.Request != null)
            {
                request = this.Request;
            }
            else if (request == null)
            {
                request = new AIRequestCall();
            }

            var result = new AIReturn
            {
                Request = request,
                Status = AICallStatus.Finished,
            };

            result.SetBody(raw);

            this.CreateSuccess(result.Body, request);
        }

        /// <summary>
        /// Creates a new error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="request">The request that generated the error.</param>
        public void CreateError(string message, IAIRequest? request = null)
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
            this.ErrorMessage = message;
            this.Status = AICallStatus.Finished;
        }

        /// <summary>
        /// Creates a standardized provider error while preserving the raw provider message in <see cref="ErrorMessage"/>.
        /// Adds a structured message "Provider error: ..." for consistent UI surfacing.
        /// </summary>
        /// <param name="rawMessage">Raw provider error message.</param>
        /// <param name="request">The request context.</param>
        public void CreateProviderError(string rawMessage, IAIRequest? request = null)
        {
            this.CreateError(rawMessage, request);
            this.AddRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Provider, $"Provider error: {rawMessage}");
        }

        /// <summary>
        /// Creates a standardized network error (e.g., DNS, connectivity) while preserving the raw message.
        /// </summary>
        public void CreateNetworkError(string rawMessage, IAIRequest? request = null)
        {
            this.CreateError(rawMessage, request);
            this.AddRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Network, $"Network error: {rawMessage}");
        }

        /// <summary>
        /// Creates a standardized tool error while preserving the raw message.
        /// </summary>
        public void CreateToolError(string rawMessage, IAIRequest? request = null)
        {
            this.CreateError(rawMessage, request);
            this.AddRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Tool, $"Tool error: {rawMessage}");
        }

        /// <summary>
        /// Adds a structured message to this return without modifying <see cref="ErrorMessage"/>.
        /// </summary>
        public void AddRuntimeMessage(AIRuntimeMessageSeverity severity, AIRuntimeMessageOrigin origin, string text)
        {
            this.PrivateStructuredMessages.Add(new AIRuntimeMessage(severity, origin, text ?? string.Empty));
        }

        /// <summary>
        /// Merges messages and error indicator from another return into this one.
        /// Does NOT copy the source ErrorMessage into this.ErrorMessage; it only surfaces it as a message.
        /// </summary>
        /// <param name="source">Source return to merge from.</param>
        /// <param name="assumedOrigin">Origin to tag merged messages with (for context).</param>
        protected void MergeRuntimeMessagesFrom(IAIReturn source, AIRuntimeMessageOrigin assumedOrigin = AIRuntimeMessageOrigin.Return)
        {
            if (source == null)
            {
                return;
            }

            // Merge structured messages from source (they already carry severity and origin)
            if (source.Messages != null)
            {
                foreach (var m in source.Messages)
                {
                    if (m != null)
                    {
                        this.PrivateStructuredMessages.Add(m);
                    }
                }
            }

            // If the source had an error, surface it as a structured message on this return with the provided origin
            if (!string.IsNullOrEmpty(source.ErrorMessage))
            {
                this.PrivateStructuredMessages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, assumedOrigin, source.ErrorMessage));
            }
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
            this.Body.Interactions = interactions;
        }

        /// <summary>
        /// Sets the result from a raw response.
        /// </summary>
        /// <param name="raw">The raw response from the provider.</param>
        public void SetBody(JObject raw)
        {
            this.PrivateEncodedResult = raw;

            // TODO: Most of the tools do not return json to decode, but json to directly add to the body.
            this.Body.Interactions = this.Request.ProviderInstance.Decode(raw.ToString());
        }
    }

    /// <summary>
    /// Extension methods for AIReturn.
    /// </summary>
    public static class AIReturnExtensions
    {
        /// <summary>
        /// Build standardized result as JObject using reflection to map properties.
        /// </summary>
        /// <param name="aireturn">The AIReturn instance.</param>
        /// <param name="fields">Dictionary mapping JSON key names to field paths. Use "Request.PropertyName" or "Metrics.PropertyName" for nested properties.</param>
        /// <returns>JObject with mapped values.</returns>
        public static JObject ToJObject(this AIReturn aireturn, Dictionary<string, string> fields = null)
        {
            fields ??= new Dictionary<string, string>
            {
                ["success"] = "Success",
                ["result"] = "Result",
                ["error"] = "ErrorMessage",
            };

            var jo = new JObject();
            var aiReturnType = aireturn.GetType();
            var requestType = aireturn.Request?.GetType() ?? typeof(AIRequestCall);
            var metricsType = aireturn.Metrics?.GetType() ?? typeof(AIMetrics);

            foreach (var (jsonKey, sourcePath) in fields)
            {
                object? value = null;
                JToken? token;

                try
                {
                    if (sourcePath.StartsWith("Request.", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle nested Response properties
                        var propName = sourcePath["Request.".Length..];
                        var requestProp = requestType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (requestProp != null && aireturn.Request != null)
                        {
                            value = requestProp.GetValue(aireturn.Request);
                        }
                    }
                    else if (sourcePath.StartsWith("Metrics.", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle nested Response properties
                        var propName = sourcePath["Metrics.".Length..];
                        var metricsProp = metricsType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (metricsProp != null && aireturn.Metrics != null)
                        {
                            value = metricsProp.GetValue(aireturn.Metrics);
                        }
                    }
                    else if (sourcePath == "Status")
                    {
                        // Handle AICallStatus properties
                        value = aireturn.Status.ToString();
                    }
                    else
                    {
                        // Handle AIReturn properties
                        var prop = aiReturnType.GetProperty(sourcePath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (prop != null)
                        {
                            value = prop.GetValue(aireturn);
                        }
                    }

                    // Convert value to JToken
                    token = value == null
                        ? JValue.CreateNull()
                        : (value is JToken jt ? jt : JToken.FromObject(value));
                }
                catch
                {
                    // If reflection fails, use null
                    token = JValue.CreateNull();
                }

                jo[jsonKey] = token;
            }

            return jo;
        }
    }
}
