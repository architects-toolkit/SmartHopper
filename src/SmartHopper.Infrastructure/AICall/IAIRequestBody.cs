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

namespace SmartHopper.Infrastructure.AICall
{
    public interface IAIRequestBody
    {
        /// <summary>
        /// Gets or sets the interaction history.
        /// </summary>
        List<IAIInteraction> Interactions { get; set; }

        /// <summary>
        /// Gets or sets the tool filter.
        /// </summary>
        string ToolFilter { get; set; }

        /// <summary>
        /// Gets or sets the context filter.
        /// </summary>
        string ContextFilter { get; set; }

        /// <summary>
        /// Gets or sets the JSON output schema.
        /// </summary>
        string JsonOutputSchema { get; set; }

        /// <summary>
        /// A value indicating whether the request body is valid.
        /// </summary>
        (bool IsValid, List<string> Errors) IsValid();

        /// <summary>
        /// A value indicating whether the request requires a JSON output.
        /// </summary>
        bool RequiresJsonOutput();
    }
}
