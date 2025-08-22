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
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AICall
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

        /// <inheritdoc/>
        public virtual string? Provider { get; set; }

        /// <inheritdoc/>
        public virtual IAIProvider ProviderInstance { get => ProviderManager.Instance.GetProvider(this.Provider); }

        /// <inheritdoc/>
        public virtual string Model { get => this.GetModelToUse(); set => this.model = value; }

        /// <inheritdoc/>
        public string Endpoint { get; set; }

        /// <inheritdoc/>
        public virtual AICapability Capability { get; set; } = AICapability.None;

        /// <inheritdoc/>
        public virtual AIBody Body { get; set; } = new AIBody();

        /// <inheritdoc/>
        public virtual (bool IsValid, List<string> Errors) IsValid()
        {
            var messages = new List<string>();
            bool hasErrors = false;

            if (string.IsNullOrEmpty(this.model) && this.Capability != AICapability.None)
            {
                messages.Add($"(Info) Model is not specified - the default model '{this.GetModelToUse()}' will be used");
            }

            if (!string.IsNullOrEmpty(this.model) && this.model != this.GetModelToUse())
            {
                messages.Add($"(Info) Using model '{this.GetModelToUse()}' for this request instead of requested '{this.model}' based on provider configuration and model selection policy.");
            }

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
                return string.Empty;
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

            // Delegate selection to provider to hide singleton and centralize policy
            var selected = provider.SelectModel(this.Capability, this.model);
            return selected;
        }
    }
}
