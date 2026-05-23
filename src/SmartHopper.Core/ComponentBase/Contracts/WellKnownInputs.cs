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

namespace SmartHopper.Core.ComponentBase.Contracts
{
    /// <summary>
    /// Canonical names for well-known Grasshopper input/output parameters exposed by the
    /// <c>ComponentBase</c> hierarchy. Centralizing these strings prevents typos and makes
    /// them discoverable from both the bases and derived components.
    /// </summary>
    /// <remarks>
    /// These names are part of the user-facing parameter surface and changes are breaking
    /// for saved <c>.gh</c> documents. Treat them as a public contract.
    /// </remarks>
    public static class WellKnownInputs
    {
        /// <summary>
        /// Synthetic pseudo-input name representing the AI provider menu selection.
        /// Not a real parameter; appears in <c>InputsChanged()</c> when the user picks
        /// a new provider from the context menu.
        /// </summary>
        public const string AIProvider = "AIProvider";

        /// <summary>
        /// Name of the boolean <c>Run?</c> input automatically registered by
        /// <see cref="StatefulComponentBase"/>.
        /// </summary>
        public const string Run = "Run?";

        /// <summary>
        /// Name of the optional <c>Settings</c> input registered by
        /// <see cref="AIStatefulAsyncComponentBase"/>.
        /// </summary>
        public const string Settings = "Settings";

        /// <summary>
        /// Name of the <c>Metrics</c> output registered by
        /// <see cref="AIStatefulAsyncComponentBase"/>.
        /// </summary>
        public const string Metrics = "Metrics";

        /// <summary>
        /// Name of the AI input payload parameter used by adapter bases.
        /// </summary>
        public const string InputPayload = "Input >";
    }
}
