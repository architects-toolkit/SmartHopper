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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.ProviderSdk.AICall.Core.Requests
{
    /// <summary>
    /// Represents a fully-specified AI request that providers can execute, including
    /// provider information, model resolution, capability validation, and request body.
    /// </summary>
    public class AIRequestBase : IAIRequest
    {
        /// <summary>
        /// Store the desired model.
        /// </summary>
        private string? model;

        // Per-request memoization for selected model to avoid repeated selection calls.
        // The selected model depends on provider, requested model, and effective capability.
        // If these inputs don't change during the lifetime of the request, reuse the cached selection.
        private string? cachedSelectedModel;
        private string? cacheProvider;
        private string? cacheRequestedModel;
        private AICapability cacheCapability;

        /// <summary>
        /// Internal storage for structured messages.
        /// </summary>
        private List<SHRuntimeMessage> PrivateMessages { get; set; } = new List<SHRuntimeMessage>();

        /// <inheritdoc/>
        public virtual string? Provider { get; set; }

        /// <inheritdoc/>
        public virtual IAIProvider ProviderInstance { get => ProviderSdkHost.ProviderRegistry.GetProvider(this.Provider); }

        /// <inheritdoc/>
        public virtual string Model { get => this.GetModelToUse(); set => this.model = value; }

        /// <summary>
        /// Gets the user-requested model exactly as provided (may be empty, unknown, or incompatible).
        /// Exposed to derived classes for validation and messaging purposes.
        /// </summary>
        protected string RequestedModel => this.model ?? string.Empty;

        /// <inheritdoc/>
        public string Endpoint { get; set; }

        /// <inheritdoc/>
        public virtual AICapability Capability { get; set; } = AICapability.None;

        /// <inheritdoc/>
        public virtual AIBody Body { get; set; } = AIBody.Empty;

        /// <inheritdoc/>
        public virtual bool WantsStreaming { get; set; }

        /// <summary>
        /// Per-request AI settings. When set, individual properties take precedence over
        /// global provider settings (Temperature, MaxTokens, etc.) on a per-field basis.
        /// Null means fall back entirely to global settings.
        /// </summary>
        public AICall.Core.AIRequestParameters Parameters { get; set; }

        /// <inheritdoc/>
        public virtual AIRequestKind RequestKind { get; set; } = AIRequestKind.Generation;

        /// <summary>
        /// Per-request timeout in seconds applied to provider HTTP calls and tool execution wrappers.
        /// When null, the timeout is resolved from settings by RequestTimeoutPolicy.
        /// When set, this value takes precedence over settings.
        /// Normalized by RequestTimeoutPolicy to be within valid bounds.
        /// </summary>
        public virtual int? TimeoutSeconds { get; set; }

        /// <summary>
        /// When true, skips AIMetrics.IsValid() checks for this request's results.
        /// Use this for local-only tool calls that do not rely on provider/model/finish_reason
        /// (for example, Grasshopper canvas utilities like gh_put or gh_get).
        /// </summary>
        public bool SkipMetricsValidation { get; set; }

        /// <summary>
        /// Backing field for ForceToolCall. When true, user explicitly wants to force a tool call.
        /// </summary>
        private bool forceToolCallExplicit;

        /// <summary>
        /// When true, forces the provider to make a tool call (via tool_choice: required or equivalent).
        /// The specific tool name to force can be specified in <see cref="ForceToolName"/>.
        /// If <see cref="ForceToolName"/> is set, this property automatically returns true.
        /// If <see cref="ForceToolName"/> is not set but this property is true, the provider will force
        /// using any available tool without specifying which one.
        /// Providers translate this to their API-specific format (e.g., tool_choice for OpenAI/Mistral, function_calling_config for Gemini).
        /// </summary>
        public virtual bool ForceToolCall
        {
            get => this.forceToolCallExplicit || !string.IsNullOrEmpty(this.ForceToolName);
            set => this.forceToolCallExplicit = value;
        }

        /// <summary>
        /// When set, specifies the exact tool name to force. Setting this property automatically
        /// enables <see cref="ForceToolCall"/>. Can be null or empty when <see cref="ForceToolCall"/>
        /// is explicitly set to true to force using any tool without specifying which one.
        /// </summary>
        public virtual string ForceToolName { get; set; }

        /// <inheritdoc/>
        public List<SHRuntimeMessage> Messages
        {
            get
            {
                // Build combined list without mutating private storage to keep it always up-to-date and deduplicated
                var combined = new List<SHRuntimeMessage>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                // 1) Messages explicitly added to request
                if (this.PrivateMessages != null)
                {
                    foreach (var m in this.PrivateMessages)
                    {
                        if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                        {
                            combined.Add(m);
                        }
                    }
                }

                // 2) Dynamic validation messages
                var (isValid, errors) = this.IsValid();
                if (errors != null)
                {
                    foreach (var m in errors)
                    {
                        if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                        {
                            combined.Add(m);
                        }
                    }
                }

                // 3) Sort by severity: Error > Warning > Info
                int Rank(SHRuntimeMessageSeverity s) => s == SHRuntimeMessageSeverity.Error ? 3 : (s == SHRuntimeMessageSeverity.Warning ? 2 : 1);
                combined.Sort((a, b) => Rank(b.Severity).CompareTo(Rank(a.Severity)));

                return combined;
            }

            set
            {
                this.PrivateMessages = value ?? new List<SHRuntimeMessage>();
            }
        }

        /// <inheritdoc/>
        public virtual (bool IsValid, List<SHRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<SHRuntimeMessage>();

            // Consider any request-level messages already attached (e.g., from policies)
            if (this.PrivateMessages != null && this.PrivateMessages.Count > 0)
            {
                messages.AddRange(this.PrivateMessages);
            }

            // Unified TurnId invariant: all interactions must have TurnId.
            // Validate early at request level so providers always receive well-formed bodies.
            try
            {
                if (this.Body != null && !this.Body.AreTurnIdsValid())
                {
                    messages.Add(new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Validation,
                        SHMessageCode.BodyInvalid,
                        "Request body contains interactions without TurnId. Ensure TurnId is set (e.g., via AIBodyBuilder.WithTurnId(...)) before building the body."));
                }
            }
            catch
            {
                // Defensive: validation should never throw
                messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Validation,
                    SHMessageCode.BodyInvalid,
                    "Failed to validate TurnId invariants for the request body."));
            }

            // Streaming support validation (blocking for streaming flows): when streaming is requested but unsupported, flag as error to fallback to non-streaming
            if (this.WantsStreaming)
            {
                var provider = this.Provider;
                var modelUsed = this.GetModelToUse();
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    // 1) Provider-level toggle must allow streaming
                    var settings = ProviderSdkHost.ProviderRegistry.GetProviderSettings(provider);
                    if (settings != null && settings.EnableStreaming == false)
                    {
                        messages.Add(new SHRuntimeMessage(
                            SHRuntimeMessageSeverity.Error,
                            SHRuntimeMessageOrigin.Validation,
                            SHMessageCode.StreamingDisabledProvider,
                            $"Streaming requested but provider '{provider}' has streaming disabled in settings."));
                    }

                    // 2) Model must support streaming
                    if (!string.IsNullOrWhiteSpace(modelUsed))
                    {
                        var supports = AIModelCapabilityRegistry.Instance.ModelSupportsStreaming(provider, modelUsed);
                        if (!supports)
                        {
                            messages.Add(new SHRuntimeMessage(
                                SHRuntimeMessageSeverity.Error,
                                SHRuntimeMessageOrigin.Validation,
                                SHMessageCode.StreamingUnsupportedModel,
                                $"Streaming requested but the selected model '{modelUsed}' on provider '{provider}' does not support streaming."));
                        }
                    }
                }
            }

            var hasErrors = messages.Any(m => m.Severity == SHRuntimeMessageSeverity.Error);

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public virtual Task<AIReturn> Exec(System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Exec() must be implemented in a derived class.");
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public virtual void Initialize(string provider, string model, AIBody body, string endpoint, AICapability capability = AICapability.TextOutput, string? toolFilter = null)
        {
            AIBody finalBody = body ?? AIBody.Empty;

            if (!string.IsNullOrEmpty(toolFilter))
            {
                var builder = AIBodyBuilder.FromImmutable(finalBody);
                builder.WithToolFilter(toolFilter);
                finalBody = builder.Build();
            }

            this.Provider = provider;
            this.Model = model;
            this.Endpoint = endpoint ?? string.Empty;
            this.Body = finalBody;
            this.Capability = capability;

            // Auto-initialize Parameters if not already set
            if (this.Parameters == null)
            {
                this.Parameters = AIRequestParameters.FromModel(model);
            }
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public virtual void Initialize(string provider, string model, List<IAIInteraction> interactions, string endpoint, AICapability capability = AICapability.TextOutput, string? toolFilter = null)
        {
            var builder = AIBodyBuilder.Create();
            if (interactions != null)
            {
                builder.AddRange(interactions);
            }

            if (!string.IsNullOrEmpty(toolFilter))
            {
                builder.WithToolFilter(toolFilter);
            }

            var body = builder.Build();
            this.Initialize(provider, model, body, endpoint ?? string.Empty, capability);
        }

        /// <summary>
        /// Gets the model to use for the request.
        /// </summary>
        private string GetModelToUse()
        {
            if (this.Capability == AICapability.None)
            {
                // No capability context has been set yet (e.g., intermediary tool calls reading Model).
                // In this case, do NOT override: return the user-requested model verbatim.
                // Model selection (validation/fallback) must happen only when a capability is known.
                return this.model ?? string.Empty;
            }

            if (string.IsNullOrEmpty(this.Provider))
            {
                return string.Empty;
            }

            var provider = this.ProviderInstance;
            if (provider == null)
            {
                return string.Empty;
            }

            // Use memoized selection when inputs haven't changed
            if (!string.IsNullOrEmpty(this.cachedSelectedModel)
                && string.Equals(this.cacheProvider, this.Provider, StringComparison.Ordinal)
                && string.Equals(this.cacheRequestedModel, this.model, StringComparison.Ordinal)
                && this.cacheCapability == this.Capability)
            {
                return this.cachedSelectedModel;
            }

            // Delegate selection to provider to hide singleton and centralize policy
            var selected = provider.SelectModel(this.Capability, this.model);

            // Cache selection for subsequent accesses within the same request
            this.cacheProvider = this.Provider;
            this.cacheRequestedModel = this.model;
            this.cacheCapability = this.Capability;
            this.cachedSelectedModel = selected;

            return selected;
        }

        /// <summary>
        /// Helper to build a standardized error return with this request context.
        /// </summary>
        /// <param name="message">Error message to set (kept raw).</param>
        /// <returns>AIReturn with ErrorMessage and Request set.</returns>
        protected AIReturn BuildErrorReturn(string message)
        {
            var ret = new AIReturn();
            ret.CreateError(message, this);
            return ret;
        }
    }
}
