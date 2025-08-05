/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Core.Grasshopper.Models
{
    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components.
    /// </summary>
    /// <typeparam name="T">The type of the result value, typically a Grasshopper data type.</typeparam>
    public class AIEvaluationResult<T>
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
        /// Gets or sets the level of the error message.
        /// </summary>
        public GH_RuntimeMessageLevel ErrorLevel { get; set; }

        /// <summary>
        /// Gets a value indicating whether the evaluation was successful.
        /// </summary>
        /// <returns>True if the evaluation was successful; otherwise, false.</returns>
        public bool Success => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Creates a new successful result.
        /// </summary>
        /// <param name="response">The AI response.</param>
        /// <param name="result">The result value.</param>
        /// <returns>A new success result instance.</returns>
        public static AIEvaluationResult<T> CreateSuccess(AIResponse response, T result)
        {
            return new AIEvaluationResult<T>
            {
                Response = response,
                Result = result,
            };
        }

        /// <summary>
        /// Creates a new error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="level">The error level.</param>
        /// <param name="response">Optional AI response that may have caused the error.</param>
        /// <returns>A new error result instance.</returns>
        public static AIEvaluationResult<T> CreateError(string message, GH_RuntimeMessageLevel level, AIResponse response = null)
        {
            return new AIEvaluationResult<T>
            {
                Response = response,
                ErrorMessage = message,
                ErrorLevel = level,
            };
        }
    }
}

