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
    /// Metadata for a newly detected provider assembly shown in the unified
    /// enable-providers dialog: declared display name, hash-verification status,
    /// file last-write date and declared model snapshot count. Data only; no provider images.
    /// </summary>
    internal sealed class NewProviderCandidate
    {
        /// <summary>Gets or sets the assembly file name without extension — the <c>TrustedProviders</c> key.</summary>
        internal string AssemblyName { get; set; } = string.Empty;

        /// <summary>Gets or sets the full path of the provider DLL.</summary>
        internal string AssemblyPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the manifest-declared display name, or the file name when unavailable.</summary>
        internal string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the SHA-256 hash-verification outcome; <see langword="null"/> when verification could not run.</summary>
        internal ProviderVerificationResult? Verification { get; set; }

        /// <summary>Gets or sets the last write time of the provider DLL (local time).</summary>
        internal DateTime LastUpdate { get; set; }

        /// <summary>Gets or sets the manifest-declared model snapshot count, or null for an unknown/dynamic catalogue.</summary>
        internal int? ModelCount { get; set; }

        /// <summary>
        /// Gets or sets an extra trust note shown on the dialog row — e.g. why a
        /// previously trusted provider is being re-confirmed. <see langword="null"/> for
        /// ordinary first-seen candidates.
        /// </summary>
        internal string? TrustNote { get; set; }
    }
}
