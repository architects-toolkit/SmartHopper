/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AICall
{
    public class AIRequest : IAIRequest
    {
        /// <summary>
        /// Gets or sets the AI provider name (e.g., "openai", "anthropic").
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Gets or sets the model name (e.g., "gpt-4", "claude-3-opus").
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the required capabilities to process this request. Requires at least an input and an output capability to be defined.
        /// </summary>
        public AICapability Capability { get; set; } = AICapability.None;

        /// <summary>
        /// Gets or sets the endpoint or full URL to use for the request.
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method to use for the request (GET, POST, DELETE, PATCH).
        /// </summary>
        public string HttpMethod { get; set; } = "GET";

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        public IAIRequestBody Body { get; set; }

        /// <summary>
        /// Gets or sets the content type of the request body.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the authentication method to use for the request.
        /// </summary>
        public string Authentication { get; set; } = "bearer";

        /// <summary>
        /// A value indicating whether the request is valid.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(this.Provider) || string.IsNullOrEmpty(this.Model) || !this.Capability.HasInput() || !this.Capability.HasOutput() || string.IsNullOrEmpty(this.Endpoint) || string.IsNullOrEmpty(this.HttpMethod) || string.IsNullOrEmpty(this.Authentication) || string.IsNullOrEmpty(this.ContentType))
            {
                return false;
            }

            if (!this.Body.IsValid())
            {
                return false;
            }

            if (this.Capability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
            {
                return false;
            }

            return true;
        }
    }
}
