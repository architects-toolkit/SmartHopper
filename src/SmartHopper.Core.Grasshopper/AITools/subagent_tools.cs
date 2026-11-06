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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Subagents;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Conductor tools for the WebChat surface: spawn, instruct, await, and list
    /// background subagents owned by the session's <see cref="ISubagentHost"/>.
    /// Subagents run in the background and report back automatically — a finished
    /// run leaves an evidence line in the chat and wakes the conversation when it
    /// is idle, so the model may end its turn while workers run.
    /// </summary>
    public sealed class subagent_tools : IAIToolProvider
    {
        private const string SpawnName = "spawn_subagent";
        private const string SendName = "send_to_subagent";
        private const string WaitName = "wait_subagents";
        private const string ListName = "list_subagents";

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                SpawnName,
                "Spawn a persistent background subagent that works a delegated brief in its own conversation and reports back automatically. You may end your turn while it runs — the chat resumes when it finishes. Prefer one well-scoped brief over many tiny tasks; canvas-mutating tools are unavailable to subagents.",
                "AgentPlanning",
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""label"": { ""type"": ""string"", ""description"": ""Short human-readable name for the worker (max 40 chars)."" },
                        ""brief"": { ""type"": ""string"", ""description"": ""Self-contained instruction the subagent executes."" }
                    },
                    ""required"": [""brief""]
                }",
                this.ExecuteSpawnAsync,
                mutatesCanvas: false,
                tags: new[] { "subagents", "orchestration", "background" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""subagent_id"": { ""type"": ""string"" }, ""label"": { ""type"": ""string"" }, ""status"": { ""type"": ""string"" } } }",
                surfaces: AIToolSurface.Chat,
                activityLabel: "starting some subagents");

            yield return new AITool(
                SendName,
                "Send a follow-up instruction to an existing subagent. Default 'queue' mode holds it until the subagent's current run finishes and delivers it automatically as the next run; 'on-the-fly' steers the live run immediately when the session can safely accept a user message (otherwise it queues).",
                "AgentPlanning",
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""subagent_id"": { ""type"": ""string"" },
                        ""message"": { ""type"": ""string"", ""description"": ""Follow-up instruction for the subagent."" },
                        ""mode"": { ""type"": ""string"", ""enum"": [""queue"", ""on-the-fly""], ""description"": ""Delivery while the subagent is mid-run: 'queue' (default) holds the instruction until its current run finishes; 'on-the-fly' injects it into the live session immediately."" }
                    },
                    ""required"": [""subagent_id"", ""message""]
                }",
                this.ExecuteSendAsync,
                mutatesCanvas: false,
                tags: new[] { "subagents", "orchestration", "background" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""subagent_id"": { ""type"": ""string"" }, ""status"": { ""type"": ""string"" } } }",
                surfaces: AIToolSurface.Chat);

            yield return new AITool(
                WaitName,
                "Block until the selected subagents finish (or the timeout elapses) and collect their results inside this turn. Optional — finished subagents also report back automatically when the turn ends.",
                "AgentPlanning",
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""subagent_ids"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Ids to await; omit to await every subagent in the session."" },
                        ""timeout_seconds"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 120 }
                    }
                }",
                this.ExecuteWaitAsync,
                mutatesCanvas: false,
                tags: new[] { "subagents", "orchestration", "background" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""subagents"": { ""type"": ""array"" } } }",
                surfaces: AIToolSurface.Chat);

            yield return new AITool(
                ListName,
                "List this session's subagents with their latest run status, bounded result or error, and the instruction that produced it.",
                "AgentPlanning",
                @"{
                    ""type"": ""object"",
                    ""properties"": { }
                }",
                this.ExecuteListAsync,
                mutatesCanvas: false,
                tags: new[] { "subagents", "orchestration", "background" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""subagents"": { ""type"": ""array"" } } }",
                surfaces: AIToolSurface.Chat);
        }

        /// <summary>Resolves the owning session's subagent host or fails the tool call.</summary>
        private static ISubagentHost RequireHost(AIToolCall toolCall)
        {
            var host = toolCall.InvocationContext?.SubagentHost;
            if (host == null)
            {
                throw new InvalidOperationException("Subagents are not available in this session.");
            }

            return host;
        }

        /// <summary>Serializes a subagent item to the bounded model-facing shape.</summary>
        private static JObject ToJson(SubagentItem item)
        {
            var json = new JObject
            {
                ["subagent_id"] = item.SubagentId,
                ["label"] = item.Label,
                ["status"] = item.Status,
                ["run_id"] = item.RunId,
                ["result"] = item.Result,
                ["error"] = item.Error,
                ["last_instruction"] = item.LastInstruction,
                ["finished_at"] = item.FinishedAt?.ToString("o"),
                ["pending_count"] = item.PendingCount,
            };
            if (item.Delivery != null)
            {
                json["delivery"] = item.Delivery;
            }

            return json;
        }

        /// <summary>
        /// Maps the tool's <c>mode</c> argument onto <see cref="SubagentSendMode"/>:
        /// absent/empty means <see cref="SubagentSendMode.Queue"/>; on-the-fly
        /// aliases (<c>immediate</c>, <c>steer</c>, …) normalize to the canonical value.
        /// </summary>
        private static SubagentSendMode ParseSendMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return SubagentSendMode.Queue;
            }

            var normalized = mode.Trim().ToLowerInvariant().Replace("_", "-");
            return normalized switch
            {
                "queue" or "queued" or "defer" => SubagentSendMode.Queue,
                "on-the-fly" or "onthefly" or "immediate" or "steer" or "now" => SubagentSendMode.OnTheFly,
                _ => throw new ArgumentException($"send_to_subagent: unknown mode '{mode}' (use 'queue' or 'on-the-fly')."),
            };
        }

        private async Task<AIReturn> ExecuteSpawnAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var host = RequireHost(toolCall);
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var label = args["label"]?.ToString() ?? string.Empty;
                var brief = args["brief"]?.ToString() ?? string.Empty;

                var item = await host.SpawnAsync(label, brief, toolCall.CancellationToken).ConfigureAwait(false);
                var result = ToJson(item);
                result["note"] = "The subagent runs in the background and reports back automatically; you may end your turn — if you do, tell the user it is still working and the conversation will resume when it reports. Use wait_subagents only when you need the result inside this turn.";
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: SpawnName)
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
                return output;
            }
        }

        private async Task<AIReturn> ExecuteSendAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var host = RequireHost(toolCall);
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var subagentId = args["subagent_id"]?.ToString() ?? string.Empty;
                var message = args["message"]?.ToString() ?? string.Empty;

                var mode = ParseSendMode(args["mode"]?.ToString());
                var item = await host.SendAsync(subagentId, message, mode, toolCall.CancellationToken).ConfigureAwait(false);
                var result = ToJson(item);
                result["note"] = item.Delivery switch
                {
                    "queued" => $"Queued — '{item.Label}' is mid-run; the instruction delivers automatically as its next run when it finishes.",
                    "steered" => "Delivered into the running session — the subagent reads it at its next provider call.",
                    _ => "The instruction started a new run; the subagent reports back when it finishes.",
                };
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: SendName)
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
                return output;
            }
        }

        private async Task<AIReturn> ExecuteWaitAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var host = RequireHost(toolCall);
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();
                var ids = (args["subagent_ids"] as JArray)?.Values<string>()
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .ToList();
                var timeout = args["timeout_seconds"]?.Value<int?>() ?? 30;

                var items = await host.WaitAsync(ids, timeout, toolCall.CancellationToken).ConfigureAwait(false);
                var result = new JObject
                {
                    ["subagents"] = new JArray(items.Select(ToJson)),
                };
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: WaitName)
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
                return output;
            }
        }

        private Task<AIReturn> ExecuteListAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var host = RequireHost(toolCall);
                var result = new JObject
                {
                    ["subagents"] = new JArray(host.List().Select(ToJson)),
                };
                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: ListName)
                    .Build(), toolCall);
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
            }

            return Task.FromResult(output);
        }
    }
}
