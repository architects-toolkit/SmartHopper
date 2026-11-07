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

namespace SmartHopper.Menu.Dialogs.SettingsTabs.Models
{
    using SmartHopper.ProviderSdk.Logging;

    /// <summary>
    /// Model for the Logging settings tab.
    /// </summary>
    public class LogSettings
    {
        /// <summary>
        /// Gets or sets the local logging consent override: null follows the
        /// diagnostics consent, true/false forces local logging on/off.
        /// </summary>
        public bool? LoggingEnabled { get; set; }

        /// <summary>Gets or sets whether log entries echo to Rhino's command-line output.</summary>
        public bool LiveOutputEnabled { get; set; } = true;

        /// <summary>Gets or sets the minimum level echoed to the live output.</summary>
        public ProviderLogLevel LiveOutputLevel { get; set; } = ProviderLogLevel.Info;

        /// <summary>Gets or sets whether log entries are written to the rolling log file.</summary>
        public bool LogFileEnabled { get; set; } = true;

        /// <summary>Gets or sets the minimum level written to the log file.</summary>
        public ProviderLogLevel LogFileLevel { get; set; } = ProviderLogLevel.Trace;

        /// <summary>Gets or sets how many days log files are retained.</summary>
        public int LogRetentionDays { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether log entries are shipped to the telemetry collector
        /// (effective only while diagnostics consent is granted).
        /// </summary>
        public bool OtlpLogEnabled { get; set; } = true;

        /// <summary>Gets or sets the minimum level shipped to the telemetry collector.</summary>
        public ProviderLogLevel OtlpLogLevel { get; set; } = ProviderLogLevel.Warning;
    }
}
