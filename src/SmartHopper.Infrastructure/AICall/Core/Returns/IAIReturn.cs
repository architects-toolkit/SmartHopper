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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Returns
{
    /// <summary>
    /// Generic result type for AI evaluations, providing a standard interface between tools and components.
    /// </summary>
    public interface IAIReturn
    {
        /// <summary>
        /// Gets the decoded result interactions.
        /// </summary>
        AIBodyImmutable Body { get; }

        /// <summary>
        /// Gets or sets the request sent to the provider.
        /// </summary>
        IAIRequest Request { get; set; }

        /// <summary>
        /// Gets the metrics about this call.
        /// </summary>
        AIMetrics Metrics { get; }

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
        /// Gets or sets validation messages produced during request preparation and execution.
        /// These are informational, warning, or error notes that should be surfaced by components.
        /// Expected format uses prefixes, e.g. "(Error) ...", "(Warning) ...", "(Info) ...".
        /// </summary>
        List<AIRuntimeMessage> Messages { get; set; }

        /// <summary>
        /// Value indicating whether the structure of this IAIReturn is valid.
        /// </summary>
        (bool IsValid, List<AIRuntimeMessage> Errors) IsValid();

        /// <summary>
        /// Sets the body of the result.
        /// </summary>
        /// <param name="body">The body to set as result.</param>
        void SetBody(AIBodyImmutable body);

        /// <summary>
        /// Creates a standardized provider error while preserving the raw provider message in ErrorMessage.
        /// Adds a structured message tagged as originating from the provider.
        /// </summary>
        /// <param name="rawMessage">Raw provider error message.</param>
        /// <param name="request">Optional request context.</param>
        void CreateProviderError(string rawMessage, IAIRequest? request = null);

        /// <summary>
        /// Creates a standardized network error (e.g., DNS, connectivity) while preserving the raw message.
        /// Adds a structured message tagged as originating from the network.
        /// </summary>
        /// <param name="rawMessage">Raw network error message.</param>
        /// <param name="request">Optional request context.</param>
        void CreateNetworkError(string rawMessage, IAIRequest? request = null);

        /// <summary>
        /// Creates a standardized tool error while preserving the raw message.
        /// Adds a structured message tagged as originating from a tool.
        /// </summary>
        /// <param name="rawMessage">Raw tool error message.</param>
        /// <param name="request">Optional request context.</param>
        void CreateToolError(string rawMessage, IAIRequest? request = null);

        /// <summary>
        /// Adds a structured runtime message without modifying ErrorMessage.
        /// </summary>
        /// <param name="severity">The message severity.</param>
        /// <param name="origin">The message origin.</param>
        /// <param name="text">The message text.</param>
        void AddRuntimeMessage(AIRuntimeMessageSeverity severity, AIRuntimeMessageOrigin origin, string text);

        /// <summary>
        /// Sets the completion time to the last interaction.
        /// </summary>
        /// <param name="completionTime">The completion time to set.</param>
        void SetCompletionTime(double completionTime);
    }
}
