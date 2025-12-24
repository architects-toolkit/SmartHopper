/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Policies;
    using SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns;
    using SmartHopper.Infrastructure.AICall.Utilities;
    using SmartHopper.Infrastructure.AIModels;
    using SmartHopper.Infrastructure.Streaming;

    /// <summary>
    /// ConversationSession partial class containing special turn execution logic.
    /// </summary>
    public sealed partial class ConversationSession
    {
        /// <summary>
        /// Executes a special turn with custom configuration.
        /// Special turns run through the regular conversation flow but can override interactions,
        /// provider settings, tools, and history persistence behavior.
        /// </summary>
        /// <param name="config">The special turn configuration.</param>
        /// <param name="preferStreaming">Whether to prefer streaming execution (can be overridden by config.ForceNonStreaming).</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The AIReturn result from the special turn execution.</returns>
        public async Task<AIReturn> ExecuteSpecialTurnAsync(
            SpecialTurnConfig config,
            bool preferStreaming,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var turnId = Guid.NewGuid().ToString("N");
            Debug.WriteLine($"[ConversationSession.SpecialTurn] Starting special turn: type={config.TurnType}, turnId={turnId}, preferStreaming={preferStreaming}, forceNonStreaming={config.ForceNonStreaming}");

            // Snapshot current request state for final persistence
            var snapshot = this.CreateRequestSnapshot();

            try
            {
                // Create isolated request for special turn execution (prevents observer notifications)
                var specialRequest = this.CloneRequestForSpecialTurn(config);

                // Execute through regular paths on isolated request (respects ForceNonStreaming)
                var useStreaming = preferStreaming && !config.ForceNonStreaming;
                AIReturn result;

                // Link cancellation the same way as TurnLoopAsync:
                // - session cancel token (this.cts.Token)
                // - external token passed to this method
                // - timeout via CancelAfter
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, cancellationToken);
                if (config.TimeoutMs.HasValue)
                {
                    linkedCts.CancelAfter(config.TimeoutMs.Value);
                }

                var effectiveCt = linkedCts.Token;

                if (useStreaming)
                {
                    result = await this.ExecuteStreamingSpecialTurnAsync(specialRequest, config, turnId, effectiveCt).ConfigureAwait(false);
                }
                else
                {
                    result = await this.ExecuteNonStreamingSpecialTurnAsync(specialRequest, config, turnId, effectiveCt).ConfigureAwait(false);
                }

                // Apply persistence strategy to main conversation (this is where observers get notified)
                this.ApplyPersistenceStrategy(config, snapshot, result, turnId);

                Debug.WriteLine($"[ConversationSession.SpecialTurn] Completed special turn: type={config.TurnType}, turnId={turnId}, success={result?.Status == AICallStatus.Finished}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession.SpecialTurn] Error in special turn: type={config.TurnType}, turnId={turnId}, error={ex.Message}");

                // Create error return and persist it to history
                var errorReturn = new AIReturn();
                errorReturn.CreateProviderError($"Special turn failed: {ex.Message}", this.Request);

                // Apply persistence strategy for error
                this.ApplyPersistenceStrategy(config, snapshot, errorReturn, turnId);

                return errorReturn;
            }
        }

        /// <summary>
        /// Executes a special turn using non-streaming mode on an isolated request.
        /// No observer notifications are sent during execution.
        /// </summary>
        private async Task<AIReturn> ExecuteNonStreamingSpecialTurnAsync(
            AIRequestCall specialRequest,
            SpecialTurnConfig config,
            string turnId,
            CancellationToken ct)
        {
            // Execute on isolated request (no observer notifications)
            ct.ThrowIfCancellationRequested();
            var result = await specialRequest.Exec(stream: false).ConfigureAwait(false);

            if (result == null)
            {
                return this.CreateError("Special turn provider returned no response");
            }

            // Apply TurnId to new interactions
            var newInteractions = result.Body?.GetNewInteractions();
            InteractionUtility.EnsureTurnId(newInteractions, turnId);

            // Do NOT notify observers here - they will be notified by ApplyPersistenceStrategy
            return result;
        }

        /// <summary>
        /// Executes a special turn using streaming mode on an isolated request.
        /// No observer notifications are sent during execution.
        /// </summary>
        private async Task<AIReturn> ExecuteStreamingSpecialTurnAsync(
            AIRequestCall specialRequest,
            SpecialTurnConfig config,
            string turnId,
            CancellationToken ct)
        {
            // Ensure request policies are applied for streaming special turns as well.
            // Streaming adapters bypass AIRequestCall.Exec(), so policies like ContextInjectionRequestPolicy
            // must be applied explicitly to keep context up-to-date.
            await PolicyPipeline.Default.ApplyRequestPoliciesAsync(specialRequest).ConfigureAwait(false);

            var exec = new DefaultProviderExecutor();
            var adapter = exec.TryGetStreamingAdapter(specialRequest);

            if (adapter == null)
            {
                // Provider doesn't support streaming, fall back to non-streaming
                Debug.WriteLine($"[ConversationSession.SpecialTurn] Provider doesn't support streaming, falling back to non-streaming");
                return await this.ExecuteNonStreamingSpecialTurnAsync(specialRequest, config, turnId, ct).ConfigureAwait(false);
            }

            AIReturn? lastDelta = null;
            AIInteractionText? accumulatedText = null;

            // Stream deltas internally (no observer notifications)
            await foreach (var rawDelta in adapter.StreamAsync(specialRequest, new StreamingOptions(), ct))
            {
                if (rawDelta == null)
                {
                    continue;
                }

                // Normalize delta to handle provider-specific formats
                var delta = adapter.NormalizeDelta(rawDelta);

                // Apply TurnId to new interactions
                var newInteractions = delta.Body?.GetNewInteractions();
                InteractionUtility.EnsureTurnId(newInteractions, turnId);

                // Do NOT notify observers here - special turn execution is isolated
                // Accumulate text deltas in memory
                if (newInteractions != null)
                {
                    foreach (var interaction in newInteractions)
                    {
                        if (interaction is AIInteractionText textDelta)
                        {
                            accumulatedText = TextStreamCoalescer.Coalesce(
                                accumulatedText,
                                textDelta,
                                turnId,
                                preserveMetrics: false);
                        }
                    }
                }

                lastDelta = delta;
            }

            if (lastDelta == null)
            {
                return this.CreateError("Special turn streaming returned no response");
            }

            // Build final result with accumulated text by reusing the provider's last delta
            // This preserves status/metrics and avoids assigning to read-only properties
            var builder = AIBodyBuilder.Create();

            if (accumulatedText != null && !string.IsNullOrWhiteSpace(accumulatedText.Content))
            {
                accumulatedText.TurnId = turnId;
                builder.Add(accumulatedText);
            }

            var finalBody = builder.Build();

            // Reuse lastDelta AIReturn instance and only replace its body
            lastDelta.SetBody(finalBody);

            // Do NOT notify observers here - they will be notified by ApplyPersistenceStrategy
            return lastDelta;
        }

        /// <summary>
        /// Creates a snapshot of the current request state before applying special turn overrides.
        /// </summary>
        private RequestSnapshot CreateRequestSnapshot()
        {
            return new RequestSnapshot
            {
                OriginalBody = this.Request.Body,
                OriginalProvider = this.Request.Provider,
                OriginalModel = this.Request.Model,
                OriginalEndpoint = this.Request.Endpoint,
                OriginalCapability = this.Request.Capability,
            };
        }

        /// <summary>
        /// Creates an isolated request for special turn execution with config overrides applied.
        /// This prevents observer notifications during special turn execution.
        /// </summary>
        private AIRequestCall CloneRequestForSpecialTurn(SpecialTurnConfig config)
        {
            // Clone the body for isolated execution
            var builder = AIBodyBuilder.Create();

            if (config.OverrideInteractions != null)
            {
                // Use override interactions
                builder.AddRange(config.OverrideInteractions);
            }
            else
            {
                // Clone existing interactions from main conversation
                var existingInteractions = this.Request.Body?.Interactions?.ToList();
                if (existingInteractions != null && existingInteractions.Count > 0)
                {
                    builder.AddRange(existingInteractions);
                }
            }

            // Apply context and tool filters
            if (config.OverrideContextFilter != null)
            {
                builder.WithContextFilter(config.OverrideContextFilter);
            }
            else if (this.Request.Body?.ContextFilter != null)
            {
                builder.WithContextFilter(this.Request.Body.ContextFilter);
            }

            if (config.OverrideToolFilter != null)
            {
                builder.WithToolFilter(config.OverrideToolFilter);
            }
            else if (this.Request.Body?.ToolFilter != null)
            {
                builder.WithToolFilter(this.Request.Body.ToolFilter);
            }

            // Create new isolated request (use parameterless ctor and assign Body explicitly)
            var specialRequest = new AIRequestCall();
            specialRequest.Body = builder.Build();
            specialRequest.Provider = config.OverrideProvider ?? this.Request.Provider;
            specialRequest.Model = config.OverrideModel ?? this.Request.Model;
            specialRequest.Endpoint = config.OverrideEndpoint ?? this.Request.Endpoint;
            specialRequest.Capability = config.OverrideCapability ?? this.Request.Capability;

            Debug.WriteLine($"[ConversationSession.SpecialTurn] Created isolated request: provider={specialRequest.Provider}, model={specialRequest.Model}");
            return specialRequest;
        }

        /// <summary>
        /// Applies the persistence strategy to the special turn result.
        /// </summary>
        private void ApplyPersistenceStrategy(
            SpecialTurnConfig config,
            RequestSnapshot snapshot,
            AIReturn result,
            string turnId)
        {
            // First, restore the original body
            this.Request.Body = snapshot.OriginalBody;

            switch (config.PersistenceStrategy)
            {
                case HistoryPersistenceStrategy.PersistResult:
                    this.PersistResultOnly(result, turnId);
                    break;

                case HistoryPersistenceStrategy.PersistAll:
                    this.PersistAll(result, config.PersistenceFilter, turnId);
                    break;

                case HistoryPersistenceStrategy.Ephemeral:
                    // Don't persist anything, just restore snapshot
                    Debug.WriteLine($"[ConversationSession.SpecialTurn] Ephemeral turn, not persisting to history");
                    break;

                case HistoryPersistenceStrategy.ReplaceAbove:
                    this.ReplaceAbove(snapshot, result, config.PersistenceFilter, turnId);
                    break;
            }
        }

        /// <summary>
        /// Persists only the result interactions to history.
        /// </summary>
        private void PersistResultOnly(AIReturn result, string turnId)
        {
            var resultInteractions = result?.Body?.Interactions?.Where(i => i.Agent == AIAgent.Assistant).ToList();
            if (resultInteractions != null && resultInteractions.Count > 0)
            {
                foreach (var interaction in resultInteractions)
                {
                    if (string.IsNullOrWhiteSpace(interaction.TurnId))
                    {
                        interaction.TurnId = turnId;
                    }

                    this.AppendToSessionHistory(interaction);
                }

                this.UpdateLastReturn();
                this.NotifyFinal(this.GetHistoryReturn());
            }
        }

        /// <summary>
        /// Persists all interactions from the special turn to history, filtered by the specified filter.
        /// </summary>
        private void PersistAll(AIReturn result, InteractionFilter? filter, string turnId)
        {
            filter = filter ?? InteractionFilter.Default;
            var interactions = result?.Body?.Interactions?.Where(i => filter.ShouldInclude(i)).ToList();

            if (interactions != null && interactions.Count > 0)
            {
                foreach (var interaction in interactions)
                {
                    if (string.IsNullOrWhiteSpace(interaction.TurnId))
                    {
                        interaction.TurnId = turnId;
                    }

                    this.AppendToSessionHistory(interaction);
                }

                this.UpdateLastReturn();
                this.NotifyFinal(this.GetHistoryReturn());
            }
        }

        /// <summary>
        /// Replaces all previous interactions (filtered) with the special turn result.
        /// </summary>
        private void ReplaceAbove(RequestSnapshot snapshot, AIReturn result, InteractionFilter? filter, string turnId)
        {
            filter = filter ?? InteractionFilter.PreserveSystemContext;

            // Get interactions to preserve from original history
            var originalInteractions = snapshot.OriginalBody?.Interactions?.ToList() ?? new List<IAIInteraction>();
            var preservedInteractions = originalInteractions.Where(i => !filter.ShouldInclude(i)).ToList();

            // Get result interactions
            var resultInteractions = result?.Body?.Interactions?.ToList() ?? new List<IAIInteraction>();

            // Build new body with preserved interactions + result
            var builder = AIBodyBuilder.Create()
                .WithToolFilter(this.Request.Body?.ToolFilter)
                .WithContextFilter(this.Request.Body?.ContextFilter)
                .WithJsonOutputSchema(this.Request.Body?.JsonOutputSchema)
                .AsHistory();
            builder.AddRange(preservedInteractions);

            foreach (var interaction in resultInteractions)
            {
                if (string.IsNullOrWhiteSpace(interaction.TurnId))
                {
                    interaction.TurnId = turnId;
                }

                builder.Add(interaction, markAsNew: false);
            }

            this.Request.Body = builder.Build();
            this.UpdateLastReturn();
            this.NotifyFinal(this.GetHistoryReturn());
        }

        /// <summary>
        /// Snapshot of request state for restoration after special turn.
        /// </summary>
        private class RequestSnapshot
        {
            public AIBody OriginalBody { get; set; }

            public string OriginalProvider { get; set; }

            public string OriginalModel { get; set; }

            public string OriginalEndpoint { get; set; }

            public AICapability OriginalCapability { get; set; }
        }
    }
}
