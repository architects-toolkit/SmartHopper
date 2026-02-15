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
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Metrics;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AICall.Utilities;

    /// <summary>
    /// Helper methods supporting <see cref="ConversationSession"/> orchestration logic.
    /// </summary>
    public sealed partial class ConversationSession
    {

        private static bool IsContextExceededError(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            // Prefer message checks since provider exceptions might wrap response details.
            return ConversationSession.IsContextExceededError(ex.Message);
        }

        private static bool IsContextExceededReturn(AIReturn ret)
        {
            if (ret == null)
            {
                return false;
            }

            try
            {
                // Check error messages (if any)
                var msg = ret.Messages?.FirstOrDefault(m => m?.Severity == AIRuntimeMessageSeverity.Error)?.Message;
                if (!string.IsNullOrEmpty(msg) && ConversationSession.IsContextExceededError(msg))
                {
                    return true;
                }

                // Check error interactions
                var errInteraction = ret.Body?.Interactions?.OfType<AIInteractionError>()?.LastOrDefault();
                if (errInteraction != null && ConversationSession.IsContextExceededError(errInteraction.Content))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TrySummarizeAndRetryAsync(string reason, CancellationToken ct)
        {
            if (this._summarizationAttempted)
            {
                Debug.WriteLine($"[ConversationSession] Reactive summarization skipped (already attempted this turn). Reason: {reason}");
                return false;
            }

            Debug.WriteLine($"[ConversationSession] Reactive summarization triggered. Reason: {reason}");
            var summarized = await this.TrySummarizeContextAsync(ct).ConfigureAwait(false);
            if (!summarized)
            {
                Debug.WriteLine("[ConversationSession] Reactive summarization failed (TrySummarizeContextAsync returned false). Not retrying.");
                return false;
            }

            // Caller will perform the retry.
            return true;
        }

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

            /// <summary>
            /// Accumulated text interaction deltas during streaming. Only the final aggregated text is persisted to history.
            /// </summary>
            public AIInteractionText AccumulatedText;
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
        private async Task<List<AIReturn>> ResolvePendingToolsAsync(SessionOptions options, string turnId, CancellationToken ct)
        {
            return await this.ProcessPendingToolsAsync(options, turnId, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a provider turn and optionally performs a post-tool pass when tools are enabled.
        /// Returns the last AIReturn produced in this composite step.
        /// </summary>
        private async Task<AIReturn> ExecuteProviderTurnAsync(SessionOptions options, string turnId, CancellationToken ct)
        {
            var providerReturn = await this.HandleProviderTurnAsync(options, turnId, ct).ConfigureAwait(false);

            if (options.ProcessTools)
            {
                var afterTools = await this.ProcessPendingToolsAsync(options, turnId, ct).ConfigureAwait(false);
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

            // Persist the tool_call into session history to maintain correct ordering
            // Some providers (e.g., Mistral) require an assistant tool_calls message
            // before any tool (role=tool) message. If the provider did not return the
            // tool_call in the previous turn for any reason, ensure it exists here.
            try
            {
                if (tc != null)
                {
                    if (string.IsNullOrWhiteSpace(tc.TurnId)) tc.TurnId = turnId;
                    if (tc.Agent != AIAgent.ToolCall) tc.Agent = AIAgent.ToolCall;

                    // Append only if this tool_call was not already persisted (avoid introducing duplicates ourselves).
                    var exists = this.Request?.Body?.Interactions?.OfType<AIInteractionToolCall>()?
                        .Any(x => !string.IsNullOrWhiteSpace(x?.Id) && string.Equals(x.Id, tc.Id, StringComparison.Ordinal)) ?? false;

                    if (!exists)
                    {
                        this.AppendToSessionHistory(tc);

                        // Emit a partial delta so observers can render the tool call immediately
                        var tcDelta = this.BuildDeltaReturn(turnId, new[] { tc });
                        this.NotifyInteractionCompleted(tcDelta);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession] Warning: failed to persist tool_call before execution: {ex.Message}");
            }

            var toolRq = new AIToolCall();
            toolRq.FromToolCallInteraction(tc, this.Request.Provider, this.Request.Model);

            // Measure tool execution time
            var stopwatch = Stopwatch.StartNew();
            var toolRet = await this.executor.ExecToolAsync(toolRq, ct).ConfigureAwait(false);
            stopwatch.Stop();

            var toolInteraction = toolRet?.Body?.GetLastInteraction() as AIInteractionToolResult;
            if (toolInteraction == null)
            {
                var fallback = new AIInteractionToolResult
                {
                    Id = tc.Id,
                    Name = tc.Name,
                    TurnId = tc.TurnId, // Inherit TurnId from ToolCall for turn consistency
                    Result = new JObject
                    {
                        ["success"] = false,
                        ["messages"] = toolRet?.Messages != null ? JArray.FromObject(toolRet.Messages) : new JArray()
                    },
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

            // ALWAYS preserve TurnId from the originating tool call to maintain turn consistency
            // ToolResults must share TurnId with their ToolCall for correct metrics aggregation
            toolInteraction.TurnId = tc.TurnId;

            this.PersistToolResult(toolInteraction, turnId);

            // Attach tool metrics and completion time to the tool result interaction
            if (toolRet?.Metrics != null)
            {
                toolInteraction.Metrics = toolRet.Metrics;

                // Override with actual measured time (includes tool execution + any overhead)
                toolInteraction.Metrics.CompletionTime = stopwatch.Elapsed.TotalSeconds;
            }
            else if (toolInteraction.Metrics == null)
            {
                // Create metrics with at least the completion time
                toolInteraction.Metrics = new AIMetrics
                {
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                    Provider = this.Request.Provider,
                    Model = this.Request.Model,
                    FinishReason = "stop", // Tool completed normally
                };
            }

            var deltaOk = new AIReturn();
            var okBody = AIBodyBuilder.Create()
                .WithTurnId(turnId)
                .Add(toolInteraction)
                .Build();
            deltaOk.SetBody(okBody);

            return deltaOk;
        }

        /// <summary>
        /// Persists a tool result to history and notifies observers.
        /// </summary>
        private void PersistToolResult(AIInteractionToolResult result, string turnId)
        {
            // TurnId should already be set by ExecuteSingleToolAsync to match the tool call's TurnId
            // Do not override it here to maintain correct message ordering
            this.AppendToSessionHistory(result);
            this.NotifyToolResult(result);
            try
            {
                var deltaReturn = this.BuildDeltaReturn(turnId, new[] { result });
                this.NotifyInteractionCompleted(deltaReturn);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession] Error emitting tool result delta: {ex.Message}");
            }
        }

        /// <summary>
        /// Persists final streaming snapshot (tool_calls and assistant text), updates last return,
        /// and logs unresolved pending tool-calls if any.
        /// </summary>
        /// <param name="completionTime">Total time taken for the streaming operation in seconds.</param>
        private void PersistStreamingSnapshot(AIReturn lastToolCallsDelta, AIReturn lastDelta, string turnId, AIInteractionText accumulatedText, double completionTime = 0)
        {
            // Persist the final aggregated text interaction (accumulated during streaming)
            if (accumulatedText != null && !string.IsNullOrWhiteSpace(accumulatedText.Content))
            {
                // Ensure TurnId is set
                if (string.IsNullOrWhiteSpace(accumulatedText.TurnId))
                {
                    accumulatedText.TurnId = turnId;
                }

                // Transfer final metrics from the provider's last delta to the accumulated text
                var finalAssistant = lastDelta?.Body?.GetLastInteraction(AIAgent.Assistant) as AIInteractionText;
                if (finalAssistant != null)
                {
                    // Copy complete metrics from the final provider delta
                    if (finalAssistant.Metrics != null)
                    {
                        accumulatedText.Metrics = finalAssistant.Metrics;

                        // Override with actual measured streaming time
                        accumulatedText.Metrics.CompletionTime = completionTime;
                    }

                    // Update time if available
                    if (finalAssistant.Time != default)
                    {
                        accumulatedText.Time = finalAssistant.Time;
                    }

                    // Preserve reasoning if present in final delta
                    if (!string.IsNullOrWhiteSpace(finalAssistant.Reasoning))
                    {
                        accumulatedText.Reasoning = finalAssistant.Reasoning;
                    }
                }

                // Persist the final aggregated text to session history
                this.AppendToSessionHistory(accumulatedText);
                Debug.WriteLine($"[ConversationSession.Stream] Persisted final aggregated text: turnId={turnId}, length={accumulatedText.Content?.Length ?? 0}");
            }

            // Update last return snapshot to use session body with correct TurnIds (not provider's raw delta)
            this.UpdateLastReturn();

            // Guard against unresolved tool calls before next provider turn
            var pendingAfterStream = this.Request.Body.PendingToolCallsCount();
            if (pendingAfterStream > 0)
            {
                Debug.WriteLine($"[ConversationSession.Stream] INFO: {pendingAfterStream} tool call(s) remain unresolved after streaming. They will be processed in subsequent passes.");
            }
        }

        private async Task<(AIReturn Result, bool ShouldBreak)> ExecuteNonStreamingProviderFollowUpAsync(string turnId, CancellationToken ct)
        {
            var followUp = await this.ExecProviderAsync(ct).ConfigureAwait(false);
            if (followUp == null)
            {
                var err = this.CreateError("Provider returned no response");
                this.NotifyFinal(err);
                return (err, true);
            }

            var followUpNew = followUp.Body?.GetNewInteractions();
            InteractionUtility.EnsureTurnId(followUpNew, turnId);
            this.MergeNewToSessionBody(followUpNew, toolsOnly: false);
            this._lastReturn = followUp;
            this.UpdateLastReturn();
            this.NotifyInteractionCompleted(followUp);
            return (followUp, false);
        }

        /// <summary>
        /// Updates the cached last return with the current request body.
        /// Metrics are already attached to individual interactions via NotifyInteractionCompleted,
        /// and AIBody.Metrics aggregates them automatically.
        /// </summary>
        private void UpdateLastReturn()
        {
            this.UpdateLastReturnCore(this.Request.Body);
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
            this.UpdateLastReturnCore(bodyHistoryOnly);
        }

        private void UpdateLastReturnCore(AIBody body)
        {
            var snapshot = new AIReturn();
            snapshot.SetBody(body);

            // Get context usage from aggregated metrics for logging
            var contextUsage = snapshot.Metrics?.ContextUsagePercent;
#if DEBUG
            var aggregatedMetrics = snapshot.Metrics;
            if (aggregatedMetrics != null)
            {
                var contextUsageStr = contextUsage.HasValue
                    ? $", Context={contextUsage.Value:P1}"
                    : string.Empty;
                Debug.WriteLine($"[ConversationSession.UpdateLastReturn] Final aggregated metrics from body: Tokens In={aggregatedMetrics.InputTokensPrompt}, Out={aggregatedMetrics.OutputTokensGeneration}, Time={aggregatedMetrics.CompletionTime:F2}s, FinishReason={aggregatedMetrics.FinishReason}{contextUsageStr}");
            }

#endif
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
                Debug.WriteLine($"[ConversationSession] NotifyDelta: new={(newInteractions?.Count ?? 0)}, total={ret.Body?.Interactions?.Count ?? 0}");
#if DEBUG
                try
                {
                    var summary = BuildInteractionSummaryForLog(newInteractions, maxItems: 5, textPreview: 50);
                    this.DebugAppendEvent($"Delta: new={(newInteractions?.Count ?? 0)} | {summary}");
                }
                catch { }
#endif
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
        private void NotifyInteractionCompleted(AIReturn ret)
        {
            if (this.Observer == null || ret?.Body?.Interactions == null)
            {
                return;
            }

            var newInteractions = ret.Body.GetNewInteractions();
            try
            {
                Debug.WriteLine($"[ConversationSession] NotifyInteractionCompleted: new={(newInteractions?.Count ?? 0)}, total={ret.Body?.Interactions?.Count ?? 0}");
#if DEBUG
                try
                {
                    var summary = BuildInteractionSummaryForLog(newInteractions, maxItems: 5, textPreview: 50);
                    this.DebugAppendEvent($"Partial: new={(newInteractions?.Count ?? 0)} | {summary}");
                }
                catch { }
#endif
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
#if DEBUG
                try
                {
                    var interactions = ret?.Body?.GetNewInteractions();
                    if (interactions == null || interactions.Count == 0)
                    {
                        var last = ret?.Body?.GetLastInteraction();
                        interactions = last != null ? new List<IAIInteraction> { last } : new List<IAIInteraction>();
                    }

                    var summary = BuildInteractionSummaryForLog(interactions, maxItems: 5, textPreview: 50);
                    this.DebugAppendEvent($"Final: total={(ret?.Body?.Interactions?.Count ?? 0)} | {summary}");
                }
                catch { }
#endif
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
        private void NotifyToolCall(AIInteractionToolCall toolCall)
        {
#if DEBUG
            try { this.DebugAppendEvent($"ToolCall: name={toolCall?.Name}, id={toolCall?.Id}"); } catch { }
#endif
            this.Observer?.OnToolCall(toolCall);
        }

        /// <summary>
        /// Surfaces tool result notifications to observers.
        /// </summary>
        private void NotifyToolResult(AIInteractionToolResult toolResult)
        {
#if DEBUG
            try { this.DebugAppendEvent($"ToolResult: name={toolResult?.Name}, id={toolResult?.Id}"); } catch { }
#endif
            this.Observer?.OnToolResult(toolResult);
        }

        /// <summary>
        /// Signals session start to observers.
        /// </summary>
        private void NotifyStart(AIRequestCall request)
        {
#if DEBUG
            try { this.DebugResetEventLog(); this.DebugAppendEvent($"Start: provider={request?.Provider}, model={request?.Model}, endpoint={request?.Endpoint}"); } catch { }
#endif
            this.Observer?.OnStart(request);
        }

        /// <summary>
        /// Signals an error to observers.
        /// </summary>
        private void NotifyError(Exception error)
        {
#if DEBUG
            try { this.DebugAppendEvent($"Error: {error?.Message}"); } catch { }
#endif
            this.Observer?.OnError(error);
        }

        /// <summary>
        /// Appends a single interaction to the session history without marking it as new.
        /// </summary>
        private void AppendToSessionHistory(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return;
            }

#if DEBUG
            try
            {
                var preview = interaction switch
                {
                    AIInteractionText txt => $"Text(agent={txt.Agent}, turnId={txt.TurnId}, content='{txt.Content?.Substring(0, Math.Min(50, txt.Content?.Length ?? 0))}...')",
                    AIInteractionToolResult tr => $"ToolResult(name={tr.Name}, id={tr.Id}, turnId={tr.TurnId})",
                    AIInteractionToolCall tc => $"ToolCall(name={tc.Name}, id={tc.Id}, turnId={tc.TurnId}, argsLen={tc.Arguments?.ToString()?.Length ?? 0})",
                    _ => $"{interaction.GetType().Name}(agent={interaction.Agent}, turnId={interaction.TurnId})"
                };
                Debug.WriteLine($"[ConversationSession.AppendToSessionHistory] {preview}");
            }
            catch { }
#endif

            var builder = AIBodyBuilder.FromImmutable(this.Request.Body)
                .ClearNewMarkers()
                .AsHistory();
            builder.Add(interaction, markAsNew: false);
            this.Request.Body = builder.Build();

#if DEBUG
            try
            {
                this.DebugWriteConversationHistory();
            }
            catch
            {
                // debug-only logging, ignore failures
            }

#endif
        }

        /// <summary>
        /// Updates an existing tool call in session history with new arguments.
        /// Used during streaming when tool call arguments arrive incrementally.
        /// </summary>
        /// <param name="updatedToolCall">The tool call with updated arguments.</param>
        /// <returns>True if the tool call was found and updated; false otherwise.</returns>
        private bool UpdateToolCallInHistory(AIInteractionToolCall updatedToolCall)
        {
            if (updatedToolCall == null || string.IsNullOrWhiteSpace(updatedToolCall.Id))
            {
                return false;
            }

            var interactions = this.Request?.Body?.Interactions;
            if (interactions == null || interactions.Count == 0)
            {
                return false;
            }

            // Find the existing tool call by ID
            var existingIndex = -1;
            for (int i = 0; i < interactions.Count; i++)
            {
                if (interactions[i] is AIInteractionToolCall tc &&
                    string.Equals(tc.Id, updatedToolCall.Id, StringComparison.Ordinal))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                return false;
            }

            // Only update if the new arguments are more complete
            var existingTc = (AIInteractionToolCall)interactions[existingIndex];
            var existingArgsLen = existingTc.Arguments?.ToString()?.Length ?? 0;
            var newArgsLen = updatedToolCall.Arguments?.ToString()?.Length ?? 0;

            if (newArgsLen <= existingArgsLen)
            {
                // New arguments are not more complete, skip update
                return false;
            }

#if DEBUG
            Debug.WriteLine($"[ConversationSession.UpdateToolCallInHistory] Updating tool call {updatedToolCall.Id}: argsLen {existingArgsLen} -> {newArgsLen}");
#endif

            // Replace the interaction at the existing index
            var newInteractions = new List<IAIInteraction>(interactions);
            newInteractions[existingIndex] = updatedToolCall;

            // Rebuild from scratch with updated list
            var newBuilder = AIBodyBuilder.Create()
                .WithToolFilter(this.Request.Body?.ToolFilter)
                .WithContextFilter(this.Request.Body?.ContextFilter)
                .WithJsonOutputSchema(this.Request.Body?.JsonOutputSchema)
                .AsHistory();

            foreach (var interaction in newInteractions)
            {
                newBuilder.Add(interaction, markAsNew: false);
            }

            this.Request.Body = newBuilder.Build();
            return true;
        }

#if DEBUG
        /// <summary>
        /// Builds a compact summary string for interactions, including type and a 50-char preview for text.
        /// </summary>
        private static string BuildInteractionSummaryForLog(IEnumerable<IAIInteraction> interactions, int maxItems = 5, int textPreview = 50)
        {
            try
            {
                if (interactions == null)
                {
                    return string.Empty;
                }

                var items = new List<string>();
                int count = 0;
                foreach (var it in interactions)
                {
                    if (it == null)
                    {
                        continue;
                    }

                    if (count++ >= maxItems)
                    {
                        break;
                    }

                    string token;
                    switch (it)
                    {
                        case AIInteractionText txt:
                            var content = txt.Content ?? string.Empty;
                            content = content.Replace("\r", " ").Replace("\n", " ");
                            if (content.Length > textPreview)
                            {
                                content = content.Substring(0, textPreview) + "...";
                            }

                            token = $"Text:\"{content}\"";
                            break;

                        case AIInteractionToolResult res:
                            token = $"ToolResult:{res?.Name ?? ""}#{res?.Id ?? ""}";
                            break;

                        case AIInteractionToolCall call:
                            token = $"ToolCall:{call?.Name ?? ""}#{call?.Id ?? ""}";
                            break;

                        default:
                            token = it.GetType().Name;
                            break;
                    }

                    items.Add(token);
                }

                return string.Join(" | ", items);
            }
            catch
            {
                return string.Empty;
            }
        }

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
        /// Creates a standardized provider error return.
        /// </summary>
        private AIReturn CreateError(string message)
        {
            var ret = new AIReturn();
            ret.CreateProviderError(message, this.Request);
            return ret;
        }

        /// <summary>
        /// Creates a provider error return, surfaces error interactions to observers, and notifies error.
        /// Centralizes error handling to avoid duplication across catch blocks.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <returns>The error AIReturn.</returns>
        private AIReturn HandleAndNotifyError(Exception ex)
        {
            var errorMessage = ex is OperationCanceledException
                ? "Call cancelled or timed out"
                : ex.Message;

            var error = new AIReturn();
            error.CreateProviderError(errorMessage, this.Request);

            // Surface error interactions from the body before calling OnError
            var errorInteractions = error.Body?.Interactions;
            if (errorInteractions != null && errorInteractions.Count > 0)
            {
                foreach (var errInteraction in errorInteractions)
                {
                    if (errInteraction is AIInteractionError)
                    {
                        this.Observer?.OnInteractionCompleted(errInteraction);
                    }
                }
            }

            this.NotifyError(ex);
            return error;
        }

#if DEBUG

        /// <summary>
        /// Writes the entire conversation history to a Markdown file under %APPDATA%/Grasshopper/SmartHopper/Debug.
        /// File name: ConversationSession-History.md
        /// After summarization, appends the new summarized history instead of overwriting.
        /// </summary>
        private void DebugWriteConversationHistory()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "Grasshopper", "SmartHopper", "Debug");
                Directory.CreateDirectory(folder);
                var filePath = Path.Combine(folder, "ConversationSession-History.md");

                var sb = new StringBuilder();

                // If summarization occurred, we're appending to the file (marker was already added)
                // Otherwise, start fresh with a header
                if (!this._summarizationOccurred)
                {
                    sb.AppendLine("# SmartHopper Conversation History");
                    sb.AppendLine();
                }
                else
                {
                    // Add a section header for the summarized conversation
                    sb.AppendLine();
                    sb.AppendLine("## Summarized Conversation (Current State)");
                    sb.AppendLine();
                }

                sb.AppendLine($"Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
                sb.AppendLine($"Provider: {this.Request?.Provider ?? ""}");
                sb.AppendLine($"Model: {this.Request?.Model ?? ""}");
                sb.AppendLine($"Endpoint: {this.Request?.Endpoint ?? ""}");
                sb.AppendLine();

                // Aggregate metrics block (session-level)
                var agg = this.Request?.Body?.Metrics;
                if (agg != null)
                {
                    sb.AppendLine("## Aggregate Metrics");
                    WriteMetricsBlock(sb, agg);
                    sb.AppendLine();
                }

                var interactions = this.Request?.Body?.Interactions ?? new List<IAIInteraction>();
                int index = 1;
                foreach (var it in interactions)
                {
                    if (it == null)
                    {
                        continue;
                    }

                    var role = it.Agent.ToString();
                    sb.AppendLine($"## {index}. {role}");
                    if (!string.IsNullOrWhiteSpace(it.TurnId))
                    {
                        sb.AppendLine($"TurnId: `{it.TurnId}`");
                    }

                    sb.AppendLine();

                    switch (it)
                    {
                        case AIInteractionText txt:
                            WriteCodeBlock(sb, txt.Content ?? string.Empty, "text");
                            break;

                        case AIInteractionToolResult res:
                            sb.AppendLine($"Tool Result: `{res.Name}`  ");
                            sb.AppendLine($"Id: `{res.Id}`");
                            sb.AppendLine();
                            var result = res.Result;
                            WriteCodeBlock(sb, result != null ? result.ToString(Newtonsoft.Json.Formatting.Indented) : "{}", "json");
                            break;

                        case AIInteractionToolCall call:
                            sb.AppendLine($"Tool: `{call.Name}`  ");
                            sb.AppendLine($"Id: `{call.Id}`");
                            sb.AppendLine();
                            var args = call.Arguments;
                            WriteCodeBlock(sb, args != null ? args.ToString(Newtonsoft.Json.Formatting.Indented) : "{}", "json");
                            break;

                        default:
                            WriteCodeBlock(sb, it?.ToString() ?? string.Empty, "text");
                            break;
                    }

                    // Per-interaction metrics (when available)
                    try
                    {
                        if (it?.Metrics != null)
                        {
                            sb.AppendLine("### Metrics");
                            WriteMetricsBlock(sb, it.Metrics);
                        }
                    }
                    catch { }

                    sb.AppendLine();
                    index++;
                }

                // If summarization occurred, append to preserve history; otherwise overwrite
                if (this._summarizationOccurred)
                {
                    File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
                else
                {
                    File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession.Debug] Error writing conversation markdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to write a fenced code block to the StringBuilder.
        /// </summary>
        private static void WriteCodeBlock(StringBuilder sb, string content, string language)
        {
            if (sb == null)
            {
                return;
            }

            language = string.IsNullOrWhiteSpace(language) ? "" : language.Trim();
            sb.AppendLine($"```{language}");
            sb.AppendLine(content ?? string.Empty);
            sb.AppendLine("```");
        }

        /// <summary>
        /// Helper to write a metrics block (simple key/value list) to the StringBuilder.
        /// </summary>
        private static void WriteMetricsBlock(StringBuilder sb, AIMetrics m)
        {
            if (sb == null || m == null)
            {
                return;
            }

            // Render as a compact markdown list
            sb.AppendLine("- **provider**: " + (m.Provider ?? string.Empty));
            sb.AppendLine("- **model**: " + (m.Model ?? string.Empty));
            sb.AppendLine("- **finish_reason**: " + (m.FinishReason ?? string.Empty));
            sb.AppendLine("- **completion_time**: " + m.CompletionTime);
            sb.AppendLine("- **estimated_input_tokens**: " + m.EstimatedInputTokens);
            sb.AppendLine("- **estimated_output_tokens**: " + m.EstimatedOutputTokens);
            sb.AppendLine("- **total_estimated_tokens**: " + m.TotalEstimatedTokens);
            sb.AppendLine("- **input_tokens_prompt**: " + m.InputTokensPrompt);
            sb.AppendLine("- **input_tokens_cached**: " + m.InputTokensCached + " (total: " + m.InputTokens + ")");
            sb.AppendLine("- **output_tokens_reasoning**: " + m.OutputTokensReasoning);
            sb.AppendLine("- **output_tokens_generation**: " + m.OutputTokensGeneration + " (total: " + m.OutputTokens + ")");
            sb.AppendLine("- **total_tokens**: " + m.TotalTokens);
            sb.AppendLine("- **effective_total_tokens**: " + m.EffectiveTotalTokens);
            sb.AppendLine("- **context_usage_percent**: " + (m.ContextUsagePercent?.ToString() ?? string.Empty));
        }

        /// <summary>
        /// Resets the event log file at the start of a conversation.
        /// </summary>
        private void DebugResetEventLog()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "Grasshopper", "SmartHopper", "Debug");
                Directory.CreateDirectory(folder);
                var eventPath = Path.Combine(folder, "ConversationSession-Events.log");
                var header = $"# ConversationSession Events\r\nStarted: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}\r\n";
                File.WriteAllText(eventPath, header, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession.Debug] Error resetting events log: {ex.Message}");
            }
        }

        /// <summary>
        /// Appends a single line event to the event log file.
        /// </summary>
        private void DebugAppendEvent(string evt)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "Grasshopper", "SmartHopper", "Debug");
                Directory.CreateDirectory(folder);
                var eventPath = Path.Combine(folder, "ConversationSession-Events.log");
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz} - {evt}{Environment.NewLine}";
                File.AppendAllText(eventPath, line, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession.Debug] Error appending event: {ex.Message}");
            }
        }

#endif
    }
}
