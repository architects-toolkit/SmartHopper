/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    public interface IAIReturn<T>
    {
        /// <summary>
        /// Gets or sets the request sent to the AI.
        /// </summary>
        AIRequest Request { get; set; }	
        
        /// <summary>
        /// Gets or sets the raw response from the AI.
        /// </summary>
        AIResponse Response { get; set; }

        /// <summary>
        /// Gets or sets the processed result value.
        /// </summary>
        T Result { get; set; }

        /// <summary>
        /// Gets or sets the error message if any occurred during evaluation.
        /// </summary>
        string ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the evaluation was successful.
        /// </summary>
        bool Success { get; set; }

        /// <summary>
        /// Value indicating whether the structure of this IAIReturn is valid.
        /// </summary>
        bool IsValid();
    }
}
