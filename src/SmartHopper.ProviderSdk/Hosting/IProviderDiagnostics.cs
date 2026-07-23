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

using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Surface that providers use to emit user-visible runtime messages and metrics
    /// without depending on host-side runtime-message UI.
    /// </summary>
    public interface IProviderDiagnostics
    {
        /// <summary>
        /// Surface a runtime message to the user. The host decides where to render it
        /// (component balloon, WebChat banner, status bar, etc.).
        /// </summary>
        void Report(string providerName, SHRuntimeMessage message);
    }

    /// <summary>
    /// No-op diagnostics sink used when SDK code runs without a host attached.
    /// </summary>
    public sealed class NullProviderDiagnostics : IProviderDiagnostics
    {
        /// <inheritdoc />
        public void Report(string providerName, SHRuntimeMessage message)
        {
        }
    }
}
