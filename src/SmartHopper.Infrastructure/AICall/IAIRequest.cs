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
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AICall
{
    public interface IAIRequest
    {
        /// <summary>
        /// Gets or sets the AI provider name.
        /// </summary>
        string Provider { get; set; }

        /// <summary>
        /// Gets the AI provider instance.
        /// </summary>
        IAIProvider ProviderInstance { get; }

        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        string Model { get; set; }

        /// <summary>
        /// Gets or sets the required capabilities to process this request.
        /// </summary>
        AICapability Capability { get; set; }

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        AIBody Body { get; set; }

        /// <summary>
        /// Gets or sets validation messages produced during request preparation and execution.
        /// These are informational, warning, or error notes that should be surfaced by components.
        /// Expected format uses prefixes, e.g. "(Error) ...", "(Warning) ...", "(Info) ...".
        /// </summary>
        List<string> Messages { get; set; }

        /// <summary>
        /// A value indicating whether the request is valid.
        /// </summary>
        (bool IsValid, List<string> Errors) IsValid();

        /// <summary>
        /// Executes the request and gets the result.
        /// </summary>
        /// <returns>The result of the request in <see cref="AIReturn"/> format.</returns>
        Task<AIReturn> Exec();
    }
}
