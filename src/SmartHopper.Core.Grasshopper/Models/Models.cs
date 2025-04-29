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
using SmartHopper.Config.Models;

namespace SmartHopper.Core.Grasshopper.Models
{
    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components
    /// </summary>
    /// <typeparam name="T">The type of the result value, typically a Grasshopper data type</typeparam>
    public class AIEvaluationResult<T>
    {
        /// <summary>
        /// The raw response from the AI
        /// </summary>
        public AIResponse Response { get; set; }

        /// <summary>
        /// The processed result value
        /// </summary>
        public T Result { get; set; }

        /// <summary>
        /// Error message if any occurred during evaluation
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Level of the error message
        /// </summary>
        public GH_RuntimeMessageLevel ErrorLevel { get; set; }

        /// <summary>
        /// Whether the evaluation was successful
        /// </summary>
        public bool Success => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Creates a new successful result
        /// </summary>
        /// <param name="response">The AI response</param>
        /// <param name="result">The processed result value</param>
        public static AIEvaluationResult<T> CreateSuccess(AIResponse response, T result)
        {
            return new AIEvaluationResult<T>
            {
                Response = response,
                Result = result
            };
        }

        /// <summary>
        /// Creates a new error result
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="level">The error level</param>
        /// <param name="response">Optional AI response that may have caused the error</param>
        public static AIEvaluationResult<T> CreateError(string message, GH_RuntimeMessageLevel level, AIResponse response = null)
        {
            return new AIEvaluationResult<T>
            {
                Response = response,
                ErrorMessage = message,
                ErrorLevel = level
            };
        }
    }
}

