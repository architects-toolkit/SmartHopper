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
    /// Policy that runs before the provider call to normalize/augment the request.
    /// Must NOT perform network I/O.
    /// </summary>
    public interface IRequestPolicy
    {
        /// <summary>
        /// Applies the policy to the current request context.
        /// </summary>
        Task ApplyAsync(PolicyContext context);
    }
}
