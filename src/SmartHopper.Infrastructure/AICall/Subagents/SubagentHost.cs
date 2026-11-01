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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Execution;
using SmartHopper.Infrastructure.AICall.Sessions;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Infrastructure.AICall.Subagents
{
    /// <summary>
    /// Per-session conductor registry: owns the session's subagents, launches their
    /// runs as background <see cref="ConversationSession"/> executions cloned from
    /// the parent request, and reports terminal runs through
    /// <see cref="RunFinished"/> so the chat host can wake the conductor.
    /// </summary>
    /// <remarks>
    /// Child sessions inherit the parent's provider/model/endpoint and tool filter
    /// minus the conductor tools, receive no presenters and no
    /// <see cref="ISubagentHost"/> — so they cannot spawn recursively (depth cap 1)
    /// and consent-gated tools fail closed. Runs are in-memory; they do not
    /// survive a Rhino restart.
    /// </remarks>
    public sealed class SubagentHost : ISubagentHost, IDisposable
    {
        /// <summary>Maximum subagents per session (cloud parity).</summary>
        public const int MaxSubagents = 8;

        /// <summary>Maximum concurrent runs per session (cloud parity).</summary>
        public const int MaxConcurrentRuns = 8;

        /// <summary>Upper bound for label/instruction/report text kept in memory and rendered.</summary>
        private const int TextBound = 4000;

        private const string SubagentPreamble =
            "You are a subagent of the SmartHopper Grasshopper assistant, working a delegated task in the background. " +
            "Do the focused work described by the instruction and return a concise, self-contained report — " +
            "the orchestrator sees only your final answer, not your intermediate steps. " +
            "You cannot spawn other subagents. " +
            "Tools that mutate the Grasshopper canvas are unavailable to you (there is no user consent context " +
            "in a background run); keep end-user-facing changes to the orchestrator. " +
            "Stay on task: prefer a few well-chosen tool calls over broad exploration.";

        /// <summary>Conductor tool names excluded from every child request's tool filter.</summary>
        private static readonly string[] ConductorToolNames =
        {
            "spawn_subagent",
            "send_to_subagent",
            "wait_subagents",
            "list_subagents",
        };

        private readonly AIRequestCall parentRequest;
        private readonly IProviderExecutor executor;
        private readonly Func<AIRequestCall, ConversationSession> childSessionFactory;
        private readonly List<Subagent> subagents = new ();
        private readonly SemaphoreSlim runSlots = new (MaxConcurrentRuns);
        private readonly CancellationTokenSource hostCts = new ();
        private readonly object gate = new ();
        private volatile bool cancelled;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubagentHost"/> class.
        /// </summary>
        /// <param name="parentRequest">
        /// The parent session's request; child runs clone provider, model, endpoint,
        /// capability, surface, and tool filter (minus conductor tools) from it.
        /// </param>
        /// <param name="executor">Optional executor override; defaults to the session executor.</param>
        /// <param name="childSessionFactory">
        /// Optional factory producing the child <see cref="ConversationSession"/> for a
        /// spawned subagent; used by tests to substitute sessions backed by stub requests.
        /// Defaults to a session without observer, presenters, or subagent host.
        /// </param>
        public SubagentHost(
            AIRequestCall parentRequest,
            IProviderExecutor? executor = null,
            Func<AIRequestCall, ConversationSession>? childSessionFactory = null)
        {
            this.parentRequest = parentRequest ?? throw new ArgumentNullException(nameof(parentRequest));
            this.executor = executor ?? new DefaultProviderExecutor();
            this.childSessionFactory = childSessionFactory
                ?? (request => new ConversationSession(
                    request,
                    observer: null,
                    executor: this.executor,
                    generateGreeting: false));
        }

        /// <inheritdoc/>
        public event EventHandler<SubagentRunReport>? RunFinished;

        /// <inheritdoc/>
        public event EventHandler? PendingChanged;

        /// <inheritdoc/>
        public IReadOnlyList<SubagentPendingItem> PendingItems
        {
            get
            {
                lock (this.gate)
                {
                    return this.subagents
                        .SelectMany(s => s.PendingInstructions)
                        .OrderBy(i => i.EnqueuedAtUtc)
                        .ToList();
                }
            }
        }

        /// <inheritdoc/>
        public int RunningCount
        {
            get
            {
                lock (this.gate)
                {
                    return this.subagents.Sum(s => s.Runs.Count(r => r.Status == SubagentRunStatus.Running));
                }
            }
        }

        /// <inheritdoc/>
        public Task<SubagentItem> SpawnAsync(string label, string brief, CancellationToken cancellationToken)
        {
            if (this.cancelled)
            {
                throw new InvalidOperationException("The subagent host is cancelled (chat closed).");
            }

            label = Bound(label, 40)?.Trim() ?? string.Empty;
            brief = Bound(brief, TextBound)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = "subagent";
            }

            if (string.IsNullOrWhiteSpace(brief))
            {
                throw new ArgumentException("spawn_subagent requires a non-empty 'brief'.", nameof(brief));
            }

            lock (this.gate)
            {
                if (this.subagents.Count >= MaxSubagents)
                {
                    throw new InvalidOperationException($"Subagent limit reached ({MaxSubagents} per session); reuse an existing subagent or wait for one to finish.");
                }

                var childRequest = this.BuildChildRequest(label);
                var childSession = this.childSessionFactory(childRequest);

                var subagent = new Subagent(label, childSession);
                this.subagents.Add(subagent);

                var run = this.Launch(subagent, brief);
                return Task.FromResult(ToItem(subagent, run));
            }
        }

        /// <inheritdoc/>
        public Task<SubagentItem> SendAsync(string subagentId, string message, SubagentSendMode mode, CancellationToken cancellationToken)
        {
            if (this.cancelled)
            {
                throw new InvalidOperationException("The subagent host is cancelled (chat closed).");
            }

            message = Bound(message, TextBound)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("send_to_subagent requires a non-empty 'message'.", nameof(message));
            }

            SubagentItem item;
            var pendingChanged = false;
            lock (this.gate)
            {
                var subagent = this.Resolve(subagentId);
                var latest = subagent.LatestRun;
                if (latest == null || latest.IsTerminal)
                {
                    var run = this.Launch(subagent, message);
                    item = ToItem(subagent, run, "started");
                }
                else if (mode == SubagentSendMode.OnTheFly && CanSteer(subagent))
                {
                    // On-the-fly steering: the instruction lands in the live
                    // session and the worker reads it at its next provider call.
                    // A steer that lands after the run's last call stays an
                    // unaddressed user-text tail — KickNext gives it a
                    // continuation run so it is never swallowed by history.
                    subagent.Session.AddInteraction(message);
                    item = ToItem(subagent, latest, "steered");
                }
                else
                {
                    // Queue mode (or an on-the-fly send that is unsafe to inject,
                    // e.g. a pending tool call): the instruction waits FIFO in the
                    // worker's pending list and KickNext delivers it on settle.
                    subagent.PendingInstructions.Add(new SubagentPendingItem
                    {
                        SubagentId = subagent.Id,
                        Label = subagent.Label,
                        Text = message,
                    });
                    item = ToItem(subagent, latest, "queued");
                    pendingChanged = true;
                }
            }

            if (pendingChanged)
            {
                this.NotifyPendingChanged();
            }

            return Task.FromResult(item);
        }

        /// <inheritdoc/>
        public bool TryApplyPendingAction(SubagentPendingAction action, string itemId, string? text)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            var changed = false;
            lock (this.gate)
            {
                foreach (var subagent in this.subagents)
                {
                    var index = subagent.PendingInstructions.FindIndex(p => string.Equals(p.Id, itemId, StringComparison.Ordinal));
                    if (index < 0)
                    {
                        continue;
                    }

                    switch (action)
                    {
                        case SubagentPendingAction.Remove:
                            subagent.PendingInstructions.RemoveAt(index);
                            changed = true;
                            break;
                        case SubagentPendingAction.Edit:
                            var next = Bound(text, TextBound)?.Trim();
                            if (!string.IsNullOrWhiteSpace(next) &&
                                !string.Equals(next, subagent.PendingInstructions[index].Text, StringComparison.Ordinal))
                            {
                                subagent.PendingInstructions[index].Text = next;
                                changed = true;
                            }

                            break;
                        case SubagentPendingAction.Promote:
                            if (index > 0)
                            {
                                var pending = subagent.PendingInstructions[index];
                                subagent.PendingInstructions.RemoveAt(index);
                                subagent.PendingInstructions.Insert(0, pending);
                                changed = true;
                            }

                            break;
                    }

                    break;
                }
            }

            if (changed)
            {
                this.NotifyPendingChanged();
            }

            return changed;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SubagentItem>> WaitAsync(IReadOnlyList<string>? subagentIds, int timeoutSeconds, CancellationToken cancellationToken)
        {
            List<(Subagent Agent, SubagentRun Run)> pending;
            lock (this.gate)
            {
                var selected = subagentIds == null || subagentIds.Count == 0
                    ? this.subagents.ToList()
                    : subagentIds.Select(this.Resolve).Distinct().ToList();

                pending = selected
                    .Select(s => (s, s.LatestRun))
                    .Where(t => t.Item2 != null)
                    .Select(t => (t.Item1, t.Item2!))
                    .ToList();
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, Math.Min(timeoutSeconds, 120)));
            while (true)
            {
                var running = pending.Where(p => p.Run.Status == SubagentRunStatus.Running && p.Run.Work != null).Select(p => p.Run.Work!).ToList();
                if (running.Count == 0 || DateTime.UtcNow >= deadline || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var untilDeadline = Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                try
                {
                    await Task.WhenAny(Task.WhenAll(running), untilDeadline).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            var items = new List<SubagentItem>();
            lock (this.gate)
            {
                foreach (var (agent, run) in pending)
                {
                    if (run.IsTerminal)
                    {
                        // The outcome now reaches the conductor inline; it must not wake the session later.
                        run.Delivered = true;
                    }

                    items.Add(ToItem(agent, run));
                }
            }

            return items;
        }

        /// <inheritdoc/>
        public IReadOnlyList<SubagentItem> List()
        {
            lock (this.gate)
            {
                return this.subagents
                    .Select(s => ToItem(s, s.LatestRun))
                    .ToList();
            }
        }

        /// <inheritdoc/>
        public void CancelAll()
        {
            this.cancelled = true;
            this.hostCts.Cancel();

            var pendingDropped = false;
            lock (this.gate)
            {
                foreach (var subagent in this.subagents)
                {
                    pendingDropped |= subagent.PendingInstructions.Count > 0;
                    subagent.PendingInstructions.Clear();
                }
            }

            if (pendingDropped)
            {
                this.NotifyPendingChanged();
            }
        }

        /// <summary>Cancels all runs and releases the concurrency primitives.</summary>
        public void Dispose()
        {
            this.CancelAll();
            this.hostCts.Dispose();
            this.runSlots.Dispose();
        }

        /// <summary>Extracts the last assistant text from a finished child session.</summary>
        private static string? LastAssistantText(ConversationSession session)
        {
            return session
                .GetHistoryInteractionList()
                .OfType<AIInteractionText>()
                .LastOrDefault(i => i.Agent == AIAgent.Assistant)
                ?.Content;
        }

        /// <summary>Truncates text to <paramref name="max"/> characters.</summary>
        private static string? Bound(string? text, int max)
        {
            if (text == null || text.Length <= max)
            {
                return text;
            }

            return string.Concat(text.AsSpan(0, max), "…");
        }

        /// <summary>Serializes a status to the wire form used in items and evidence lines.</summary>
        private static string StatusToWire(SubagentRunStatus status)
        {
            return status switch
            {
                SubagentRunStatus.Succeeded => "succeeded",
                SubagentRunStatus.Failed => "failed",
                _ => "running",
            };
        }

        /// <summary>
        /// A user instruction may only be appended to a live session where no
        /// tool call is pending — otherwise it would split a call/result pair
        /// the provider requires adjacent. Checked under the gate; the residual
        /// race (a tool call landing between check and append) is a bounded
        /// provider-error risk, never a silent mutation.
        /// </summary>
        private static bool CanSteer(Subagent subagent)
        {
            return (subagent.Session.Request.Body?.PendingToolCallsCount() ?? 0) == 0;
        }

        /// <summary>Projects a subagent + run to the model-facing item shape.</summary>
        private static SubagentItem ToItem(Subagent subagent, SubagentRun? run, string? delivery = null)
        {
            return new SubagentItem
            {
                SubagentId = subagent.Id,
                Label = subagent.Label,
                RunId = run?.Id,
                Status = run == null ? "idle" : StatusToWire(run.Status),
                Result = run?.Result,
                Error = run?.Error,
                LastInstruction = Bound(run?.Instruction, 200) ?? string.Empty,
                FinishedAt = run?.FinishedAt,
                PendingCount = subagent.PendingInstructions.Count,
                Delivery = delivery,
            };
        }

        /// <summary>
        /// Starts a run for <paramref name="subagent"/> in the background and returns it.
        /// The run task appends the instruction, executes to a stable result, records the
        /// outcome, and fires <see cref="RunFinished"/> — the only place results flow
        /// back to the conductor outside <c>wait_subagents</c>.
        /// </summary>
        private SubagentRun Launch(Subagent subagent, string instruction)
        {
            var run = new SubagentRun(instruction ?? string.Empty);
            subagent.Runs.Add(run);

            run.Work = Task.Run(
                async () =>
                {
                    var slotAcquired = false;
                    try
                    {
                        await this.runSlots.WaitAsync().ConfigureAwait(false);
                        slotAcquired = true;

                        if (this.cancelled)
                        {
                            run.Status = SubagentRunStatus.Failed;
                            run.Error = "Cancelled: the chat session ended before this run could start.";
                            return;
                        }

                        subagent.Session.AddInteraction(instruction);

                        var options = new SessionOptions
                        {
                            ProcessTools = true,
                            MaxAutonomousTime = TimeSpan.FromMinutes(5),
                            MaxAutonomousTokens = 150_000,
                            CancellationToken = this.hostCts.Token,
                        };

                        var result = await subagent.Session.RunToStableResult(options).ConfigureAwait(false);

                        var reply = LastAssistantText(subagent.Session);
                        if (!string.IsNullOrWhiteSpace(reply))
                        {
                            run.Status = SubagentRunStatus.Succeeded;
                            run.Result = Bound(reply, TextBound);
                        }
                        else
                        {
                            run.Status = SubagentRunStatus.Failed;
                            var err = result?.Messages?.LastOrDefault(m => m.Severity == SHRuntimeMessageSeverity.Error)?.Message;
                            run.Error = Bound(err ?? "The run finished without a reply.", 1000);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        run.Status = SubagentRunStatus.Failed;
                        run.Error = "Cancelled.";
                    }
                    catch (Exception ex)
                    {
                        Logger.For<SubagentHost>().Exception(ex, $"[SubagentHost] Run {run.Id} failed");
                        run.Status = SubagentRunStatus.Failed;
                        run.Error = Bound(ex.Message, 1000);
                    }
                    finally
                    {
                        run.FinishedAt = DateTime.UtcNow;
                        if (slotAcquired)
                        {
                            this.runSlots.Release();
                        }

                        this.Report(subagent, run);
                        this.KickNext(subagent, run);
                    }
                });

            return run;
        }

        /// <summary>
        /// Delivers whatever the worker owes next once <paramref name="finished"/>
        /// settles: the head of its pending queue launches as a new run. When the
        /// queue is empty, an on-the-fly steer still gets a guaranteed delivery:
        /// a steer injected mid-run leaves a user-text entry the run may never
        /// have seen (it lands inside the last provider call's window), so when
        /// the last user text in history is not the finished run's own
        /// instruction, a continuation run answers it. The continuation is
        /// idempotent — a steer the run did see costs one brief confirmation
        /// turn — and self-terminating, since the continuation's own instruction
        /// becomes the last user text.
        /// </summary>
        private void KickNext(Subagent subagent, SubagentRun finished)
        {
            if (this.cancelled)
            {
                return;
            }

            var pendingChanged = false;
            lock (this.gate)
            {
                if (this.cancelled)
                {
                    return;
                }

                if (subagent.PendingInstructions.Count > 0)
                {
                    var pending = subagent.PendingInstructions[0];
                    subagent.PendingInstructions.RemoveAt(0);
                    this.Launch(subagent, pending.Text);
                    pendingChanged = true;
                }
                else
                {
                    var lastUserText = subagent.Session.GetHistoryInteractionList()
                        .OfType<AIInteractionText>()
                        .LastOrDefault(i => i.Agent == AIAgent.User);
                    if (lastUserText != null
                        && !string.Equals(lastUserText.Content, finished.Instruction, StringComparison.Ordinal))
                    {
                        this.Launch(
                            subagent,
                            "The orchestrator sent follow-up input while you were working — address the latest user message(s) you have not answered yet; if you already covered them, reply briefly confirming.");
                    }
                }
            }

            if (pendingChanged)
            {
                this.NotifyPendingChanged();
            }
        }

        /// <summary>Raises <see cref="PendingChanged"/>; always called outside the gate.</summary>
        private void NotifyPendingChanged()
        {
            try
            {
                this.PendingChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // A misbehaving sink must never poison the run's completion path.
                Logger.For<SubagentHost>().Exception(ex, "[SubagentHost] PendingChanged subscriber threw");
            }
        }

        /// <summary>Builds a fresh child request cloned from the parent minus conductor tools.</summary>
        private AIRequestCall BuildChildRequest(string label)
        {
            var parentFilter = this.parentRequest.Body?.ToolFilter ?? string.Empty;
            var excludes = string.Join(" ", ConductorToolNames.Select(n => "-" + n));
            var childFilter = string.IsNullOrWhiteSpace(parentFilter)
                ? excludes
                : $"{parentFilter} {excludes}";

            var request = new AIRequestCall
            {
                ToolSurface = this.parentRequest.ToolSurface,
            };
            request.Initialize(
                this.parentRequest.Provider ?? string.Empty,
                this.parentRequest.Model,
                $"{SubagentPreamble}\nYou are '{label}'.",
                this.parentRequest.Endpoint,
                this.parentRequest.Capability,
                childFilter);
            return request;
        }

        /// <summary>Resolves a subagent by exact id or unique id prefix (models sometimes truncate ids).</summary>
        private Subagent Resolve(string subagentId)
        {
            subagentId = subagentId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subagentId))
            {
                throw new ArgumentException("A 'subagent_id' is required.", nameof(subagentId));
            }

            var exact = this.subagents.FirstOrDefault(s => string.Equals(s.Id, subagentId, StringComparison.Ordinal));
            if (exact != null)
            {
                return exact;
            }

            var matches = this.subagents.Where(s => s.Id.StartsWith(subagentId, StringComparison.Ordinal)).ToList();
            if (matches.Count == 1)
            {
                return matches[0];
            }

            throw new KeyNotFoundException(matches.Count > 1
                ? $"Subagent id '{subagentId}' is ambiguous ({matches.Count} matches); use a longer prefix."
                : $"No subagent with id '{subagentId}' in this session.");
        }

        /// <summary>Notifies the report sink that a run reached a terminal state.</summary>
        private void Report(Subagent subagent, SubagentRun run)
        {
            try
            {
                this.RunFinished?.Invoke(this, new SubagentRunReport(run, subagent.Id, subagent.Label));
            }
            catch (Exception ex)
            {
                // A misbehaving sink must never poison the run's completion path.
                Logger.For<SubagentHost>().Exception(ex, "[SubagentHost] RunFinished subscriber threw");
            }
        }
    }
}
