/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using Rhino;
using SmartHopper.Infrastructure.Interfaces;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Context provider that supplies environment information (OS, Rhino version) to AI queries
    /// </summary>
    public class EnvironmentContextProvider : IAIContextProvider
    {
        /// <summary>
        /// Gets the provider identifier
        /// </summary>
        public string ProviderId => "environment";

        /// <summary>
        /// Gets the environment context for AI queries
        /// </summary>
        /// <returns>A dictionary containing environment information</returns>
        public Dictionary<string, string> GetContext()
        {
            var rhinoVersion = RhinoApp.Version.ToString();

            return new Dictionary<string, string>
            {
                { "operating-system", Environment.OSVersion.ToString() },
                { "rhino-version", rhinoVersion },
                { "platform", Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit" }
            };
        }
    }
}
