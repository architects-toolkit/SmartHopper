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
using System.Linq;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Decides whether a provider-integrity findings set should be reported to the
    /// user again. Findings are fingerprinted order-insensitively over
    /// (file, severity, status): an unchanged set is reported once per session
    /// instead of on every rescan, a changed set reports again, and a clean scan
    /// resets the gate so future findings still surface.
    /// </summary>
    internal sealed class ProviderIntegrityReportGate
    {
        private string? _lastSignature;

        /// <summary>
        /// Computes the order-insensitive signature of a findings set. Empty sets
        /// map to <see cref="string.Empty"/>.
        /// </summary>
        internal static string ComputeSignature(IEnumerable<ProviderIntegrityIssue>? issues)
        {
            if (issues == null)
            {
                return string.Empty;
            }

            var parts = issues
                .Select(i => $"{i.FileName}|{(int)i.Severity}|{i.Status}")
                .OrderBy(p => p, StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        /// <summary>
        /// Returns <see langword="true"/> when this findings set was not reported yet
        /// (new or changed set); <see langword="false"/> for repeats of the last
        /// reported set and for empty sets (which also reset the gate).
        /// </summary>
        internal bool ShouldReport(IEnumerable<ProviderIntegrityIssue>? issues)
        {
            var signature = ComputeSignature(issues);
            if (signature.Length == 0)
            {
                this._lastSignature = null;
                return false;
            }

            if (string.Equals(signature, this._lastSignature, StringComparison.Ordinal))
            {
                return false;
            }

            this._lastSignature = signature;
            return true;
        }
    }
}
