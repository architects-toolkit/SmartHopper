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

namespace SmartHopper.Infrastructure.AIContext
{
    /// <summary>
    /// Interface for providing context to AI queries
    /// </summary>
    public interface IAIContextProvider
    {
        /// <summary>
        /// Gets the provider identifier
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Gets the current context for AI queries
        /// </summary>
        /// <returns>A dictionary of context key-value pairs</returns>
        Dictionary<string, string> GetContext();
    }
}