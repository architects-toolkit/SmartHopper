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
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Tools;

    /// <summary>
    /// Helper methods supporting <see cref="ConversationSession"/> orchestration logic.
    /// </summary>
    public sealed partial class ConversationSession
    {

        /// <summary>
        /// Per-turn state carrier to keep streaming yields and control flags together.
        /// </summary>
        private struct TurnState
        {
            public string TurnId;
            public List<AIReturn> DeltaYields;
            public List<AIReturn> PendingToolYields;
            public AIReturn FinalProviderYield;
            public AIReturn ErrorYield;
            public bool ShouldBreak;
            public AIReturn LastDelta;
            public AIReturn LastToolCallsDelta;
        }

        /// <summary>
        /// Builds a small AIReturn containing a delta body for the given interactions using the provided TurnId.
        /// </summary>
        private AIReturn BuildDeltaReturn(string turnId, IEnumerable<IAIInteraction> interactions)
        {
            var builder = AIBodyBuilder.Create().WithTurnId(turnId);
            builder.AddRange(interactions);
            var body = builder.Build();
            var ret = new AIReturn();
            ret.SetBody(body);
            return ret;
        }

        /// <summary>
        /// Drains any pending tool calls before a provider turn begins.
        /// </summary>
        private async Task<List<AIReturn>> ResolvePendingToolsAsync(SessionOptions options, CancellationToken ct, string turnId)
        {
            return await this.ProcessPendingToolsAsync(options, ct, turnId).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a provider turn and optionally performs a post-tool pass when tools are enabled.
        /// Returns the last AIReturn produced in this composite step.
        /// </summary>
        private async Task<AIReturn> ExecuteProviderTurnAsync(SessionOptions options, CancellationToken ct, string turnId)
        {
            var providerReturn = await this.HandleProviderTurnAsync(options, ct, turnId).ConfigureAwait(false);
            if (options.ProcessTools)
            {
                var afterTools = await this.ProcessPendingToolsAsync(options, ct, turnId).ConfigureAwait(false);
                if (afterTools != null && afterTools.Count > 0)
                {
                    providerReturn = afterTools.Last();
                }
            }

            return providerReturn;
        }

        /// <summary>
        /// Runs a single tool call and persists its result into the session history.
        /// Emits partial deltas for UI and returns the final AIReturn the tool produced (or a synthetic error result if needed).
        /// </summary>
        private async Task<AIReturn> ExecuteSingleToolAsync(AIInteractionToolCall tc, string turnId, CancellationToken ct)
        {
            this.NotifyToolCall(tc);

            var toolRq = new AIToolCall();
            toolRq.FromToolCallInteraction(tc, this.Request.Provider, this.Request.Model);
            var toolRet = await this.executor.ExecToolAsync(toolRq, ct).ConfigureAwait(false);

            var toolInteraction = toolRet?.Body?.GetLastInteraction() as AIInteractionToolResult;
            if (toolInteraction == null)
            {
                var fallback = new AIInteractionToolResult
                {
                    Id = tc.Id,
                    Name = tc.Name,
                    Result = new JObject { ["error"] = toolRet?.ErrorMessage ?? "Tool execution failed or returned no result" },
                };
                this.PersistToolResult(fallback, turnId);

                var delta = new AIReturn();
                var deltaBody = AIBodyBuilder.Create().WithTurnId(turnId).Add(fallback).Build();
                delta.SetBody(deltaBody);
                return delta;
            }

            // Normalize metadata to guarantee correlation
            if (string.IsNullOrWhiteSpace(toolInteraction.Id)) toolInteraction.Id = tc.Id;
            if (string.IsNullOrWhiteSpace(toolInteraction.Name)) toolInteraction.Name = tc.Name;
            if (toolInteraction.Agent != AIAgent.ToolResult) toolInteraction.Agent = AIAgent.ToolResult;

            this.PersistToolResult(toolInteraction, turnId);

            var deltaOk = new AIReturn();
            var okBody = AIBodyBuilder.Create().WithTurnId(turnId).Add(toolInteraction).Build();
            deltaOk.SetBody(okBody);
            return deltaOk;
        }

        /// <summary>
        /// Persists a tool result to history and notifies observers.
        /// </summary>
        private void PersistToolResult(AIInteractionToolResult result, string turnId)
        {
            if (string.IsNullOrWhiteSpace(result.TurnId)) result.TurnId = turnId;
            this.AppendToSessionHistory(result);
            this.NotifyToolResult(result);
            try
            {
                var deltaReturn = this.BuildDeltaReturn(turnId, new[] { result });
                this.NotifyPartial(deltaReturn);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession] Error emitting tool result delta: {ex.Message}");
            }
        }

        /// <summary>
        /// Persists final streaming snapshot (tool_calls and assistant text), dedupes tool calls, updates last return,
        /// emits partial notifications, and logs unresolved pending tool-calls if any.
        /// </summary>
        private void PersistStreamingSnapshot(AIReturn lastToolCallsDelta, AIReturn lastDelta, string turnId)
        {
            // 1) Persist tool calls once (dedupe by id)
            var toolCallsToPersist = lastToolCallsDelta?.Body?.GetNewInteractions()?.OfType<AIInteractionToolCall>()?.ToList();
            if (toolCallsToPersist != null && toolCallsToPersist.Count > 0)
            {
                Debug.WriteLine($"[ConversationSession.Stream] Persisting tool calls once after stream: count={toolCallsToPersist.Count}");
                EnsureTurnIdFor(toolCallsToPersist, turnId);
                foreach (var tc in toolCallsToPersist)
                {
                    this.ReplaceToolCallInSessionHistory(tc);
                }
            }

            // 2) Persist final assistant text once
            var finalAssistantText = lastDelta?.Body?.GetNewInteractions()?.OfType<AIInteractionText>()?.LastOrDefault();
            if (finalAssistantText != null && !string.IsNullOrWhiteSpace(finalAssistantText.Content))
            {
                finalAssistantText.TurnId = turnId;
                this.AppendToSessionHistory(finalAssistantText);
            }

            // Update last return snapshot
            this._lastReturn = lastDelta;
            this.UpdateLastReturn();

            // Emit a partial delta in history order
            var persistedList = new List<IAIInteraction>();
            if (toolCallsToPersist != null && toolCallsToPersist.Count > 0)
            {
                persistedList.AddRange(toolCallsToPersist);
            }

            if (finalAssistantText != null && !string.IsNullOrWhiteSpace(finalAssistantText.Content))
            {
                persistedList.Add(finalAssistantText);
            }

            if (persistedList.Count > 0)
            {
                try
                {
                    var deltaReturn = this.BuildDeltaReturn(turnId, persistedList);
                    this.NotifyPartial(deltaReturn);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConversationSession.Stream] Error emitting persisted delta: {ex.Message}");
                }
            }

            // Guard against unresolved tool calls before next provider turn
            var pendingAfterStream = this.Request.Body.PendingToolCallsCount();
            if (pendingAfterStream > 0)
            {
                Debug.WriteLine($"[ConversationSession.Stream] WARNING: {pendingAfterStream} tool call(s) remain unresolved after streaming persistence.");
            }
        }

        /// <summary>
        /// Updates the cached last return with the current request body.
        /// </summary>
        private void UpdateLastReturn()
        {
            var snapshot = new AIReturn();
            snapshot.SetBody(this.Request.Body);
            this._lastReturn = snapshot;
        }

        /// <summary>
        /// Updates the cached last return preserving "new" markers from a source body.
        /// </summary>
        private void UpdateLastReturn(AIReturn sourceWithNewMarkers)
        {
            if (sourceWithNewMarkers?.Body == null)
            {
                this.UpdateLastReturn();
                return;
            }

            var sessionBody = this.Request.Body;
            var bodyHistoryOnly = AIBodyBuilder
                .FromImmutable(sessionBody)
                .ClearNewMarkers()
                .Build();

            var snapshot = new AIReturn();
            snapshot.SetBody(bodyHistoryOnly);
            this._lastReturn = snapshot;
        }

        /// <summary>
        /// Prepares a return containing only interactions marked as new.
        /// </summary>
        private AIReturn PrepareNewOnlyReturn(AIReturn source)
        {
            if (source == null)
            {
                return source;
            }

            var newOnly = source.Body?.GetNewInteractions() ?? new List<IAIInteraction>();
            var reduced = AIBodyBuilder.Create().AddRange(newOnly).Build();
            source.SetBody(reduced);
            return source;
        }

        /// <summary>
        /// Merges provider-returned interactions into the session body.
        /// </summary>
        private void MergeNewToSessionBody(IEnumerable<IAIInteraction>? interactions, bool toolsOnly)
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
                    var contentPreview = interaction is AIInteractionText t
                        ? (t.Content ?? string.Empty)
                        : interaction is AIInteractionToolCall tc
                            ? $"tool:{tc.Name}"
                            : interaction is AIInteractionToolResult tr
                                ? $"tool_result:{tr.Name}"
                                : string.Empty;

                    Debug.WriteLine(
                        $"[ConversationSession.MergeNewToSessionBody] appending: type={interaction.GetType().Name}, agent={interaction.Agent.ToString()}, content={contentPreview}");

                    if (interaction is AIInteractionToolCall tc2)
                    {
                        var existingToolCalls = this.Request.Body?.Interactions?.OfType<AIInteractionToolCall>()?.ToList() ?? new List<AIInteractionToolCall>();
                        var dupCount = existingToolCalls.Count(x => string.Equals(x?.Id, tc2.Id, StringComparison.Ordinal));
                        if (dupCount > 0)
                        {
                            Debug.WriteLine($"[ConversationSession.MergeNewToSessionBody] WARNING: inserting tool_call with duplicate id='{tc2?.Id}', existingCount={dupCount}, name='{tc2?.Name}'");
                        }
                    }
                }
                catch
                {
                    // logging only
                }

                this.AppendToSessionHistory(interaction);
            }
        }

        /// <summary>
        /// Notifies observer of streaming deltas.
        /// </summary>
        private void NotifyDelta(AIReturn ret)
        {
            if (this.Observer == null || ret?.Body?.Interactions == null)
            {
                return;
            }

            var newInteractions = ret.Body.GetNewInteractions();
            try
            {
                Debug.WriteLine($"[ConversationSession] NotifyDelta: new={newInteractions?.Count ?? 0}, total={ret.Body?.Interactions?.Count ?? 0}");
            }
            catch
            {
                // logging only
            }

            foreach (var interaction in newInteractions)
            {
                this.Observer.OnDelta(interaction);
            }
        }

        /// <summary>
        /// Notifies observer when interactions complete and are persisted.
        /// </summary>
        private void NotifyPartial(AIReturn ret)
        {
            if (this.Observer == null || ret?.Body?.Interactions == null)
            {
                return;
            }

            var newInteractions = ret.Body.GetNewInteractions();
            try
            {
                Debug.WriteLine($"[ConversationSession] NotifyPartial: new={newInteractions?.Count ?? 0}, total={ret.Body?.Interactions?.Count ?? 0}");
            }
            catch
            {
                // logging only
            }

            foreach (var interaction in newInteractions)
            {
                this.Observer.OnInteractionCompleted(interaction);
            }
        }

        /// <summary>
        /// Notifies observer that a turn has produced a final stable result.
        /// </summary>
        private void NotifyFinal(AIReturn ret)
        {
            try
            {
                Debug.WriteLine($"[ConversationSession] NotifyFinal: total={ret?.Body?.Interactions?.Count ?? 0}");
            }
            catch
            {
                // logging only
            }

            this.Observer?.OnFinal(ret);
        }

        /// <summary>
        /// Surfaces tool call notifications to observers.
        /// </summary>
        private void NotifyToolCall(AIInteractionToolCall toolCall) => this.Observer?.OnToolCall(toolCall);

        /// <summary>
        /// Surfaces tool result notifications to observers.
        /// </summary>
        private void NotifyToolResult(AIInteractionToolResult toolResult) => this.Observer?.OnToolResult(toolResult);

        /// <summary>
        /// Signals session start to observers.
        /// </summary>
        private void NotifyStart(AIRequestCall request) => this.Observer?.OnStart(request);

        /// <summary>
        /// Signals an error to observers.
        /// </summary>
        private void NotifyError(Exception error) => this.Observer?.OnError(error);

        /// <summary>
        /// Appends a single interaction to the session history without marking it as new.
        /// </summary>
        private void AppendToSessionHistory(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return;
            }

            var builder = AIBodyBuilder.FromImmutable(this.Request.Body)
                .ClearNewMarkers()
                .AsHistory();
            builder.Add(interaction, markAsNew: false);
            this.Request.Body = builder.Build();
        }

