/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Defines the contract for AI-related operations.
 * This service handles interactions with AI models, including
 * prompt generation, response processing, and input validation.
 */

using System.Threading.Tasks;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for AI services. Implement this to handle
    /// AI-related operations in your components.
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// Generates a response from the AI model based on a prompt.
        /// Implement this to handle AI model interactions.
        /// </summary>
        /// <param name="prompt">The input prompt for the AI model</param>
        /// <returns>The generated response from the AI model</returns>
        /// <remarks>
        /// Implementation should handle:
        /// - API rate limiting
        /// - Error handling for API calls
        /// - Response validation
        /// - Token management
        /// </remarks>
        Task<string> GenerateResponse(string prompt);

        /// <summary>
        /// Validates input using AI capabilities.
        /// Implement this to check if input meets AI model requirements.
        /// </summary>
        /// <param name="input">The input to validate</param>
        /// <returns>True if the input is valid for AI processing</returns>
        /// <remarks>
        /// Implementation should consider:
        /// - Input length limits
        /// - Content safety checks
        /// - Format requirements
        /// - Token count estimation
        /// </remarks>
        Task<bool> ValidateInput(string input);

        // TODO: Consider adding these methods:
        // - Task<float> EstimateTokenCount(string input)
        // - Task<bool> IsWithinContextWindow(string input)
        // - Task<Dictionary<string, float>> AnalyzePromptQuality(string prompt)
    }
}
