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

namespace SmartHopper.ProviderSdk.AICall.Core
{
    /// <summary>
    /// Centralized timeout defaults and bounds for AI request, tool, and provider HTTP layers.
    /// </summary>
    /// <remarks>
    /// Resolution chain (highest to lowest priority):
    /// <list type="number">
    ///   <item><description>Explicit per-request value on <c>AIRequestBase.TimeoutSeconds</c>.</description></item>
    ///   <item><description><c>RequestTimeoutPolicy</c> reads <c>Global.TimeoutSeconds</c> from <c>SmartHopperSettings</c>.</description></item>
    ///   <item><description><see cref="DefaultTimeoutSeconds"/> (this class) as the final safety-net fallback when the policy is bypassed.</description></item>
    /// </list>
    /// All layers (provider HTTP calls, batch HTTP calls, tool execution) share <see cref="DefaultTimeoutSeconds"/>
    /// to keep behavior consistent if the policy pipeline does not run.
    /// </remarks>
    public static class TimeoutDefaults
    {
        /// <summary>
        /// Default fallback timeout in seconds when neither an explicit per-request value
        /// nor a settings-based value is available.
        /// </summary>
        public const int DefaultTimeoutSeconds = 300;

        /// <summary>Minimum allowed timeout in seconds.</summary>
        public const int MinTimeoutSeconds = 1;

        /// <summary>Maximum allowed timeout in seconds (10 minutes guard).</summary>
        public const int MaxTimeoutSeconds = 600;
    }
}
