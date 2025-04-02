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
            var timeZone = TimeZoneInfo.Local;
            var utcOffset = timeZone.BaseUtcOffset;

            return new Dictionary<string, string>
            {
                { "current-datetime", now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "current-timezone", $"UTC{(utcOffset.Hours >= 0 ? "+" : "")}{utcOffset.Hours:D2}:{utcOffset.Minutes:D2}" }
            };
        }
    }

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
