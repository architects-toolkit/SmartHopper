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

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Tools
{
    /// <summary>
    /// Typed envelope returned by component tool-call helpers
    /// (e.g. <c>AIStatefulAsyncComponentBase.CallAIToolAsync</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the previous <c>Task&lt;JObject&gt;</c> contract that forced every
    /// caller to redundantly check both <c>result?["success"] == false</c> and
    /// "missing means success" semantics. <see cref="Success"/> is the single
    /// authoritative execution-level flag; <see cref="Result"/> still carries the
    /// tool's own JSON payload, which may itself contain a tool-defined
    /// <c>success</c> field for tool-level outcomes (e.g. <c>script_review</c>).
    /// </para>
    /// <para>
    /// To minimize churn in existing call sites, the envelope exposes an indexer
    /// and an overridden <see cref="ToString"/> that delegate to <see cref="Result"/>,
    /// so legacy patterns like <c>toolResult?["key"]</c> and
    /// <c>toolResult?.ToString()</c> continue to compile and behave as before.
    /// </para>
    /// </remarks>
    public sealed class ToolCallResult
    {
        private static readonly IReadOnlyList<SHRuntimeMessage> EmptyMessages = new List<SHRuntimeMessage>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallResult"/> class.
        /// </summary>
        /// <param name="success">Whether tool execution completed successfully at the
        /// AI-call layer (i.e. the call did not throw and returned a tool result).</param>
        /// <param name="result">The raw tool payload as a <see cref="JObject"/>, or
        /// <c>null</c> if no payload was produced.</param>
        /// <param name="messages">Diagnostic messages produced during execution.
        /// May be <c>null</c>, treated as empty.</param>
        /// <param name="isBatchSentinel">Whether this is a placeholder returned by
        /// the batch path while the request is queued for later submission.</param>
        public ToolCallResult(
            bool success,
            JObject result,
            IReadOnlyList<SHRuntimeMessage> messages = null,
            bool isBatchSentinel = false)
        {
            this.Success = success;
            this.Result = result;
            this.Messages = messages ?? EmptyMessages;
            this.IsBatchSentinel = isBatchSentinel;
        }

        /// <summary>
        /// Gets a value indicating whether the tool call completed successfully at
        /// the AI-call layer. Tools may additionally expose a tool-level
        /// <c>success</c> inside <see cref="Result"/>.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the raw tool payload as returned by the registered tool, or
        /// <c>null</c> when the call failed before producing a payload.
        /// </summary>
        public JObject Result { get; }

        /// <summary>
        /// Gets the diagnostic messages produced during the tool call.
        /// </summary>
        public IReadOnlyList<SHRuntimeMessage> Messages { get; }

        /// <summary>
        /// Gets a value indicating whether this is a batch sentinel placeholder
        /// returned while the request is queued for later submission. When
        /// <c>true</c>, <see cref="Result"/> contains a sentinel token under
        /// <c>"result"</c> rather than real data.
        /// </summary>
        public bool IsBatchSentinel { get; }

        /// <summary>
        /// Convenience indexer that delegates to <see cref="Result"/>. Returns
        /// <c>null</c> when the payload is missing or the key is absent.
        /// </summary>
        /// <param name="key">JSON property name to look up on the payload.</param>
        /// <returns>The <see cref="JToken"/> at <paramref name="key"/>, or <c>null</c>.</returns>
        public JToken this[string key] => this.Result?[key];

        /// <summary>
        /// Creates a successful <see cref="ToolCallResult"/> wrapping
        /// <paramref name="payload"/>.
        /// </summary>
        /// <param name="payload">The tool payload.</param>
        /// <param name="messages">Optional diagnostic messages.</param>
        /// <returns>A new <see cref="ToolCallResult"/> with <see cref="Success"/> set to <c>true</c>.</returns>
        public static ToolCallResult FromSuccess(JObject payload, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            return new ToolCallResult(true, payload, messages);
        }

        /// <summary>
        /// Creates a failure <see cref="ToolCallResult"/> with a single error
        /// message. The <see cref="Result"/> payload is populated with a
        /// best-effort <c>{ "success": false, "messages": [...] }</c> shape so
        /// that legacy callers reading <c>toolResult["success"]</c> still observe
        /// the failure.
        /// </summary>
        /// <param name="errorMessage">Human-readable error description.</param>
        /// <returns>A new failure <see cref="ToolCallResult"/>.</returns>
        public static ToolCallResult FromError(string errorMessage)
        {
            var payload = new JObject
            {
                ["success"] = false,
                ["messages"] = new JArray(new JObject
                {
                    ["severity"] = "Error",
                    ["origin"] = "Return",
                    ["message"] = errorMessage,
                }),
            };
            var msg = new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Return,
                SHMessageCode.ReturnInvalid,
                errorMessage);
            return new ToolCallResult(false, payload, new[] { msg });
        }

        /// <summary>
        /// Creates a batch-sentinel <see cref="ToolCallResult"/>. The payload is
        /// expected to contain the sentinel token (typically under <c>"result"</c>).
        /// </summary>
        /// <param name="sentinelPayload">The sentinel-bearing payload.</param>
        /// <returns>A new sentinel <see cref="ToolCallResult"/>.</returns>
        public static ToolCallResult FromBatchSentinel(JObject sentinelPayload)
        {
            return new ToolCallResult(true, sentinelPayload, isBatchSentinel: true);
        }

        /// <summary>
        /// Creates a <see cref="ToolCallResult"/> by extracting the last
        /// <see cref="AIInteractionToolResult"/> from an <see cref="AIReturn"/> body.
        /// </summary>
        /// <param name="aiReturn">The return object produced by <c>AIToolCall.Exec()</c>.</param>
        /// <param name="agent">The agent type to look for (defaults to <see cref="AIAgent.ToolResult"/>).</param>
        /// <returns>
        /// A successful <see cref="ToolCallResult"/> when the interaction is found;
        /// otherwise a failure result carrying any messages already present on
        /// <paramref name="aiReturn"/>.
        /// </returns>
        public static ToolCallResult FromAIReturn(AIReturn aiReturn, AIAgent agent = AIAgent.ToolResult)
        {
            if (aiReturn?.Body == null)
            {
                var msg = new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Return,
                    SHMessageCode.ReturnInvalid,
                    "Tool execution returned no body.");
                return new ToolCallResult(false, null, new[] { msg });
            }

            var interaction = aiReturn.Body.GetLastInteraction(agent) as AIInteractionToolResult;
            if (interaction == null)
            {
                var msgs = aiReturn.Messages?.Count > 0
                    ? aiReturn.Messages.ToList()
                    : new List<SHRuntimeMessage>
                    {
                        new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Return,
                            SHMessageCode.ReturnInvalid,
                            $"No {agent} interaction found in response."),
                    };
                return new ToolCallResult(false, null, msgs);
            }

            return new ToolCallResult(
                true,
                interaction.Result,
                interaction.Messages ?? EmptyMessages);
        }

        /// <summary>
        /// Returns the JSON representation of <see cref="Result"/>, or an empty
        /// string when the payload is <c>null</c>.
        /// </summary>
        /// <returns>The serialized payload.</returns>
        public override string ToString()
        {
            return this.Result?.ToString() ?? string.Empty;
        }
    }
}
