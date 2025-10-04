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
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Execution;
    using SmartHopper.Infrastructure.AICall.Metrics;
    using SmartHopper.Infrastructure.AICall.Utilities;
    using SmartHopper.Infrastructure.AIModels;
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
        /// The cancellation token source for this session.
        /// </summary>
        private readonly CancellationTokenSource cts = new ();

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
        /// The greeting return if greeting generation was requested and completed.
        /// </summary>
        private AIReturn? _greetingReturn;

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
                await this.GenerateGreetingAsync(streamChunks: yieldDeltas, cancellationToken: linkedCts.Token).ConfigureAwait(false);

                // If greeting was emitted, provide a single yield and finalize the sequence
                if (this._greetingEmitted)
                {
                    var snapshot = this.GetHistoryReturn();

                    // Reset flag so subsequent calls (after user messages) will proceed
                    this._greetingEmitted = false;
                    yield return snapshot;
                    yield break;
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

                    if (!yieldDeltas)
                    {
                        AIReturn nsPrepared = null;
                        AIReturn nsError = null;
                        bool nsShouldBreak = false;

                        try
                        {
                            // Non-streaming composite turn
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
                        catch (OperationCanceledException oce)
                        {
                            this.NotifyError(oce);
                            nsError = new AIReturn();
                            nsError.CreateProviderError("Call cancelled or timed out", this.Request);
                            nsShouldBreak = true;
                        }
                        catch (Exception ex)
                        {
                            this.NotifyError(ex);
                            nsError = new AIReturn();
                            nsError.CreateProviderError(ex.Message, this.Request);
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
                            // Streaming path: forward each chunk to observer, accumulate text in memory, persist non-text immediately
                            await foreach (var delta in adapter.StreamAsync(this.Request, streamingOptions!, linkedCts.Token))
                            {
                                if (delta == null)
                                {
                                    continue;
                                }

                                // Notify UI with streaming deltas for live updates
                                var newInteractions = delta.Body?.GetNewInteractions();
                                InteractionUtility.EnsureTurnId(newInteractions, state.TurnId);
                                this.NotifyDelta(this.PrepareNewOnlyReturn(delta));

                                // Process new interactions: accumulate text, persist non-text immediately
                                if (newInteractions != null && newInteractions.Count > 0)
                                {
                                    var nonTextInteractions = new List<IAIInteraction>();

                                    foreach (var interaction in newInteractions)
                                    {
                                        if (interaction is AIInteractionText textDelta)
                                        {
                                            // Accumulate text deltas in memory (will be persisted after streaming completes)
                                            state.AccumulatedText = TextStreamCoalescer.Coalesce(state.AccumulatedText, textDelta, state.TurnId, preserveMetrics: false);
                                        }
                                        else
                                        {
                                            // Persist non-text interactions (tool calls, tool results) immediately
                                            this.AppendToSessionHistory(interaction);
                                            nonTextInteractions.Add(interaction);
                                        }
                                    }

                                    // Emit partial notification only for persisted non-text interactions
                                    if (nonTextInteractions.Count > 0)
                                    {
                                        try
                                        {
                                            var persistedDelta = this.BuildDeltaReturn(state.TurnId, nonTextInteractions);
                                            this.NotifyPartial(persistedDelta);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"[ConversationSession.Stream] Error emitting persisted non-text delta: {ex.Message}");
                                        }
                                    }

                                    // Keep a reference to last tool_calls delta if needed by diagnostics
                                    state.LastToolCallsDelta = delta;
                                }

                                state.DeltaYields.Add(delta);
                                state.LastDelta = delta;
                                lastReturn = delta;
                            }

                            // After streaming ends, either error (no deltas) or persist final snapshot then continue
                            if (state.LastDelta == null)
                            {
                                var errDelta = this.CreateError("Provider returned no response");
                                this.NotifyFinal(errDelta);
                                state.ErrorYield = errDelta;
                                state.ShouldBreak = true;
                            }
                            else
                            {
                                // Persist final aggregated text and update last-return snapshot
                                this.PersistStreamingSnapshot(state.LastToolCallsDelta, state.LastDelta, state.TurnId, state.AccumulatedText);

                                if (!options.ProcessTools)
                                {
                                    this.NotifyFinal(state.LastDelta);
                                    state.FinalProviderYield = state.LastDelta;
                                    state.ShouldBreak = true;
                                }
                                else
                                {
                                    // Process tools after streaming completes
                                    state.PendingToolYields = await this.ProcessPendingToolsAsync(options, state.TurnId, linkedCts.Token).ConfigureAwait(false);
                                    if (state.PendingToolYields.Count > 0)
                                    {
                                        lastReturn = state.PendingToolYields.Last();
                                    }

                                    // If stable, finish the turn
                                    if (this.Request.Body.PendingToolCallsCount() == 0)
                                    {
                                        this.NotifyFinal(lastReturn ?? state.LastDelta);
                                        state.ShouldBreak = true;
                                    }

                                    state.FinalProviderYield = state.LastDelta;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        this.NotifyError(oce);
                        var cancelled = new AIReturn();
                        cancelled.CreateProviderError("Call cancelled or timed out", this.Request);
                        state.ErrorYield = cancelled;
                        state.ShouldBreak = true;
                    }
                    catch (Exception ex)
                    {
                        this.NotifyError(ex);
                        var error = new AIReturn();
                        error.CreateProviderError(ex.Message, this.Request);
                        state.ErrorYield = error;
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
            };

            // Append user input to session history without marking it as 'new'
            this.AppendToSessionHistory(userInteraction);
            this.UpdateLastReturn();
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
        /// Generates an AI greeting if enabled in settings and greeting generation was requested.
        /// Uses the provider's default Text2Text model, overriding the initial request model.
        /// </summary>
        /// <param name="streamChunks">When true, emits streaming chunks for the greeting before persisting the final message.</param>
        /// <param name="cancellationToken">Cancellation token for the greeting generation.</param>
        /// <returns>Task representing the greeting generation operation.</returns>
        private async Task GenerateGreetingAsync(bool streamChunks = false, CancellationToken cancellationToken = default)
        {
            if (!this._generateGreeting)
            {
                return;
            }

            // Already emitted once for this session
            if (this._greetingEmitted)
            {
                return;
            }

            // Check if AI greeting is enabled in settings
            var settings = SmartHopperSettings.Instance;
            if (!settings.SmartHopperAssistant.EnableAIGreeting)
            {
                Debug.WriteLine("[ConversationSession] AI greeting disabled in settings, skipping greeting generation");
                return;
            }

            try
            {
                var defaultGreeting = "Hello! I'm your AI assistant. How can I help you today?";

                // Determine the greeting prompt based on system message
                var systemMessageText = this._initialRequest?.Body?.Interactions?
                    .OfType<AIInteractionText>()
                    .FirstOrDefault(x => x.Agent == AIAgent.System);

                string greetingPrompt;
                if (systemMessageText != null && !string.IsNullOrEmpty(systemMessageText.Content))
                {
                    greetingPrompt = $"You are a chat assistant. The user has provided the following instructions:\n---\n{systemMessageText.Content}\n---\nBased on the instructions, generate a brief, friendly greeting message that welcomes the user to the chat and naturally guides the conversation toward your area of expertise. Be warm and professional, highlighting your unique capabilities without overwhelming the user with technical details. Keep it concise and engaging. One or two sentences maximum.";
                }
                else
                {
                    greetingPrompt = "Your job is to generate a brief, friendly greeting message that welcomes the user to the chat. This is a generic purpose chat. Keep the greeting concise: one or two sentences maximum.";
                }

                // Create greeting interactions
                var greetingInteractions = new List<IAIInteraction>
                {
                    new AIInteractionText
                    {
                        Agent = AIAgent.System,
                        Content = greetingPrompt,
                    },
                    new AIInteractionText
                    {
                        Agent = AIAgent.User,
                        Content = "Please send a short friendly greeting to start the chat. Keep it to one or two sentences.",
                    },
                };

                // Get the provider's default Text2Text model (overriding the initial request model)
                var defaultModel = ModelManager.Instance.GetDefaultModel(this._initialRequest.Provider, AICapability.Text2Text);
                if (string.IsNullOrEmpty(defaultModel))
                {
                    Debug.WriteLine($"[ConversationSession] No default Text2Text model available for provider '{this._initialRequest.Provider}', using fallback greeting");
                    this._greetingReturn = this.CreateFallbackGreeting(defaultGreeting);
                    return;
                }

                // Create greeting request with Text2Text model override
                var greetingRequest = new AIRequestCall();
                greetingRequest.Initialize(
                    this._initialRequest.Provider,
                    defaultModel, // Override with default Text2Text model
                    greetingInteractions,
                    this._initialRequest.Endpoint,
                    AICapability.Text2Text,
                    "-*"); // Disable all tools for greeting

                Debug.WriteLine($"[ConversationSession] Generating greeting with provider='{this._initialRequest.Provider}', model='{defaultModel}' (Text2Text default), interactions={greetingInteractions.Count}");

                // Execute with 30s timeout
                var greetingTask = greetingRequest.Exec();
                var timeoutTask = Task.Delay(30000, cancellationToken);
                var completed = await Task.WhenAny(greetingTask, timeoutTask).ConfigureAwait(false);

                if (completed == greetingTask && !greetingTask.IsFaulted)
                {
                    this._greetingReturn = greetingTask.Result;
                    Debug.WriteLine($"[ConversationSession] Greeting generation completed successfully");
                }
                else
                {
                    Debug.WriteLine("[ConversationSession] Greeting generation timed out or failed, using fallback greeting");
                    this._greetingReturn = this.CreateFallbackGreeting(defaultGreeting);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession] Error generating greeting: {ex.Message}");
                this._greetingReturn = this.CreateFallbackGreeting("Hello! I'm your AI assistant. How can I help you today?");
            }

            // If we have a greeting, append it to the session history and notify observers
            try
            {
                var greetingText = this._greetingReturn?.Body?.Interactions?
                    .OfType<AIInteractionText>()
                    .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                if (greetingText != null && !string.IsNullOrWhiteSpace(greetingText.Content))
                {
                    // Assign a unified TurnId for the greeting so UI can coalesce streaming chunks
                    var turnId = Guid.NewGuid().ToString("N");

                    if (streamChunks)
                    {
                        // Emit incremental chunks as streaming deltas
                        const int chunkSize = 24;
                        var text = greetingText.Content ?? string.Empty;
                        for (int pos = 0; pos < text.Length; pos += chunkSize)
                        {
                            var len = Math.Min(chunkSize, text.Length - pos);
                            var chunk = text.Substring(pos, len);
                            var partial = new AIInteractionText
                            {
                                Agent = AIAgent.Assistant,
                                Content = chunk,
                                TurnId = turnId,
                            };

                            var delta = new AIReturn();
                            var deltaBody = AIBodyBuilder.Create().Add(partial).Build();
                            delta.SetBody(deltaBody);

                            // Stream the partial chunk to observers (UI will aggregate)
                            this.NotifyDelta(delta);
                        }
                    }
                    else
                    {
                        // Non-streaming: push a single partial containing the full greeting
                        // Ensure the partial carries the same TurnId as the final persisted message so UI upserts instead of duplicating
                        var partialGreeting = new AIInteractionText
                        {
                            Agent = AIAgent.Assistant,
                            Content = greetingText.Content,
                            Reasoning = greetingText.Reasoning,
                            Metrics = greetingText.Metrics,
                            Time = greetingText.Time,
                            TurnId = turnId,
                        };
                        var delta = new AIReturn();
                        var deltaBody = AIBodyBuilder.Create().Add(partialGreeting).Build();
                        delta.SetBody(deltaBody);
                        this.NotifyPartial(delta);
                    }

                    // Persist the final greeting into history with the unified TurnId
                    try
                    {
                        var finalGreeting = new AIInteractionText
                        {
                            Agent = AIAgent.Assistant,
                            Content = greetingText.Content,
                            Reasoning = greetingText.Reasoning,
                            Metrics = greetingText.Metrics,
                            Time = greetingText.Time,
                            TurnId = turnId,
                        };
                        this.AppendToSessionHistory(finalGreeting);
                        this.UpdateLastReturn();
                    }
                    catch { }

                    // Mark emitted and immediately emit a final snapshot for initialization flows
                    this._greetingEmitted = true;
                    this.NotifyFinal(this.GetHistoryReturn());

                    // One-shot: prevent greeting generation on subsequent turns
                    this._generateGreeting = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession] Error appending/emitting greeting: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a fallback greeting AIReturn when greeting generation fails.
        /// </summary>
        /// <param name="greetingText">The fallback greeting text.</param>
        /// <returns>AIReturn containing the fallback greeting.</returns>
        private AIReturn CreateFallbackGreeting(string greetingText)
        {
            var fallbackReturn = new AIReturn();
            var greetingInteraction = new AIInteractionText
            {
                Agent = AIAgent.Assistant,
                Content = greetingText,
                Metrics = new AIMetrics(),
            };

            var body = AIBodyBuilder.Create().Add(greetingInteraction).Build();
            fallbackReturn.SetBody(body);
            return fallbackReturn;
        }

        /// <summary>
        /// Executes the provider once using non-streaming mode.
        /// </summary>
        /// <param name="ct">A cancellation token for the operation.</param>
        /// <returns>The provider's <see cref="AIReturn"/>, or null if execution failed to produce a result.</returns>
        private async Task<AIReturn?> ExecProviderAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var res = await this.Request.Exec(stream: false).ConfigureAwait(false);
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

            this.NotifyPartial(callResult);
            return callResult;
        }

        /// <summary>
        /// Processes pending tool calls for bounded passes. Emits partial returns and merges new interactions.
        /// Returns a list of prepared returns in the order they were produced (for streaming fallback).
        /// </summary>
        /// <param name="options">Session execution options.</param>
        /// <param name="turnId">Unified TurnId applied to interactions produced within these tool passes.</param>
        /// <param name="ct">Cancellation token.</param>
        private async Task<List<AIReturn>> ProcessPendingToolsAsync(SessionOptions options, string turnId, CancellationToken ct)
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
                var followUp = await this.ExecProviderAsync(ct).ConfigureAwait(false);
                if (followUp == null)
                {
                    var err = this.CreateError("Provider returned no response");
                    this.NotifyFinal(err);
                    preparedYields.Add(err);
                    break;
                }

                // Persist new interactions and update last return
                var followUpNew = followUp.Body?.GetNewInteractions();
                InteractionUtility.EnsureTurnId(followUpNew, turnId);
                this.MergeNewToSessionBody(followUpNew, toolsOnly: false);
                this._lastReturn = followUp;
                this.UpdateLastReturn();

                this.NotifyPartial(followUp);
                preparedYields.Add(followUp);

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
