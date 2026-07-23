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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Batch;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// AI execution logic for AIStatefulAsyncComponentBase.
    /// Handles tool calls, batch interception, and result processing.
    /// </summary>
    public abstract partial class AIStatefulAsyncComponentBase
    {
        #region TIMEOUT CONFIGURATION

        /// <summary>
        /// Centralized timeout configuration for AI requests.
        /// This method configures the timeout on any request object (AIToolCall, AIRequestCall, etc.)
        /// based on custom timeout from Settings input. If no custom timeout is set,
        /// the timeout is left null to be resolved by RequestTimeoutPolicy from settings.
        /// </summary>
        /// <param name="request">The request object to configure (AIToolCall, AIRequestCall, etc.)</param>
        protected void ConfigureRequestTimeout(AIRequestBase request)
        {
            if (request == null)
            {
                return;
            }

            var parameters = this.GetParameters();

            // Honor custom timeout from Settings input if set
            if (parameters.TimeoutSeconds.HasValue && parameters.TimeoutSeconds.Value > 0)
            {
                request.TimeoutSeconds = parameters.TimeoutSeconds.Value;
            }
            else
            {
                // Leave null to trigger settings lookup in RequestTimeoutPolicy
                request.TimeoutSeconds = null;
            }
        }

        #endregion

        #region AI

        /// <summary>
        /// Attempts to queue a request for batch processing if batch mode is active.
        /// Returns a sentinel result if successful, or null if the request should be executed synchronously.
        /// </summary>
        /// <typeparam name="T">The return type: JObject for CallAIToolAsync, AIReturn for CallAIAsync.</typeparam>
        /// <param name="request">The request to queue (AIToolCall or AIRequestCall).</param>
        /// <param name="identifier">Identifier for logging (tool name or endpoint).</param>
        /// <param name="sentinelFactory">Factory function to create the sentinel result.</param>
        /// <returns>Sentinel result if queued successfully, null if should execute synchronously.</returns>
        private T TryQueueBatchRequest<T>(AIRequestCall request, string identifier, Func<string, T> sentinelFactory)
        {
            if (!this.IsBatchRequest())
                return default(T);

            try
            {
                var index = this._batchState.Queue?.Count ?? 0;
                var customId = AIBatchSubmission.GenerateCustomId(identifier, index);

                if (this._batchState.Queue == null) this._batchState.Queue = new List<(string, AIRequestCall)>();
                if (this._batchState.SentinelIds == null) this._batchState.SentinelIds = new HashSet<string>();
                this._batchState.Queue.Add((customId, request));
                this._batchState.SentinelIds.Add(customId);

                Debug.WriteLine($"[AIStatefulAsync] Queued batch item #{index}: customId={customId}, identifier={identifier}");
                return sentinelFactory(customId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIStatefulAsync] Batch queuing failed for '{identifier}': {ex.Message}, falling back to sync");
                return default(T);
            }
        }

        /// <summary>
        /// Stores the AI return snapshot and surfaces any messages from the result.
        /// </summary>
        private void ProcessAIResult(AIReturn result, string origin)
        {
            if (result != null)
            {
                this.AIReturnSnapshot = result;
            }

            if (result?.Messages != null && result.Messages.Count > 0)
            {
                this.SurfaceMessagesFromReturn(result, origin);
            }
        }

        /// <summary>
        /// Executes an AI tool via AIToolManager, auto-injecting provider/model
        /// and storing returned metrics.
        /// </summary>
        /// <param name="toolName">Name of the registered tool.</param>
        /// <param name="parameters">Tool-specific parameters; provider/model will be injected.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Typed <see cref="ToolCallResult"/> envelope carrying execution
        /// success, the raw tool payload and diagnostic messages. The envelope's
        /// indexer and <see cref="ToolCallResult.ToString"/> delegate to the
        /// underlying payload for backward compatibility.</returns>
        protected async Task<ToolCallResult> CallAIToolAsync(string toolName, JObject parameters, System.Threading.CancellationToken cancellationToken = default)
        {
            parameters ??= new JObject();

            // Provider and model
            var providerName = this.GetActualAIProviderName();
            var model = this.GetModel();

            // Debug log parameters
            var requestParams = this.GetParameters();
            Debug.WriteLine($"[CallAIToolAsync] Tool: {toolName}, Provider: {providerName}, Model: {model}, MaxTokens: {requestParams?.MaxTokens}, Temperature: {requestParams?.Temperature}, TimeoutSeconds: {requestParams?.TimeoutSeconds}");

            // Create the tool call interaction with proper structure
            var toolCallInteraction = new AIInteractionToolCall
            {
                Name = toolName,
                Arguments = parameters,
                Agent = AIAgent.Assistant,
            };

            // Create the tool call request with proper body
            var toolCall = new AIToolCall();
            toolCall.Provider = providerName;
            toolCall.Model = model;
            toolCall.Endpoint = toolName;
            toolCall.Parameters = this.GetParameters();
            toolCall.CancellationToken = cancellationToken;
            var immutableBody = AIBodyBuilder.Create()
                .Add(toolCallInteraction)
                .Build();
            toolCall.Body = immutableBody;

            // Apply centralized timeout configuration (handles both batch and regular paths)
            this.ConfigureRequestTimeout(toolCall);

            // Batch interception: if batch mode is active and the tool supports BuildRequest,
            // queue the request instead of executing it and return a sentinel placeholder.
            if (this.IsBatchRequest())
            {
                var tools = AIToolManager.GetTools();
                if (tools.TryGetValue(toolName, out var batchTool) && batchTool.BuildRequest != null)
                {
                    var batchRequest = batchTool.BuildRequest(toolCall);

                    // Preserve AIRequestParameters from the original tool call
                    batchRequest.Parameters = toolCall.Parameters;

                    // Validate the request and surface warnings before queuing
                    var (isValid, validationMessages) = batchRequest.IsValid();
                    if (validationMessages?.Count > 0)
                    {
                        var warnings = validationMessages.Where(m => m.Severity == SHRuntimeMessageSeverity.Warning);
                        if (warnings.Any())
                        {
                            foreach (var msg in warnings)
                            {
                                this.SetPersistentRuntimeMessage(
                                    "batch_val_warning",
                                    GH_RuntimeMessageLevel.Warning,
                                    msg.Message,
                                    false);
                            }

                            Debug.WriteLine($"[AIStatefulAsync] Surfaced {warnings.Count()} validation warnings for batch request");
                        }

                        // Only log errors, do not surface. Call will fail if errors are relevant
                        var errors = validationMessages.Where(m => m.Severity == SHRuntimeMessageSeverity.Error);
                        if (errors.Any())
                        {
                            Debug.WriteLine($"[AIStatefulAsync] Batch request has {errors.Count()} validation errors - proceeding with queuing anyway");
                        }
                    }

                    var sentinel = this.TryQueueBatchRequest(
                        batchRequest,
                        toolName,
                        customId => ToolCallResult.FromBatchSentinel(new JObject { ["result"] = BatchSentinel.Wrap(customId) }));

                    if (sentinel != null)
                        return sentinel;
                }
            }

            // Validation/capability messages will be surfaced from AIReturn after execution
            AIReturn toolResult;
            ToolCallResult result;

            try
            {
                toolResult = await toolCall.Exec(cancellationToken).ConfigureAwait(false);

                // Extract the result from the AIReturn
                var toolResultInteraction = toolResult.Body.Interactions
                    .OfType<AIInteractionToolResult>()
                    .FirstOrDefault();

                if (toolResultInteraction?.Result != null)
                {
                    result = new ToolCallResult(toolResult.Success, toolResultInteraction.Result, toolResult.Messages);
                }
                else
                {
                    var fallbackPayload = new JObject
                    {
                        ["success"] = toolResult.Success,
                        ["messages"] = JArray.FromObject(toolResult.Messages),
                    };
                    result = new ToolCallResult(toolResult.Success, fallbackPayload, toolResult.Messages);
                }
            }
            catch (Exception ex)
            {
                // Execution error
                this.SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    ex.Message,
                    false);
                result = ToolCallResult.FromError(ex.Message);
                toolResult = null;
            }

            // Store snapshot and surface messages
            this.ProcessAIResult(toolResult, "ai");
            return result;
        }

        /// <summary>
        /// Executes a full provider chat completion with optional forced tool calling.
        /// This method is used by output components to execute AI requests with conversation context.
        /// </summary>
        /// <param name="body">The AIBody containing interactions and context.</param>
        /// <param name="forceToolName">Optional tool name to force the provider to call.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The AIReturn result from the provider.</returns>
        protected async Task<AIReturn> CallAIAsync(AIBody body, string forceToolName = null, System.Threading.CancellationToken cancellationToken = default)
        {
            body ??= AIBody.Empty;

            // Provider and model
            var providerName = this.GetActualAIProviderName();
            var model = this.GetModel();

            // Build the request
            var request = new AIRequestCall
            {
                Provider = providerName,
                Model = model,
                Endpoint = this.Name,
                Body = body,
                Parameters = this.GetParameters(),
                ForceToolName = forceToolName,
                Capability = this.RequiredCapability,
            };

            // Apply centralized timeout configuration (handles both batch and regular paths)
            this.ConfigureRequestTimeout(request);

            // Batch interception: if batch mode is active, queue the request instead of executing
            var sentinel = this.TryQueueBatchRequest(
                request,
                this.GetType().Name,
                customId =>
                {
                    // Placeholder return used only to carry the sentinel string back
                    // through the tool-call machinery. No real request/metrics are associated
                    // (the real request is already queued for batch submission).
                    var sentinelReturn = new AIReturn
                    {
                        SkipRequestValidation = true,
                        SkipMetricsValidation = true,
                    };
                    sentinelReturn.SetBody(AIBodyBuilder.Create()
                        .AddText(AIAgent.Assistant, BatchSentinel.Wrap(customId))
                        .Build());
                    return sentinelReturn;
                });

            if (sentinel != null)
                return sentinel;

            // Sync path: execute immediately
            AIReturn result;
            try
            {
                result = await request.Exec(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Execution error
                this.SetPersistentRuntimeMessage(
                    "ai_error",
                    GH_RuntimeMessageLevel.Error,
                    ex.Message,
                    false);
                // Exception occurred before a real AIReturn was produced; attach the request
                // for context and skip metrics validation since no metrics were collected.
                result = new AIReturn { SkipMetricsValidation = true };
                result.Request = request;
                result.SetBody(AIBody.Empty);
                result.AddRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Return,
                    ex.Message);
            }

            // Store snapshot and surface messages
            this.ProcessAIResult(result, "ai");
            return result;
        }

        #endregion
    }
}
