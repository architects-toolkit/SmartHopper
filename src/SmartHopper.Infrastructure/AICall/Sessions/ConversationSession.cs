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
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AIModels;
    using SmartHopper.Infrastructure.Streaming;

    /// <summary>
    /// Minimal non-streaming conversation session that delegates to AIRequestCall.Exec.
    /// Future phases will add multi-turn loops, tool orchestration, and streaming.
    /// </summary>
    public sealed class ConversationSession : IConversationSession
    {
        /// <summary>
        /// The cancellation token source for this session.
        /// </summary>
        private readonly CancellationTokenSource cts = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationSession"/> class.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <param name="observer">The observer to notify of events.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is null.</exception>
        public ConversationSession(AIRequestCall request, IConversationObserver? observer = null)
        {
            this.Request = request ?? throw new ArgumentNullException(nameof(request));
            this.Observer = observer;
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

                // Centralized validation: early interrupt non-streaming flow if request is invalid
                var (okRun, errRun) = this.ValidateBeforeStart(wantsStreaming: false);
                if (!okRun)
                {
                    this.Observer?.OnFinal(errRun ?? new AIReturn());
                    return errRun ?? new AIReturn();
                }

                int turns = 0;
                AIReturn lastReturn = null;

                while (turns < options.MaxTurns)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();

                    // Provider call (single turn, no tool processing here)
                    var callResult = await this.Request.Exec().ConfigureAwait(false);

                    if (callResult == null)
                    {
                        var none = new AIReturn();
                        none.CreateProviderError("Provider returned no response", this.Request);
                        this.Observer?.OnFinal(none);
                        return none;
                    }

                    // Emit partial result for the provider turn
                    this.Observer?.OnPartial(callResult);
                    lastReturn = callResult;

                    // Merge provider interactions into the session request body (skip dynamic context)
                    var providerInteractions = callResult.Body?.Interactions;
                    if (providerInteractions != null && providerInteractions.Count > 0)
                    {
                        foreach (var interaction in providerInteractions)
                        {
                            if (interaction.Agent == AIAgent.Context)
                            {
                                continue;
                            }
                            this.Request.Body = this.Request.Body.WithAppended(interaction);
                        }
                    }

                    if (!options.ProcessTools)
                    {
                        this.Observer?.OnFinal(callResult);
                        return callResult;
                    }

                    // Process pending tool calls with bounded passes
                    int toolPass = 0;
                    while (toolPass < options.MaxToolPasses)
                    {
                        linkedCts.Token.ThrowIfCancellationRequested();

                        var pendingToolCalls = this.Request.Body.PendingToolCallsList();
                        if (pendingToolCalls == null || pendingToolCalls.Count == 0)
                        {
                            break; // Stable: no tools to execute
                        }

                        // Sequential execution (AllowParallelTools planned for future)
                        foreach (var tc in pendingToolCalls)
                        {
                            linkedCts.Token.ThrowIfCancellationRequested();

                            this.Observer?.OnToolCall(tc);

                            var toolRq = new AIToolCall();
                            toolRq.FromToolCallInteraction(tc, this.Request.Provider, this.Request.Model);
                            var toolRet = await toolRq.Exec().ConfigureAwait(false);

                            // Append tool result to session body and notify
                            var toolInteraction = toolRet?.Body?.GetLastInteraction() as AIInteractionToolResult;
                            if (toolInteraction != null)
                            {
                                this.Request.Body = this.Request.Body.WithAppended(toolInteraction);
                                this.Observer?.OnToolResult(toolInteraction);
                            }
                            else
                            {
                                // Standardize a tool error if none provided
                                var none = new AIReturn();
                                none.CreateToolError("Tool not found or did not return a value", toolRq);
                                var errInteraction = none.Body?.GetLastInteraction() as AIInteractionToolResult;
                                if (errInteraction != null)
                                {
                                    this.Request.Body = this.Request.Body.WithAppended(errInteraction);
                                    this.Observer?.OnToolResult(errInteraction);
                                }
                            }
                        }

                        toolPass++;

                        // Let the provider consume tool results and possibly request more tools
                        linkedCts.Token.ThrowIfCancellationRequested();
                        var followUp = await this.Request.Exec().ConfigureAwait(false);

                        if (followUp == null)
                        {
                            var none = new AIReturn();
                            none.CreateProviderError("Provider returned no response", this.Request);
                            this.Observer?.OnFinal(none);
                            return none;
                        }

                        // Emit partial result and merge interactions
                        this.Observer?.OnPartial(followUp);
                        lastReturn = followUp;

                        var followUpInteractions = followUp.Body?.Interactions;
                        if (followUpInteractions != null && followUpInteractions.Count > 0)
                        {
                            foreach (var interaction in followUpInteractions)
                            {
                                if (interaction.Agent == AIAgent.Context)
                                {
                                    continue;
                                }
                                this.Request.Body = this.Request.Body.WithAppended(interaction);
                            }
                        }

                        // If now stable, break inner loop
                        if (this.Request.Body.PendingToolCallsCount() == 0)
                        {
                            break;
                        }
                    }

                    // If stable after provider + tool passes, finalize
                    if (this.Request.Body.PendingToolCallsCount() == 0)
                    {
                        this.Observer?.OnFinal(lastReturn ?? new AIReturn());
                        return lastReturn ?? new AIReturn();
                    }

                    // Otherwise, continue to next turn
                    turns++;
                }

                // Max turns reached without stability
                var final = lastReturn ?? new AIReturn();
                if (lastReturn == null)
                {
                    final.CreateProviderError("Max turns reached without a stable result", this.Request);
                }
                this.Observer?.OnFinal(final);
                return final;
            }
            catch (OperationCanceledException oce)
            {
                this.Observer?.OnError(oce);
                var cancelled = new AIReturn();
                cancelled.CreateProviderError("Call cancelled or timed out", this.Request);
                return cancelled;
            }
            catch (Exception ex)
            {
                this.Observer?.OnError(ex);
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

                // Try to obtain a provider-specific streaming adapter via reflection to keep provider-agnostic design
                IStreamingAdapter adapter = null;
                try
                {
                    var provider = this.Request.ProviderInstance;
                    var mi = provider?.GetType().GetMethod("GetStreamingAdapter", Type.EmptyTypes);
                    var obj = mi?.Invoke(provider, null);
                    adapter = obj as IStreamingAdapter;
                    Debug.WriteLine(adapter != null
                        ? $"[ConversationSession] Using streaming adapter from provider '{provider?.Name}'"
                        : $"[ConversationSession] No streaming adapter available for provider '{provider?.Name}', falling back to non-adapter path");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConversationSession] Error probing streaming adapter: {ex.Message}");
                }

                int turns = 0;
                AIReturn lastReturn = null;

                while (turns < options.MaxTurns)
                {
                    var yieldsThisTurn = new List<AIReturn>();
                    bool endStreaming = false;

                    linkedCts.Token.ThrowIfCancellationRequested();

                    // Adapter streaming branch (no try/catch allowed around yield)
                    if (adapter != null)
                    {
                        Debug.WriteLine("[ConversationSession] Streaming via provider adapter...");
                        await foreach (var delta in adapter.StreamAsync(this.Request, streamingOptions, linkedCts.Token))
                        {
                            linkedCts.Token.ThrowIfCancellationRequested();

                            // Forward delta
                            this.Observer?.OnPartial(delta);
                            lastReturn = delta;

                            // Merge interactions into session body (skip dynamic context)
                            var interactions = delta.Body?.Interactions;
                            if (interactions != null && interactions.Count > 0)
                            {
                                foreach (var interaction in interactions)
                                {
                                    if (interaction.Agent == AIAgent.Context)
                                    {
                                        continue;
                                    }
                                    // During streaming, only persist tool interactions; defer assistant/user text to finalization
                                    if (interaction is AIInteractionToolCall || interaction is AIInteractionToolResult)
                                    {
                                        this.Request.Body = this.Request.Body.WithAppended(interaction);
                                    }
                                    // Notify tool calls immediately for UI
                                    if (interaction is AIInteractionToolCall toolCall)
                                    {
                                        this.Observer?.OnToolCall(toolCall);
                                    }
                                }
                            }

                            // Emit to consumer as we go
                            yield return delta;
                        }

                        Debug.WriteLine("[ConversationSession] Provider adapter stream completed.");

                        if (!options.ProcessTools)
                        {
                            this.Observer?.OnFinal(lastReturn ?? new AIReturn());
                            yield break;
                        }
                    }

                    // Non-adapter path and tool processing with error handling
                    try
                    {
                        if (adapter == null)
                        {
                            // Fallback non-adapter streaming: execute provider call and prepare the chunk(s)
                            var callResult = await this.Request.Exec().ConfigureAwait(false);
                            if (callResult == null)
                            {
                                var none = new AIReturn();
                                none.CreateProviderError("Provider returned no response", this.Request);
                                this.Observer?.OnFinal(none);
                                yieldsThisTurn.Add(none);
                                endStreaming = true;
                            }
                            else
                            {
                                this.Observer?.OnPartial(callResult);
                                lastReturn = callResult;
                                yieldsThisTurn.Add(callResult);

                                // Merge provider interactions into the session request body (skip dynamic context)
                                var providerInteractions = callResult.Body?.Interactions;
                                if (providerInteractions != null && providerInteractions.Count > 0)
                                {
                                    foreach (var interaction in providerInteractions)
                                    {
                                        if (interaction.Agent == AIAgent.Context)
                                        {
                                            continue;
                                        }
                                        this.Request.Body = this.Request.Body.WithAppended(interaction);
                                    }
                                }

                                if (!options.ProcessTools)
                                {
                                    this.Observer?.OnFinal(callResult);
                                    endStreaming = true;
                                }
                            }
                        }

                        // If tool processing is enabled, handle bounded tool loop
                        if (!endStreaming && options.ProcessTools)
                        {
                            int toolPass = 0;
                            while (!endStreaming && toolPass < options.MaxToolPasses)
                            {
                                linkedCts.Token.ThrowIfCancellationRequested();

                                var pendingToolCalls = this.Request.Body.PendingToolCallsList();
                                if (pendingToolCalls == null || pendingToolCalls.Count == 0)
                                {
                                    break; // Stable: no tools to execute
                                }

                                // Sequential execution (AllowParallelTools planned for future)
                                foreach (var tc in pendingToolCalls)
                                {
                                    linkedCts.Token.ThrowIfCancellationRequested();

                                    this.Observer?.OnToolCall(tc);

                                    var toolRq = new AIToolCall();
                                    toolRq.FromToolCallInteraction(tc, this.Request.Provider, this.Request.Model);
                                    var toolRet = await toolRq.Exec().ConfigureAwait(false);

                                    // Append tool result to session body and notify
                                    var toolInteraction = toolRet?.Body?.GetLastInteraction() as AIInteractionToolResult;
                                    if (toolInteraction != null)
                                    {
                                        this.Request.Body = this.Request.Body.WithAppended(toolInteraction);
                                        this.Observer?.OnToolResult(toolInteraction);
                                    }
                                    else
                                    {
                                        // Standardize a tool error if none provided
                                        var noneTool = new AIReturn();
                                        noneTool.CreateToolError("Tool not found or did not return a value", toolRq);
                                        var errInteraction = noneTool.Body?.GetLastInteraction() as AIInteractionToolResult;
                                        if (errInteraction != null)
                                        {
                                            this.Request.Body = this.Request.Body.WithAppended(errInteraction);
                                            this.Observer?.OnToolResult(errInteraction);
                                        }
                                    }
                                }

                                toolPass++;

                                // Let the provider consume tool results and possibly request more tools
                                linkedCts.Token.ThrowIfCancellationRequested();
                                var followUp = await this.Request.Exec().ConfigureAwait(false);
                                if (followUp == null)
                                {
                                    var none2 = new AIReturn();
                                    none2.CreateProviderError("Provider returned no response", this.Request);
                                    this.Observer?.OnFinal(none2);
                                    yieldsThisTurn.Add(none2);
                                    endStreaming = true;
                                    break;
                                }

                                this.Observer?.OnPartial(followUp);
                                lastReturn = followUp;
                                yieldsThisTurn.Add(followUp);

                                var followUpInteractions = followUp.Body?.Interactions;
                                if (followUpInteractions != null && followUpInteractions.Count > 0)
                                {
                                    foreach (var interaction in followUpInteractions)
                                    {
                                        if (interaction.Agent == AIAgent.Context)
                                        {
                                            continue;
                                        }
                                        this.Request.Body = this.Request.Body.WithAppended(interaction);
                                    }
                                }

                                // If now stable, break inner loop
                                if (this.Request.Body.PendingToolCallsCount() == 0)
                                {
                                    break;
                                }
                            }

                            // If stable after provider + tool passes, finalize
                            if (!endStreaming && this.Request.Body.PendingToolCallsCount() == 0)
                            {
                                this.Observer?.OnFinal(lastReturn ?? new AIReturn());
                                endStreaming = true;
                            }
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        this.Observer?.OnError(oce);
                        var cancelled = new AIReturn();
                        cancelled.CreateProviderError("Call cancelled or timed out", this.Request);
                        yieldsThisTurn.Add(cancelled);
                        endStreaming = true;
                    }
                    catch (Exception ex)
                    {
                        this.Observer?.OnError(ex);
                        var error = new AIReturn();
                        error.CreateProviderError(ex.Message, this.Request);
                        yieldsThisTurn.Add(error);
                        endStreaming = true;
                    }

                    // Emit any prepared yields outside of try/catch
                    foreach (var item in yieldsThisTurn)
                    {
                        yield return item;
                    }

                    if (endStreaming)
                    {
                        yield break;
                    }

                    // Otherwise, continue to next turn
                    turns++;
                }

                // Max turns reached without stability
                var final = lastReturn ?? new AIReturn();
                if (lastReturn == null)
                {
                    final.CreateProviderError("Max turns reached without a stable result", this.Request);
                }
                this.Observer?.OnFinal(final);
                yield return final;
            }
            finally
            {
                linkedCts.Dispose();
            }
        }
    }
}