#if DEBUG
        /// <summary>
        /// Debug helper: logs current tool_call Ids in history and highlights duplicates.
        /// </summary>
        private void DebugLogToolCallIds(string phase)
        {
            try
            {
                var toolCalls = this.Request?.Body?.Interactions?.OfType<AIInteractionToolCall>()?.ToList() ?? new List<AIInteractionToolCall>();
                var total = toolCalls.Count;
                var dupGroups = toolCalls
                    .GroupBy(tc => tc?.Id ?? string.Empty)
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
                    .Select(g => $"{g.Key} x{g.Count()} [{string.Join(",", g.Select(t => t?.Name ?? ""))}]");

                var dupSummary = dupGroups.Any() ? string.Join("; ", dupGroups) : "none";
                Debug.WriteLine($"[ConversationSession.Debug] {phase}: tool_calls total={total}, duplicates={dupSummary}");
            }
            catch
            {
                // logging only
            }
        }
#endif

        /// <summary>
        /// Replaces an existing tool call snapshot with the latest metadata.
        /// </summary>
        private void ReplaceToolCallInSessionHistory(AIInteractionToolCall toolCall)
        {
            if (toolCall == null)
            {
                return;
            }

            var interactions = this.Request.Body?.Interactions?.ToList();
            bool replacedAny = false;
            if (interactions != null && interactions.Count > 0)
            {
                for (int index = 0; index < interactions.Count; index++)
                {
                    if (interactions[index] is AIInteractionToolCall existing && string.Equals(existing.Id, toolCall.Id, StringComparison.Ordinal))
                    {
                        interactions[index] = toolCall;
                        replacedAny = true;
                        // do not break; replace all duplicates with latest snapshot
                    }
                }
            }

            if (!replacedAny)
            {
                this.AppendToSessionHistory(toolCall);
                return;
            }

            var builder = AIBodyBuilder.FromImmutable(this.Request.Body)
                .ClearNewMarkers()
                .AsHistory();
            builder.ReplaceLastRange(interactions, markAsNew: false);
            this.Request.Body = builder.Build();
        }

        /// <summary>
        /// Ensures all interactions in a collection share the same turn identifier.
        /// </summary>
        private static void EnsureTurnIdFor(IEnumerable<IAIInteraction> interactions, string turnId)
        {
            if (string.IsNullOrWhiteSpace(turnId) || interactions == null)
            {
                return;
            }

            foreach (var interaction in interactions)
            {
                if (interaction == null)
                {
                    continue;
                }

                interaction.TurnId = turnId;
            }
        }

        /// <summary>
        /// Creates a standardized provider error return.
        /// </summary>
        private AIReturn CreateError(string message)
        {
            var ret = new AIReturn();
            ret.CreateProviderError(message, this.Request);
            return ret;
        }
    }
}
