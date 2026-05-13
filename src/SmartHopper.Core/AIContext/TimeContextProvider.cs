/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Core.AIContext
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
                { "current-datetime", now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
                { "current-timezone", $"UTC{(utcOffset.Hours >= 0 ? "+" : "")}{utcOffset.Hours:D2}:{utcOffset.Minutes:D2}" },
            };
        }
    }
}
