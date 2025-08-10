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
using System.Reflection;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Generic container for provider responses, carrying processed and raw results,
    /// request context, metrics, tool calls, status and error details.
    /// Implements <see cref="IAIReturn"/>.
    /// </summary>
    public class AIReturn : IAIReturn
    {
        /// <inheritdoc/>
        public List<IAIInteraction> Result {
            get => 
                {
                    var (valid, errors) = this.IsValid();
                    if (valid)
                    {
                        return this.Request.ProviderInstance.Decode(this.EncodedResult);
                    }
                    else
                    {
                        return new List<IAIInteraction>();
                    }
                };
            set =>
                {
                    this.EncodedResult = this.Request.ProviderInstance.Encode(value);
                };
            }

        /// <inheritdoc/>
        public string EncodedResult { get; set; }

        /// <inheritdoc/>
        public IAIRequest Request { get; set; }

        /// <inheritdoc/>
        public AIMetrics Metrics { get; set; }

        /// <inheritdoc/>
        public List<AIToolCall> ToolCalls { get; set; } = new List<AIToolCall>();

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

            if (this.Result == null && this.EncodedResult == null && this.ErrorMessage == null)
            {
                errors.Add("Either result or error message must be set");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Creates a new successful result.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="request">The request that generated the result.</param>
        /// <param name="metrics">The metrics from the response.</param>
        /// <returns>A new success result instance.</returns>
        public static AIReturn CreateSuccess(T result, IAIRequest? request = null, AIMetrics? metrics = null)
        {
            if (request == null)
            {
                request = new AIRequest();
            }

            if (metrics == null)
            {
                metrics = new AIMetrics();
            }

            return new AIReturn
            {
                Result = result,
                Request = request,
                Status = AICallStatus.Finished,
                Metrics = metrics,
            };
        }

        /// <summary>
        /// Creates a new successful result from the raw response.
        /// </summary>
        /// <param name="raw">The raw response from the provider.</param>
        /// <param name="request">The request that generated the result.</param>
        /// <param name="metrics">The metrics from the response.</param>
        /// <returns>A new success result instance.</returns>
        public static AIReturn<T> CreateRawSuccess(string raw, IAIRequest? request = null, AIMetrics? metrics = null)
        {
            if (request == null)
            {
                request = new AIRequest();
            }

            if (metrics == null)
            {
                metrics = new AIMetrics();
            }

            return new AIReturn<T>
            {
                EncodedResult = raw,
                Request = request,
                Status = AICallStatus.Finished,
                Metrics = metrics,
            };
        }

        /// <summary>
        /// Creates a new error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="request">The request that generated the error.</param>
        /// <param name="metrics">Optional metrics from the response that may have caused the error.</param>
        /// <returns>A new error result instance.</returns>
        public static AIReturn<T> CreateError(string message, IAIRequest? request = null, AIMetrics? metrics = null)
        {
            if (request == null)
            {
                request = new AIRequest();
            }

            if (metrics == null)
            {
                metrics = new AIMetrics();
            }

            metrics.FinishReason = "error";

            return new AIReturn<T>
            {
                Request = request,
                Metrics = metrics,
                ErrorMessage = message,
                Status = AICallStatus.Finished,
            };
        }
    }

    /// <summary>
    /// Extension methods for AIReturn<T>.
    /// </summary>
    public static class AIReturnExtensions
    {
        /// <summary>
        /// Build standardized result as JObject using reflection to map properties.
        /// </summary>
        /// <param name="aireturn">The AIReturn instance.</param>
        /// <param name="fields">Dictionary mapping JSON key names to field paths. Use "Request.PropertyName" or "Metrics.PropertyName" for nested properties.</param>
        /// <returns>JObject with mapped values.</returns>
        public static JObject ToJObject<T>(this AIReturn<T> aireturn, Dictionary<string, string> fields = null)
        {
            fields ??= new Dictionary<string, string>
            {
                ["success"] = "Success",
                ["result"] = "Result",
                ["error"] = "ErrorMessage",
            };

            var jo = new JObject();
            var aiReturnType = aireturn.GetType();
            var requestType = aireturn.Request?.GetType() ?? typeof(AIRequest);
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
                        // Handle AIReturn<T> properties
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
