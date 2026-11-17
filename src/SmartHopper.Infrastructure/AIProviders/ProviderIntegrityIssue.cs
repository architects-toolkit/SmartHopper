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

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>How severe an integrity finding is for the user.</summary>
    internal enum ProviderIntegrityIssueSeverity
    {
        /// <summary>The provider was loaded despite the finding.</summary>
        Warning,

        /// <summary>The provider was blocked and its code never executed.</summary>
        Error,
    }

    /// <summary>
    /// A single provider integrity finding collected during provider loading and
    /// shown as one row in the unified integrity report in
    /// <see cref="Dialogs.ProviderTrustDialog"/>. Data only — producing the
    /// finding must never show UI itself.
    /// </summary>
    internal sealed class ProviderIntegrityIssue
    {
        /// <summary>Gets or sets the provider file name (e.g. <c>SmartHopper.Providers.OpenAI.dll</c>).</summary>
        internal string FileName { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the provider was blocked (error) or loaded anyway (warning).</summary>
        internal ProviderIntegrityIssueSeverity Severity { get; set; }

        /// <summary>Gets or sets the one-line reason shown under the provider name.</summary>
        internal string Status { get; set; } = string.Empty;

        /// <summary>Gets or sets the full multi-line explanation shown in the row body.</summary>
        internal string Detail { get; set; } = string.Empty;
    }
}
