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

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    public class AIReturn<T>
    {
        /// <summary>
        /// Gets or sets the raw response from the AI.
        /// </summary>
        public AIResponse Response { get; set; }

        /// <summary>
        /// Gets or sets the processed result value.
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// Gets or sets the error message if any occurred during evaluation.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the evaluation was successful.
        /// </summary>
        /// <returns>True if the evaluation was successful; otherwise, false.</returns>
        public bool Success => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Value indicating whether the structure of this IAIReturn is valid.
        /// </summary>
        public bool IsValid()
        {
            //if (!this.Response.IsValid())
            //{
            //    return false;
            //}

            if (this.Result == null && this.ErrorMessage == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a new successful result.
        /// </summary>
        /// <param name="response">The AI response.</param>
        /// <param name="result">The result value.</param>
        /// <returns>A new success result instance.</returns>
        public static AIReturn<T> CreateSuccess(AIResponse response, T result)
        {
            return new AIReturn<T>
            {
                Response = response,
                Result = result,
            };
        }

        /// <summary>
        /// Creates a new error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="response">Optional AI response that may have caused the error.</param>
        /// <returns>A new error result instance.</returns>
        public static AIReturn<T> CreateError(string message, AIResponse response = null)
        {
            return new AIReturn<T>
            {
                Response = response,
                ErrorMessage = message,
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
        /// <param name="fields">Dictionary mapping JSON key names to field paths. Use "Response.PropertyName" for nested properties.</param>
        /// <returns>JObject with mapped values.</returns>
        public static JObject ToJObject<T>(this AIReturn<T> aireturn, Dictionary<string, string> fields = null)
        {
            fields ??= new Dictionary<string, string>
            {
                ["success"] = "Success",
                ["result"] = "Result",
                ["error"] = "ErrorMessage",
                ["rawResponse"] = "Response",
            };

            var jo = new JObject();
            var aiReturnType = aireturn.GetType();
            var responseType = typeof(AIResponse);

            foreach (var (jsonKey, sourcePath) in fields)
            {
                object? value = null;
                JToken? token;

                try
                {
                    if (sourcePath.StartsWith("Response.", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle nested Response properties
                        var propName = sourcePath["Response.".Length..];
                        var responseProp = responseType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                        if (responseProp != null && aireturn.Response != null)
                        {
                            value = responseProp.GetValue(aireturn.Response);
                        }
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
