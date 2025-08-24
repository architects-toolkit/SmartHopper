/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

    /// <summary>
    /// Minimal non-streaming conversation session that delegates to AIRequestCall.Exec.
    /// Future phases will add multi-turn loops, tool orchestration, and streaming.
    /// </summary>
    public sealed class ConversationSession : IConversationSession
    {
        private readonly CancellationTokenSource cts = new();

        public ConversationSession(AIRequestCall request, IConversationObserver? observer = null)
        {
            this.Request = request ?? throw new ArgumentNullException(nameof(request));
            this.Observer = observer;
        }

        public AIRequestCall Request { get; }

        public IConversationObserver? Observer { get; }

        public void Cancel()
        {
            if (!this.cts.IsCancellationRequested)
            {
                this.cts.Cancel();
            }
        }

        public async Task<AIReturn> RunToStableResult(SessionOptions options, CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.cts.Token, options.CancellationToken, cancellationToken);
            try
            {
                this.Observer?.OnStart(this.Request);

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
                            this.Request.Body.AddLastInteraction(interaction);
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
                                this.Request.Body.AddLastInteraction(toolInteraction);
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
                                    this.Request.Body.AddLastInteraction(errInteraction);
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
                                this.Request.Body.AddLastInteraction(interaction);
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
    }
}

