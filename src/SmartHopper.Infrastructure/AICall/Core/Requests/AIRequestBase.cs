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
using System.Linq;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AICall.Core.Requests
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
        private List<AIRuntimeMessage> PrivateMessages { get; set; } = new List<AIRuntimeMessage>();

        /// <inheritdoc/>
        public virtual string? Provider { get; set; }

        /// <inheritdoc/>
        public virtual IAIProvider ProviderInstance { get => ProviderManager.Instance.GetProvider(this.Provider); }

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
        public virtual AIBody Body { get; set; } = new AIBody();

        /// <summary>
        /// Indicates that the caller intends to stream this request. This lets validation
        /// surface helpful messages about streaming support without blocking non-streaming flows.
        /// </summary>
        public virtual bool WantsStreaming { get; set; } = false;

        /// <inheritdoc/>
        public List<AIRuntimeMessage> Messages
        {
            get
            {
                // Build combined list without mutating private storage to keep it always up-to-date and deduplicated
                var combined = new List<AIRuntimeMessage>();
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
                int Rank(AIRuntimeMessageSeverity s) => s == AIRuntimeMessageSeverity.Error ? 3 : (s == AIRuntimeMessageSeverity.Warning ? 2 : 1);
                combined.Sort((a, b) => Rank(b.Severity).CompareTo(Rank(a.Severity)));

                return combined;
            }

            set
            {
                this.PrivateMessages = value ?? new List<AIRuntimeMessage>();
            }
        }

        /// <inheritdoc/>
        public virtual (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
        {
            var messages = new List<AIRuntimeMessage>();

            if (string.IsNullOrEmpty(this.model) && this.Capability != AICapability.None)
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, $"Model is not specified - the default model '{this.GetModelToUse()}' will be used"));
            }

            if (!string.IsNullOrEmpty(this.model) && this.model != this.GetModelToUse())
            {
                messages.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Info, AIRuntimeMessageOrigin.Validation, $"Using model '{this.GetModelToUse()}' for this request instead of requested '{this.model}' based on provider configuration and model selection policy."));
            }

            // Streaming support validation (blocking for streaming flows): when streaming is requested but unsupported, flag as error
            if (this.WantsStreaming)
            {
                var provider = this.Provider;
                var modelUsed = this.GetModelToUse();
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    // 1) Provider-level toggle must allow streaming
                    var settings = ProviderManager.Instance.GetProviderSettings(provider);
                    if (settings != null && settings.EnableStreaming == false)
                    {
                        messages.Add(new AIRuntimeMessage(
                            AIRuntimeMessageSeverity.Error,
                            AIRuntimeMessageOrigin.Validation,
                            $"Streaming requested but provider '{provider}' has streaming disabled in settings."));
                    }

                    // 2) Model must support streaming
                    if (!string.IsNullOrWhiteSpace(modelUsed))
                    {
                        var supports = ModelManager.Instance.ModelSupportsStreaming(provider, modelUsed);
                        if (!supports)
                        {
                            messages.Add(new AIRuntimeMessage(
                                AIRuntimeMessageSeverity.Error,
                                AIRuntimeMessageOrigin.Validation,
                                $"Streaming requested but the selected model '{modelUsed}' on provider '{provider}' does not support streaming."));
                        }
                    }
                }
            }

            var hasErrors = messages.Count(m => m.Severity == AIRuntimeMessageSeverity.Error) > 0;

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public virtual Task<AIReturn> Exec()
        {
            throw new NotImplementedException("Exec() must be implemented in a derived class.");
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public virtual void Initialize(string provider, string model,  AIBody body, string endpoint, AICapability capability = AICapability.TextOutput)
        {
            this.Provider = provider;
            this.Model = model;
            this.Endpoint = endpoint ?? string.Empty;
            this.Body = body;
            this.Capability = capability;
        }

        /// <summary>
        /// Initializes the call request.
        /// </summary>
        public virtual void Initialize(string provider, string model,  List<IAIInteraction> interactions, string endpoint, AICapability capability = AICapability.TextOutput, string? toolFilter = null)
        {
            var body = new AIBody();
            body.Interactions = interactions;
            if (!string.IsNullOrEmpty(toolFilter))
            {
                body.ToolFilter = toolFilter;
            }
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
