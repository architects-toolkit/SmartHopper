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
        public bool Success => string.IsNullOrEmpty(this.ErrorMessage);

        /// <inheritdoc/>
        public (bool IsValid, List<string> Errors) IsValid()
        {
            var errors = new List<string>();

            if (this.Request == null)
            {
                errors.Add("Request must not be null");
            }
            else
            {
                var (rqOk, rqErr) = this.Request.IsValid();
                if (!rqOk)
                {
                    errors.AddRange(rqErr);
                }
            }

            if (this.Metrics == null)
            {
                errors.Add("Metrics must not be null");
            }
            else
            {
                var (mOk, mErr) = this.Metrics.IsValid();
                if (!mOk)
                {
                    errors.AddRange(mErr);
                }
            }

            if (this.Body == null && this.ErrorMessage == null)
            {
                errors.Add("Either body or error message must be set");
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
        public static JObject ToJObject<T>(this AIReturn aireturn, Dictionary<string, string> fields = null)
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
