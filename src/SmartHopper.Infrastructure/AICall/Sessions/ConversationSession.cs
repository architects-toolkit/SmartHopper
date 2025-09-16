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
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AIModels;
    using SmartHopper.Infrastructure.Settings;
    using SmartHopper.Infrastructure.Streaming;

    /// <summary>
    /// Conversation session that manages chat history and orchestrates multi-turn conversations.
    /// Handles both streaming and non-streaming execution with proper history management.
    /// </summary>
    public sealed class ConversationSession : IConversationSession
    {
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
        /// The last complete return from the conversation.
        /// </summary>
        private AIReturn _lastReturn = new AIReturn();

        /// <summary>
        /// Indicates whether greeting generation was requested for this session.
        /// </summary>
        private readonly bool _generateGreeting;

        /// <summary>
        /// The greeting return if greeting was generated.
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
        /// Centralizes wantsStreaming flag and aggregates error messages into an AIReturn.
        /// </summary>
        /// <param name="wantsStreaming">Whether the caller intends to stream.</param>
        /// <returns>Tuple with validity and optional error return.</returns>
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

        /// <inheritdoc/>
        public AIRequestCall Request { get; }

        /// <summary>
        /// Gets the observer for this session.
        /// </summary>
        public IConversationObserver? Observer { get; }

        /// <summary>
        /// Gets the greeting return if greeting generation was requested and completed.
        /// </summary>
        /// <returns>The greeting AIReturn, or null if no greeting was generated or if it failed.</returns>
        public AIReturn? GetGreeting()
        {
            return this._greetingReturn;
        }

        /// <summary>
        /// Adds a new user interaction to the conversation.
        /// </summary>
        /// <param name="userMessage">The user message content.</param>
        public void AddInteraction(string userMessage)
        {
            var userInteraction = new AIInteractionText 
            { 
                Agent = AIAgent.User, 
                Content = userMessage 
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
        /// Gets the last return from the conversation.
        /// </summary>
        /// <returns>The most recent AIReturn.</returns>
        public AIReturn GetReturn()
        {
            return this._lastReturn;
        }

        /// <summary>
        /// Gets only the new interactions from the last conversation turn.
        /// </summary>
        /// <returns>List of new interactions.</returns>
        public List<IAIInteraction> GetNewInteractionList()
        {
            return this._lastReturn.Body?.GetNewInteractions()?.ToList() ?? new List<IAIInteraction>();
        }

        /// <summary>
        /// Gets the initial request that created this conversation session.
        /// </summary>
        /// <returns>The initial <see cref="AIRequestCall"/>.</returns>
        public AIRequestCall GetInitialRequest()
        {
            return this._initialRequest;
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
        /// Updates the _lastReturn with current conversation state.
        /// Creates a fresh snapshot return to avoid mutating any provider returns.
        /// </summary>
        private void UpdateLastReturn()
        {
            var snapshot = new AIReturn();
            snapshot.SetBody(this.Request.Body);
            this._lastReturn = snapshot;
        }

        /// <summary>
        /// Updates the _lastReturn with current conversation state, preserving new interaction markers from a source return.
        /// </summary>
        private void UpdateLastReturn(AIReturn sourceWithNewMarkers)
        {
            if (sourceWithNewMarkers?.Body == null)
            {
                this.UpdateLastReturn();
                return;
            }

            // Preserve the source return object for observer events, but keep the session snapshot
            // without 'new' markers (history only), so future returns don't re-mark items as new.
            var sessionBody = this.Request.Body;
            var bodyHistoryOnly = AIBodyBuilder
                .FromImmutable(sessionBody)
                .ClearNewMarkers() // ensure no 'new' markers bleed into the snapshot
                .Build();

            var snapshot = new AIReturn();
            snapshot.SetBody(bodyHistoryOnly);
            this._lastReturn = snapshot;
        }

        /// <summary>
        /// Prepares a return with only new interactions for streaming deltas.
        /// </summary>
        private AIReturn PrepareNewOnlyReturn(AIReturn source)
        {
            if (source == null) return source;
            
            var newOnly = source.Body?.GetNewInteractions() ?? new List<IAIInteraction>();
            var reduced = AIBodyBuilder.Create().AddRange(newOnly).Build();
            source.SetBody(reduced);
            return source;
        }

        /// <summary>
        /// Append new interactions into the session body, skipping dynamic context interactions.
        /// Optionally, when toolsOnly is true, only persists tool call/result interactions (for streaming).
        /// </summary>
        private void MergeNewToSessionBody(IEnumerable<IAIInteraction>? interactions, bool toolsOnly = false)
        {
            if (interactions == null)
            {
                return;
            }
            foreach (var interaction in interactions)
            {
                if (interaction == null || interaction.Agent == AIAgent.Context)
                {
                    continue;
                }
                if (toolsOnly && interaction is not AIInteractionToolCall && interaction is not AIInteractionToolResult)
                {
                    continue;
                }
                try
                {
                    Debug.WriteLine($"[ConversationSession.MergeNewToSessionBody] appending: type={interaction?.GetType().Name}, agent={interaction?.Agent.ToString() ?? "?"}, content={(interaction is AIInteractionText t ? (t.Content ?? string.Empty) : (interaction is AIInteractionToolCall tc ? $"tool:{tc.Name}" : interaction is AIInteractionToolResult tr ? $"tool_result:{tr.Name}" : string.Empty))}");
                }
                catch { /* logging only */ }
                // Append provider-returned interactions to session as history (not new)
                this.AppendToSessionHistory(interaction);
            }
        }

        private void NotifyDelta(AIReturn ret)
        {
            if (this.Observer == null || ret?.Body?.Interactions == null)
                return;

            // For delta updates, notify observer of each new interaction individually
            var newInteractions = ret.Body.GetNewInteractions();
            foreach (var interaction in newInteractions)
            {
                this.Observer.OnDelta(interaction);
            }
        }

        private void NotifyPartial(AIReturn ret)
        {
            if (this.Observer == null || ret?.Body?.Interactions == null)
                return;

            // For partial updates, notify observer of each new interaction individually
            var newInteractions = ret.Body.GetNewInteractions();
            foreach (var interaction in newInteractions)
            {
                this.Observer.OnPartial(interaction);
            }
        }
        private void NotifyFinal(AIReturn ret) => this.Observer?.OnFinal(ret);
        private void NotifyToolCall(AIInteractionToolCall toolCall) => this.Observer?.OnToolCall(toolCall);
        private void NotifyToolResult(AIInteractionToolResult toolResult) => this.Observer?.OnToolResult(toolResult);
        private void NotifyStart(AIRequestCall request) => this.Observer?.OnStart(request);
        private void NotifyError(Exception error) => this.Observer?.OnError(error);

        /// <summary>
        /// Appends a single interaction to the session history without marking it as new.
        /// </summary>
        private void AppendToSessionHistory(IAIInteraction interaction)
        {
            if (interaction == null) return;
            var builder = AIBodyBuilder.FromImmutable(this.Request.Body)
                .ClearNewMarkers()
                .AsHistory();
            builder.Add(interaction, markAsNew: false);
            this.Request.Body = builder.Build();
        }

        /// <summary>
        /// Appends a range of interactions to the session history without marking them as new.
        /// </summary>
        private void AppendRangeToSessionHistory(IEnumerable<IAIInteraction> interactions)
        {
            if (interactions == null) return;
            var items = interactions.Where(i => i != null).Select(i => (interaction: i, isNew: false)).ToList();
            var builder = AIBodyBuilder.FromImmutable(this.Request.Body)
                .ClearNewMarkers()
                .AsHistory();
            builder.AddRange(items);
            this.Request.Body = builder.Build();
        }

        /// <summary>
        /// Creates a standardized error AIReturn.
        /// </summary>
        private AIReturn CreateError(string message)
        {
            var ret = new AIReturn();
            ret.CreateProviderError(message, this.Request);
            return ret;
        }

        /// <summary>
        /// Generates an AI greeting if enabled in settings and greeting generation was requested.
        /// Uses the provider's default Text2Text model, overriding the initial request model.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the greeting generation.</param>
        /// <returns>Task representing the greeting generation operation.</returns>
        private async Task GenerateGreetingAsync(CancellationToken cancellationToken = default)
        {
            if (!this._generateGreeting)
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
                    }
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

                Debug.WriteLine($"[ConversationSession] Generating greeting with provider='{this._initialRequest.Provider}', model='{defaultModel}' (Text2Text default), interactions={greetingInteractions?.Count ?? 0}");

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
                    // Persist greeting into conversation history as history (not new)
                    this.AppendToSessionHistory(greetingText);
                    this.UpdateLastReturn();

                    // Emit partial with only the greeting interaction for clean UI append
                    var delta = new AIReturn();
                    var deltaBody = AIBodyBuilder.Create().Add(greetingText).Build();
                    delta.SetBody(deltaBody);
                    this.NotifyPartial(delta);

                    // Mark emitted and immediately emit a final snapshot for initialization flows
                    this._greetingEmitted = true;
                    this.NotifyFinal(this.GetHistoryReturn());
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
                Metrics = new AIMetrics()
            };
            
            var body = AIBodyBuilder.Create().Add(greetingInteraction).Build();
            fallbackReturn.SetBody(body);
            return fallbackReturn;
        }

        /// <summary>
        /// Executes the provider once using non-streaming mode.
        /// </summary>
        private async Task<AIReturn?> ExecProviderAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var res = await this.Request.Exec(stream: false).ConfigureAwait(false);
            return res;
        }

        /// <summary>
        /// Executes the provider using streaming mode.
        /// </summary>
        private async Task<AIReturn?> ExecProviderStreamingAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var res = await this.Request.Exec(stream: true).ConfigureAwait(false);
            return res;
        }

        /// <summary>
        /// One provider turn: exec provider, emit partial, merge new interactions.
        /// Returns the AIReturn (or error if provider returned null).
        /// </summary>
        private async Task<AIReturn> HandleProviderTurnAsync(SessionOptions options, CancellationToken ct)
        {
            var callResult = await this.ExecProviderAsync(ct).ConfigureAwait(false);
            if (callResult == null)
            {
                var err = this.CreateError("Provider returned no response");
                return err;
            }

            // Merge new interactions into the session body
            var newInteractions = callResult.Body?.GetNewInteractions();
            this.MergeNewToSessionBody(newInteractions, toolsOnly: false);
            
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
        private async Task<List<AIReturn>> ProcessPendingToolsAsync(SessionOptions options, CancellationToken ct)
        {
            var preparedYields = new List<AIReturn>();
            int toolPass = 0;
            while (toolPass < options.MaxToolPasses)
            {
                ct.ThrowIfCancellationRequested();

                var pendingToolCalls = this.Request.Body.PendingToolCallsList();
                if (pendingToolCalls == null || pendingToolCalls.Count == 0)
                {
                    break;
                }

                foreach (var tc in pendingToolCalls)
                {
                    ct.ThrowIfCancellationRequested();
                    this.NotifyToolCall(tc);

                    var toolRq = new AIToolCall();
                    toolRq.FromToolCallInteraction(tc, this.Request.Provider, this.Request.Model);
                    var toolRet = await this.executor.ExecToolAsync(toolRq, ct).ConfigureAwait(false);

                    var toolInteraction = toolRet?.Body?.GetLastInteraction() as AIInteractionToolResult;
                    if (toolInteraction != null)
                    {
                        // Append tool result to session as history (not new)
                        this.AppendToSessionHistory(toolInteraction);
                        this.NotifyToolResult(toolInteraction);
                    }
                    else
                    {
                        var none = new AIReturn();
                        none.CreateToolError("Tool not found or did not return a value", toolRq);
                        var errInteraction = none.Body?.GetLastInteraction() as AIInteractionToolResult;
                        if (errInteraction != null)
                        {
                            this.AppendToSessionHistory(errInteraction);
                            this.NotifyToolResult(errInteraction);
                        }
                    }
                }

                toolPass++;

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
        }

        /// <inheritdoc/>
        public async Task<AIReturn> RunToStableResult(SessionOptions options, CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, options.CancellationToken, cancellationToken);
            try
            {
                this.Observer?.OnStart(this.Request);

                // Generate greeting if requested before starting conversation
                await this.GenerateGreetingAsync(linkedCts.Token).ConfigureAwait(false);

                // If greeting was emitted for an initialization-only run, short-circuit further execution
                if (this._generateGreeting && this._greetingEmitted)
                {
                    return this._greetingReturn ?? this.GetHistoryReturn();
                }

                // Centralized validation: early interrupt non-streaming flow if request is invalid
                var (okRun, errRun) = this.ValidateBeforeStart(wantsStreaming: false);
                if (!okRun)
                {
                    var errorReturn = errRun ?? new AIReturn();
                    this.NotifyFinal(errorReturn);
                    return errorReturn;
                }

                int turns = 0;
                AIReturn last = null;

                while (turns < options.MaxTurns)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();

                    // One provider turn
                    var preparedTurn = await this.HandleProviderTurnAsync(options, linkedCts.Token).ConfigureAwait(false);
                    last = preparedTurn;

                    if (!options.ProcessTools)
                    {
                        this.NotifyFinal(preparedTurn);
                        return preparedTurn;
                    }

                    // Bounded tool passes
                    var afterToolsYields = await this.ProcessPendingToolsAsync(options, linkedCts.Token).ConfigureAwait(false);
                    last = afterToolsYields.LastOrDefault() ?? last;

                    // If stable after provider + tool passes, finalize
                    if (this.Request.Body.PendingToolCallsCount() == 0)
                    {
                        var finalStable = last ?? new AIReturn();
                        this._lastReturn = finalStable;
                        this.UpdateLastReturn();
                        // Pass the result that includes 'new' markers to the observer
                        this.NotifyFinal(finalStable);
                        return finalStable;
                    }

                    // Otherwise, continue to next turn
                    turns++;
                }

                // Max turns reached without stability
                var final = last ?? this.CreateError("Max turns reached without a stable result");
                this._lastReturn = final;
                this.UpdateLastReturn();
                // Pass the result with markers
                this.NotifyFinal(final);
                return final;
            }
            catch (OperationCanceledException oce)
            {
                this.NotifyError(oce);
                var cancelled = new AIReturn();
                cancelled.CreateProviderError("Call cancelled or timed out", this.Request);
                return cancelled;
            }
            catch (Exception ex)
            {
                this.NotifyError(ex);
                var error = new AIReturn();
                error.CreateProviderError(ex.Message, this.Request);
                return error;
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<AIReturn> Stream(
            SessionOptions options,
            StreamingOptions streamingOptions,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, options.CancellationToken, cancellationToken);
            try
            {
                this.Observer?.OnStart(this.Request);

                // Generate greeting if requested before starting conversation
                await this.GenerateGreetingAsync(linkedCts.Token).ConfigureAwait(false);

                // If greeting was emitted, provide a single yield and finalize the stream
                if (this._generateGreeting && this._greetingEmitted)
                {
                    var toYield = this._greetingReturn ?? this.GetHistoryReturn();
                    // Also emit a delta to match streaming semantics
                    if (this._greetingReturn != null)
                    {
                        this.NotifyDelta(this._greetingReturn);
                    }
                    this.NotifyFinal(this.GetHistoryReturn());
                    yield return toYield;
                    yield break;
                }

                // Centralized validation: early interrupt streaming if request is invalid (e.g., streaming unsupported)
                var (okStream, errStream) = this.ValidateBeforeStart(wantsStreaming: true);
                if (!okStream)
                {
                    this.Observer?.OnFinal(errStream ?? new AIReturn());
                    if (errStream != null)
                    {
                        yield return errStream;
                    }
                    yield break;
                }

                int turns = 0;
                AIReturn lastReturn = null;

                while (turns < options.MaxTurns)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    // Prepare variables to avoid yielding inside try/catch blocks
                    List<AIReturn> deltaYields = null;   // per-chunk streaming yields
                    List<AIReturn> pendingToolYields = null;
                    AIReturn finalProviderYield = null;  // final provider result for this turn
                    AIReturn errorYield = null;
                    bool shouldBreak = false;

                    try
                    {
                        // Try to obtain a streaming adapter and stream deltas inline
                        var executor = new DefaultProviderExecutor();
                        var adapter = executor.TryGetStreamingAdapter(this.Request);

                        if (adapter == null)
                        {
                            // Provider doesn't support streaming: fall back to non-streaming single turn
                            var nonStream = await this.ExecProviderAsync(linkedCts.Token).ConfigureAwait(false);
                            if (nonStream == null)
                            {
                                var err = this.CreateError("Provider returned no response");
                                this.NotifyFinal(err);
                                errorYield = err;
                                shouldBreak = true;
                            }
                            else
                            {
                                // Merge and notify as a partial (single shot)
                                var newInteractions = nonStream.Body?.GetNewInteractions();
                                this.MergeNewToSessionBody(newInteractions, toolsOnly: false);
                                this._lastReturn = nonStream;
                                this.UpdateLastReturn();
                                this.NotifyPartial(this.PrepareNewOnlyReturn(nonStream));
                                lastReturn = nonStream;

                                if (!options.ProcessTools)
                                {
                                    this.NotifyFinal(nonStream);
                                    finalProviderYield = nonStream;
                                    shouldBreak = true;
                                }
                                else
                                {
                                    pendingToolYields = await this.ProcessPendingToolsAsync(options, linkedCts.Token).ConfigureAwait(false);
                                    if (pendingToolYields.Count > 0)
                                    {
                                        lastReturn = pendingToolYields.Last();
                                    }
                                    if (this.Request.Body.PendingToolCallsCount() == 0)
                                    {
                                        this.NotifyFinal(lastReturn ?? nonStream);
                                        shouldBreak = true;
                                    }
                                    finalProviderYield = nonStream;
                                }
                            }
                        }
                        else
                        {
                            // Streaming path: forward each chunk to observer and caller
                            deltaYields = new List<AIReturn>();
                            AIReturn lastDelta = null;

                            await foreach (var delta in adapter.StreamAsync(this.Request, streamingOptions, linkedCts.Token))
                            {
                                if (delta == null)
                                {
                                    continue;
                                }

                                // Merge chunk into session state and notify as partial
                                var newInteractions = delta.Body?.GetNewInteractions();
                                this.MergeNewToSessionBody(newInteractions, toolsOnly: false);
                                this._lastReturn = delta;
                                this.UpdateLastReturn();
                                this.NotifyPartial(this.PrepareNewOnlyReturn(delta));

                                // Surface tool call notifications immediately upon appearance
                                if (newInteractions != null)
                                {
                                    foreach (var interaction in newInteractions)
                                    {
                                        if (interaction is AIInteractionToolCall toolCall)
                                        {
                                            this.NotifyToolCall(toolCall);
                                        }
                                    }
                                }

                                // Queue yield for the caller
                                deltaYields.Add(delta);
                                lastDelta = delta;
                                lastReturn = delta;
                            }

                            // No deltas received -> error
                            if (lastDelta == null)
                            {
                                var err = this.CreateError("Provider returned no response");
                                this.NotifyFinal(err);
                                errorYield = err;
                                shouldBreak = true;
                            }
                            else if (!options.ProcessTools)
                            {
                                // Finalize this turn with the last delta
                                this.NotifyFinal(lastDelta);
                                finalProviderYield = lastDelta;
                                shouldBreak = true;
                            }
                            else
                            {
                                // Process tools after streaming completes
                                pendingToolYields = await this.ProcessPendingToolsAsync(options, linkedCts.Token).ConfigureAwait(false);
                                if (pendingToolYields.Count > 0)
                                {
                                    lastReturn = pendingToolYields.Last();
                                }
                                // If stable, finish the turn
                                if (this.Request.Body.PendingToolCallsCount() == 0)
                                {
                                    this.NotifyFinal(lastReturn ?? lastDelta);
                                    shouldBreak = true;
                                }
                                finalProviderYield = lastDelta;
                            }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        this.NotifyError(oce);
                        var cancelled = new AIReturn();
                        cancelled.CreateProviderError("Call cancelled or timed out", this.Request);
                        errorYield = cancelled;
                        shouldBreak = true;
                    }
                    catch (Exception ex)
                    {
                        this.NotifyError(ex);
                        var error = new AIReturn();
                        error.CreateProviderError(ex.Message, this.Request);
                        errorYield = error;
                        shouldBreak = true;
                    }

                    // Perform all yields outside the try/catch blocks to satisfy iterator constraints
                    if (deltaYields != null && deltaYields.Count > 0)
                    {
                        foreach (var dy in deltaYields)
                        {
                            yield return dy;
                        }
                    }

                    if (pendingToolYields != null && pendingToolYields.Count > 0)
                    {
                        foreach (var toolYield in pendingToolYields)
                        {
                            yield return toolYield;
                        }
                    }

                    if (finalProviderYield != null)
                    {
                        yield return finalProviderYield;
                    }

                    if (errorYield != null)
                    {
                        yield return errorYield;
                    }

                    if (shouldBreak)
                    {
                        yield break;
                    }

                    turns++;
                }

                // Max turns reached without stability
                var final = lastReturn ?? this.CreateError("Max turns reached without a stable result");
                this._lastReturn = final;
                this.UpdateLastReturn();
                // Notify final with the last provider result to keep 'new' markers
                this.NotifyFinal(final);
                yield return final;
            }
            finally
            {
                linkedCts.Dispose();
            }
        }
    }
}
