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

namespace SmartHopper.Core.Tests.Batch
{
    using System.Collections.Generic;
    using SmartHopper.Core.ComponentBase.Cores;
    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="BatchRunState"/> per-run state bundle.
    /// </summary>
    public class BatchRunStateTests
    {
#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.ResetForNextRun clears per-run scratch [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.ResetForNextRun clears per-run scratch [Core]")]
#endif
        public void ResetForNextRun_ClearsPerRunScratch()
        {
            var state = new BatchRunState
            {
                Queue = new List<(string CustomId, ProviderSdk.AICall.Core.Requests.AIRequestCall Request, string MetricsRole)>(),
                SentinelIds = new HashSet<string> { "id1" },
                ProgressCompleted = 3,
                UnsupportedChecked = true,
                BatchSupported = true,
                RunInputHashes = new Dictionary<string, int> { { "In", 1 } },
                RunBranchCounts = new Dictionary<string, int> { { "In", 2 } },
                RunProviderSelection = "Prov",
                StartTime = System.DateTime.UtcNow,
                CompletionTime = 1.5,
                PersistedMetricsList = new ProviderSdk.AICall.Metrics.AIMetricsList(),
            };

            state.ResetForNextRun();

            Assert.Null(state.Queue);
            Assert.Null(state.SentinelIds);
            Assert.Equal(0, state.ProgressCompleted);
            Assert.False(state.UnsupportedChecked);
            Assert.False(state.BatchSupported);
            Assert.Null(state.RunInputHashes);
            Assert.Null(state.RunBranchCounts);
            Assert.Null(state.RunProviderSelection);
            Assert.Null(state.StartTime);
            Assert.Null(state.CompletionTime);
            Assert.Null(state.PersistedMetricsList);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.ResetForNextRun preserves cross-run survivors [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.ResetForNextRun preserves cross-run survivors [Core]")]
#endif
        public void ResetForNextRun_PreservesCrossRunSurvivors()
        {
            var submission = new ProviderSdk.AICall.Batch.AIBatchSubmission("batch-1", "prov", null, new List<string> { "c1" }.AsReadOnly());
            var sentinelTrees = new Dictionary<string, object> { { "Result", new object() } };
            var submittedModels = new Dictionary<string, string> { { "c1", "model-x" } };
            var state = new BatchRunState
            {
                Submission = submission,
                SubmittedProviderName = "prov",
                SubmittedModels = submittedModels,
                SentinelTrees = sentinelTrees,
            };

            state.ResetForNextRun();

            Assert.Same(submission, state.Submission);
            Assert.Equal("prov", state.SubmittedProviderName);
            Assert.Same(submittedModels, state.SubmittedModels);
            Assert.Same(sentinelTrees, state.SentinelTrees);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.ResetForRestoration clears everything [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.ResetForRestoration clears everything [Core]")]
#endif
        public void ResetForRestoration_ClearsEverything()
        {
            var state = new BatchRunState
            {
                Submission = new ProviderSdk.AICall.Batch.AIBatchSubmission("b", "p", null, new List<string>().AsReadOnly()),
                SubmittedProviderName = "p",
                SubmittedModels = new Dictionary<string, string> { { "c1", "m" } },
                SentinelTrees = new Dictionary<string, object>(),
                UnsupportedChecked = true,
                BatchSupported = true,
                RunInputHashes = new Dictionary<string, int>(),
                RunBranchCounts = new Dictionary<string, int>(),
                RunProviderSelection = "Prov",
            };

            state.ResetForRestoration();

            Assert.Null(state.Submission);
            Assert.Null(state.SubmittedProviderName);
            Assert.Null(state.SubmittedModels);
            Assert.Null(state.SentinelTrees);
            Assert.False(state.UnsupportedChecked);
            Assert.False(state.BatchSupported);
            Assert.Null(state.RunInputHashes);
            Assert.Null(state.RunBranchCounts);
            Assert.Null(state.RunProviderSelection);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.BatchInFlight tracks pending submission and submission [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.BatchInFlight tracks pending submission and submission [Core]")]
#endif
        public void BatchInFlight_TracksPendingAndSubmission()
        {
            var state = new BatchRunState();

            Assert.False(state.BatchInFlight);

            // The submit window: queue consumed, provider call not yet answered —
            // lifecycle guards must already treat this as batch-in-flight.
            var epoch = state.BeginPendingSubmission();

            Assert.Equal(epoch, state.Epoch);
            Assert.Equal(epoch, state.PendingSubmissionEpoch);
            Assert.True(state.BatchInFlight);

            // Once the provider answers, Submission covers the in-flight period.
            state.PendingSubmissionEpoch = -1;
            state.Submission = new ProviderSdk.AICall.Batch.AIBatchSubmission("b", "p", null, new List<string>().AsReadOnly());

            Assert.True(state.BatchInFlight);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.InvalidateAsyncWork bumps both epochs and clears pending [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.InvalidateAsyncWork bumps both epochs and clears pending [Core]")]
#endif
        public void InvalidateAsyncWork_BumpsEpochsAndClearsPending()
        {
            var state = new BatchRunState();
            var epoch = state.Epoch;
            var finalizeEpoch = state.FinalizeEpoch;

            state.BeginPendingSubmission();
            state.InvalidateAsyncWork();

            Assert.Equal(epoch + 1, state.Epoch);
            Assert.Equal(finalizeEpoch + 1, state.FinalizeEpoch);
            Assert.Equal(-1, state.PendingSubmissionEpoch);
            Assert.False(state.BatchInFlight);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.ResetForNextRun bumps epoch but not finalize epoch [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.ResetForNextRun bumps epoch but not finalize epoch [Core]")]
#endif
        public void ResetForNextRun_BumpsEpochOnly()
        {
            var state = new BatchRunState();
            var epoch = state.Epoch;
            var finalizeEpoch = state.FinalizeEpoch;

            state.BeginPendingSubmission();
            state.ResetForNextRun();

            // A superseded run's pending submission detects the moved Epoch and
            // discards (remote-cancels) its late provider response.
            Assert.Equal(epoch + 1, state.Epoch);

            // FinalizeEpoch only moves on destructive events — a plain run reset
            // must not invalidate a legitimate poll completion in flight.
            Assert.Equal(finalizeEpoch, state.FinalizeEpoch);
            Assert.Equal(-1, state.PendingSubmissionEpoch);
            Assert.Null(state.DecodeErrors);
            Assert.False(state.CompletionFailed);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.SubmissionTimestamp preserves the original clock across reopen [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.SubmissionTimestamp preserves the original clock across reopen [Core]")]
#endif
        public void SubmissionTimestamp_PreservesOriginalClockAcrossReopen()
        {
            // Simulates a save/reopen: StartTime carries the ORIGINAL submission
            // instant restored from the archive while the reconstructed
            // AIBatchSubmission stamps its construction time (now). Persisting
            // SubmissionTimestamp — not Submission.SubmittedAt — keeps the real
            // elapsed duration on every successive reopen.
            var original = System.DateTime.UtcNow.AddHours(-12);
            var reconstructedSubmission = new ProviderSdk.AICall.Batch.AIBatchSubmission(
                "b", "p", null, new List<string> { "c1" }.AsReadOnly());
            var state = new BatchRunState
            {
                StartTime = original,
                Submission = reconstructedSubmission,
            };

            var persisted = state.SubmissionTimestamp;

            Assert.Equal(new System.DateTimeOffset(original), persisted);
            Assert.NotEqual(reconstructedSubmission.SubmittedAt, persisted);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.SubmissionTimestamp falls back to the live submission clock [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.SubmissionTimestamp falls back to the live submission clock [Core]")]
#endif
        public void SubmissionTimestamp_FallsBackToLiveSubmission()
        {
            var submission = new ProviderSdk.AICall.Batch.AIBatchSubmission(
                "b", "p", null, new List<string> { "c1" }.AsReadOnly());
            var state = new BatchRunState { Submission = submission };

            Assert.Equal(submission.SubmittedAt, state.SubmissionTimestamp);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.NoteSubmissionInstalled invalidates queued finalizations [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.NoteSubmissionInstalled invalidates queued finalizations [Core]")]
#endif
        public void NoteSubmissionInstalled_BumpsFinalizeEpoch()
        {
            var state = new BatchRunState();
            var finalizeEpoch = state.FinalizeEpoch;

            state.NoteSubmissionInstalled();

            // A stale finalization callback captured before the new submission
            // reads the moved epoch and discards itself.
            Assert.Equal(finalizeEpoch + 1, state.FinalizeEpoch);
        }

#if NET7_WINDOWS
        [Fact(DisplayName = "BatchRunState.ResetForNextRun clears staged import state [Windows]")]
#else
        [Fact(DisplayName = "BatchRunState.ResetForNextRun clears staged import state [Core]")]
#endif
        public void ResetForNextRun_ClearsStagedImportState()
        {
            var state = new BatchRunState
            {
                StagedImportResults = new Dictionary<string, Newtonsoft.Json.Linq.JObject>
                {
                    ["c1"] = new Newtonsoft.Json.Linq.JObject(),
                },
                StagedImportMessages = new List<ProviderSdk.Diagnostics.SHRuntimeMessage>(),
            };

            state.ResetForNextRun();

            Assert.Null(state.StagedImportResults);
            Assert.Null(state.StagedImportMessages);
        }
    }
}
