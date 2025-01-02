/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Config.Interfaces
{
    /// <summary>
    /// Interface for components that provide custom endpoints for API calls
    /// </summary>
    public interface IEndpointProvider
    {
        /// <summary>
        /// Gets the endpoint URL for the provider
        /// </summary>
        string GetEndpoint();
    }
}