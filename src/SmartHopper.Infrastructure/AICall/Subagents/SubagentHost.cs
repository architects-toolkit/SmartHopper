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
        public Task<SubagentItem> SendAsync(string subagentId, string message, CancellationToken cancellationToken)
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

            lock (this.gate)
            {
                var subagent = this.Resolve(subagentId);
                var latest = subagent.LatestRun;
                if (latest != null && !latest.IsTerminal)
                {
                    throw new InvalidOperationException($"Subagent '{subagent.Label}' is still running; wait for it to finish (or call wait_subagents) before sending new instructions.");
                }

                var run = this.Launch(subagent, message);
                return Task.FromResult(ToItem(subagent, run));
            }
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

        /// <summary>Projects a subagent + run to the model-facing item shape.</summary>
        private static SubagentItem ToItem(Subagent subagent, SubagentRun? run)
        {
            return new SubagentItem
            {
                SubagentId = subagent.Id,
                Label = subagent.Label,
                RunId = run?.Id,
                Status = run == null ? "idle" : StatusToWire(run.Status),
                Result = run?.Result,
                Error = run?.Error,
                LastInstruction = run?.Instruction ?? string.Empty,
                FinishedAt = run?.FinishedAt,
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
            var run = new SubagentRun(Bound(instruction, 200) ?? string.Empty);
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
                    }
                });

            return run;
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
