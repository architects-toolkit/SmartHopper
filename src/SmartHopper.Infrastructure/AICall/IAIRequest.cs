/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    public interface IAIRequest
    {
        /// <summary>
        /// Gets or sets the AI provider name (e.g., "openai", "anthropic").
        /// </summary>
        string Provider { get; set; }

        /// <summary>
        /// Gets or sets the model name (e.g., "gpt-4", "claude-3-opus").
        /// </summary>
        string Model { get; set; }

        /// <summary>
        /// Gets or sets the required capabilities to process this request.
        /// </summary>
        AICapability Capability { get; set; }

        /// <summary>
        /// Gets or sets the endpoint or full URL to use for the request.
        /// </summary>
        string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method to use for the request (GET, POST, DELETE, PATCH).
        /// </summary>
        string HttpMethod { get; set; }

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        IAIRequestBody Body { get; set; }

        /// <summary>
        /// Gets or sets the content type of the request body.
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the authentication method to use for the request.
        /// </summary>
        string Authentication { get; set; }

        /// <summary>
        /// A value indicating whether the request is valid.
        /// </summary>
        bool IsValid();
    }
}
