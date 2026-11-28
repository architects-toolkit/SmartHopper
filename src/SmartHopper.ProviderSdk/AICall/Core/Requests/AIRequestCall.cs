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
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AICall.Validation;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;
using SmartHopper.ProviderSdk.Streaming;
namespace SmartHopper.ProviderSdk.AICall.Core.Requests
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
        /// Gets or sets the credential the provider resolved for this request during
        /// <c>PreCallAsync</c>. When set, it overrides the provider's default credential
        /// lookup for this request (for example a signed-in OAuth token instead of the
        /// configured API key).
        /// </summary>
        public string? ResolvedCredential { get; set; }

        /// <summary>
        /// Gets or sets an opaque tag identifying which credential slot
        /// <see cref="ResolvedCredential"/> was selected from. Providers can use it to
        /// pick a different credential when retrying after an authentication failure.
        /// </summary>
        public string? ResolvedCredentialTag { get; set; }
        /// Gets the canonical request this execution copy descends from. Set by
        /// <see cref="CreateExecutionCopy"/> to the root request — copies of copies
        /// keep pointing at the original caller-owned instance. Null on requests
        /// that are not execution copies.
        /// <remarks>
        /// Pipeline stages use <c>Source</c> to attach per-logical-request state
        /// (for example SmartHopper Cloud's idempotency keys) to the canonical
        /// instance instead of a transient copy.
        /// </remarks>
        public AIRequestCall? Source { get; internal set; }

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
        public override (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<SHRuntimeMessage>();

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
            }

            if (string.IsNullOrEmpty(this.Provider))
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.ProviderMissing,
                    "Provider is required"));
            }
            else
            {
                // Evaluate provider trust/integrity through the centralized policy.
                var trustResult = ProviderTrustPolicy.Evaluate(this);
                messages.AddRange(trustResult.Messages);
                Logger.For<AIRequestCall>().Trace($"[AIRequestCall] Provider '{this.Provider}' trust verdict: {trustResult.Verdict}");

                if (trustResult.Verdict == ProviderTrustVerdict.Block)
                {
                    return (false, messages);
                }
            }

            if (this.ProviderInstance == null)
            {
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.UnknownProvider,
                    $"Unknown provider '{this.Provider}'"));
            }

            // Check model and body only in Generation request kind
            if (this.RequestKind == AIRequestKind.Generation)
            {
                // Validate resolved model selection (provider-scoped). Empty means no capable model was found.
                var effectiveCapability = this.GetEffectiveCapabilities(out _);
                var resolvedModel = this.Model; // Triggers provider-scoped selection
                if (string.IsNullOrEmpty(resolvedModel))
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Validation,
                        SHMessageCode.NoCapableModel,
                        $"No capable model found for provider '{this.Provider}' with capability {effectiveCapability.ToString()}"));
                }

                // Additional diagnostics based on user-requested model vs selected model
                // - UnknownModel: requested model not registered for provider
                // - CapabilityMismatch: requested known but not capable; selection replaced it
                var requestedModel = this.RequestedModel;
                if (!string.IsNullOrWhiteSpace(this.Provider) && !string.IsNullOrWhiteSpace(requestedModel))
                {
                    var requestedCaps = AIModelCapabilityRegistry.Instance.GetCapabilities(this.Provider, requestedModel);
                    if (requestedCaps == null)
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Info,
                            SHRuntimeMessageOrigin.Validation,
                            SHMessageCode.UnknownModel,
                            $"Requested model '{requestedModel}' is not registered for '{this.Provider}'. Proceeding with the model, but capability validation cannot be performed."));
                    }
                    else if (!requestedCaps.HasCapability(effectiveCapability))
                    {
                        // If a fallback was selected, surface the replacement
                        if (!string.IsNullOrWhiteSpace(resolvedModel) && !string.Equals(resolvedModel, requestedModel, StringComparison.Ordinal))
                        {
                            messages.Add(new SHRuntimeMessage(
                                SHRuntimeMessageSeverity.Info,
                                SHRuntimeMessageOrigin.Validation,
                                SHMessageCode.CapabilityMismatch,
                                $"Requested model '{requestedModel}' does not support {effectiveCapability.ToString()}; selected '{resolvedModel}' instead."));
                        }
                        else
                        {
                            messages.Add(new SHRuntimeMessage(
                                SHRuntimeMessageSeverity.Warning,
                                SHRuntimeMessageOrigin.Validation,
                                SHMessageCode.CapabilityMismatch,
                                $"Requested model '{requestedModel}' does not support {effectiveCapability.ToString()}"));
                        }
                    }
                }

                if (this.Body == null)
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Validation,
                        SHMessageCode.BodyInvalid,
                        "Body is required"));
                }
                else
                {
                    if (this.Body.InteractionsCount == 0)
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Validation,
                            SHMessageCode.BodyInvalid,
                            "At least one interaction is required"));
                    }
                    else if (!this.Body.HasConversationContent())
                    {
                        // Every provider routes system/context/summary interactions to its
                        // instruction channel (system messages, system_instruction, the
                        // top-level "system" field). A body made only of them would submit a
                        // generation request with no conversational input — Gemini and
                        // Anthropic answer HTTP 400 outright, and the remaining providers
                        // generate a meaningless continuation from instructions alone.
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Validation,
                            SHMessageCode.BodyInvalid,
                            "Request body has no conversation content — at least one user, assistant, or tool interaction is required (system/context instructions alone cannot be answered)."));
                    }

                    if (effectiveCapability.HasFlag(AICapability.JsonOutput) && string.IsNullOrEmpty(this.Body.JsonOutputSchema))
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Validation,
                            SHMessageCode.BodyInvalid,
                            "JsonOutput capability requires a non-empty JsonOutputSchema"));
                    }
                }
            }

            // Validate ForceToolCall requirements
            if (this.ForceToolCall)
            {
                var effectiveCapability = this.GetEffectiveCapabilities(out _);
                if (!effectiveCapability.HasFlag(AICapability.FunctionCalling))
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Validation,
                        SHMessageCode.BodyInvalid,
                        "ForceToolCall requires FunctionCalling capability"));
                }
            }

            var hasErrors = messages.Any(m => m.Severity == SHRuntimeMessageSeverity.Error);

            return (!hasErrors, messages);
        }

        /// <summary>
        /// Creates a detached copy of this request safe for outbound submission: mutable
        /// fields (headers, request-level messages, preparation cache reference) are
        /// detached so preparation can replace the body without touching canonical
        /// session evidence, while the immutable <see cref="AIBody"/> and
        /// <see cref="AIRequestParameters"/> instances are retained by reference until
        /// the preparation step assigns a transformed body.
        /// </summary>
        /// <returns>A new <see cref="AIRequestCall"/> carrying this request's full configuration.</returns>
        public virtual AIRequestCall CreateExecutionCopy()
        {
            var copy = new AIRequestCall
            {
                Provider = this.Provider,
                Origin = this.Origin,
                Endpoint = this.Endpoint,
                Source = this.Source ?? this,
                RequestKind = this.RequestKind,
                ToolSurface = this.ToolSurface,
                WantsStreaming = this.WantsStreaming,
                HttpMethod = this.HttpMethod,
                Authentication = this.Authentication,
                ContentType = this.ContentType,
                Headers = new Dictionary<string, string>(this.Headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
                Body = this.Body,
                Parameters = this.Parameters,
                TimeoutSeconds = this.TimeoutSeconds,
                SkipMetricsValidation = this.SkipMetricsValidation,
                ForceToolCall = this.ForceToolCall,
                ForceToolName = this.ForceToolName,
                PreparationCache = this.PreparationCache,
            };

            copy.Capability = this.capability;
            copy.Model = this.RequestedModel;
            copy.Messages = this.StoredMessages != null
                ? new List<SHRuntimeMessage>(this.StoredMessages)
                : new List<SHRuntimeMessage>();

            return copy;
        }

        /// <summary>
        /// Executes a single provider turn without streaming (backward compatibility).
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The result of the provider call in <see cref="AIReturn"/> format.</returns>
        public override async Task<AIReturn> Exec(System.Threading.CancellationToken cancellationToken = default)
        {
            return await this.Exec(stream: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a single provider turn and returns the result. No tool orchestration here.
        /// Conversation orchestration (multi-turn, tools, streaming) is handled by ConversationSession.
        /// </summary>
        /// <param name="stream">If true, uses streaming mode via provider's streaming adapter.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The result of the provider call in <see cref="AIReturn"/> format.</returns>
        public virtual async Task<AIReturn> Exec(bool stream = false, System.Threading.CancellationToken cancellationToken = default)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Logger.For<AIRequestCall>().Trace($"[AIRequest.Exec] Provider='{this.Provider}', Model='{this.Model}', ToolFilter='{this.Body?.ToolFilter ?? "null"}'");

            AIRequestPreparation preparation = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Always-on: run request policies before validation/provider call
                await ProviderSdkHost.PolicyPipeline.ApplyRequestPoliciesAsync(this).ConfigureAwait(false);

                // Enforce provider trust/integrity policy before the provider call.
                // This is a security boundary: the policy decides block/warn/allow based on the
                // configured integrity mode, independent of the structural validation below.
                var trustResult = ProviderTrustPolicy.Evaluate(this);
                if (trustResult.Verdict == ProviderTrustVerdict.Block)
                {
                    stopwatch.Stop();

                    var errorResult = new AIReturn();
                    errorResult.Request = this;
                    errorResult.Status = AICallStatus.Finished;
                    errorResult.SetBody(AIBodyBuilder.Create().AddError("Provider trust check failed", null).Build());

                    foreach (var message in trustResult.Messages)
                    {
                        errorResult.AddRuntimeMessage(message);
                    }

                    return errorResult;
                }

                // Model-bound preparation: adapt outbound media to the resolved model's
                // confirmed capabilities. Produces a detached execution copy; the canonical
                // request/body keep the original session evidence. Blocking errors abort
                // the provider call — failed or missing conversions are never submitted.
                preparation = await ProviderSdkHost.PolicyPipeline.PrepareExecutionAsync(this, cancellationToken).ConfigureAwait(false);
                var outbound = preparation?.Request;

                // Fail closed: a null preparation or null prepared request must never
                // silently submit an unprepared execution copy — and charged helper
                // metrics recorded so far are still surfaced.
                if (outbound == null)
                {
                    stopwatch.Stop();
                    var failed = this.BuildErrorReturn("Request preparation failed");
                    if (preparation?.ExtraMetrics != null)
                    {
                        failed.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                    }

                    return failed;
                }

                // Strict final outbound gate (modality + structural + trust) runs here
                // independently of whether the active host already ran any of it.
                preparation.Messages.AddRange(AIRequestPreparation.ValidateOutboundRequest(outbound));

                if (preparation.HasBlockingErrors)
                {
                    stopwatch.Stop();
                    var blocked = this.BuildErrorReturn("Request preparation failed");
                    if (preparation.ExtraMetrics.Count > 0)
                    {
                        blocked.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                    }

                    foreach (var message in preparation.Messages)
                    {
                        if (message != null)
                        {
                            blocked.AddRuntimeMessage(message);
                        }
                    }

                    return blocked;
                }

                // Validate the outbound view only — source media that adaptation resolved
                // must not fail validation here, and leftover unsupported media does.
                var (rqOk, rqErrors) = outbound.IsValid();
                if (!rqOk)
                {
                    stopwatch.Stop();
                    var invalid = this.BuildErrorReturn("Request validation failed");
                    if (preparation.ExtraMetrics.Count > 0)
                    {
                        invalid.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                    }

                    return invalid;
                }

                // Guard against missing provider
                if (outbound.ProviderInstance == null)
                {
                    stopwatch.Stop();
                    var errorResult = new AIReturn();
                    errorResult.CreateProviderError("Provider is missing", this);
                    if (preparation.ExtraMetrics.Count > 0)
                    {
                        errorResult.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                    }

                    return errorResult;
                }

                // Execute the request from the provider (single turn)
                IAIReturn result;
                if (stream)
                {
                    // Use streaming mode
                    result = await outbound.ExecStreamingInternal(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Use non-streaming mode
                    result = await outbound.ProviderInstance.Call(outbound, cancellationToken).ConfigureAwait(false);
                }

                // Provider returned null result — paid helper work must still be metered.
                if (result == null)
                {
                    stopwatch.Stop();
                    var none = new AIReturn();
                    none.CreateProviderError("Provider returned no response", this);
                    if (preparation?.ExtraMetrics != null && preparation.ExtraMetrics.Count > 0)
                    {
                        none.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                    }

                    if (preparation?.Messages != null)
                    {
                        foreach (var message in preparation.Messages)
                        {
                            if (message != null)
                            {
                                none.AddRuntimeMessage(message);
                            }
                        }
                    }

                    return none;
                }

                // If provider produced no body and no error messages, standardize it
                if (result.Body == null && !result.Messages.Any(m => m.Severity == SHRuntimeMessageSeverity.Error))
                {
                    result.CreateProviderError("Provider returned no response", this);
                }

                // Carry preparation output forward: helper-call metrics are metered once,
                // separately from the primary call, and non-blocking diagnostics surface.
                var air = (AIReturn)result;
                if (preparation != null)
                {
                    if (preparation.ExtraMetrics != null && preparation.ExtraMetrics.Count > 0)
                    {
                        air.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                    }

                    if (preparation.Messages != null)
                    {
                        foreach (var message in preparation.Messages)
                        {
                            if (message != null)
                            {
                                air.AddRuntimeMessage(message);
                            }
                        }
                    }
                }

                // Run response policies to decode/normalize/validate the response
                try
                {
                    Logger.For<AIRequestCall>().Trace($"[AIRequest.Exec] before policies: interactions={air?.Body?.InteractionsCount ?? 0}, new={string.Join(",", air?.Body?.InteractionsNew ?? new System.Collections.Generic.List<int>())}");
                }
                catch
                {
                    /* logging only */
                }

                await ProviderSdkHost.PolicyPipeline.ApplyResponsePoliciesAsync(air).ConfigureAwait(false);
                try
                {
                    Logger.For<AIRequestCall>().Trace($"[AIRequest.Exec] after policies: interactions={air?.Body?.InteractionsCount ?? 0}, new={string.Join(",", air?.Body?.InteractionsNew ?? new System.Collections.Generic.List<int>())}");
                }
                catch
                {
                    // logging only
                }

                stopwatch.Stop();
                return air;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                var errorResult = new AIReturn();
                errorResult.CreateProviderError("Call cancelled or timed out", this);
                if (preparation?.ExtraMetrics != null)
                {
                    errorResult.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                }

                return errorResult;
            }
            catch (TimeoutException ex)
            {
                stopwatch.Stop();
                Logger.For<AIRequestCall>().Exception(ex, "[AIRequest.Exec] Call timed out");
                var errorResult = new AIReturn();
                errorResult.CreateProviderError("Call cancelled or timed out", this);
                if (preparation?.ExtraMetrics != null)
                {
                    errorResult.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                }

                return errorResult;
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                Logger.For<AIRequestCall>().Exception(ex, "[AIRequest.Exec] HTTP request failed");

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

                if (preparation?.ExtraMetrics != null)
                {
                    errorResult.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                }

                return errorResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.For<AIRequestCall>().Exception(ex, "[AIRequest.Exec] Call failed");

                // Preserve raw provider/unknown error and standardize
                var error = this.ExtractProviderErrorMessage(ex);
                var errorResult = new AIReturn();
                errorResult.CreateProviderError(error, this);
                if (preparation?.ExtraMetrics != null)
                {
                    errorResult.ExtraMetrics.AddRange(preparation.ExtraMetrics);
                }

                return errorResult;
            }
        }

        /// <summary>
        /// Internal implementation for streaming execution using the provider's streaming adapter.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>Aggregated AIReturn from the streaming results.</returns>
        private async Task<AIReturn> ExecStreamingInternal(System.Threading.CancellationToken cancellationToken = default)
        {
            // Declare streaming intent so PreCall (invoked by streaming adapters) can pick
            // streaming endpoints (e.g. SSE) instead of the regular generation endpoint.
            this.WantsStreaming = true;

            // Get the streaming adapter directly from the provider instance.
            IStreamingAdapter adapter = null;
            try
            {
                adapter = this.ProviderInstance?.GetStreamingAdapter();
            }
            catch (Exception ex)
            {
                Logger.For<AIRequestCall>().Exception(ex, $"[AIRequestCall.ExecStreamingInternal] Error getting streaming adapter: {ex.Message}");
            }

            if (adapter == null)
            {
                // Provider doesn't support streaming, fall back to regular execution.
                // Reset the streaming intent so provider PreCall does not pick an SSE endpoint
                // that the non-streaming CallApi pipeline cannot parse.
                this.WantsStreaming = false;
                Logger.For<AIRequestCall>().Warning($"[AIRequest.ExecStreamingInternal] No streaming adapter for provider '{this.Provider}', falling back to non-streaming");
                var result = await this.ProviderInstance.Call(this, cancellationToken).ConfigureAwait(false);
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
                await foreach (var delta in adapter.StreamAsync(this, streamingOptions, cancellationToken).ConfigureAwait(false))
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
            catch (OperationCanceledException)
            {
                Logger.For<AIRequestCall>().Warning("[AIRequest.ExecStreamingInternal] Streaming cancelled");
                finalReturn.CreateProviderError("Streaming cancelled", this);
            }
            catch (Exception ex)
            {
                Logger.For<AIRequestCall>().Exception(ex, $"[AIRequest.ExecStreamingInternal] Streaming failed: {ex.Message}");
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
        /// <param name="origin">Semantic provenance label for this request (tool name, component tag); stored on <see cref="AIRequestBase.Origin"/>.</param>
        /// <param name="capability">Desired capabilities for this request (input/output and options).</param>
        /// <param name="toolFilter">Optional tool filter expression (e.g., "-*" to disable all tools).</param>
        public void Initialize(string provider, string model, string systemPrompt, string? origin, AICapability capability = AICapability.TextOutput, string? toolFilter = null)
        {
            var interactionList = new List<IAIInteraction>
            {
                new AIInteractionText { Agent = AIAgent.System, Content = systemPrompt },
            };

            this.Initialize(provider, model, interactionList, origin, capability, toolFilter ?? string.Empty);
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
        private AICapability GetEffectiveCapabilities(out List<SHRuntimeMessage> notes)
        {
            notes = new List<SHRuntimeMessage>();
            var effective = this.capability;

            // If body requires JSON output but capability lacks it, add it (informational)
            if (this.Body?.RequiresJsonOutput == true && !effective.HasFlag(AICapability.JsonOutput))
            {
                effective |= AICapability.JsonOutput;
                notes.Add(new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Validation, SHMessageCode.BodyInvalid, "Body requires JSON output but Capability lacks JsonOutput - treating request as JsonOutput"));
            }

            // If tools are requested but capability lacks FunctionCalling, add it (informational)
            if (!string.IsNullOrEmpty(this.Body?.ToolFilter) && this.Body?.ToolFilter != "-*" && !effective.HasFlag(AICapability.FunctionCalling))
            {
                effective |= AICapability.FunctionCalling;
                notes.Add(new SHRuntimeMessage(SHRuntimeMessageSeverity.Info, SHRuntimeMessageOrigin.Validation, SHMessageCode.BodyInvalid, "Tool filter provided but Capability lacks FunctionCalling - treating request as requiring FunctionCalling"));
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
