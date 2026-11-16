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
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Providers.Test
{
    /// <summary>
    /// Deterministic local provider used to drive the batch lifecycle end to end
    /// without network, credentials, or cost. Batch calls are fully scriptable
    /// through <see cref="TestBatchControl"/>; every entry point also records an
    /// event so the live assertion surface can verify submission/poll/cancel
    /// ordering. Progression is derived from
    /// <see cref="AIBatchSubmission.SubmittedAt"/> elapsed time, so restored
    /// submissions behave deterministically after a host restart without any
    /// surviving provider-side state — the exact property the restore
    /// scenarios exercise.
    /// <para/>
    /// Synchronous <see cref="AIProvider.Call"/> is intentionally left unwired:
    /// no endpoint is ever resolved, so stray non-batch requests return a
    /// structured "did not resolve a wire endpoint" error instead of touching
    /// the network.
    /// </summary>
    public class TestProvider : AIProvider<TestProvider>, IAIBatchProvider
    {
        /// <summary>Model name registered by <see cref="TestProviderModels"/>.</summary>
        public const string BatchModel = "test-batch";

        private int _batchSequence;

        private TestProvider()
        {
            this.Models = new TestProviderModels(this);
        }

        /// <inheritdoc/>
        public override string Name => "TestProvider";

        /// <inheritdoc/>
        public override Image Icon => null;

        /// <inheritdoc/>
        public override bool IsEnabled => true;

        /// <inheritdoc/>
        public override bool IsConfigured => true;

        /// <inheritdoc/>
        public override Uri DefaultServerUrl => new Uri("http://localhost");

        /// <inheritdoc/>
        public override string Encode(AIRequestCall request) => "{\"stub\":true}";

        /// <inheritdoc/>
        public override string Encode(IAIInteraction interaction) => string.Empty;

        /// <inheritdoc/>
        /// <remarks>
        /// Result bodies produced by <see cref="GetBatchStatusAsync"/> carry a
        /// single <c>text</c> property; decoding wraps it in an assistant text
        /// interaction, mirroring the simplest real-provider shape.
        /// </remarks>
        public override List<IAIInteraction> Decode(JObject response)
        {
            var text = response?["text"]?.ToString() ?? string.Empty;
            return new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.Assistant, Content = text },
            };
        }

        /// <inheritdoc/>
        public override string SelectModel(AICapability requiredCapability, string requestedModel) => BatchModel;

        /// <inheritdoc/>
        public async Task<AIBatchSubmission> SubmitBatchAsync(
            IReadOnlyList<(string CustomId, AIRequestCall Request)> items,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref TestBatchControl.SubmitCalls);
            var ids = items?.Select(i => i.CustomId).ToList() ?? new List<string>();
            TestBatchControl.Record($"SUBMIT items={ids.Count} delay={TestBatchControl.SubmitDelayMs}ms");

            if (TestBatchControl.SubmitDelayMs > 0)
            {
                await Task.Delay(TestBatchControl.SubmitDelayMs, cancellationToken).ConfigureAwait(false);
            }

            if (TestBatchControl.ThrowOnSubmit)
            {
                TestBatchControl.Record("SUBMIT threw (ThrowOnSubmit)");
                throw new InvalidOperationException("TestProvider: submit forced to throw by TestBatchControl.ThrowOnSubmit.");
            }

            var batchId = $"test-batch-{Interlocked.Increment(ref this._batchSequence)}";
            var submission = new AIBatchSubmission(
                batchId,
                this.Name,
                "{\"stub\":true}",
                ids.AsReadOnly());

            TestBatchControl.Record($"SUBMITTED {batchId} customIds=[{string.Join(", ", ids)}]");
            return submission;
        }

        /// <inheritdoc/>
        public Task<AIBatchStatus> GetBatchStatusAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref TestBatchControl.PollCalls);
            var batchId = submission?.BatchId ?? "unknown";

            if (TestBatchControl.CancelledBatches.ContainsKey(batchId))
            {
                TestBatchControl.Record($"POLL {batchId} -> Cancelled");
                return Task.FromResult(new AIBatchStatus(batchId, AIBatchState.Cancelled));
            }

            var elapsedMs = (DateTimeOffset.UtcNow - (submission?.SubmittedAt ?? DateTimeOffset.UtcNow)).TotalMilliseconds;
            if (elapsedMs < TestBatchControl.CompleteAfterMs)
            {
                TestBatchControl.Record($"POLL {batchId} -> InProgress ({elapsedMs:F0}ms < {TestBatchControl.CompleteAfterMs}ms)");
                return Task.FromResult(new AIBatchStatus(batchId, AIBatchState.InProgress));
            }

            if (TestBatchControl.FailWholeBatch)
            {
                TestBatchControl.Record($"POLL {batchId} -> Failed (FailWholeBatch)");
                return Task.FromResult(new AIBatchStatus(batchId, AIBatchState.Failed, "TestProvider: batch forced to fail by TestBatchControl.FailWholeBatch."));
            }

            var results = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var ids = submission?.CustomIds ?? (IReadOnlyList<string>)Array.Empty<string>();
            var omit = TestBatchControl.OmitResultIndices ?? Array.Empty<int>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (omit.Contains(i))
                {
                    continue;
                }

                results[ids[i]] = new JObject { ["text"] = $"ok {ids[i]}" };
            }

            TestBatchControl.Record($"POLL {batchId} -> Completed results={results.Count}/{ids.Count}");
            return Task.FromResult(new AIBatchStatus(batchId, results));
        }

        /// <inheritdoc/>
        public Task CancelBatchAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref TestBatchControl.CancelCalls);
            var batchId = submission?.BatchId ?? "unknown";
            TestBatchControl.CancelledBatches[batchId] = true;
            TestBatchControl.Record($"CANCEL {batchId}");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> DownloadBatchResultsAsync(AIBatchSubmission submission, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref TestBatchControl.DownloadCalls);
            var ids = submission?.CustomIds ?? (IReadOnlyList<string>)Array.Empty<string>();
            var lines = ids
                .Select(id => new JObject
                {
                    ["custom_id"] = id,
                    ["response"] = new JObject { ["text"] = $"ok {id}" },
                }.ToString(Newtonsoft.Json.Formatting.None))
                .ToList();

            TestBatchControl.Record($"DOWNLOAD {submission?.BatchId ?? "unknown"} lines={lines.Count}");
            return Task.FromResult((IReadOnlyList<string>)lines.AsReadOnly());
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Parses the JSONL shape emitted by <see cref="DownloadBatchResultsAsync"/>:
        /// <c>{"custom_id": "...", "response": {…}}</c> per line. First-wins on
        /// duplicate custom ids, matching the shared merge contract.
        /// </remarks>
        public AIBatchStatus ParseBatchResultsFiles(IReadOnlyList<string> fileContents, string? batchId = null)
        {
            var results = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var content in fileContents ?? Enumerable.Empty<string>())
            {
                foreach (var line in (content ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(line);
                    }
                    catch (Newtonsoft.Json.JsonException)
                    {
                        continue;
                    }

                    var customId = parsed["custom_id"]?.ToString();
                    var response = parsed["response"] as JObject;
                    if (!string.IsNullOrEmpty(customId) && response != null && !results.ContainsKey(customId))
                    {
                        results[customId] = response;
                    }
                }
            }

            TestBatchControl.Record($"PARSE files={fileContents?.Count ?? 0} -> results={results.Count}");
            return new AIBatchStatus(batchId, results);
        }

        /// <inheritdoc/>
        protected override string GetApiKey() => "test-key";
    }
}
