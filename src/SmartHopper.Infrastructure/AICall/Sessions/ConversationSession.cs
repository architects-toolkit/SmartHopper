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

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Metrics;
    using SmartHopper.Infrastructure.AICall.Policies;
    using SmartHopper.Infrastructure.AICall.Utilities;
    using SmartHopper.Infrastructure.Settings;
    using SmartHopper.Infrastructure.Streaming;

    /// <summary>
    /// Conversation session that manages chat history and orchestrates multi-turn conversations.
    /// Handles both streaming and non-streaming execution with proper history management.
    /// </summary>
    /// <remarks>
    /// Invariants:
    /// - Providers are invoked only if there are no pending tool calls (PendingToolCallsCount() == 0).
    /// - Only provider turns increment the MaxTurns counter (tool passes do not consume turns).
    /// - Streaming text deltas are accumulated in memory and only the final aggregated text is persisted to history.
    /// - Non-text interactions (tool calls, tool results) are persisted immediately as they arrive.
    /// </remarks>
    public sealed partial class ConversationSession : IConversationSession
    {
        /// <summary>
        /// Result of streaming processing, encapsulating accumulated state and deltas.
        /// </summary>
        private sealed class StreamProcessingResult
        {
            public AIInteractionText? AccumulatedText { get; set; }
            public AIReturn? LastDelta { get; set; }
            public AIReturn? LastToolCallsDelta { get; set; }
            public List<AIReturn> Deltas { get; } = new List<AIReturn>();
            public double ElapsedSeconds { get; set; }
            public bool HasError { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// The cancellation token source for this session.
        /// </summary>
        private readonly CancellationTokenSource cts = new();

        /// <summary>
        /// Executor abstraction for provider and tool calls.
        /// </summary>
        private readonly IProviderExecutor executor;

        /// <summary>
        /// The initial request that started this conversation session.
        /// </summary>
        private readonly AIRequestCall _initialRequest;

        /// <summary>
        /// Indicates whether greeting generation was requested for this session.
        /// Becomes false after a greeting is emitted to ensure one-shot behavior.
        /// </summary>
        private bool _generateGreeting;

        /// <summary>
        /// The last complete return from the conversation.
        /// </summary>
        private AIReturn _lastReturn = new AIReturn();

        /// <summary>
        /// Tracks whether the greeting was inserted into the session history and emitted to observers.
        /// Used to short-circuit provider execution for greeting-only initialization runs.
        /// </summary>
        private bool _greetingEmitted;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationSession"/> class.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <param name="observer">The observer to notify of events.</param>
        /// <param name="executor">The provider executor to use for tool calls.</param>
        /// <param name="generateGreeting">Whether to generate an AI greeting when the conversation is initialized.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is null.</exception>
        public ConversationSession(AIRequestCall request, IConversationObserver? observer = null, IProviderExecutor? executor = null, bool generateGreeting = false)
        {
            this.Request = request ?? throw new ArgumentNullException(nameof(request));
            this._initialRequest = request;
            this.Observer = observer;
            this.executor = executor ?? new DefaultProviderExecutor();
            this._generateGreeting = generateGreeting;

            // Initialize _lastReturn with initial request body
            this._lastReturn.SetBody(request.Body);
        }

        /// <summary>
        /// Validates the request before starting a session execution path.
        /// Centralizes the <c>WantsStreaming</c> flag and aggregates error messages into an <see cref="AIReturn"/> when invalid.
        /// </summary>
        /// <param name="wantsStreaming">When true, applies streaming-related validation rules; when false, validates non-streaming constraints.</param>
        /// <returns>
        /// A tuple where <c>IsValid</c> indicates whether the request is valid for the intended mode,
        /// and <c>Error</c> contains an <see cref="AIReturn"/> populated with error messages if invalid; otherwise null.
        /// </returns>
        private (bool IsValid, AIReturn? Error) ValidateBeforeStart(bool wantsStreaming)
        {
            // Set streaming intent for validation rules
            this.Request.WantsStreaming = wantsStreaming;

            var validation = this.Request.IsValid();
            if (validation.IsValid)
            {
                return (true, null);
            }

            var errorMessages = validation.Errors?
                .Where(m => m.Severity == AIRuntimeMessageSeverity.Error)
                .Select(m => m.Message)
                .ToList();

            var combined = (errorMessages != null && errorMessages.Count > 0)
                ? string.Join(" \n", errorMessages)
                : "Request validation failed.";

            var err = new AIReturn();
            err.CreateError(combined, this.Request);
            return (false, err);
        }

        /// <summary>
        /// Unified turn loop that powers both non-streaming and streaming APIs.
        /// It emits a sequence of AIReturns according to the chosen mode:
        /// - yieldDeltas=false: emits only stable results at key points (typically one item)
        /// - yieldDeltas=true: emits streaming deltas, tool results, and final snapshots.
        /// </summary>
        private async IAsyncEnumerable<AIReturn> TurnLoopAsync(
            SessionOptions options,
            StreamingOptions? streamingOptions,
            bool yieldDeltas,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, options.CancellationToken, cancellationToken);
            try
            {
                this.NotifyStart(this.Request);

                // Generate greeting if requested before starting conversation
                if (this._generateGreeting && !this._greetingEmitted)
                {
                    var greetingResult = await this.GenerateGreetingAsync(streamChunks: yieldDeltas, cancellationToken: linkedCts.Token).ConfigureAwait(false);
                    if (greetingResult != null)
                    {
                        // Reset flags so subsequent calls (after user messages) will proceed
                        this._greetingEmitted = false;
                        this._generateGreeting = false;
                        yield return greetingResult;
                        yield break;
                    }
                }

                // Centralized validation: early interrupt if request is invalid
                var (ok, err) = this.ValidateBeforeStart(wantsStreaming: yieldDeltas);
                if (!ok)
                {
                    this.Observer?.OnFinal(err ?? new AIReturn());
                    if (err != null)
                    {
                        yield return err;
                    }

                    yield break;
                }

                int turns = 0;
                AIReturn lastReturn = null;

                while (turns < options.MaxTurns)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();

                    // Allocate a fresh TurnId for this assistant turn
                    var turnId = Guid.NewGuid().ToString("N");

                    // Reset summarization flag at the start of each turn
                    this.ResetSummarizationFlag();

                    // Check context usage before provider call and summarize if needed (applies to both streaming and non-streaming)
                    await this.CheckAndSummarizeContextAsync(linkedCts.Token).ConfigureAwait(false);

                    if (!yieldDeltas)
                    {
                        AIReturn nsPrepared = null;
                        AIReturn nsError = null;
                        bool nsShouldBreak = false;

                        try
                        {
                            // Non-streaming composite turn
                            nsPrepared = await this.ExecuteProviderTurnAsync(options, turnId, linkedCts.Token).ConfigureAwait(false);

                            // Reactive summarization: if provider failed due to context size, summarize once and retry.
                            if (IsContextExceededReturn(nsPrepared))
                            {
                                var retryOk = await this.TrySummarizeAndRetryAsync("Provider returned context exceeded error", linkedCts.Token).ConfigureAwait(false);
                                if (retryOk)
                                {
                                    Debug.WriteLine("[ConversationSession] Retrying provider turn after summarization (non-streaming)");
                                    nsPrepared = await this.ExecuteProviderTurnAsync(options, turnId, linkedCts.Token).ConfigureAwait(false);
                                }
                            }

                            lastReturn = nsPrepared;

                            if (!options.ProcessTools)
                            {
                                this.NotifyFinal(nsPrepared);
                                nsShouldBreak = true;
                            }
                            else if (this.Request.Body.PendingToolCallsCount() == 0)
                            {
                                var finalStable = lastReturn ?? new AIReturn();
                                this._lastReturn = finalStable;
                                this.UpdateLastReturn();
                                this.NotifyFinal(finalStable);
                                nsPrepared = finalStable;
                                nsShouldBreak = true;
                            }
                        }
                        catch (OperationCanceledException oce)
                        {
                            nsError = this.HandleAndNotifyError(oce);
                            nsShouldBreak = true;
                        }
                        catch (Exception ex)
                        {
                            // Reactive summarization: if the provider threw due to context size, summarize once and retry.
                            if (IsContextExceededError(ex))
                            {
                                var retryOk = await this.TrySummarizeAndRetryAsync("Provider threw context exceeded exception", linkedCts.Token).ConfigureAwait(false);
                                if (retryOk)
                                {
                                    try
                                    {
                                        Debug.WriteLine("[ConversationSession] Retrying provider turn after summarization (non-streaming exception)");
                                        nsPrepared = await this.ExecuteProviderTurnAsync(options, turnId, linkedCts.Token).ConfigureAwait(false);
                                        lastReturn = nsPrepared;

                                        if (!options.ProcessTools)
                                        {
                                            this.NotifyFinal(nsPrepared);
                                            nsShouldBreak = true;
                                        }
                                        else if (this.Request.Body.PendingToolCallsCount() == 0)
                                        {
                                            var finalStable = lastReturn ?? new AIReturn();
                                            this._lastReturn = finalStable;
                                            this.UpdateLastReturn();
                                            this.NotifyFinal(finalStable);
                                            nsPrepared = finalStable;
                                            nsShouldBreak = true;
                                        }
                                    }
                                    catch (Exception retryEx)
                                    {
                                        nsError = this.HandleAndNotifyError(retryEx);
                                        nsShouldBreak = true;
                                    }
                                }
                                else
                                {
                                    nsError = this.HandleAndNotifyError(ex);
                                    nsShouldBreak = true;
                                }
                            }
                            else
                            {
                                nsError = this.HandleAndNotifyError(ex);
                                nsShouldBreak = true;
                            }
                            nsShouldBreak = true;
                        }

                        // Perform emissions outside try/catch to satisfy iterator constraints
                        if (nsError != null)
                        {
                            yield return nsError;
                            yield break;
                        }

                        if (nsPrepared != null)
                        {
                            yield return nsPrepared;
                            if (nsShouldBreak)
                            {
                                yield break;
                            }
                        }

                        // Otherwise, continue to next turn
                        turns++;
                        continue;
                    }

                    // Streaming path
                    // Prepare per-turn state
                    TurnState state = new TurnState
                    {
                        TurnId = turnId,
                        DeltaYields = new List<AIReturn>(),
                        PendingToolYields = new List<AIReturn>(),
                        FinalProviderYield = null,
                        ErrorYield = null,
                        ShouldBreak = false,
                        LastDelta = null,
                        LastToolCallsDelta = null,
                        AccumulatedText = null,
                    };

                    try
                    {
                        // Try to obtain a streaming adapter and stream deltas inline
                        var exec = new DefaultProviderExecutor();
                        var adapter = exec.TryGetStreamingAdapter(this.Request);

                        if (adapter == null)
                        {
                            // Provider doesn't support streaming: reuse the non-stream composite path
                            var composite = await this.ExecuteProviderTurnAsync(options, state.TurnId, linkedCts.Token).ConfigureAwait(false);
                            lastReturn = composite;
                            this.NotifyFinal(composite);
                            state.FinalProviderYield = composite;
                            state.ShouldBreak = true;
                        }
                        else
                        {
                            // Streaming path: use shared helper for delta processing
                            var streamResult = await this.ProcessStreamingDeltasAsync(adapter, streamingOptions!, state.TurnId, linkedCts.Token).ConfigureAwait(false);

                            // Reactive summarization: if streaming failed due to context size, summarize once and retry streaming.
                            if (streamResult.HasError && ConversationSession.IsContextExceededError(streamResult.ErrorMessage))
                            {
                                var retryOk = await this.TrySummarizeAndRetryAsync("Streaming provider returned context exceeded error", linkedCts.Token).ConfigureAwait(false);
                                if (retryOk)
                                {
                                    Debug.WriteLine("[ConversationSession] Retrying streaming after summarization");
                                    streamResult = await this.ProcessStreamingDeltasAsync(adapter, streamingOptions!, state.TurnId, linkedCts.Token).ConfigureAwait(false);
                                }
                            }

                            // Transfer results to turn state
                            state.AccumulatedText = streamResult.AccumulatedText;
                            state.LastDelta = streamResult.LastDelta;
                            state.LastToolCallsDelta = streamResult.LastToolCallsDelta;
                            state.DeltaYields.AddRange(streamResult.Deltas);
                            if (streamResult.LastDelta != null)
                            {
                                lastReturn = streamResult.LastDelta;
                            }

                            if (streamResult.HasError)
                            {
                                var errDelta = this.CreateError(streamResult.ErrorMessage ?? "Provider returned no response");
                                this.NotifyFinal(errDelta);
                                state.ErrorYield = errDelta;
                                state.ShouldBreak = true;
                            }
                            else
                            {
                                // Persist final aggregated text and update last-return snapshot with measured time
                                this.PersistStreamingSnapshot(state.LastToolCallsDelta, state.LastDelta, state.TurnId, state.AccumulatedText, streamResult.ElapsedSeconds);

                                if (!options.ProcessTools)
                                {
                                    this.NotifyFinal(this._lastReturn);
                                    state.FinalProviderYield = this._lastReturn;
                                    state.ShouldBreak = true;
                                }
                                else
                                {
                                    // Process tools after streaming completes - continue using streaming for follow-up responses
                                    state.PendingToolYields = await this.ProcessPendingToolsAsync(options, state.TurnId, linkedCts.Token, streamingOptions, useStreaming: true).ConfigureAwait(false);
                                    if (state.PendingToolYields.Count > 0)
                                    {
                                        lastReturn = state.PendingToolYields.Last();
                                    }

                                    // If stable, finish the turn - use _lastReturn which has persisted interactions with correct TurnId
                                    if (this.Request.Body.PendingToolCallsCount() == 0)
                                    {
                                        this.NotifyFinal(this._lastReturn);
                                        state.ShouldBreak = true;
                                    }

                                    state.FinalProviderYield = this._lastReturn;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        state.ErrorYield = this.HandleAndNotifyError(oce);
                        state.ShouldBreak = true;
                    }
                    catch (Exception ex)
                    {
                        // Reactive summarization: if we blew context length, summarize once and retry with streaming disabled.
                        if (IsContextExceededError(ex))
                        {
                            var retryOk = await this.TrySummarizeAndRetryAsync("Streaming provider threw context exceeded exception", linkedCts.Token).ConfigureAwait(false);
                            if (retryOk)
                            {
                                try
                                {
                                    Debug.WriteLine("[ConversationSession] Retrying as non-streaming after summarization (streaming exception)");
                                    var composite = await this.ExecuteProviderTurnAsync(options, state.TurnId, linkedCts.Token).ConfigureAwait(false);
                                    lastReturn = composite;
                                    this.NotifyFinal(composite);
                                    state.FinalProviderYield = composite;
                                    state.ShouldBreak = true;
                                }
                                catch (Exception retryEx)
                                {
                                    state.ErrorYield = this.HandleAndNotifyError(retryEx);
                                    state.ShouldBreak = true;
                                }
                            }
                            else
                            {
                                state.ErrorYield = this.HandleAndNotifyError(ex);
                                state.ShouldBreak = true;
                            }
                        }
                        else
                        {
                            state.ErrorYield = this.HandleAndNotifyError(ex);
                            state.ShouldBreak = true;
                        }
                        state.ShouldBreak = true;
                    }

                    // Perform all yields outside the try/catch blocks to satisfy iterator constraints
                    if (state.DeltaYields != null && state.DeltaYields.Count > 0)
                    {
                        foreach (var dy in state.DeltaYields)
                        {
                            yield return dy;
                        }
                    }

                    if (state.PendingToolYields != null && state.PendingToolYields.Count > 0)
                    {
                        foreach (var toolYield in state.PendingToolYields)
                        {
                            yield return toolYield;
                        }
                    }

                    if (state.FinalProviderYield != null)
                    {
                        yield return state.FinalProviderYield;
                    }

                    if (state.ErrorYield != null)
                    {
                        yield return state.ErrorYield;
                    }

                    if (state.ShouldBreak)
                    {
                        yield break;
                    }

                    turns++;
                }

                // Max turns reached without stability
                var final = lastReturn ?? this.CreateError("Max turns reached without a stable result");
                this._lastReturn = final;
                this.UpdateLastReturn();
                this.NotifyFinal(final);
                yield return final;
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        /// <inheritdoc/>
        public AIRequestCall Request { get; }

        /// <summary>
        /// Gets the observer for this session.
        /// </summary>
        public IConversationObserver? Observer { get; }

        /// <summary>
        /// Adds a new user interaction to the conversation.
        /// </summary>
        /// <param name="userMessage">The user message content.</param>
        public void AddInteraction(string userMessage)
        {
            var userInteraction = new AIInteractionText
            {
                Agent = AIAgent.User,
                Content = userMessage,
                TurnId = InteractionUtility.GenerateTurnId(),
            };

            // Append user input to session history without marking it as 'new'
            this.AppendToSessionHistory(userInteraction);
            this.UpdateLastReturn();

            // Notify observer so user message is rendered in the UI
            this.Observer?.OnInteractionCompleted(userInteraction);
        }

        /// <summary>
        /// Adds a new interaction to the conversation.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddInteraction(IAIInteraction interaction)
        {
            if (interaction != null)
            {
                // Append generic interaction to session history without marking it as 'new'
                this.AppendToSessionHistory(interaction);
                this.UpdateLastReturn();
            }
        }

        /// <summary>
        /// Gets the current conversation return with full history.
        /// </summary>
        /// <returns>AIReturn containing all interactions in the conversation.</returns>
        public AIReturn GetHistoryReturn()
        {
            var historyReturn = new AIReturn();
            historyReturn.SetBody(this.Request.Body);
            return historyReturn;
        }

        /// <summary>
        /// Gets all interactions in the conversation history.
        /// </summary>
        /// <returns>List of all interactions.</returns>
        public List<IAIInteraction> GetHistoryInteractionList()
        {
            return this.Request.Body?.Interactions?.ToList() ?? new List<IAIInteraction>();
        }

        /// <summary>
        /// Gets the last <see cref="AIReturn"/> produced by the conversation.
        /// </summary>
        public AIReturn LastReturn => this._lastReturn;

        /// <summary>
        /// Gets only the new interactions from the last conversation turn.
        /// </summary>
        /// <returns>List of new interactions.</returns>
        public List<IAIInteraction> GetNewInteractionList()
        {
            return this._lastReturn.Body?.GetNewInteractions()?.ToList() ?? new List<IAIInteraction>();
        }

        /// <summary>
        /// Aggregates metrics from interactions in the conversation.
        /// </summary>
        /// <param name="newInteractionsOnly">When true, returns metrics from new interactions only; when false, returns metrics from all history.</param>
        /// <returns>Combined <see cref="AIMetrics"/>.</returns>
        public AIMetrics GetCombinedMetrics(bool newInteractionsOnly = false)
        {
            var combined = new AIMetrics();
            var interactions = newInteractionsOnly
                ? this.GetNewInteractionList()
                : this.GetHistoryInteractionList();

            foreach (var interaction in interactions)
            {
                if (interaction?.Metrics != null)
                {
                    combined.Combine(interaction.Metrics);
                }
            }

            return combined;
        }

        /// <summary>
        /// Aggregates metrics from all interactions belonging to a specific turn.
        /// A turn is defined as all interactions sharing the same TurnId.
        /// This includes assistant messages, tool calls, and tool results within the turn.
        /// </summary>
        /// <param name="turnId">The TurnId to aggregate metrics for.</param>
        /// <returns>Combined <see cref="AIMetrics"/> for the specified turn, or empty metrics if turnId is null/empty or no matching interactions found.</returns>
        public AIMetrics GetTurnMetrics(string turnId)
        {
            var combined = new AIMetrics();

            if (string.IsNullOrWhiteSpace(turnId))
            {
                return combined;
            }

            var interactions = this.GetHistoryInteractionList();

            foreach (var interaction in interactions
                .Where(i => i?.Metrics != null && string.Equals(i.TurnId, turnId, StringComparison.Ordinal)))
            {
                combined.Combine(interaction.Metrics);
            }

            return combined;
        }

        /// <summary>
        /// Generates an AI greeting using the special turn system.
        /// </summary>
        /// <param name="streamChunks">When true, uses streaming mode for greeting generation.</param>
        /// <param name="cancellationToken">Cancellation token for the greeting generation.</param>
        /// <returns>The AIReturn with the greeting, or null if greeting is disabled or failed.</returns>
        private async Task<AIReturn?> GenerateGreetingAsync(bool streamChunks = false, CancellationToken cancellationToken = default)
        {
            // Check if AI greeting is enabled in settings
            var settings = SmartHopperSettings.Instance;
            if (!settings.SmartHopperAssistant.EnableAIGreeting)
            {
                Debug.WriteLine("[ConversationSession] AI greeting disabled in settings, skipping greeting generation");
                return null;
            }

            try
            {
                // Extract system prompt from initial request
                var systemMessageText = this._initialRequest?.Body?.Interactions?
                    .OfType<AIInteractionText>()
                    .FirstOrDefault(x => x.Agent == AIAgent.System);

                // Create greeting special turn configuration
                var greetingConfig = SpecialTurns.BuiltIn.GreetingSpecialTurn.Create(
                    this._initialRequest.Provider,
                    systemMessageText?.Content);

                Debug.WriteLine($"[ConversationSession] Generating greeting via special turn: provider={this._initialRequest.Provider}, streaming={streamChunks}");

                // Execute greeting as a special turn
                var result = await this.ExecuteSpecialTurnAsync(
                    greetingConfig,
                    preferStreaming: streamChunks,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Mark greeting as emitted
                this._greetingEmitted = true;

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession] Error generating greeting via special turn: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Executes the provider request without streaming and measures completion time.
        /// </summary>
        /// <param name="ct">A cancellation token for the operation.</param>
        /// <returns>The provider's <see cref="AIReturn"/>, or null if execution failed to produce a result.</returns>
        private async Task<AIReturn?> ExecProviderAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            var res = await this.Request.Exec(stream: false).ConfigureAwait(false);
            stopwatch.Stop();

            // Attach completion time to the last interaction in the result
            if (res?.Body != null)
            {
                var lastInteraction = res.Body.GetLastInteraction();
                if (lastInteraction?.Metrics != null)
                {
                    lastInteraction.Metrics.CompletionTime = stopwatch.Elapsed.TotalSeconds;
                }
            }

            return res;
        }

        /// <summary>
        /// One provider turn: exec provider, emit partial, merge new interactions.
        /// Returns the AIReturn (or error if provider returned null).
        /// </summary>
        /// <param name="options">Session execution options.</param>
        /// <param name="turnId">Unified TurnId applied to all new interactions for this provider turn.</param>
        /// <param name="ct">Cancellation token.</param>
        private async Task<AIReturn> HandleProviderTurnAsync(SessionOptions options, string turnId, CancellationToken ct)
        {
            var callResult = await this.ExecProviderAsync(ct).ConfigureAwait(false);
            if (callResult == null)
            {
                var err = this.CreateError("Provider returned no response");
                return err;
            }

            // Merge new interactions into the session body
            var newInteractions = callResult.Body?.GetNewInteractions();

            // Apply unified TurnId to all new interactions for this provider turn
            InteractionUtility.EnsureTurnId(newInteractions, turnId);
            this.MergeNewToSessionBody(newInteractions, toolsOnly: false);
#if DEBUG
            // Debug: observe tool_call ids after provider merge
            this.DebugLogToolCallIds($"after-merge provider turn {turnId}");
#endif

            // Update our last return, preserving the new interaction markers from the provider response
            this._lastReturn = callResult;
            this.UpdateLastReturn(callResult);

            this.NotifyInteractionCompleted(callResult);
            return callResult;
        }

        /// <summary>
        /// Shared streaming processing logic. Processes deltas from a streaming adapter,
        /// accumulates text, persists non-text interactions, and notifies observers.
        /// </summary>
        /// <param name="adapter">The streaming adapter to consume deltas from.</param>
        /// <param name="streamingOptions">Streaming options for the adapter.</param>
        /// <param name="turnId">The turn ID for this streaming session.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A <see cref="StreamProcessingResult"/> containing accumulated state and deltas.</returns>
        private async Task<StreamProcessingResult> ProcessStreamingDeltasAsync(
            IStreamingAdapter adapter,
            StreamingOptions streamingOptions,
            string turnId,
            CancellationToken ct)
        {
            var result = new StreamProcessingResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Ensure request policies are applied for streaming calls as well.
                // Streaming adapters can bypass AIRequestCall.Exec(), so policies like ContextInjectionRequestPolicy
                // must be applied explicitly to keep context up-to-date on every provider call.
                await PolicyPipeline.Default.ApplyRequestPoliciesAsync(this.Request).ConfigureAwait(false);

                await foreach (var rawDelta in adapter.StreamAsync(this.Request, streamingOptions, ct))
                {
                    if (rawDelta == null)
                    {
                        continue;
                    }

                    // Normalize delta to handle provider-specific formats
                    var delta = adapter.NormalizeDelta(rawDelta);

                    var newInteractions = delta.Body?.GetNewInteractions();
                    InteractionUtility.EnsureTurnId(newInteractions, turnId);

                    if (newInteractions != null && newInteractions.Count > 0)
                    {
                        var nonTextInteractions = new List<IAIInteraction>();
                        var textInteractions = new List<IAIInteraction>();

                        foreach (var interaction in newInteractions)
                        {
                            if (interaction is AIInteractionText textDelta)
                            {
                                // Accumulate text deltas in memory (will be persisted after streaming completes)
                                result.AccumulatedText = TextStreamCoalescer.Coalesce(result.AccumulatedText, textDelta, turnId, preserveMetrics: false);
                                textInteractions.Add(textDelta);
                            }
                            else if (interaction is AIInteractionToolCall tc)
                            {
                                // Check if this tool call already exists in history
                                var exists = this.Request?.Body?.Interactions?.OfType<AIInteractionToolCall>()?
                                    .Any(x => !string.IsNullOrWhiteSpace(x?.Id) && string.Equals(x.Id, tc.Id, StringComparison.Ordinal)) ?? false;

                                if (!exists)
                                {
                                    // Persist tool call if not already in history
                                    this.AppendToSessionHistory(interaction);
                                    nonTextInteractions.Add(interaction);
                                }
                                else
                                {
                                    // Tool call exists - update it with more complete arguments (streaming accumulation)
                                    if (this.UpdateToolCallInHistory(tc))
                                    {
                                        // Arguments were updated, notify UI
                                        nonTextInteractions.Add(interaction);
                                    }
                                }
                            }
                            else
                            {
                                // Persist other non-text interactions (tool results, images, etc.) immediately
                                this.AppendToSessionHistory(interaction);
                                nonTextInteractions.Add(interaction);
                            }
                        }

                        // Notify UI with streaming deltas ONLY for text interactions
                        if (textInteractions.Count > 0)
                        {
                            var textDeltaReturn = this.BuildDeltaReturn(turnId, textInteractions);
                            this.NotifyDelta(textDeltaReturn);
                        }

                        // Emit partial notification only for persisted non-text interactions
                        if (nonTextInteractions.Count > 0)
                        {
                            try
                            {
                                var persistedDelta = this.BuildDeltaReturn(turnId, nonTextInteractions);
                                this.NotifyInteractionCompleted(persistedDelta);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ConversationSession.ProcessStreamingDeltasAsync] Error emitting persisted non-text delta: {ex.Message}");
                            }
                        }

                        // Keep a reference to last tool_calls delta if needed by diagnostics
                        result.LastToolCallsDelta = delta;
                    }

                    result.Deltas.Add(delta);
                    result.LastDelta = delta;
                }
            }
            finally
            {
                stopwatch.Stop();
                result.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            }

            // Check if streaming produced no results
            if (result.LastDelta == null)
            {
                result.HasError = true;
                result.ErrorMessage = "Provider returned no response";
            }

            return result;
        }

        /// <summary>
        /// Processes pending tool calls for bounded passes. Emits partial returns and merges new interactions.
        /// Returns a list of prepared returns in the order they were produced (for streaming fallback).
        /// </summary>
        /// <param name="options">Session execution options.</param>
        /// <param name="turnId">Unified TurnId applied to interactions produced within these tool passes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="streamingOptions">Optional streaming options to use for follow-up provider calls after tool execution.</param>
        /// <param name="useStreaming">Whether to use streaming for follow-up provider calls.</param>
        private async Task<List<AIReturn>> ProcessPendingToolsAsync(SessionOptions options, string turnId, CancellationToken ct, StreamingOptions? streamingOptions = null, bool useStreaming = false)
        {
            var preparedYields = new List<AIReturn>();
            int toolPass = 0;
            while (toolPass < options.MaxToolPasses)
            {
                ct.ThrowIfCancellationRequested();
#if DEBUG
                // Debug: snapshot of tool_call ids before each tool pass
                this.DebugLogToolCallIds($"before tool pass {toolPass} turn {turnId}");
#endif

                var pendingToolCalls = this.Request.Body.PendingToolCallsList();
                if (pendingToolCalls == null || pendingToolCalls.Count == 0)
                {
                    break;
                }

                foreach (var tc in pendingToolCalls)
                {
                    ct.ThrowIfCancellationRequested();
                    var delta = await this.ExecuteSingleToolAsync(tc, turnId, ct).ConfigureAwait(false);
                    if (delta != null)
                    {
                        preparedYields.Add(delta);
                    }
                }

                toolPass++;

                // Guard: providers must only be invoked when every tool_call already has a persisted tool_result
                if (this.Request.Body.PendingToolCallsCount() > 0)
                {
                    Debug.WriteLine("[ConversationSession.ProcessPendingToolsAsync] Pending tool calls remain after execution; deferring provider turn until results are appended.");
                    continue;
                }

                // Provider consumes tool results
                ct.ThrowIfCancellationRequested();

                // Use streaming if requested and available
                if (useStreaming && streamingOptions != null)
                {
                    var exec = new DefaultProviderExecutor();
                    var adapter = exec.TryGetStreamingAdapter(this.Request);

                    if (adapter != null)
                    {
                        // Stream the follow-up response using shared helper
                        var streamResult = await this.ProcessStreamingDeltasAsync(adapter, streamingOptions, turnId, ct).ConfigureAwait(false);

                        if (streamResult.HasError)
                        {
                            var err = this.CreateError(streamResult.ErrorMessage ?? "Provider returned no response");
                            this.NotifyFinal(err);
                            preparedYields.Add(err);
                            break;
                        }

                        // Persist final aggregated text and update last return snapshot
                        this.PersistStreamingSnapshot(streamResult.LastDelta, streamResult.LastDelta, turnId, streamResult.AccumulatedText, streamResult.ElapsedSeconds);
                        this.NotifyInteractionCompleted(this._lastReturn);
                        preparedYields.Add(this._lastReturn);
                    }
                    else
                    {
                        // Fallback to non-streaming if adapter not available
                        var (result, shouldBreak) = await this.ExecuteNonStreamingProviderFollowUpAsync(turnId, ct).ConfigureAwait(false);
                        preparedYields.Add(result);
                        if (shouldBreak)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // Non-streaming path (original behavior)
                    var (result, shouldBreak) = await this.ExecuteNonStreamingProviderFollowUpAsync(turnId, ct).ConfigureAwait(false);
                    preparedYields.Add(result);
                    if (shouldBreak)
                    {
                        break;
                    }
                }

                if (this.Request.Body.PendingToolCallsCount() == 0)
                {
                    break;
                }
            }

            return preparedYields;
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            if (!this.cts.IsCancellationRequested)
            {
                this.cts.Cancel();
            }

#if DEBUG
            try { this.DebugAppendEvent("Cancel requested"); } catch { }
#endif
        }

        /// <inheritdoc/>
        public async Task<AIReturn> RunToStableResult(SessionOptions options, CancellationToken cancellationToken = default)
        {
            AIReturn last = null;
            await foreach (var ret in this.TurnLoopAsync(options, streamingOptions: null, yieldDeltas: false, cancellationToken))
            {
                last = ret;
            }

            return last ?? this.CreateError("No result");
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<AIReturn> Stream(
            SessionOptions options,
            StreamingOptions streamingOptions,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var ret in this.TurnLoopAsync(options, streamingOptions, yieldDeltas: true, cancellationToken))
            {
                yield return ret;
            }
        }
    }
}
