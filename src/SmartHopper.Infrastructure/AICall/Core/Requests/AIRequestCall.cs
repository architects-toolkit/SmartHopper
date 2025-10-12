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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Execution;
using SmartHopper.Infrastructure.AICall.Policies;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Streaming;


namespace SmartHopper.Infrastructure.AICall.Core.Requests
{
    /// <summary>
    /// Represents a fully-specified AI request that providers can execute, including
    /// provider information, model resolution, capability validation, and request body.
    /// </summary>
    public class AIRequestCall : AIRequestBase
    {
        private AICapability capability = AICapability.Text2Text;

        /// <inheritdoc/>
        public override AICapability Capability
        {
            get => this.GetEffectiveCapabilities(out _);
            set => this.capability = value;
        }

        /// <summary>
        /// Gets or sets the HTTP method to use for the request.
        /// </summary>
        public string HttpMethod { get; set; } = "POST";

        /// <summary>
        /// Gets or sets the authentication method to use for the request.
        /// </summary>
        public string Authentication { get; set; } = "bearer";

        /// <summary>
        /// Additional HTTP headers to include in the request. Keys are case-insensitive.
        /// Useful for provider-specific requirements (e.g., version headers).
        /// Reserved headers are applied internally and ignored here: 'Authorization', 'x-api-key'.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the content type to use for the request.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets the encoded request for the specified provider.
        /// </summary>
        public string EncodedRequestBody
        {
            get
            {
                var (valid, _) = this.IsValid();
                if (valid)
                {
                    return this.ProviderInstance.Encode(this);
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<AIRuntimeMessage>();

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
            }

            if (string.IsNullOrEmpty(this.Provider))
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    AIMessageCode.ProviderMissing,
                    "Provider is required"));
            }

