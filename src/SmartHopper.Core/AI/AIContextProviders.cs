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
using System.Reflection;
using Rhino;

namespace SmartHopper.Core.AI
{
    /// <summary>
    /// Context provider that supplies the current time information to AI queries
    /// </summary>
    public class TimeContextProvider : IAIContextProvider
    {
        /// <summary>
        /// Gets the provider identifier
        /// </summary>
        public string ProviderId => "time";

        /// <summary>
        /// Gets the current time context for AI queries
        /// </summary>
        /// <returns>A dictionary containing current time information</returns>
        public Dictionary<string, string> GetContext()
        {
            var now = DateTime.Now;
            return new Dictionary<string, string>
            {
                { "current-datetime", now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "current-timezone", TimeZoneInfo.Local.DisplayName }
            };
        }
    }

    /// <summary>
    /// Context provider that supplies environment information (OS, Rhino version, GH version) to AI queries
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
            var ghVersion = GetGrasshopperVersion();
            
            return new Dictionary<string, string>
            {
                { "operating-system", Environment.OSVersion.ToString() },
                { "rhino-version", rhinoVersion },
                { "grasshopper-version", ghVersion },
                { "platform", Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit" }
            };
        }

        /// <summary>
        /// Gets the Grasshopper version
        /// </summary>
        /// <returns>The Grasshopper version as a string</returns>
        private string GetGrasshopperVersion()
        {
            try
            {
                // Try to get the Grasshopper assembly version
                var ghAssembly = Assembly.Load("Grasshopper");
                if (ghAssembly != null)
                {
                    return ghAssembly.GetName().Version.ToString();
                }
            }
            catch (Exception)
            {
                // Ignore errors and return unknown
            }
            
            return "Unknown";
        }
    }
}
