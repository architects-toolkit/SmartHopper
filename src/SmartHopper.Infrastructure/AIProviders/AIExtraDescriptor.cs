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

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Describes a single provider-specific extra parameter that can be surfaced
    /// as a dynamic input on the <c>AIExtraSettingsComponent</c>.
    /// </summary>
    public sealed class AIExtraDescriptor
    {
        /// <summary>Gets the JSON key sent to the provider API (e.g. "service_tier").</summary>
        public string Key { get; }

        /// <summary>Gets the human-readable display name (e.g. "Service Tier").</summary>
        public string DisplayName { get; }

        /// <summary>Gets the description shown in the Grasshopper parameter tooltip.</summary>
        public string Description { get; }

        /// <summary>
        /// Gets the .NET type of the parameter value.
        /// Supported: <see cref="string"/>, <see cref="int"/>, <see cref="double"/>, <see cref="bool"/>.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the default value. Null means the parameter is optional with no default.
        /// </summary>
        public object DefaultValue { get; }

        /// <summary>
        /// Gets the set of allowed string values. Null means free-form input is accepted.
        /// </summary>
        public string[] AllowedValues { get; }

        /// <summary>
        /// Initializes a new <see cref="AIExtraDescriptor"/>.
        /// </summary>
        /// <param name="key">JSON key sent to the provider API.</param>
        /// <param name="displayName">Human-readable name for the Grasshopper input.</param>
        /// <param name="description">Tooltip description for the Grasshopper input.</param>
        /// <param name="type">Parameter value type (string, int, double, bool).</param>
        /// <param name="defaultValue">Optional default value.</param>
        /// <param name="allowedValues">Optional enumeration of valid string values.</param>
        public AIExtraDescriptor(
            string key,
            string displayName,
            string description,
            Type type,
            object defaultValue = null,
            string[] allowedValues = null)
        {
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.DisplayName = displayName ?? key;
            this.Description = description ?? string.Empty;
            this.Type = type ?? typeof(string);
            this.DefaultValue = defaultValue;
            this.AllowedValues = allowedValues;
        }
    }
}