            if (this.ProviderInstance == null)
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    AIMessageCode.UnknownProvider,
                    $"Unknown provider '{this.Provider}'"));
            }

            if (string.IsNullOrEmpty(this.Endpoint))
            {
                messages.Add(new AIRuntimeMessage(
                    AIRuntimeMessageSeverity.Error,
                    AIRuntimeMessageOrigin.Validation,
                    AIMessageCode.BodyInvalid,
                    "Endpoint is required"));
            }

            // Check model and body only in Generation request kind
            if (this.RequestKind == AIRequestKind.Generation)
            {
                // Validate resolved model selection (provider-scoped). Empty means no capable model was found.
                var effectiveCapability = this.GetEffectiveCapabilities(out _);
                var resolvedModel = this.Model; // Triggers provider-scoped selection
                if (string.IsNullOrEmpty(resolvedModel))
                {
                    messages.Add(new AIRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Validation,
                        AIMessageCode.NoCapableModel,
                        $"No capable model found for provider '{this.Provider}' with capability {effectiveCapability.ToString()}"));
                }

                // Additional diagnostics based on user-requested model vs selected model
                // - UnknownModel: requested model not registered for provider
                // - CapabilityMismatch: requested known but not capable; selection replaced it
                var requestedModel = this.RequestedModel;
                if (!string.IsNullOrWhiteSpace(this.Provider) && !string.IsNullOrWhiteSpace(requestedModel))
                {
                    var requestedCaps = ModelManager.Instance.GetCapabilities(this.Provider, requestedModel);
                    if (requestedCaps == null)
                    {
                        messages.Add(new AIRuntimeMessage(
                            AIRuntimeMessageSeverity.Info,
                            AIRuntimeMessageOrigin.Validation,
                            AIMessageCode.UnknownModel,
                            $"Requested model '{requestedModel}' is not registered for provider '{this.Provider}'."));
                    }
                    else if (!requestedCaps.HasCapability(effectiveCapability))
                    {
                        // If a fallback was selected, surface the replacement
                        if (!string.IsNullOrWhiteSpace(resolvedModel) && !string.Equals(resolvedModel, requestedModel, StringComparison.Ordinal))
                        {
                            messages.Add(new AIRuntimeMessage(
                                AIRuntimeMessageSeverity.Info,
                                AIRuntimeMessageOrigin.Validation,
                                AIMessageCode.CapabilityMismatch,
                                $"Requested model '{requestedModel}' does not support {effectiveCapability.ToString()}; selected '{resolvedModel}' instead."));
                        }
                        else
                        {
                            messages.Add(new AIRuntimeMessage(
                                AIRuntimeMessageSeverity.Warning,
                                AIRuntimeMessageOrigin.Validation,
                                AIMessageCode.CapabilityMismatch,
                                $"Requested model '{requestedModel}' does not support {effectiveCapability.ToString()}"));
                        }
                    }
                }

                if (this.Body == null)
                {
                    messages.Add(new AIRuntimeMessage(
                        AIRuntimeMessageSeverity.Error,
                        AIRuntimeMessageOrigin.Validation,
                        AIMessageCode.BodyInvalid,
                        "Body is required"));
                }
                else
                {
                    if (this.Body.InteractionsCount == 0)
                    {
                        messages.Add(new AIRuntimeMessage(
                            AIRuntimeMessageSeverity.Error,
                            AIRuntimeMessageOrigin.Validation,
                            AIMessageCode.BodyInvalid,
                            "At least one interaction is required"));
                    }

                    if (effectiveCapability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
                    {
                        messages.Add(new AIRuntimeMessage(
                            AIRuntimeMessageSeverity.Error,
                            AIRuntimeMessageOrigin.Validation,
                            AIMessageCode.BodyInvalid,
                            "JsonOutput capability requires a non-empty JsonOutputSchema"));
                    }
                }
            }

            var hasErrors = messages.Count(m => m.Severity == AIRuntimeMessageSeverity.Error) > 0;

            return (!hasErrors, messages);
        }

        /// <summary>
        /// Executes a single provider turn without streaming (backward compatibility).
        /// </summary>
        /// <returns>The result of the provider call in <see cref="AIReturn"/> format.</returns>
        public override async Task<AIReturn> Exec()
        {
            return await this.Exec(stream: false).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a single provider turn and returns the result. No tool orchestration here.
        /// Conversation orchestration (multi-turn, tools, streaming) is handled by ConversationSession.
        /// </summary>
        /// <param name="stream">If true, uses streaming mode via provider's streaming adapter.</param>
        /// <returns>The result of the provider call in <see cref="AIReturn"/> format.</returns>
        public async Task<AIReturn> Exec(bool stream = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Debug.WriteLine($"[AIRequest.Exec] Provider='{this.Provider}', Model='{this.Model}', ToolFilter='{this.Body?.ToolFilter ?? "null"}'");

            try
            {
                // Always-on: run request policies before validation/provider call
                await PolicyPipeline.Default.ApplyRequestPoliciesAsync(this).ConfigureAwait(false);

                // Validate early to avoid provider calls when request is invalid
                var (rqOk, rqErrors) = this.IsValid();
                if (!rqOk)
                {
                    stopwatch.Stop();
                    return this.BuildErrorReturn("Request validation failed");
                }

                // Guard against missing provider
                if (this.ProviderInstance == null)
                {
                    stopwatch.Stop();
                    var errorResult = new AIReturn();
                    errorResult.CreateProviderError("Provider is missing", this);
                    return errorResult;
                }

                // Execute the request from the provider (single turn)
                IAIReturn result;
                if (stream)
                {
                    // Use streaming mode
                    result = await this.ExecStreamingInternal().ConfigureAwait(false);
                }
                else
                {
                    // Use non-streaming mode
                    result = await this.ProviderInstance.Call(this).ConfigureAwait(false);
                }

                // Provider returned null result
                if (result == null)
                {
                    stopwatch.Stop();
                    var none = new AIReturn();
                    none.CreateProviderError("Provider returned no response", this);
                    return none;
                }

                // If provider produced no body and no explicit error, standardize it
                if (result.Body == null && string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.CreateProviderError("Provider returned no response", this);
                }

                // Run response policies to decode/normalize/validate the response
                var air = (AIReturn)result;
                try
                {
                    Debug.WriteLine($"[AIRequest.Exec] before policies: interactions={air?.Body?.InteractionsCount ?? 0}, new={string.Join(",", air?.Body?.InteractionsNew ?? new System.Collections.Generic.List<int>())}");
                }
                catch
                {
                    /* logging only */
                }

                await PolicyPipeline.Default.ApplyResponsePoliciesAsync(air).ConfigureAwait(false);
                try
                {
                    Debug.WriteLine($"[AIRequest.Exec] after policies: interactions={air?.Body?.InteractionsCount ?? 0}, new={string.Join(",", air?.Body?.InteractionsNew ?? new System.Collections.Generic.List<int>())}");
                }
                catch
                {
                    // logging only
                }

                return air;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                var errorResult = new AIReturn();
                errorResult.CreateProviderError("Call cancelled or timed out", this);
                return errorResult;
            }
            catch (TimeoutException)
            {
                stopwatch.Stop();
                var errorResult = new AIReturn();
                errorResult.CreateProviderError("Call cancelled or timed out", this);
                return errorResult;
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();

                // Network-related issues (DNS, connectivity). Keep raw but standardize user-facing message.
                var raw = this.ExtractProviderErrorMessage(ex);
                var errorResult = new AIReturn();
                if (ex.InnerException is SocketException)
                {
                    errorResult.CreateNetworkError(raw, this);
                }
                else
                {
                    // Treat other HttpRequestException as network as well
                    errorResult.CreateNetworkError(raw, this);
                }

                return errorResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Preserve raw provider/unknown error and standardize
                var error = this.ExtractProviderErrorMessage(ex);
                var errorResult = new AIReturn();
                errorResult.CreateProviderError(error, this);
                return errorResult;
            }
        }

        /// <summary>
        /// Internal implementation for streaming execution using the provider's streaming adapter.
        /// </summary>
        /// <returns>Aggregated AIReturn from the streaming results.</returns>
        private async Task<AIReturn> ExecStreamingInternal()
        {
            // Get the streaming adapter from the provider
            var executor = new DefaultProviderExecutor();
            var adapter = executor.TryGetStreamingAdapter(this);

            if (adapter == null)
            {
                // Provider doesn't support streaming, fall back to regular execution
                Debug.WriteLine($"[AIRequest.ExecStreamingInternal] No streaming adapter for provider '{this.Provider}', falling back to non-streaming");
                var result = await this.ProviderInstance.Call(this).ConfigureAwait(false);
                return (AIReturn)result;
            }

            // Use streaming adapter to get deltas and aggregate them
            var finalReturn = new AIReturn();
            var streamingOptions = new StreamingOptions
            {
                // Coalesce smaller token chunks into brief batches for smoother UI
                CoalesceTokens = true,
                CoalesceDelayMs = 40,
                PreferredChunkSize = 24,
            };

            try
            {
                await foreach (var delta in adapter.StreamAsync(this, streamingOptions, CancellationToken.None))
                {
                    // Aggregate the streaming deltas into the final return
                    if (delta != null)
                    {
                        // Take the last complete delta as our final result
                        finalReturn = delta;

                        // If this delta contains a complete interaction, we can consider it final
                        if (delta.Body?.GetNewInteractions()?.Any() == true)
                        {
                            var newInteractions = delta.Body.GetNewInteractions();
                            var lastInteraction = newInteractions.LastOrDefault();

                            // If it's a complete text response (not a partial), use this as final
                            if (lastInteraction is AIInteractionText textInteraction &&
                                !string.IsNullOrEmpty(textInteraction.Content))
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIRequest.ExecStreamingInternal] Streaming failed: {ex.Message}");
                finalReturn.CreateProviderError($"Streaming failed: {ex.Message}", this);
            }

            return finalReturn;
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        /// <param name="provider">Provider ID to use (for example, the assembly name of the provider).</param>
        /// <param name="model">Model name to target. If incompatible, the provider's default model will be used.</param>
        /// <param name="systemPrompt">System message inserted as the first interaction of the request.</param>
        /// <param name="endpoint">Provider endpoint or route. If null, an empty string will be used.</param>
        /// <param name="capability">Desired capabilities for this request (input/output and options).</param>
        /// <param name="toolFilter">Optional tool filter expression (e.g., "-*" to disable all tools).</param>
        public void Initialize(string provider, string model, string systemPrompt, string? endpoint, AICapability capability = AICapability.TextOutput, string? toolFilter = null)
        {
            var interactionList = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.System, Content = systemPrompt },
            };

            this.Initialize(provider, model, interactionList, endpoint ?? string.Empty, capability, toolFilter ?? string.Empty);
        }

        /// <summary>
        /// Replace the interactions list from the body.
        /// </summary>
        /// <param name="interactions">New interactions list that replaces the current body interactions.</param>
        public void OverrideInteractions(List<IAIInteraction> interactions)
        {
            // Replace interactions by rebuilding immutable body while preserving filters/schema
            var builder = AIBodyBuilder.Create()
                .WithToolFilter(this.Body?.ToolFilter)
                .WithContextFilter(this.Body?.ContextFilter)
                .WithJsonOutputSchema(this.Body?.JsonOutputSchema);
            if (interactions != null)
            {
                builder.AddRange(interactions);
            }

            this.Body = builder.Build();
        }

        /// <summary>
        /// Computes the effective capabilities for this request, augmenting with additional flags
        /// implied by the body (e.g., JsonOutput when a schema is provided, FunctionCalling when tools are requested).
        /// Returns the effective capabilities and a list of informational notes describing adjustments.
        /// </summary>
        /// <param name="notes">Output list populated with informational messages about inferred capabilities.</param>
        private AICapability GetEffectiveCapabilities(out List<AIRuntimeMessage> notes)
        {
            notes = new List<AIRuntimeMessage>();
            var effective = this.capability;

            // If body requires JSON output but capability lacks it, add it (informational)
            if (this.Body?.RequiresJsonOutput == true && !effective.HasFlag(AICapability.JsonOutput))
            {
                effective |= AICapability.JsonOutput;
                notes.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, "Body requires JSON output but Capability lacks JsonOutput - treating request as JsonOutput"));
            }

            // If tools are requested but capability lacks FunctionCalling, add it (informational)
            if (!string.IsNullOrEmpty(this.Body?.ToolFilter) && this.Body?.ToolFilter != "-*" && !effective.HasFlag(AICapability.FunctionCalling))
            {
                effective |= AICapability.FunctionCalling;
                notes.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, "Tool filter provided but Capability lacks FunctionCalling - treating request as requiring FunctionCalling"));
            }

            return effective;
        }

        /// <summary>
        /// Returns the provider exception message as-is, without sanitization or parsing.
        /// </summary>
        /// <param name="ex">The thrown exception.</param>
        /// <returns>The raw exception message.</returns>
        private string ExtractProviderErrorMessage(Exception ex)
        {
            // Prefer inner exception message if available (providers often wrap exceptions)
            var msg = ex.InnerException?.Message ?? ex.Message;
            return string.IsNullOrEmpty(msg) ? "Unknown error" : msg;
        }
    }
}
