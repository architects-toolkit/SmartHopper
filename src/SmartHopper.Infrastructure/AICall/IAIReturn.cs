/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components.
    /// </summary>
    public interface IAIReturn
    {
        /// <summary>
        /// Gets the decoded result interactions.
        /// </summary>
        List<IAIInteraction> Result { get; set; }

        /// <summary>
        /// Gets or sets the encoded response from the provider.
        /// </summary>
        string EncodedResult { get; set; }

        /// <summary>
        /// Gets or sets the request sent to the provider.
        /// </summary>
        IAIRequest Request { get; set; }

        /// <summary>
        /// Gets or sets the metrics about this call.
        /// </summary>
        AIMetrics Metrics { get; }

        /// <summary>
        /// Gets or sets the list of tool calls made by the provider after the request.
        /// </summary>
        List<AIToolCall> ToolCalls { get; set; }

        /// <summary>
        /// Gets or sets the current status of the request.
        /// </summary>
        AICallStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the error message if any occurred during evaluation.
        /// </summary>
        string ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the evaluation was successful.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Value indicating whether the structure of this IAIReturn is valid.
        /// </summary>
        (bool IsValid, List<string> Errors) IsValid();
    }
}
