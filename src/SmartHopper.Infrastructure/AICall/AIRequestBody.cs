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
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall
{
    public class AIRequestBody : IAIRequestBody
    {
        /// <summary>
        /// Gets or sets the interaction history.
        /// </summary>
        public List<IAIInteraction> Interactions { get; set; }

        /// <summary>
        /// Gets the number of interactions.
        /// </summary>
        public int InteractionsCount()
        {
            return this.Interactions.Count;
        }

        /// <summary>
        /// Gets or sets the tool filter.
        /// </summary>
        public string ToolFilter { get; set; }

        /// <summary>
        /// Gets or sets the context filter.
        /// </summary>
        public string ContextFilter { get; set; }

        /// <summary>
        /// Gets or sets the JSON output schema.
        /// </summary>
        public string JsonOutputSchema { get; set; }

        /// <summary>
        /// A value indicating whether the request body is valid.
        /// </summary>
        public bool IsValid()
        {
            return this.InteractionsCount() > 0;
        }
    }
}
