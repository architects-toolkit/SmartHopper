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

/*
 * The reflection-based tool-discovery pattern is structurally adapted from Cordyceps
 * (https://github.com/brookstalley/cordyceps, McpServer.cs). SmartHopper does not
 * use [McpServerTool] reflection: tools are already discoverable through
 * AIToolManager, so the adapter projects that catalog into MCP descriptors instead.
 * Copyright (c) 2026 Brooks Talley. Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Bridges SmartHopper's <see cref="AIToolManager"/> catalog to the Model Context
    /// Protocol's <c>tools/list</c> and <c>tools/call</c> surface.
    /// </summary>
    /// <remarks>
    /// All schema-layer concerns (GhJSON marshalling, validation, fix-up, placement)
    /// continue to live inside the tools themselves and the upstream
    /// <c>architects-toolkit/ghjson-dotnet</c> library. This adapter only translates
    /// between the MCP envelope and <see cref="AIToolCall"/> / <see cref="AIReturn"/>.
    /// Mutating calls carry an additional ownership contract: a
    /// <c>requestId</c> argument reserves the operation inside the caller's MCP
    /// session before execution, so a retry caused by a dropped connection joins
    /// or replays the recorded outcome instead of applying twice. An owned
    /// operation that outlives its observer's wait reports
    /// <c>operation_in_progress</c> — never a fabricated terminal failure — and
    /// the real result stays fetchable under the same key.
    /// </remarks>
    public sealed class AIToolMcpAdapter
    {
        private readonly McpServerOptions options;
        private readonly Func<IReadOnlyDictionary<string, AITool>> toolSource;
        private readonly Func<AIToolCall, CancellationToken, Task<AIReturn>> executor;
        private readonly Func<AIToolCall, CancellationToken, Task<AIReturn>> mutationExecutor;
        private readonly Func<JObject?, string?>? documentBindingResolver;
        private readonly TimeSpan? mutationObserverBudget;
        private readonly McpMutationReservations reservations = new McpMutationReservations();

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolMcpAdapter"/> class.
        /// </summary>
        public AIToolMcpAdapter(McpServerOptions options)
            : this(
                options,
                () => AIToolManager.GetTools(),
                (call, ct) => call.Exec(ct),
                (call, _) => AIToolManager.ExecuteTool(call),
                null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolMcpAdapter"/> class with
        /// injectable tool source and executor. Used by tests and embedding scenarios
        /// that want to substitute the catalog or execution pipeline.
        /// </summary>
        public AIToolMcpAdapter(
            McpServerOptions options,
            Func<IReadOnlyDictionary<string, AITool>> toolSource,
            Func<AIToolCall, Task<AIReturn>> executor)
            : this(options, toolSource, (call, _) => executor(call))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolMcpAdapter"/> class with
        /// a cancellation-aware executor.
        /// </summary>
        public AIToolMcpAdapter(
            McpServerOptions options,
            Func<IReadOnlyDictionary<string, AITool>> toolSource,
            Func<AIToolCall, CancellationToken, Task<AIReturn>> executor)
            : this(options, toolSource, executor, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AIToolMcpAdapter"/> class with
        /// a cancellation-aware executor, a separate mutation-commit executor and an
        /// optional document-binding resolver.
        /// </summary>
        /// <param name="options">Server options.</param>
        /// <param name="toolSource">Tool catalog.</param>
        /// <param name="executor">Executor for ordinary (non-mutating) calls — bounds and cancels per Exec.</param>
        /// <param name="mutationExecutor">
        /// Executor for mutating calls. It receives the raw manager task: the adapter
        /// owns the observer wait and publishes the eventual outcome through the
        /// reservation, so an observer timeout can never fabricate a dead operation.
        /// Defaults to <see cref="AIToolManager.ExecuteTool"/>.
        /// </param>
        /// <param name="documentBindingResolver">
        /// Resolves the document identity a mutation targets (documentId argument or
        /// ambient host document) — marshalled to the UI thread by the host. Null
        /// leaves the binding unresolved; recorded bindings still join retries.
        /// </param>
        internal AIToolMcpAdapter(
            McpServerOptions options,
            Func<IReadOnlyDictionary<string, AITool>> toolSource,
            Func<AIToolCall, CancellationToken, Task<AIReturn>> executor,
            Func<AIToolCall, CancellationToken, Task<AIReturn>>? mutationExecutor,
            Func<JObject?, string?>? documentBindingResolver,
            TimeSpan? mutationObserverBudget = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.toolSource = toolSource ?? throw new ArgumentNullException(nameof(toolSource));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.mutationExecutor = mutationExecutor ?? executor;
            this.documentBindingResolver = documentBindingResolver;
            this.mutationObserverBudget = mutationObserverBudget;
        }

        /// <summary>
        /// Builds the list of MCP tool descriptors to expose, applying the configured
        /// allow-list and per-tool mutability filter. Mutating tools advertise the
        /// optional <c>requestId</c> transport field in their input schema.
        /// </summary>
        public IReadOnlyList<McpToolDescriptor> BuildDescriptors()
        {
            var allTools = this.toolSource();
            var descriptors = new List<McpToolDescriptor>();
            foreach (var pair in allTools)
            {
                var tool = pair.Value;
                if (!this.IsExposed(tool.Name))
                {
                    continue;
                }

                JObject inputSchema = ParseSchema(tool.ParametersSchema);
                if (tool.MutatesCanvas)
                {
                    inputSchema = AugmentWithRequestId(inputSchema);
                }

                JObject outputSchema = ParseSchema(tool.OutputSchema);
                descriptors.Add(new McpToolDescriptor(
                    tool.Name,
                    tool.RichDescription,
                    inputSchema,
                    tool.Tags,
                    outputSchema,
                    tool.Annotations));
            }

            descriptors.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return descriptors;
        }

        /// <summary>
        /// Returns whether the named tool is currently exposed (allow-list, per-tool
        /// mutability, and view-control category gating).
        /// </summary>
        public bool IsExposed(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            var tool = this.toolSource().TryGetValue(toolName, out var resolvedTool) ? resolvedTool : null;
            if (tool == null)
            {
                return false;
            }

            if (!tool.Enabled || (tool.Surfaces & SmartHopper.ProviderSdk.Hosting.AIToolSurface.Mcp) == 0)
            {
                return false;
            }

            if (this.options.EnabledTools != null && this.options.EnabledTools.Count > 0)
            {
                return this.options.EnabledTools.Any(t =>
                    string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase));
            }

            if (!this.options.ExposeMutatingTools && tool.MutatesCanvas)
            {
                return false;
            }

            if (!this.options.AllowViewControl
                && string.Equals(tool.Category, "Canvas.ViewControl", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!this.options.AllowExternalTools && tool.Annotations?.OpenWorldHint == true)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Executes a tool and returns the JSON payload to embed in the MCP
        /// <c>tools/call</c> response. Stateless compatibility overload — no session
        /// correlation, so <c>requestId</c> mutations are rejected as
        /// <c>session_required</c>.
        /// </summary>
        /// <param name="toolName">Tool name.</param>
        /// <param name="arguments">JSON object containing tool arguments.</param>
        public Task<McpToolCallResult> ExecuteAsync(string toolName, JObject? arguments)
        {
            return this.ExecuteAsync(toolName, arguments, null, CancellationToken.None);
        }

        /// <summary>
        /// Executes a tool via the configured executor and returns the JSON payload
        /// to embed in the MCP <c>tools/call</c> response.
        /// </summary>
        /// <param name="toolName">Tool name.</param>
        /// <param name="arguments">JSON object containing tool arguments.</param>
        /// <param name="session">The caller's MCP session, or null for stateless calls.</param>
        /// <param name="cancellationToken">Request cancellation (JSON-RPC cancel or server stop).</param>
        internal async Task<McpToolCallResult> ExecuteAsync(
            string toolName,
            JObject? arguments,
            McpSession? session,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return McpToolCallResult.Error("Tool name is required");
            }

            if (!this.IsExposed(toolName))
            {
                return McpToolCallResult.Error($"Tool '{toolName}' is not exposed via MCP");
            }

            var tools = this.toolSource();
            if (!tools.TryGetValue(toolName, out var tool))
            {
                return McpToolCallResult.Error($"Tool '{toolName}' is not registered");
            }

            // 'requestId' is transport metadata for mutating tools: extract it into
            // invocation bookkeeping and remove it from the business-argument clone
            // so schema validation never sees it. The caller's JObject is never
            // mutated.
            var requestId = arguments?.Value<string>("requestId");
            var hasRequestId = tool.MutatesCanvas && !string.IsNullOrWhiteSpace(requestId);
            JObject businessArguments;
            if (hasRequestId)
            {
                if (session == null)
                {
                    return McpToolCallResult.Error(
                        $"'requestId' on mutating tool '{toolName}' requires an initialized MCP session (Mcp-Session-Id header)",
                        new JObject { ["code"] = "session_required" });
                }

                businessArguments = (JObject)(arguments?.DeepClone() ?? new JObject());
                businessArguments.Remove("requestId");
            }
            else
            {
                businessArguments = arguments ?? new JObject();
            }

            McpMutationReservations.Reservation? reservation = null;
            if (hasRequestId)
            {
                var fingerprint = McpMutationReservations.Fingerprint(businessArguments);
                var binding = this.documentBindingResolver?.Invoke(businessArguments);
                var attempt = this.reservations.TryReserve(session!, toolName, requestId!, fingerprint, binding, out reservation);
                switch (attempt)
                {
                    case McpMutationReservations.Attempt.Conflict:
                        return McpToolCallResult.Error(
                            $"requestId '{requestId}' was already used with different arguments for tool '{toolName}'",
                            new JObject { ["code"] = "idempotency_conflict", ["requestId"] = requestId });
                    case McpMutationReservations.Attempt.DocumentChanged:
                        return McpToolCallResult.Error(
                            $"requestId '{requestId}' was reserved against a different document; the mutation can only apply to its original target",
                            new JObject { ["code"] = "document_changed", ["requestId"] = requestId });
                    case McpMutationReservations.Attempt.Joined:
                        return await this.ObserveAsync(reservation!, cancellationToken).ConfigureAwait(false);
                    case McpMutationReservations.Attempt.Owned:
                        break; // execute below, then complete the reservation
                }
            }

            var lifecycle = new ToolExecutionLifecycle();
            var interaction = new AIInteractionToolCall
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = toolName,
                Arguments = businessArguments,
            };

            var toolCall = new AIToolCall
            {
                SkipMetricsValidation = true,
                ToolSurface = SmartHopper.ProviderSdk.Hosting.AIToolSurface.Mcp,
                CancellationToken = cancellationToken,
                InvocationContext = new MutationInvocationContext
                {
                    Source = MutationInvocationSource.Mcp,
                    Surface = SmartHopper.ProviderSdk.Hosting.AIToolSurface.Mcp,
                    BypassMutationsApproval = this.options.BypassMutationsApproval,
                    ToolCallId = interaction.Id,
                    ToolName = toolName,
                    Lifecycle = lifecycle,
                },
            };
            toolCall.FromToolCallInteraction(interaction);

            if (tool.MutatesCanvas)
            {
                return await this.ExecuteMutationAsync(
                    toolName,
                    tool,
                    toolCall,
                    session,
                    requestId,
                    reservation,
                    lifecycle,
                    cancellationToken).ConfigureAwait(false);
            }

            return await this.ExecutePlainAsync(toolName, tool, toolCall, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Honest intermediate state: owned work is still running.</summary>
        private static McpToolCallResult InProgressResult(
            McpMutationReservations.Reservation? reservation,
            ToolExecutionLifecycle? lifecycle)
        {
            var payload = new JObject
            {
                ["status"] = "operation_in_progress",
                ["operationId"] = reservation?.OperationId ?? lifecycle?.OperationId ?? string.Empty,
                ["state"] = (lifecycle?.State ?? ToolExecutionState.Queued).ToString(),
                ["applied"] = lifecycle?.CommitClaimed == true ? "unknown" : "false",
            };
            return McpToolCallResult.Ok(payload);
        }

        /// <summary>Deep clone for replayed results — mutating the replay must not touch the stored result.</summary>
        private static McpToolCallResult CloneResult(McpToolCallResult result)
        {
            var payload = result.Payload?.DeepClone();
            return result.IsError
                ? McpToolCallResult.Error(
                    result.ErrorMessage ?? string.Empty,
                    payload as JObject,
                    result.ExtraMetrics,
                    result.Diagnostics,
                    result.Images)
                : McpToolCallResult.Ok(
                    payload ?? new JObject(),
                    result.Images,
                    result.ExtraMetrics,
                    result.Diagnostics);
        }

        /// <summary>
        /// Adds the optional <c>requestId</c> transport field to a mutating tool's
        /// input schema — the replay contract is part of the advertised surface so
        /// a legitimate replay key never reads as an unknown argument.
        /// </summary>
        private static JObject AugmentWithRequestId(JObject inputSchema)
        {
            var schema = (JObject)inputSchema.DeepClone();
            var properties = schema["properties"] as JObject ?? new JObject();
            if (properties["requestId"] == null)
            {
                properties["requestId"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional idempotency key. Retrying a call with the same requestId inside one MCP session joins or replays the original operation instead of applying the mutation again. Requires the Mcp-Session-Id header.",
                };
            }

            schema["properties"] = properties;
            return schema;
        }

        private static JObject ParseSchema(string parametersSchema)
        {
            if (string.IsNullOrWhiteSpace(parametersSchema))
            {
                return new JObject { ["type"] = "object" };
            }

            try
            {
                var parsed = JToken.Parse(parametersSchema);
                if (parsed is JObject obj)
                {
                    return obj;
                }
            }
            catch (JsonReaderException)
            {
                // Fall through to a defensive default below; surfacing this loudly
                // is the tool author's responsibility.
            }

            return new JObject { ["type"] = "object" };
        }

        /// <summary>The observer wait for owned and joined operations.</summary>
        private TimeSpan ObserverBudget(AIToolCall? toolCall)
        {
            if (this.mutationObserverBudget.HasValue)
            {
                return this.mutationObserverBudget.Value;
            }

            var timeoutSec = toolCall?.TimeoutSeconds;
            return TimeSpan.FromSeconds(
                timeoutSec.HasValue && timeoutSec.Value > 0 ? timeoutSec.Value : TimeoutDefaults.DefaultTimeoutSeconds);
        }

        /// <summary>
        /// Ordinary (non-mutating) execution: bounded wait and cooperative
        /// cancellation through the regular executor.
        /// </summary>
        private async Task<McpToolCallResult> ExecutePlainAsync(
            string toolName,
            AITool tool,
            AIToolCall toolCall,
            CancellationToken cancellationToken)
        {
            AIReturn result;
            try
            {
                result = await this.executor(toolCall, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cooperative cancellation is not a tool failure: the request ends
                // without a fabricated error result.
                throw;
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(AIToolMcpAdapter).FullName, ex, $"[AIToolMcpAdapter] Tool '{toolName}' threw an exception: {ex.Message}");
                return McpToolCallResult.Error($"Tool '{toolName}' threw an exception: {ex.Message}");
            }

            return this.BuildResult(toolName, result, tool);
        }

        /// <summary>
        /// Mutation execution: the manager task is owned by the adapter, not the
        /// observer. The observer's wait is bounded by the per-request timeout;
        /// when the operation outlives it the caller gets
        /// <c>operation_in_progress</c> and the eventual real outcome still
        /// completes the reservation — it is never reported as dead while owned.
        /// </summary>
        private async Task<McpToolCallResult> ExecuteMutationAsync(
            string toolName,
            AITool tool,
            AIToolCall toolCall,
            McpSession? session,
            string? requestId,
            McpMutationReservations.Reservation? reservation,
            ToolExecutionLifecycle lifecycle,
            CancellationToken cancellationToken)
        {
            Task<AIReturn> managerTask;
            try
            {
                managerTask = this.mutationExecutor(toolCall, cancellationToken);
            }
            catch (Exception ex)
            {
                // The executor itself refused — no operation started.
                lifecycle.TryReject(ToolExecutionState.Invalid);
                var rejected = McpToolCallResult.Error($"Tool '{toolName}' failed to start: {ex.Message}");
                this.CompleteReservation(session, toolName, requestId, reservation, rejected);
                return rejected;
            }

            var observerBudget = ObserverBudget(toolCall);
            var completed = await Task.WhenAny(managerTask, Task.Delay(observerBudget, cancellationToken)).ConfigureAwait(false);
            if (completed == managerTask)
            {
                var outcome = await this.FinalizeMutationAsync(toolName, tool, managerTask, lifecycle).ConfigureAwait(false);
                this.CompleteReservation(session, toolName, requestId, reservation, outcome);
                return outcome;
            }

            // Still owned after the observer's wait: publish the eventual outcome
            // through the reservation and report the honest intermediate state.
            _ = this.FinalizeAndCompleteAsync(toolName, tool, managerTask, lifecycle, session, requestId, reservation);
            return InProgressResult(reservation, lifecycle);
        }

        /// <summary>
        /// Awaits a joined reservation with the caller's own bounded wait. An
        /// operation still owned when the observer's deadline passes reports
        /// <c>operation_in_progress</c> — the reservation's real terminal result
        /// stays fetchable under the same key.
        /// </summary>
        private async Task<McpToolCallResult> ObserveAsync(
            McpMutationReservations.Reservation reservation,
            CancellationToken cancellationToken)
        {
            var completed = await Task.WhenAny(
                reservation.Completion,
                Task.Delay(ObserverBudget(null), cancellationToken)).ConfigureAwait(false);
            if (completed != reservation.Completion)
            {
                return InProgressResult(reservation, null);
            }

            return CloneResult(await reservation.Completion.ConfigureAwait(false));
        }

        /// <summary>Finalizes a finished mutation into a call result.</summary>
        private async Task<McpToolCallResult> FinalizeMutationAsync(
            string toolName,
            AITool tool,
            Task<AIReturn> managerTask,
            ToolExecutionLifecycle lifecycle)
        {
            try
            {
                var result = await managerTask.ConfigureAwait(false);
                var callResult = this.BuildResult(toolName, result, tool);

                // Only an operation that actually claimed the commit gets a terminal
                // lifecycle write here — pre-commit outcomes are recorded by the
                // layers that produced them, and the reservation result is
                // authoritative for observers regardless.
                if (lifecycle.State == ToolExecutionState.Applying)
                {
                    lifecycle.TryComplete(callResult.IsError ? ToolExecutionState.FailedPartial : ToolExecutionState.Applied);
                }

                return callResult;
            }
            catch (OperationCanceledException)
            {
                // Cancelled before the commit claim ends cleanly; cancelled during
                // apply is an honest partial.
                if (!lifecycle.TryCancelBeforeStart() && lifecycle.State == ToolExecutionState.Applying)
                {
                    lifecycle.TryComplete(ToolExecutionState.FailedPartial);
                }

                return McpToolCallResult.Error($"Tool '{toolName}' was cancelled");
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(AIToolMcpAdapter).FullName, ex, $"[AIToolMcpAdapter] Tool '{toolName}' threw an exception: {ex.Message}");
                lifecycle.TryComplete(ToolExecutionState.FailedPartial);
                return McpToolCallResult.Error($"Tool '{toolName}' threw an exception: {ex.Message}");
            }
        }

        /// <summary>Background publication of a detached mutation's real outcome.</summary>
        private async Task FinalizeAndCompleteAsync(
            string toolName,
            AITool tool,
            Task<AIReturn> managerTask,
            ToolExecutionLifecycle lifecycle,
            McpSession? session,
            string? requestId,
            McpMutationReservations.Reservation? reservation)
        {
            var outcome = await this.FinalizeMutationAsync(toolName, tool, managerTask, lifecycle).ConfigureAwait(false);
            this.CompleteReservation(session, toolName, requestId, reservation, outcome);
        }

        private void CompleteReservation(
            McpSession? session,
            string toolName,
            string? requestId,
            McpMutationReservations.Reservation? reservation,
            McpToolCallResult outcome)
        {
            if (session == null || requestId == null || reservation == null)
            {
                return;
            }

            this.reservations.Complete(session, reservation, outcome);
        }

        /// <summary>
        /// Converts an <see cref="AIReturn"/> into an <see cref="McpToolCallResult"/>. On
        /// argument-validation failures the error payload carries the called tool's input
        /// schema under <c>expectedSchema</c> so MCP clients can retry without refetching
        /// <c>tools/list</c>; the tool catalog is never echoed back inside an error.
        /// </summary>
        private McpToolCallResult BuildResult(string toolName, AIReturn? result, AITool tool)
        {
            if (result == null)
            {
                return McpToolCallResult.Error($"Tool '{toolName}' returned no result");
            }

            // Forward every non-Debug runtime message once, regardless of outcome.
            var diagnostics = McpDiagnostic.Project(result.Messages);

            var lastToolResult = result.Body?.Interactions?
                .OfType<AIInteractionToolResult>()
                .LastOrDefault();
            var payload = lastToolResult?.Result;

            // Failure is derived from execution errors only — Error-severity messages
            // with any origin except request validation. Request.Validation diagnostics
            // describe the call's validity, not its outcome: a real invalid call never
            // reaches a tool payload (ExecuteTool fails early via CreateToolError, which
            // additionally attaches a Tool-origin error), while request-validation noise
            // attached to a successfully executed return must not flip it to an error.
            // Conversely a partial payload can never mask a real execution error, and a
            // legitimate empty success stays a success.
            var errorMessages = result.Messages?
                .Where(m => m != null
                    && m.Severity == SHRuntimeMessageSeverity.Error
                    && m.Origin != SHRuntimeMessageOrigin.Validation)
                .ToList() ?? new List<SHRuntimeMessage>();
            if (errorMessages.Count > 0)
            {
                var firstError = errorMessages[0];
                var isValidationError = result.Messages?.Any(m =>
                    m?.Severity == SHRuntimeMessageSeverity.Error
                    && m.Origin == SHRuntimeMessageOrigin.Validation) == true;
                var details = isValidationError
                    ? new JObject { ["expectedSchema"] = ParseSchema(tool.ParametersSchema) }
                    : new JObject();
                if (payload != null)
                {
                    details["result"] = payload.DeepClone();
                }

                return McpToolCallResult.Error(
                    firstError?.Message ?? $"Tool '{toolName}' failed",
                    details,
                    result.ExtraMetrics,
                    diagnostics,
                    lastToolResult?.Images);
            }

            // Prefer the tool result interaction when present: a successful tool
            // run always appends an AIInteractionToolResult to the body.
            if (payload != null)
            {
                return McpToolCallResult.Ok(payload, lastToolResult!.Images, result.ExtraMetrics, diagnostics);
            }

            return McpToolCallResult.Ok(new JObject(), null, result.ExtraMetrics, diagnostics);
        }
    }
}
