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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SmartHopper.Providers.Test
{
    /// <summary>
    /// Static control surface for <see cref="TestProvider"/>. The
    /// <c>TestBatchControlComponent</c> Grasshopper component (in
    /// SmartHopper.Components.Test) writes the knobs below from canvas inputs so
    /// batch lifecycle scenarios can be driven deterministically from a live
    /// document; the event queue plus counters form the assertion surface that
    /// the same component exposes as outputs.
    /// All state is process-local — after a Rhino/Grasshopper restart the
    /// provider resumes from the persisted <c>AIBatchSubmission</c> alone, which
    /// is exactly the property under test for restore scenarios.
    /// </summary>
    public static class TestBatchControl
    {
        /// <summary>
        /// Delay applied inside <c>SubmitBatchAsync</c> before the submission
        /// handle is returned. Opens the prepare-and-submit window for
        /// delayed-submit and cancel-during-submit scenarios.
        /// </summary>
        public static int SubmitDelayMs;

        /// <summary>
        /// Elapsed milliseconds after <see cref="SmartHopper.ProviderSdk.AICall.Batch.AIBatchSubmission.SubmittedAt"/>
        /// before <c>GetBatchStatusAsync</c> reports <c>Completed</c>. Polls before
        /// the deadline report <c>InProgress</c>. Because the clock is the
        /// submission's own timestamp, restored submissions re-derive the same
        /// behavior without any surviving provider state.
        /// </summary>
        public static int CompleteAfterMs = 1000;

        /// <summary>
        /// Zero-based positions (within the submission's custom-id list) whose
        /// results are omitted from the completed status — simulates per-item
        /// provider failures that must surface as unresolved units.
        /// </summary>
        public static IReadOnlyCollection<int> OmitResultIndices = Array.Empty<int>();

        /// <summary>When true, <c>SubmitBatchAsync</c> throws instead of submitting.</summary>
        public static bool ThrowOnSubmit;

        /// <summary>
        /// When true, <c>GetBatchStatusAsync</c> reports <c>Failed</c> for the
        /// whole batch once the completion deadline passes.
        /// </summary>
        public static bool FailWholeBatch;

        /// <summary>Ordered event log (SUBMIT/POLL/CANCEL/DOWNLOAD/PARSE) for assertions.</summary>
        public static readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();

        /// <summary>Batch ids cancelled through <c>CancelBatchAsync</c>.</summary>
        public static readonly ConcurrentDictionary<string, bool> CancelledBatches = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>Running counters for assertion checks.</summary>
        public static int SubmitCalls;

        /// <inheritdoc cref="SubmitCalls"/>
        public static int PollCalls;

        /// <inheritdoc cref="SubmitCalls"/>
        public static int CancelCalls;

        /// <inheritdoc cref="SubmitCalls"/>
        public static int DownloadCalls;

        /// <summary>Appends an event entry and mirrors it to the provider trace log.</summary>
        public static void Record(string evt)
        {
            Events.Enqueue($"{DateTime.UtcNow:HH:mm:ss.fff} {evt}");
            ProviderSdk.Logging.Logger.For<TestProvider>().Trace($"[TestProvider] {evt}");
        }

        /// <summary>
        /// Resets knobs and the assertion surface to defaults. Call between
        /// scenarios so events and counters never leak across runs.
        /// </summary>
        public static void Reset()
        {
            SubmitDelayMs = 0;
            CompleteAfterMs = 1000;
            OmitResultIndices = Array.Empty<int>();
            ThrowOnSubmit = false;
            FailWholeBatch = false;
            CancelledBatches.Clear();
            while (Events.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref SubmitCalls, 0);
            Interlocked.Exchange(ref PollCalls, 0);
            Interlocked.Exchange(ref CancelCalls, 0);
            Interlocked.Exchange(ref DownloadCalls, 0);
        }
    }
}
