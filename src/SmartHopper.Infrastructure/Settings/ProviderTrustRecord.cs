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

namespace SmartHopper.Infrastructure.Settings
{
    /// <summary>
    /// Verification snapshot captured when a provider assembly was trusted. Stored trust
    /// (<c>TrustedProviders</c>) applies only while the file still matches what was
    /// consented to: a verified provider silently follows official updates because each
    /// load re-mints the consented hash on catalogue match, while a changed unverified
    /// file falls back to the consent flow.
    /// </summary>
    public sealed class ProviderTrustRecord
    {
        /// <summary>Gets or sets the SHA-256 of the provider bytes at consent time.</summary>
        public string? ConsentedHash { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether consent was given against a
        /// catalogue-verified binary (<see langword="true"/>) or an unverified one
        /// (<see langword="false"/>).
        /// </summary>
        public bool VerifiedTrust { get; set; }
    }
}
