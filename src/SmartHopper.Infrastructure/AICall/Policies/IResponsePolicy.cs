/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.AICall.Policies
{
    /// <summary>
    /// Policy that runs after the provider call to normalize/map/validate the response.
    /// </summary>
    public interface IResponsePolicy
    {
        /// <summary>
        /// Applies the policy to the current response context.
        /// </summary>
        Task ApplyAsync(PolicyContext context);
    }
}
