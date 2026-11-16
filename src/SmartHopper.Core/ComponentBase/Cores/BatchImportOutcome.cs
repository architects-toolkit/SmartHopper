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

namespace SmartHopper.Core.ComponentBase.Cores
{
    /// <summary>
    /// Outcome of a manual batch-results import
    /// (<see cref="AIStatefulAsyncComponentBase.ImportBatchResultContents"/>).
    /// Shared by the component menu entry and the
    /// <c>gh_batch_import_results</c> AI tool so both entry points report the
    /// same facts instead of only firing UI messages.
    /// </summary>
    public sealed class BatchImportOutcome
    {
        /// <summary>Which completion path the import took.</summary>
        public enum ImportMode
        {
            /// <summary>The import aborted before applying anything (no provider, parse failure, nothing loaded).</summary>
            Failed,

            /// <summary>Expected sentinel ids matched loaded ids — the run finalized immediately.</summary>
            Direct,

            /// <summary>
            /// No usable sentinel mapping — a collect-only re-collection run was triggered;
            /// the imported results finalize when that run submits its staged payload.
            /// </summary>
            CollectOnly,
        }

        /// <summary>True when the import was accepted and applied (either mode).</summary>
        public bool Success { get; set; }

        /// <summary>The completion path taken.</summary>
        public ImportMode Mode { get; set; } = ImportMode.Failed;

        /// <summary>Provider used to parse/decode the files.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Result bodies parsed from the files.</summary>
        public int LoadedCount { get; set; }

        /// <summary>Loaded ids that matched the component's expected sentinel ids.</summary>
        public int MatchedCount { get; set; }

        /// <summary>Sentinel ids the component was expecting, or 0 when unknown.</summary>
        public int ExpectedCount { get; set; }

        /// <summary>Failure description when <see cref="Success"/> is false; otherwise null.</summary>
        public string? Error { get; set; }
    }
}
