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

using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.Security
{
    /// <summary>
    /// Strict trust predicate for the isolated PDF renderer assembly. Unlike the general
    /// provider classification rule (token OR authenticode OR hash match), renderer code
    /// executes native image pipelines on document-controlled content, so it requires
    /// independent corroboration: <see cref="ProviderClassification.Official"/> AND
    /// <see cref="ProviderClassificationResult.StrongNameMatchesHost"/> AND
    /// (Windows ⇒ <see cref="ProviderClassificationResult.AuthenticodeMatchesHost"/>,
    /// otherwise <see cref="ProviderVerificationStatus.Match"/> on the official hash
    /// manifest). Tampered, hash-mismatched, invalid and unknown classifications are
    /// always rejected.
    /// </summary>
    public static class RendererIntegrityGate
    {
        /// <summary>
        /// Returns <c>true</c> when the classification result satisfies the strict
        /// renderer trust predicate.
        /// </summary>
        /// <param name="result">Classification result from
        /// <see cref="ProviderClassifier.ClassifyAsync(string)"/>; null rejects.</param>
        /// <param name="isWindows">Whether the current platform requires the Authenticode
        /// signal (otherwise the hash-manifest signal is required).</param>
        /// <param name="isPackagedInstallPath">Whether the classified path is exactly the
        /// packaged install folder shipped with the plugin. Only honored in DEBUG builds
        /// for locally rebuilt unsigned bits; release never relaxes on this flag.</param>
        public static bool IsTrusted(
            ProviderClassificationResult? result,
            bool isWindows,
            bool isPackagedInstallPath = false)
        {
            if (result == null)
            {
                return false;
            }

            if (result.Classification == ProviderClassification.Official
                && result.StrongNameMatchesHost
                && ((isWindows && result.AuthenticodeMatchesHost)
                    || result.HashStatus == ProviderVerificationStatus.Match))
            {
                return true;
            }

#if DEBUG
            // Controlled DEBUG policy only: locally rebuilt Community bits may run from
            // the exact packaged install folder. Never applies to foreign directories
            // or to release builds.
            if (isPackagedInstallPath
                && result.Classification == ProviderClassification.Community)
            {
                return true;
            }
#endif
            return false;
        }
    }
}
